using System;
using System.IO;
using UnityEngine;

namespace HollowCraft
{
    // The complete articulated model and its depth are rendered by Minecraft.
    internal sealed class NativePlayerView : IDisposable
    {
        internal Texture2D Texture {get;private set;}
        private GameObject root;
        private SpriteRenderer renderer;
        private byte[] rgba;
        private int sequence=-1;
        private float nextRead;
        private readonly DateTime started=DateTime.UtcNow;
        internal bool Draw(string assets,Vector3 feet,int layer,int order)
        {
            if(Time.unscaledTime>=nextRead) {
                nextRead=Time.unscaledTime+.016f;
                string file=Path.Combine(assets,"player_frames","current.bin");
                try {
                    if(File.Exists(file)&&File.GetLastWriteTimeUtc(file)>=started)using(var stream=new FileStream(file,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete))using(var reader=new BinaryReader(stream)) {
                        if(reader.ReadInt32()==0x48435037) {
                            int serial=reader.ReadInt32(),w=reader.ReadInt32(),h=reader.ReadInt32();
                            if(serial!=sequence&&w==256&&h==256&&stream.Length==16+w*h*4) {
                                if(rgba==null)rgba=new byte[w*h*4];int at=0;
                                while(at<rgba.Length){int n=stream.Read(rgba,at,rgba.Length-at);if(n<=0)break;at+=n;}
                                if(at==rgba.Length) {
                                    if(Texture==null) {
                                        Texture=new Texture2D(w,h,TextureFormat.RGBA32,false){filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp};
                                        root=new GameObject("HollowCraft.MinecraftPlayerRenderer");UnityEngine.Object.DontDestroyOnLoad(root);
                                        renderer=root.AddComponent<SpriteRenderer>();
                                        renderer.sprite=Sprite.Create(Texture,new Rect(0,0,w,h),new Vector2(.5f,48f/256),64,0,SpriteMeshType.FullRect);
                                    }
                                    Texture.LoadRawTextureData(rgba);Texture.Apply(false,false);sequence=serial;
                                }
                            }
                        }
                    }
                } catch(IOException) { } catch(UnauthorizedAccessException) { }
            }
            if(root==null)return false;
            root.SetActive(true);root.transform.position=feet;root.transform.localScale=Vector3.one;
            renderer.sortingLayerID=layer;renderer.sortingOrder=order+2;return true;
        }
        internal void Hide(){if(root!=null)root.SetActive(false);}
        public void Dispose(){if(root!=null)UnityEngine.Object.Destroy(root);if(Texture!=null)UnityEngine.Object.Destroy(Texture);}
    }
}
