using System;
using System.IO;
using UnityEngine;

namespace HollowCraft
{
    internal sealed class NativeHudView : IDisposable
    {
        private readonly HollowCraftRuntime runtime;
        private Texture2D texture;
        private byte[] rgba;
        private string sceneKey;
        private long session;
        private int sequence=-1;
        private bool ready;
        private float nextRead,lastRead;
        private DateTime readAfter=DateTime.UtcNow;
        internal NativeHudView(HollowCraftRuntime owner) {runtime=owner;}
        private static int Hash(string value) {unchecked {int result=0;foreach(char c in value)result=result*31+c;return result;}}
        internal void Reset() {ready=false;sequence=-1;session=0;readAfter=DateTime.UtcNow;}
        internal void Refresh(string assets) {
            long expected=(long?)runtime.LatestSnapshot?["hudSession"]??0;
            string key=runtime.Room+"|"+runtime.CampaignId;
            if(expected==0) {ready=false;session=0;return;}
            if(session!=expected||sceneKey!=key) {Reset();session=expected;sceneKey=key;}
            if(Time.unscaledTime<nextRead)return;nextRead=Time.unscaledTime+.025f;
            for(int slot=0;slot<3;slot++) {
                string path=Path.Combine(assets,"hud_frames","frame-"+slot+".bin");
                try {
                    if(!File.Exists(path)||File.GetLastWriteTimeUtc(path)<readAfter)continue;
                    using(var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete))using(var reader=new BinaryReader(stream)) {
                        if(stream.Length<52||reader.ReadInt32()!=0x48434855)continue;
                        int serial=reader.ReadInt32(),px=reader.ReadInt32(),py=reader.ReadInt32();long id=reader.ReadInt64();int reserved=reader.ReadInt32(),hash=reader.ReadInt32();
                        int w=reader.ReadInt32(),h=reader.ReadInt32(),flags=reader.ReadInt32(),format=reader.ReadInt32();
                        if(id!=session||reserved!=0||hash!=Hash(key)||serial<=sequence||format!=1||flags!=0||w<320||w>1024||h<240||h>576||px!=w*2||py!=h*2||stream.Length!=52+(long)px*py*4)continue;
                        int size=px*py*4;if(rgba==null||rgba.Length!=size)rgba=new byte[size];int at=0;
                        while(at<size) {int count=stream.Read(rgba,at,size-at);if(count<=0)break;at+=count;}
                        if(at!=size||reader.ReadInt32()!=serial)continue;
                        stream.Position=4;if(reader.ReadInt32()!=serial||reader.ReadInt32()!=px||reader.ReadInt32()!=py||reader.ReadInt64()!=id||reader.ReadInt32()!=reserved||reader.ReadInt32()!=hash)continue;
                        if(texture==null||texture.width!=px||texture.height!=py) {if(texture!=null)UnityEngine.Object.Destroy(texture);texture=new Texture2D(px,py,TextureFormat.RGBA32,false){filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp};}
                        texture.LoadRawTextureData(rgba);texture.Apply(false,false);sequence=serial;ready=true;lastRead=Time.unscaledTime;
                    }
                } catch(IOException) { } catch(UnauthorizedAccessException) { }
            }
        }
        internal bool Draw() {
            if(!ready||texture==null||!runtime.Connected||(long?)runtime.LatestSnapshot?["hudSession"]!=session||Time.unscaledTime-lastRead>=1.5f)return false;
            GUI.color=Color.white;GUI.DrawTexture(new Rect(0,0,Screen.width,Screen.height),texture,ScaleMode.StretchToFill,true);return true;
        }
        public void Dispose() {if(texture!=null)UnityEngine.Object.Destroy(texture);texture=null;rgba=null;Reset();}
    }
}
