using System;
using System.IO;
using UnityEngine;

namespace HollowCraft
{
    internal sealed class NativeGuiView : IDisposable
    {
        private readonly HollowCraftRuntime runtime;
        private Texture2D texture;
        private byte[] rgba;
        private string sceneKey;
        private long session;
        private int sync,sequence=-1,width,height;
        private bool ready,focused;
        private float nextRead,lastRead;
        private DateTime readAfter=DateTime.UtcNow;
        internal NativeGuiView(HollowCraftRuntime owner) {runtime=owner;}
        internal long Session=>(int?)runtime.LatestSnapshot?["gui"]?["request"]==runtime.UiRequest?(long?)runtime.LatestSnapshot?["gui"]?["session"]??0:0;
        internal bool Active=>ready&&session!=0&&Session==session&&Time.unscaledTime-lastRead<1.5f;
        internal bool TextFocused=>Active&&focused;
        internal void Reset() {ready=false;sequence=-1;session=0;readAfter=DateTime.UtcNow;}
        private static int Hash(string value) {unchecked {int result=0;foreach(char c in value)result=result*31+c;return result;}}
        internal void Refresh(string assets) {
            long expected=Session;string key=runtime.Room+"|"+runtime.CampaignId;
            if(expected==0) {ready=false;session=0;return;}
            if(session!=expected||sceneKey!=key) {Reset();session=expected;sceneKey=key;}
            if(Time.unscaledTime<nextRead)return;nextRead=Time.unscaledTime+.025f;
            int expectedSync=(int?)runtime.LatestSnapshot?["gui"]?["syncId"]??0;
            foreach(string path in NativeFrameFiles.NewestFirst(assets,"gui_frames")) {
                try {
                    if(!File.Exists(path)||File.GetLastWriteTimeUtc(path)<readAfter)continue;
                    using(var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete))using(var reader=new BinaryReader(stream)) {
                        if(stream.Length<52||reader.ReadInt32()!=0x48434755)continue;
                        int serial=reader.ReadInt32(),px=reader.ReadInt32(),py=reader.ReadInt32();long id=reader.ReadInt64();int handler=reader.ReadInt32(),hash=reader.ReadInt32();
                        int w=reader.ReadInt32(),h=reader.ReadInt32(),flags=reader.ReadInt32(),format=reader.ReadInt32();
                        if(id!=session||handler!=expectedSync||hash!=Hash(key)||serial<=sequence||format!=1||w<320||w>1024||h<240||h>576||px!=w*2||py!=h*2||stream.Length!=52+(long)px*py*4)continue;
                        int size=px*py*4;if(rgba==null||rgba.Length!=size)rgba=new byte[size];int at=0;
                        while(at<size) {int count=stream.Read(rgba,at,size-at);if(count<=0)break;at+=count;}
                        if(at!=size||reader.ReadInt32()!=serial)continue;
                        stream.Position=4;if(reader.ReadInt32()!=serial||reader.ReadInt32()!=px||reader.ReadInt32()!=py||reader.ReadInt64()!=id||reader.ReadInt32()!=handler||reader.ReadInt32()!=hash)continue;
                        if(texture==null||texture.width!=px||texture.height!=py) {if(texture!=null)UnityEngine.Object.Destroy(texture);texture=new Texture2D(px,py,TextureFormat.RGBA32,false){filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp};}
                        texture.LoadRawTextureData(rgba);texture.Apply(false,false);sequence=serial;sync=handler;width=w;height=h;focused=(flags&1)!=0;ready=true;lastRead=Time.unscaledTime;
                        break;
                    }
                } catch(IOException) { } catch(UnauthorizedAccessException) { }
            }
        }
        internal bool Draw() {
            if(!Active||texture==null)return false;
            GUI.color=Color.white;GUI.DrawTexture(new Rect(0,0,Screen.width,Screen.height),texture,ScaleMode.StretchToFill,true);
            var e=Event.current;if(!Application.isFocused)return true;
            var raw=GameCursor.ReadScreenPosition();double x=raw.x/Screen.width*width,y=(Screen.height-raw.y)/Screen.height*height;
            int mods=Modifiers;bool heldC=Input.GetKey(KeyCode.C),heldX=Input.GetKey(KeyCode.X);
            string action=null,character="";int key=-1;double amount=0,dx=0,dy=0;
            switch(e.type) {
                case EventType.MouseDown:action="down";break;
                case EventType.MouseUp:action="up";break;
                case EventType.MouseDrag:action="drag";dx=e.delta.x/Screen.width*width;dy=e.delta.y/Screen.height*height;break;
                case EventType.ScrollWheel:action="scroll";amount=-e.delta.y;break;
                case EventType.KeyDown:action="key";key=Key(e.keyCode);if(!e.control&&!e.command&&e.character>=32&&e.character!=127)character=e.character.ToString();break;
                case EventType.KeyUp:action="keyUp";key=Key(e.keyCode);break;
            }
            if(action!=null) {runtime.Send(new {type="guiInput",scene=runtime.Room,gui=session,syncId=sync,w=width,h=height,action,x,y,dx,dy,button=e.button,key,character,amount,mods,heldC,heldX});e.Use();}
            return true;
        }
        internal static int Modifiers=>(Input.GetKey(KeyCode.LeftShift)||Input.GetKey(KeyCode.RightShift)?1:0)|(Input.GetKey(KeyCode.LeftControl)||Input.GetKey(KeyCode.RightControl)?2:0)|(Input.GetKey(KeyCode.LeftAlt)||Input.GetKey(KeyCode.RightAlt)?4:0)|(Input.GetKey(KeyCode.LeftWindows)||Input.GetKey(KeyCode.RightWindows)?8:0);
        private static int Key(KeyCode key) {
            if(key>=KeyCode.A&&key<=KeyCode.Z)return 65+(key-KeyCode.A);
            if(key>=KeyCode.Alpha0&&key<=KeyCode.Alpha9)return 48+(key-KeyCode.Alpha0);
            if(key>=KeyCode.F1&&key<=KeyCode.F15)return 290+(key-KeyCode.F1);
            if(key>=KeyCode.Keypad0&&key<=KeyCode.Keypad9)return 320+(key-KeyCode.Keypad0);
            switch(key) {
                case KeyCode.Escape:return 256;case KeyCode.Return:return 257;case KeyCode.Tab:return 258;case KeyCode.Backspace:return 259;
                case KeyCode.Insert:return 260;case KeyCode.Delete:return 261;case KeyCode.RightArrow:return 262;case KeyCode.LeftArrow:return 263;case KeyCode.DownArrow:return 264;case KeyCode.UpArrow:return 265;
                case KeyCode.PageUp:return 266;case KeyCode.PageDown:return 267;case KeyCode.Home:return 268;case KeyCode.End:return 269;
                case KeyCode.CapsLock:return 280;case KeyCode.ScrollLock:return 281;case KeyCode.Numlock:return 282;case KeyCode.Print:return 283;case KeyCode.Pause:return 284;
                case KeyCode.KeypadPeriod:return 330;case KeyCode.KeypadDivide:return 331;case KeyCode.KeypadMultiply:return 332;case KeyCode.KeypadMinus:return 333;case KeyCode.KeypadPlus:return 334;case KeyCode.KeypadEnter:return 335;case KeyCode.KeypadEquals:return 336;
                case KeyCode.LeftShift:return 340;case KeyCode.LeftControl:return 341;case KeyCode.LeftAlt:return 342;case KeyCode.LeftWindows:return 343;
                case KeyCode.RightShift:return 344;case KeyCode.RightControl:return 345;case KeyCode.RightAlt:return 346;case KeyCode.RightWindows:return 347;
                case KeyCode.Space:return 32;case KeyCode.Quote:return 39;case KeyCode.Comma:return 44;case KeyCode.Minus:return 45;case KeyCode.Period:return 46;case KeyCode.Slash:return 47;
                case KeyCode.Semicolon:return 59;case KeyCode.Equals:return 61;case KeyCode.LeftBracket:return 91;case KeyCode.Backslash:return 92;case KeyCode.RightBracket:return 93;case KeyCode.BackQuote:return 96;
                default:return -1;
            }
        }
        public void Dispose() {if(texture!=null)UnityEngine.Object.Destroy(texture);texture=null;rgba=null;Reset();}
    }
}
