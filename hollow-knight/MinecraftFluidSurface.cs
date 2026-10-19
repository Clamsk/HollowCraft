using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace HollowCraft
{
    // Per-cell clipped trapezoids retain texture density at shallow levels.
    // Shared animation sprites supply original still/flow timing and lava's
    // ping-pong sequence; changing water level never allocates a new GameObject.
    internal sealed class MinecraftFluidSurface : MonoBehaviour
    {
        private Mesh mesh;private MeshRenderer display;private Vector2[] coordinates,animatedUV;private string signature,textureName;private float directionX,directionY;private Sprite frame;
        private static readonly Dictionary<string,Sprite> fallback=new Dictionary<string,Sprite>();
        private static readonly Dictionary<Texture2D,Material> materials=new Dictionary<Texture2D,Material>();
        private static Sprite Fallback(string name){if(fallback.TryGetValue(name,out var result))return result;var texture=new Texture2D(16,16,TextureFormat.RGBA32,false){filterMode=FilterMode.Point};var pixels=new Color[256];for(int i=0;i<256;i++)pixels[i]=name.StartsWith("lava",StringComparison.Ordinal)?new Color(1,.35f,.06f):Color.white;texture.SetPixels(pixels);texture.Apply();return fallback[name]=Sprite.Create(texture,new Rect(0,0,16,16),Vector2.one*.5f,16);}
        internal void Configure(JObject cell) {
            string next=(string)cell["state"];if(next==signature)return;signature=next;textureName=(string)cell["fluidTexture"];
            directionX=(float?)cell["fluidFlowX"]??0;directionY=(float?)cell["fluidFlowY"]??0;
            if(mesh==null){mesh=new Mesh{name="HollowCraft fluid surface"};gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;display=gameObject.AddComponent<MeshRenderer>();display.sortingOrder=(bool?)cell["waterlogged"]==true?1:0;}
            var vertices=new List<Vector3>();var indices=new List<int>();var uv=new List<Vector2>();var colors=new List<Color>();
            var color=(string)cell["fluidKind"]=="minecraft:water"?new Color(.247f,.463f,.894f,.7f):Color.white;
            foreach(JArray polygon in cell["fluidGeometry"] as JArray??new JArray()){
                int start=vertices.Count;foreach(JArray p in polygon){float x=(float)p[0],y=(float)p[1];vertices.Add(new Vector3(x-.5f,y-.5f,0));uv.Add(new Vector2(x,y));colors.Add(color);}
                for(int i=1;i+1<polygon.Count;i++)indices.AddRange(new[]{start,start+i+1,start+i});
            }
            mesh.Clear();mesh.SetVertices(vertices);mesh.SetColors(colors);mesh.SetTriangles(indices,0);mesh.RecalculateBounds();coordinates=uv.ToArray();animatedUV=new Vector2[coordinates.Length];frame=null;Animate();
        }
        private void Animate() {
            var next=MinecraftSpriteAnimation.Frame(textureName??"water_still",Time.time)??Fallback(textureName??"water_still");if(next==frame||mesh==null)return;frame=next;
            if(!materials.TryGetValue(next.texture,out var material))materials[next.texture]=material=new Material(Shader.Find("Sprites/Default")){mainTexture=next.texture};display.sharedMaterial=material;
            Rect r=next.rect;var texture=next.texture;var uv=animatedUV;bool flowing=(textureName??"").EndsWith("_flow",StringComparison.Ordinal);
            for(int i=0;i<uv.Length;i++){
                Vector2 p=coordinates[i];if(flowing){p=(p-Vector2.one*.5f)*.5f;if(directionY<0){}else if(directionX>.1f)p=new Vector2(-p.y,p.x);else if(directionX<-.1f)p=new Vector2(p.y,-p.x);p+=Vector2.one*.5f;}
                uv[i]=new Vector2((r.x+p.x*r.width)/texture.width,(r.y+p.y*r.height)/texture.height);
            }mesh.uv=uv;
        }
        private void LateUpdate(){Animate();}
        private void OnDestroy(){if(mesh!=null)Destroy(mesh);}
    }
}
