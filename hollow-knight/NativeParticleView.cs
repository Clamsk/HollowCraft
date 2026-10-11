using System;
using System.IO;
using UnityEngine;

namespace HollowCraft
{
    internal sealed class NativeParticleView : IDisposable
    {
        private const int Width=768,Height=512,Header=48;
        private GameObject root;
        private Texture2D texture;
        private Sprite sprite;
        private SpriteRenderer renderer;
        private byte[] rgba;
        private int sequence=-1;
        private long session;
        private string sceneKey;
        private bool ready;
        private float nextRead,lastRead;
        private Vector2 center,span;
        private DateTime readAfter=DateTime.UtcNow;
        private static int SceneHash(string value) {unchecked {int result=0;foreach(char c in value)result=result*31+c;return result;}}
        internal void Reset() {ready=false;sequence=-1;readAfter=DateTime.UtcNow;Hide();}
        internal void Hide() {if(root!=null)root.SetActive(false);}
        internal void Draw(string assets,HollowCraftRuntime runtime,HeroController hero,bool visible)
        {
            if(!visible||hero==null) {Hide();return;}
            string expected=runtime.Room+"|"+runtime.CampaignId;
            if(sceneKey!=expected) {sceneKey=expected;Reset();}
            if(Time.unscaledTime>=nextRead) {
                nextRead=Time.unscaledTime+.025f;
                for(int slot=0;slot<3;slot++) {
                    string path=Path.Combine(assets,"particle_frames","frame-"+slot+".bin");
                    try {
                        if(!File.Exists(path)||File.GetLastWriteTimeUtc(path)<readAfter)continue;
                        using(var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete))using(var reader=new BinaryReader(stream)) {
                            if(stream.Length!=Header+Width*Height*4+4||reader.ReadInt32()!=0x48435054)continue;
                            int serial=reader.ReadInt32(),w=reader.ReadInt32(),h=reader.ReadInt32();long frameSession=reader.ReadInt64();int sceneHash=reader.ReadInt32();
                            float x=reader.ReadSingle(),y=reader.ReadSingle(),sx=reader.ReadSingle(),sy=reader.ReadSingle();int format=reader.ReadInt32();
                            if(w!=Width||h!=Height||format!=1||sceneHash!=SceneHash(expected)||frameSession<session||frameSession==session&&serial<=sequence)continue;
                            if(!Finite(x)||!Finite(y)||!Finite(sx)||!Finite(sy)||Math.Abs(x)>500||y<-100||y>300||sx<2||sx>120||sy<2||sy>80)continue;
                            if(rgba==null)rgba=new byte[Width*Height*4];int at=0;
                            while(at<rgba.Length) {int n=stream.Read(rgba,at,rgba.Length-at);if(n<=0)break;at+=n;}
                            if(at!=rgba.Length||reader.ReadInt32()!=serial)continue;
                            stream.Position=4;
                            if(reader.ReadInt32()!=serial||reader.ReadInt32()!=w||reader.ReadInt32()!=h||reader.ReadInt64()!=frameSession||reader.ReadInt32()!=sceneHash)continue;
                            if(texture==null) {
                                texture=new Texture2D(Width,Height,TextureFormat.RGBA32,false) {filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp};
                                root=new GameObject("HollowCraft.VanillaParticles");UnityEngine.Object.DontDestroyOnLoad(root);renderer=root.AddComponent<SpriteRenderer>();
                                sprite=Sprite.Create(texture,new Rect(0,0,Width,Height),new Vector2(.5f,.5f),1,0,SpriteMeshType.FullRect);renderer.sprite=sprite;
                            }
                            texture.LoadRawTextureData(rgba);texture.Apply(false,false);
                            center=new Vector2(x,y);span=new Vector2(sx,sy);sequence=serial;session=frameSession;ready=true;lastRead=Time.unscaledTime;
                        }
                    } catch(IOException) { } catch(UnauthorizedAccessException) { }
                }
            }
            if(!ready||root==null||!runtime.AudioPaused&&Time.unscaledTime-lastRead>1.5f) {Hide();return;}
            var original=hero.GetComponent<Renderer>()??hero.GetComponentInChildren<Renderer>();
            renderer.sortingLayerID=original!=null?original.sortingLayerID:0;renderer.sortingOrder=(original!=null?original.sortingOrder:0)+12;
            root.transform.position=new Vector3(center.x,center.y,hero.transform.position.z-.15f);
            root.transform.localScale=new Vector3(span.x/Width,span.y/Height,1);root.SetActive(true);
        }
        private static bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
        public void Dispose() {if(root!=null)UnityEngine.Object.Destroy(root);if(sprite!=null)UnityEngine.Object.Destroy(sprite);if(texture!=null)UnityEngine.Object.Destroy(texture);root=null;texture=null;sprite=null;}
    }
}
