using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using UnityEngine;

namespace HollowCraft
{
    // Capture this game's own window only; all original messages continue to Unity.
    internal sealed class NativePointerBuffer : IDisposable
    {
        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate IntPtr SubclassProc(IntPtr window,uint message,UIntPtr wParam,IntPtr lParam,UIntPtr id,UIntPtr data);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate IntPtr GetMessageProc(int code,UIntPtr removed,IntPtr message);
        [StructLayout(LayoutKind.Sequential)] private struct ClientRect {public int left,top,right,bottom;}
        [StructLayout(LayoutKind.Sequential)] private struct WindowMessage {
            public IntPtr window;public uint message;public UIntPtr wParam;public IntPtr lParam;
            public uint time;public int x,y;public uint reserved;
        }
        internal struct Press {internal int Button,X,Y;}
        [DllImport("comctl32.dll",SetLastError=true)] private static extern bool SetWindowSubclass(IntPtr window,SubclassProc callback,UIntPtr id,UIntPtr data);
        [DllImport("comctl32.dll")] private static extern bool RemoveWindowSubclass(IntPtr window,SubclassProc callback,UIntPtr id);
        [DllImport("comctl32.dll")] private static extern IntPtr DefSubclassProc(IntPtr window,uint message,UIntPtr wParam,IntPtr lParam);
        [DllImport("user32.dll",EntryPoint="SetWindowsHookExW",SetLastError=true)] private static extern IntPtr SetWindowsHookEx(int kind,GetMessageProc callback,IntPtr module,uint thread);
        [DllImport("user32.dll",SetLastError=true)] private static extern bool UnhookWindowsHookEx(IntPtr hook);
        [DllImport("user32.dll")] private static extern IntPtr CallNextHookEx(IntPtr hook,int code,UIntPtr wParam,IntPtr lParam);
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window,out uint process);
        [DllImport("user32.dll")] private static extern bool GetClientRect(IntPtr window,out ClientRect rect);
        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
        private static readonly UIntPtr Id=new UIntPtr(0x48434D39);
        private static readonly ConcurrentDictionary<IntPtr,NativePointerBuffer> roots=new ConcurrentDictionary<IntPtr,NativePointerBuffer>();
        // Static callback remains rooted even if another thread is still returning after unhook.
        private static readonly GetMessageProc messageCallback=HookMessage;
        private readonly ConcurrentQueue<Press> presses=new ConcurrentQueue<Press>();
        private readonly SubclassProc callback;
        private IntPtr window,hook;
        private float nextInstall;
        private volatile bool capture,disposed;
        private bool unavailable,subclass;
        internal NativePointerBuffer() {callback=WindowMessageCallback;}
        internal void Context(bool enabled) {
            capture=enabled&&!disposed;if(!capture)while(presses.TryDequeue(out _)) { }
            if(window!=IntPtr.Zero||unavailable||disposed||Time.unscaledTime<nextInstall)return;
            nextInstall=Time.unscaledTime+2;
            try {
                using(var process=Process.GetCurrentProcess()) {
                    var handle=process.MainWindowHandle;if(handle==IntPtr.Zero)return;
                    uint thread=GetWindowThreadProcessId(handle,out uint owner);
                    if(owner!=(uint)process.Id||thread==0) {unavailable=true;return;}
                    window=handle;roots[handle]=this;
                    subclass=thread==GetCurrentThreadId()&&SetWindowSubclass(handle,callback,Id,UIntPtr.Zero);
                    if(!subclass)hook=SetWindowsHookEx(3,messageCallback,IntPtr.Zero,thread);
                    if(!subclass&&hook==IntPtr.Zero) {
                        int error=Marshal.GetLastWin32Error();roots.TryRemove(handle,out _);window=IntPtr.Zero;unavailable=true;
                        UnityEngine.Debug.Log("[HollowCraft] Native click queue unavailable ("+error+"); Unity input active");return;
                    }
                }
                UnityEngine.Debug.Log("[HollowCraft] Native window click queue active via "+(subclass?"subclass":"thread message hook"));
            } catch(DllNotFoundException) {Unavailable();}catch(EntryPointNotFoundException) {Unavailable();}
            catch(System.ComponentModel.Win32Exception) {Unavailable();}catch(InvalidOperationException) {Unavailable();}
        }
        private void Unavailable() {
            if(window!=IntPtr.Zero&&!subclass&&hook==IntPtr.Zero)roots.TryRemove(window,out _);
            if(!subclass&&hook==IntPtr.Zero)window=IntPtr.Zero;unavailable=true;
        }
        private static IntPtr HookMessage(int code,UIntPtr removed,IntPtr pointer) {
            try {
                // PM_NOREMOVE may repeat the same message; capture only actual dequeue.
                if(code==0&&removed.ToUInt64()==1&&pointer!=IntPtr.Zero) {
                    var message=Marshal.PtrToStructure<WindowMessage>(pointer);
                    if(roots.TryGetValue(message.window,out NativePointerBuffer buffer))buffer.Observe(message.window,message.message,message.wParam,message.lParam);
                }
            } catch(Exception) { } // Managed exceptions must never escape into the window pump.
            return CallNextHookEx(IntPtr.Zero,code,removed,pointer);
        }
        private void Observe(IntPtr handle,uint message,UIntPtr wParam,IntPtr lParam) {
            if(capture&&!disposed&&GetForegroundWindow()==handle&&(message==0x0201||message==0x0203||message==0x0204||message==0x0206)&&presses.Count<128) {
                long packed=lParam.ToInt64();presses.Enqueue(new Press {Button=message==0x0201||message==0x0203?0:1,X=(short)(packed&0xffff),Y=(short)((packed>>16)&0xffff)});
            }
            if(message==0x0008||message==0x001C&&wParam==UIntPtr.Zero) {capture=false;while(presses.TryDequeue(out _)) { }}
        }
        private IntPtr WindowMessageCallback(IntPtr handle,uint message,UIntPtr wParam,IntPtr lParam,UIntPtr id,UIntPtr data) {
            Observe(handle,message,wParam,lParam);
            if(message==0x0082) {RemoveWindowSubclass(handle,callback,Id);roots.TryRemove(handle,out _);window=IntPtr.Zero;subclass=false;capture=false;}
            return DefSubclassProc(handle,message,wParam,lParam);
        }
        internal bool Read(out Press press,out Vector2 screen) {
            screen=Vector2.zero;if(!presses.TryDequeue(out press))return false;
            if(window!=IntPtr.Zero&&GetClientRect(window,out ClientRect rect)&&rect.right>0&&rect.bottom>0)
                screen=new Vector2((float)press.X*Screen.width/rect.right,Screen.height-(float)press.Y*Screen.height/rect.bottom);
            else screen=GameCursor.ReadScreenPosition();
            return true;
        }
        public void Dispose() {
            capture=false;disposed=true;bool removed=true;
            if(subclass&&window!=IntPtr.Zero)removed=RemoveWindowSubclass(window,callback,Id);
            if(hook!=IntPtr.Zero)removed=UnhookWindowsHookEx(hook)&&removed;
            if(removed) {roots.TryRemove(window,out _);window=hook=IntPtr.Zero;subclass=false;}
            // A failed removal retains the rooted, disabled callback.
        }
    }
}
