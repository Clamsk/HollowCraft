using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace HollowCraft
{
    // All scene geometry is a real Unity mesh. GUI icons are never used as held scene models.
    internal static class MinecraftMeshes
    {
        private static readonly Dictionary<string,Material> materials=new Dictionary<string,Material>();
        private static readonly Dictionary<string,Mesh> meshes=new Dictionary<string,Mesh>();
        private static readonly Dictionary<string,Texture2D> atlases=new Dictionary<string,Texture2D>();
        internal static Material Material(Texture2D texture)
        {
            string key=texture.GetInstanceID().ToString();
            if(materials.TryGetValue(key,out Material cached)) return cached;
            Shader shader=Shader.Find("Unlit/Transparent Cutout") ?? Shader.Find("Sprites/Default");
            var material=new Material(shader){mainTexture=texture};
            material.SetFloat("_Cutoff",.05f);materials[key]=material;return material;
        }
        internal static MeshRenderer Renderer(GameObject go,Mesh mesh,Texture2D texture,int layer,int order)
        {
            go.AddComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=Material(texture);
            renderer.sortingLayerID=layer;renderer.sortingOrder=order;return renderer;
        }
        // HK does not ship Unity's cutout shader. Its sprite shader blends without depth writes,
        // so sort the actual 3D faces from the camera's far side to its near side.
        internal static void SortFaces(MeshRenderer renderer,Camera camera=null)
        {
            camera=camera??Camera.main;if(renderer.sharedMaterial.shader.name!="Sprites/Default"||camera==null) return;
            var sorter=renderer.GetComponent<MinecraftFaceSorter>()??renderer.gameObject.AddComponent<MinecraftFaceSorter>();sorter.Sort(camera);
        }
        internal static Mesh Cuboid(float width,float height,float depth,float u,float v,bool topPivot,Texture2D texture,float inflate=0)
        {
            float w=width/16,h=height/16,d=depth/16;
            float x0=-w/2-inflate,x1=w/2+inflate,y0=(topPivot?-h:-h/2)-inflate,y1=(topPivot?0:h/2)+inflate,z0=-d/2-inflate,z1=d/2+inflate;
            var vertices=new List<Vector3>();var uv=new List<Vector2>();var colors=new List<Color>();var triangles=new List<int>();
            Action<Vector3[],float,float,float,float,float> face=(points,tx,ty,tw,th,shade)=> {
                int start=vertices.Count;vertices.AddRange(points);
                uv.AddRange(new[]{new Vector2(tx/texture.width,1-(ty+th)/texture.height),new Vector2((tx+tw)/texture.width,1-(ty+th)/texture.height),new Vector2((tx+tw)/texture.width,1-ty/texture.height),new Vector2(tx/texture.width,1-ty/texture.height)});
                for(int i=0;i<4;i++) colors.Add(new Color(shade,shade,shade,1));
                triangles.AddRange(new[]{start,start+2,start+1,start,start+3,start+2});
            };
            face(new[]{new Vector3(x0,y0,z0),new Vector3(x1,y0,z0),new Vector3(x1,y1,z0),new Vector3(x0,y1,z0)},u+depth,v+depth,width,height,.9f);
            face(new[]{new Vector3(x1,y0,z1),new Vector3(x0,y0,z1),new Vector3(x0,y1,z1),new Vector3(x1,y1,z1)},u+depth+width+depth,v+depth,width,height,.75f);
            face(new[]{new Vector3(x1,y0,z0),new Vector3(x1,y0,z1),new Vector3(x1,y1,z1),new Vector3(x1,y1,z0)},u+depth+width,v+depth,depth,height,.8f);
            face(new[]{new Vector3(x0,y0,z1),new Vector3(x0,y0,z0),new Vector3(x0,y1,z0),new Vector3(x0,y1,z1)},u,v+depth,depth,height,.8f);
            face(new[]{new Vector3(x0,y1,z0),new Vector3(x1,y1,z0),new Vector3(x1,y1,z1),new Vector3(x0,y1,z1)},u+depth,v,width,depth,1);
            face(new[]{new Vector3(x0,y0,z1),new Vector3(x1,y0,z1),new Vector3(x1,y0,z0),new Vector3(x0,y0,z0)},u+depth+width,v,width,depth,.6f);
            var mesh=new Mesh{name="Minecraft cuboid"};mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetColors(colors);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
        internal static bool Item(Transform holder,string id,string assets,int layer,int order,bool leftHand=false)
        {
            if(string.IsNullOrEmpty(id)||id=="minecraft:air") {holder.gameObject.SetActive(false);return true;}
            if(GameplayBackend.Standalone&&Standalone.NativeItemModels.Draw(holder,id,layer,order,leftHand))return true;
            string[] parts=id.Split(':');if(parts.Length!=2||parts.Any(p=>p.Any(c=>!char.IsLetterOrDigit(c)&&c!='_'&&c!='/'))) return false;
            bool grip=holder.name=="HollowCraft.HeldItem3D"||holder.name=="HollowCraft.Offhand3D";
            string folder=grip?"scene_models_v2":"scene_models";
            string renderId=id+(leftHand?"_left_hand":"")+(grip?"_grip":"");
            string file=Path.Combine(assets,folder,parts[0],parts[1]+(leftHand?"_left_hand":"")+".json");if(!File.Exists(file)) return GameplayBackend.Standalone&&Standalone.NativeAssets.Item(holder,id,layer,order,leftHand);
            holder.gameObject.SetActive(true);
            var data=JObject.Parse(File.ReadAllText(file));var quads=(JArray)data["quads"];
            if(quads.Count==0) return Builtin(holder,id,assets,layer,order,grip?data["builtinMatrix"] as JArray:null);
            foreach(Transform child in holder){child.gameObject.SetActive(false);UnityEngine.Object.Destroy(child.gameObject);}
            foreach(var group in quads.OfType<JObject>().GroupBy(q=>(string)q["atlas"])) {
                string atlasFile=Path.Combine(assets,folder,group.Key+".png");if(!File.Exists(atlasFile)) return false;
                if(!atlases.TryGetValue(atlasFile,out Texture2D texture)) {texture=new Texture2D(2,2,TextureFormat.RGBA32,false){filterMode=FilterMode.Point};texture.LoadImage(File.ReadAllBytes(atlasFile));atlases[atlasFile]=texture;}
                string key=renderId+":"+group.Key;
                if(!meshes.TryGetValue(key,out Mesh mesh)) {
                    var vertices=new List<Vector3>();var uv=new List<Vector2>();var colors=new List<Color>();var indices=new List<int>();
                    foreach(var quad in group) {
                        int start=vertices.Count;
                        foreach(JArray point in (JArray)quad["vertices"]) {vertices.Add(new Vector3((float)point[0],(float)point[1],(float)point[2]));uv.Add(new Vector2((float)point[3],(float)point[4]));colors.Add(new Color((float)point[5],(float)point[6],(float)point[7],1));}
                        indices.AddRange(new[]{start,start+1,start+2,start,start+2,start+3});
                    }
                    mesh=new Mesh{name="Minecraft baked "+id};mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetColors(colors);mesh.SetTriangles(indices,0);mesh.RecalculateNormals();mesh.RecalculateBounds();meshes[key]=mesh;
                }
                var go=new GameObject("Minecraft baked model "+id);go.transform.SetParent(holder,false);Renderer(go,mesh,texture,layer,order);
            }
            return true;
        }
        internal static bool Arrow(Transform holder,string assets,int layer,int order) {
            string file=Path.Combine(assets,"arrow_projectile.png");if(!File.Exists(file))return GameplayBackend.Standalone&&Standalone.NativeAssets.Item(holder,"minecraft:arrow",layer,order,false);
            if(!atlases.TryGetValue(file,out Texture2D texture)) {texture=new Texture2D(2,2,TextureFormat.RGBA32,false){filterMode=FilterMode.Point};texture.LoadImage(File.ReadAllBytes(file));atlases[file]=texture;}
            const string key="vanilla_projectile_arrow";
            if(!meshes.TryGetValue(key,out Mesh mesh)) {
                var vertices=new List<Vector3>();var uv=new List<Vector2>();var colors=new List<Color>();var indices=new List<int>();
                Action<Vector3[],Vector2[],float> face=(points,tex,rotation)=> {
                    int start=vertices.Count;var rotate=Quaternion.Euler(rotation,0,0);
                    foreach(var point in points)vertices.Add(rotate*(point-new Vector3(4,0,0))*.05625f);
                    foreach(var coord in tex)uv.Add(new Vector2(coord.x,1-coord.y));
                    for(int n=0;n<4;n++)colors.Add(Color.white);
                    indices.AddRange(new[]{start,start+1,start+2,start,start+2,start+3});
                };
                var tip=new[]{new Vector3(-7,-2,-2),new Vector3(-7,-2,2),new Vector3(-7,2,2),new Vector3(-7,2,-2)};
                var tipUv=new[]{new Vector2(0,.15625f),new Vector2(.15625f,.15625f),new Vector2(.15625f,.3125f),new Vector2(0,.3125f)};
                face(tip,tipUv,45);face(tip.Reverse().ToArray(),tipUv.Reverse().ToArray(),45);
                for(int n=0;n<4;n++)face(new[]{new Vector3(-8,-2,0),new Vector3(8,-2,0),new Vector3(8,2,0),new Vector3(-8,2,0)},
                    new[]{new Vector2(0,0),new Vector2(.5f,0),new Vector2(.5f,.15625f),new Vector2(0,.15625f)},45+90*n);
                mesh=new Mesh{name="MC vanilla arrow projectile"};mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetColors(colors);mesh.SetTriangles(indices,0);mesh.RecalculateNormals();mesh.RecalculateBounds();meshes[key]=mesh;
            }
            var go=new GameObject("Minecraft arrow flight model");go.transform.SetParent(holder,false);Renderer(go,mesh,texture,layer,order);return true;
        }
        internal static bool Builtin(Transform holder,string id,string assets,int layer,int order,JArray grip=null)
        {
            string asset=id.StartsWith("minecraft:shield")?"shield_model":id=="minecraft:trident"?"trident_model":id=="minecraft:chest"?"chest_model":null;
            if(asset==null) {holder.gameObject.SetActive(false);return true;}
            string file=Path.Combine(assets,asset+".png");if(!File.Exists(file)) return false;
            holder.gameObject.SetActive(true);
            if(!atlases.TryGetValue(file,out Texture2D texture)) {texture=new Texture2D(2,2,TextureFormat.RGBA32,false){filterMode=FilterMode.Point};texture.LoadImage(File.ReadAllBytes(file));atlases[file]=texture;}
            foreach(Transform child in holder){child.gameObject.SetActive(false);UnityEngine.Object.Destroy(child.gameObject);}
            var matrix=Matrix4x4.identity;
            if(grip?.Count==16)for(int r=0;r<4;r++)for(int c=0;c<4;c++)matrix[r,c]=(float)grip[c*4+r];
            Action<string,int,int,int,int,int,Vector3> part=(name,w,h,d,u,v,position)=> {
                var go=new GameObject(name);go.transform.SetParent(holder,false);go.transform.localPosition=position;
                var mesh=Cuboid(w,h,d,u,v,false,texture);
                if(grip!=null&&(asset=="shield_model"||asset=="trident_model")) {
                    var vertices=mesh.vertices;for(int i=0;i<vertices.Length;i++){var p=matrix.MultiplyPoint3x4(vertices[i]+position);vertices[i]=new Vector3(-p.x,-p.y,p.z);}
                    mesh.vertices=vertices;mesh.RecalculateBounds();mesh.RecalculateNormals();go.transform.localPosition=Vector3.zero;
                }
                Renderer(go,mesh,texture,layer,order);
            };
            if(asset=="shield_model") {
                part("Shield plate",12,22,1,0,0,new Vector3(0,0,grip!=null?-.09375f:-.075f));part("Shield handle",2,6,6,26,0,new Vector3(0,0,grip!=null?.125f:.1f));
            } else if(asset=="trident_model") {
                part("Trident shaft",1,25,1,0,6,new Vector3(0,grip!=null?.90625f:0,0));part("Trident crossbar",3,2,1,4,0,new Vector3(0,grip!=null?.0625f:.8f,0));
                part("Trident left prong",1,4,1,4,3,new Vector3(-.125f,grip!=null?-.0625f:.95f,0));part("Trident center prong",1,4,1,0,0,new Vector3(0,grip!=null?-.125f:1.02f,0));part("Trident right prong",1,4,1,4,3,new Vector3(.125f,grip!=null?-.0625f:.95f,0));
            } else {
                part("Chest bottom",14,10,14,0,19,new Vector3(0,-.16f,0));part("Chest lid",14,5,14,0,0,new Vector3(0,.31f,0));part("Chest latch",2,4,1,0,0,new Vector3(0,.16f,-.47f));
            }
            holder.gameObject.SetActive(true);return true;
        }
    }
}
