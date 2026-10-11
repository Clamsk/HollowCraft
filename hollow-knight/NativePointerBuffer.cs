using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using UnityEngine;

namespace HollowCraft
{
    // Capture clicks from this game's own window, then forward every message to Unity.
    // No global hooks, raw-input registration or synthetic input are used.
    internal sealed class NativePointerBuffer : IDisposable
    {
        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate IntPtr SubclassProc(IntPtr window,uint message,UIntPtr wParam,IntPtr lParam,UIntPtr id,UIntPtr data);
        [StructLayout(LayoutKind.Sequential)] private struct ClientRect {public int left,top,right,bottom;}
        internal struct Press {internal int Button,X,Y;}
        [DllImport("comctl32.dll")] private static extern bool SetWindowSubclass(IntPtr window,SubclassProc callback,UIntPtr id,UIntPtr data);
        [DllImport("comctl32.dll")] private static extern bool RemoveWindowSubclass(IntPtr window,SubclassProc callback,UIntPtr id);
        [DllImport("comctl32.dll")] private static extern IntPtr DefSubclassProc(IntPtr window,uint message,UIntPtr wParam,IntPtr lParam);
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window,out uint process);
        [DllImport("user32.dll")] private static extern bool GetClientRect(IntPtr window,out ClientRect rect);
        [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
        private static readonly UIntPtr Id=new UIntPtr(0x48434D39);
        private static readonly Dictionary<IntPtr,NativePointerBuffer> roots=new Dictionary<IntPtr,NativePointerBuffer>();
        private readonly ConcurrentQueue<Press> presses=new ConcurrentQueue<Press>();
        private readonly SubclassProc callback;
        private IntPtr window;
        private float nextInstall;
        private volatile bool capture;
        private bool unavailable,disposed;
        internal NativePointerBuffer() {callback=WindowMessage;}
        internal void Context(bool enabled) {
            capture=enabled&&!disposed;if(!capture)while(presses.TryDequeue(out _)) { }
            if(window!=IntPtr.Zero||unavailable||disposed||Time.unscaledTime<nextInstall)return;
            nextInstall=Time.unscaledTime+2;
            try {
                using(var process=Process.GetCurrentProcess()) {
                    var handle=process.MainWindowHandle;if(handle==IntPtr.Zero)return;
                    uint thread=GetWindowThreadProcessId(handle,out uint owner);
                    if(owner!=(uint)process.Id||thread!=GetCurrentThreadId()) {unavailable=true;return;}
                    if(!SetWindowSubclass(handle,callback,Id,UIntPtr.Zero)) {unavailable=true;return;}
                    window=handle;roots[handle]=this;
                }
                UnityEngine.Debug.Log("[HollowCraft] Native window click queue active; Unity input retained");
            } catch(DllNotFoundException) {unavailable=true;}catch(EntryPointNotFoundException) {unavailable=true;}
        }
        private IntPtr WindowMessage(IntPtr handle,uint message,UIntPtr wParam,IntPtr lParam,UIntPtr id,UIntPtr data) {
            if(capture&&(message==0x0201||message==0x0203||message==0x0204||message==0x0206)&&presses.Count<128) {
                long packed=lParam.ToInt64();presses.Enqueue(new Press {Button=message==0x0201||message==0x0203?0:1,X=(short)(packed&0xffff),Y=(short)((packed>>16)&0xffff)});
            }
            if(message==0x0008||message==0x001C&&wParam==UIntPtr.Zero) {capture=false;while(presses.TryDequeue(out _)) { }}
            if(message==0x0082) {RemoveWindowSubclass(handle,callback,Id);roots.Remove(handle);window=IntPtr.Zero;capture=false;}
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
            capture=false;disposed=true;
            if(window!=IntPtr.Zero&&RemoveWindowSubclass(window,callback,Id)) {roots.Remove(window);window=IntPtr.Zero;}
            // If removal fails, roots keeps the delegate alive until WM_NCDESTROY.
        }
    }
}
