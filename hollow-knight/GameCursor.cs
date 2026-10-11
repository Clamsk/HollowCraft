using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using UnityEngine;

namespace HollowCraft
{
    public static class GameCursor
    {
        [StructLayout(LayoutKind.Sequential)] private struct Point {public int x,y;}
        [StructLayout(LayoutKind.Sequential)] private struct WindowRect {public int left,top,right,bottom;}
        [DllImport("user32.dll")] private static extern bool GetCursorPos(out Point point);
        [DllImport("user32.dll")] private static extern bool ScreenToClient(IntPtr window,ref Point point);
        [DllImport("user32.dll")] private static extern bool GetClientRect(IntPtr window,out WindowRect rect);
        private static IntPtr window;
        internal static Vector2 ReadScreenPosition()
        {
            Vector2 value=Input.mousePosition;
            try {
                if(window==IntPtr.Zero) window=Process.GetCurrentProcess().MainWindowHandle;
                if(window!=IntPtr.Zero && GetCursorPos(out Point point) && ScreenToClient(window,ref point) && GetClientRect(window,out WindowRect rect) && rect.right>0 && rect.bottom>0)
                    value=new Vector2((float)point.x*Screen.width/rect.right,Screen.height-(float)point.y*Screen.height/rect.bottom);
            } catch(DllNotFoundException) {} catch(EntryPointNotFoundException) {}
            return value;
        }
        // InputHandler normally locks the OS pointer in gameplay. The injected prefix owns this path.
        public static bool ApplyCursor()
        {
            if(HollowCraftRuntime.Instance==null || !HollowCraftRuntime.Instance.InGame) return false;
            Cursor.lockState=CursorLockMode.None;Cursor.visible=false;return true;
        }
        internal static Camera GameCamera()
        {
            var cameras=UnityEngine.Object.FindFirstObjectByType<GameCameras>();
            if(cameras!=null && cameras.mainCamera!=null) return cameras.mainCamera;
            return Camera.allCameras.Length>0 ? Array.Find(Camera.allCameras,c=>c.enabled && c.orthographic) ?? Camera.main : Camera.main;
        }
        internal static float BlockReach => HollowCraftRuntime.Instance?.Creative==true?5:4.5f;
        internal static float AttackReach => HollowCraftRuntime.Instance?.Creative==true?6:3;
        internal static Vector2 Eye(HeroController hero) {
            var body=hero.GetComponent<Collider2D>();
            return new Vector2(hero.transform.position.x,(body!=null?body.bounds.min.y:hero.transform.position.y)+(MinecraftEnvironment.Gliding?.4f:MinecraftControls.Crouching?1.27f:1.62f));
        }
        internal static bool BlockInReach(Vector2Int cell,HeroController hero) {
            var eye=Eye(hero);var nearest=new Vector2(Mathf.Clamp(eye.x,cell.x,cell.x+1),Mathf.Clamp(eye.y,cell.y,cell.y+1));
            return (eye-nearest).sqrMagnitude<=BlockReach*BlockReach+.001f;
        }
        internal static Vector2 ClampWorld(Vector2 pointer,Vector2 origin) => origin+Vector2.ClampMagnitude(pointer-origin,Math.Max(BlockReach,AttackReach));
        internal static bool WorldTarget(HeroController hero,out Vector2 target,out Vector2 gui)
        {
            var camera=GameCamera();target=hero.transform.position;gui=new Vector2(Screen.width/2,Screen.height/2);
            if(camera==null) return false;
            Vector2 screen=ReadScreenPosition();
            Vector3 raw=camera.ScreenToWorldPoint(new Vector3(screen.x,screen.y,hero.transform.position.z-camera.transform.position.z));
            target=ClampWorld(raw,Eye(hero));
            Vector3 result=camera.WorldToScreenPoint(new Vector3(target.x,target.y,hero.transform.position.z));
            gui=new Vector2(result.x,Screen.height-result.y);return true;
        }
    }
}
