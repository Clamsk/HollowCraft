using System;
using System.Collections.Generic;
using UnityEngine;

namespace HollowCraft.Standalone
{
    internal sealed class NativeModelView : IDisposable
    {
        private sealed class Geometry {
            internal Vector3[] Points;internal Vector2[] UV;internal Color[] Colors;internal int[] Triangles;internal NativeRasterVertex[] Projected;
        }
        private readonly Dictionary<Mesh,Geometry> geometry=new Dictionary<Mesh,Geometry>();
        private readonly Dictionary<Texture2D,NativeRasterTexture> textures=new Dictionary<Texture2D,NativeRasterTexture>();
        private readonly List<MeshRenderer> renderers=new List<MeshRenderer>();
        private readonly MaterialPropertyBlock tint=new MaterialPropertyBlock();
        private GameObject quad;private Mesh mesh;private Material material;
        private NativeDepthBuffer surface,portrait;private Color32[] pixels,portraitPixels;
        private Texture2D frame,portraitFrame;private float nextFrame;
        internal Texture2D Portrait=>portraitFrame;
        private NativeRasterTexture Texture(Texture2D texture) {
            if(textures.TryGetValue(texture,out var result))return result;
            var source=texture.GetPixels32();var rgba=new uint[source.Length];for(int i=0;i<source.Length;i++)rgba[i]=(uint)(source[i].r|(source[i].g<<8)|(source[i].b<<16)|(source[i].a<<24));
            result=new NativeRasterTexture(texture.width,texture.height,rgba);textures[texture]=result;return result;
        }
        private Geometry MeshGeometry(Mesh source) {
            if(geometry.TryGetValue(source,out var result))return result;
            result=new Geometry{Points=source.vertices,UV=source.uv,Colors=source.colors,Triangles=source.triangles};result.Projected=new NativeRasterVertex[result.Points.Length];geometry[source]=result;return result;
        }
        private void Render(GameObject model,NativeDepthBuffer output,Matrix4x4 projection,float scale,float centerX,float baseY) {
            output.Clear();renderers.Clear();model.GetComponentsInChildren(false,renderers);
            foreach(var renderer in renderers) {
                // Original meshes remain the articulated source, never separate
                // world sprites. The same depth test handles skin, armor and hands.
                renderer.enabled=false;var source=renderer.GetComponent<MeshFilter>()?.sharedMesh;var texture=renderer.sharedMaterial?.mainTexture as Texture2D;if(source==null||texture==null)continue;
                var data=MeshGeometry(source);var image=Texture(texture);renderer.GetPropertyBlock(tint);Color color=tint.GetColor("_Color");if(color.a<=0)color=Color.white;
                Matrix4x4 transform=projection*renderer.transform.localToWorldMatrix;
                for(int i=0;i<data.Points.Length;i++) {
                    var point=transform.MultiplyPoint3x4(data.Points[i]);var uv=i<data.UV.Length?data.UV[i]:Vector2.zero;var shade=(i<data.Colors.Length?data.Colors[i]:Color.white)*color;
                    data.Projected[i]=new NativeRasterVertex{X=point.x*scale+centerX,Y=point.y*scale+baseY,Z=point.z,U=uv.x,V=uv.y,R=shade.r,G=shade.g,B=shade.b,A=shade.a};
                }
                for(int i=0;i<data.Triangles.Length;i+=3)output.Triangle(data.Projected[data.Triangles[i]],data.Projected[data.Triangles[i+1]],data.Projected[data.Triangles[i+2]],image);
            }
        }
        private static void Upload(NativeDepthBuffer surface,Color32[] pixels,Texture2D texture) {
            for(int i=0;i<pixels.Length;i++){uint p=surface.Pixels[i];pixels[i]=new Color32((byte)p,(byte)(p>>8),(byte)(p>>16),(byte)(p>>24));}texture.SetPixels32(pixels);texture.Apply(false,false);
        }
        internal void Draw(GameObject model,Vector3 feet,int layer,int order,bool large) {
            int size=large?384:256;
            if(surface==null||surface.Width!=size){if(frame!=null)UnityEngine.Object.Destroy(frame);surface=new NativeDepthBuffer(size,size);pixels=new Color32[size*size];frame=new Texture2D(size,size,TextureFormat.RGBA32,false){filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp};nextFrame=0;
                if(quad==null){quad=new GameObject("HollowCraft.NativeDepthPlayer");UnityEngine.Object.DontDestroyOnLoad(quad);quad.AddComponent<MeshFilter>();var renderer=quad.AddComponent<MeshRenderer>();material=new Material(Shader.Find("Sprites/Default"));renderer.sharedMaterial=material;}
                if(mesh!=null)UnityEngine.Object.Destroy(mesh);mesh=new Mesh();float half=size/128f,bottom=-48f/64;mesh.vertices=new[]{new Vector3(-half,bottom,0),new Vector3(half,bottom,0),new Vector3(half,bottom+size/64f,0),new Vector3(-half,bottom+size/64f,0)};mesh.uv=new[]{Vector2.zero,Vector2.right,Vector2.one,Vector2.up};mesh.triangles=new[]{0,2,1,0,3,2};mesh.RecalculateBounds();quad.GetComponent<MeshFilter>().sharedMesh=mesh;material.mainTexture=frame;
            }
            var display=quad.GetComponent<MeshRenderer>();display.sortingLayerID=layer;display.sortingOrder=order+2;quad.SetActive(true);quad.transform.position=feet;
            renderers.Clear();model.GetComponentsInChildren(false,renderers);foreach(var renderer in renderers)renderer.enabled=false;
            if(Time.unscaledTime<nextFrame)return;nextFrame=Time.unscaledTime+1/40f;
            Render(model,surface,Matrix4x4.Translate(-feet),64,size*.5f,48);Upload(surface,pixels,frame);
        }
        internal void DrawPortrait(GameObject model) {
            if(portrait==null){portrait=new NativeDepthBuffer(128,160);portraitPixels=new Color32[128*160];portraitFrame=new Texture2D(128,160,TextureFormat.RGBA32,false){filterMode=FilterMode.Point};}
            var matrix=Matrix4x4.TRS(Vector3.zero,Quaternion.Euler(0,-20,0),Vector3.one*.9f)*model.transform.worldToLocalMatrix;
            Render(model,portrait,matrix,72,64,12);Upload(portrait,portraitPixels,portraitFrame);
        }
        internal void Hide(){if(quad!=null)quad.SetActive(false);}
        internal void ResetSources(){geometry.Clear();textures.Clear();nextFrame=0;}
        public void Dispose(){if(quad!=null)UnityEngine.Object.Destroy(quad);if(mesh!=null)UnityEngine.Object.Destroy(mesh);if(material!=null)UnityEngine.Object.Destroy(material);if(frame!=null)UnityEngine.Object.Destroy(frame);if(portraitFrame!=null)UnityEngine.Object.Destroy(portraitFrame);geometry.Clear();textures.Clear();}
    }
}
