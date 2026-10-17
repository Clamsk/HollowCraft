using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace HollowCraft.Standalone
{
    // Authored procedural fallbacks; imported game textures are entirely optional.
    internal static class NativeAssets
    {
        private static readonly Dictionary<string,Texture2D> textures=new Dictionary<string,Texture2D>();
        private static readonly Dictionary<Texture,Material> materials=new Dictionary<Texture,Material>();
        private static readonly Dictionary<string,Mesh> generatedMeshes=new Dictionary<string,Mesh>();
        internal static string Root;
        internal static Texture2D Texture(string id,string assets=null) {
            if(string.IsNullOrEmpty(id)||id=="minecraft:air")return null;
            assets=assets??Root;
            if(textures.TryGetValue(id,out var cached)&&cached!=null)return cached;string name=id.Substring(id.IndexOf(':')+1);
            if(assets!=null&&name.IndexOfAny(new[]{'/', '\\', '.', ':'})<0){string path=Path.Combine(assets,name+".png");if(File.Exists(path)){var imported=new Texture2D(2,2,TextureFormat.RGBA32,false){filterMode=FilterMode.Point,name=id};imported.LoadImage(File.ReadAllBytes(path));textures[id]=imported;return imported;}}
            if(id.StartsWith("gui_",StringComparison.Ordinal))return null;
            bool armor=name.StartsWith("armor_",StringComparison.Ordinal);int w=armor?64:16,h=armor?32:16;var image=new Texture2D(w,h,TextureFormat.RGBA32,false){filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp};var pixels=new Color[w*h];Color color=ColorFor(name);
            bool block=NativeCatalog.Get(id).Block||armor;
            for(int y=0;y<h;y++)for(int x=0;x<w;x++) {
                bool draw=block;
                if(!block){if(name.Contains("sword")||name.Contains("zenith"))draw=Math.Abs(x-y)<=1&&x>=3&&x<=13||y>=3&&y<=4&&x>=1&&x<=7;
                    else if(name.Contains("pickaxe")||name.Contains("axe")||name.Contains("hoe"))draw=Math.Abs(x-y)<=1&&x>2&&x<12||y>=10&&y<=12&&x>=4&&x<=13;
                    else if(name.Contains("bow"))draw=(x-6)*(x-6)+(y-8)*(y-8)>=30&&(x-6)*(x-6)+(y-8)*(y-8)<=55&&x>5||x==5&&y>=2&&y<=14;
                    else if(name.Contains("shield"))draw=x>=3&&x<=12&&y>=2&&y<=13&&(y>4||Math.Abs(x-7)<y);
                    else if(name.Contains("bucket"))draw=y>=3&&y<=12&&x>=2&&x<=13&&(y<10||x==2||x==13);
                    else if(name.Contains("arrow")||name.Contains("trident"))draw=Math.Abs(x-y)<=1&&x>=1&&x<=13||x>=11&&y>=11&&x+y>=24;
                    else draw=(x-7)*(x-7)+(y-7)*(y-7)<40;}
                if(draw){float shade=.8f+((x*17+y*31)%7)*.035f;pixels[y*w+x]=new Color(color.r*shade,color.g*shade,color.b*shade,1);}
            }image.SetPixels(pixels);image.Apply(false,false);textures[id]=image;return image;
        }
        private static Color ColorFor(string name) {
            if(name.Contains("water")||name.Contains("lapis"))return new Color(.18f,.42f,.95f);if(name.Contains("lava")||name.Contains("flint_and_steel"))return new Color(1,.35f,.06f);
            if(name.Contains("diamond")||name.Contains("zenith"))return new Color(.14f,.9f,.85f);if(name.Contains("gold")||name.Contains("totem"))return new Color(1,.78f,.15f);
            if(name.Contains("iron")||name.Contains("bucket")||name.Contains("armor_"))return new Color(.78f,.79f,.82f);if(name.Contains("oak")||name.Contains("wood")||name.Contains("bow")||name.Contains("chest")||name.Contains("stick"))return new Color(.55f,.35f,.17f);
            if(name.Contains("grass")||name.Contains("leaves")||name.Contains("pearl"))return new Color(.22f,.58f,.28f);if(name.Contains("tnt")||name.Contains("redstone")||name.Contains("apple"))return new Color(.87f,.13f,.12f);
            if(name.Contains("beef")||name.Contains("bread"))return new Color(.64f,.32f,.14f);if(name.Contains("bedrock")||name.Contains("coal")||name.Contains("netherite"))return new Color(.2f,.21f,.24f);return new Color(.52f,.55f,.6f);
        }
        internal static Texture2D Skin() {
            const string key="native_skin";if(textures.TryGetValue(key,out var cached)&&cached!=null)return cached;var skin=new Texture2D(64,64,TextureFormat.RGBA32,false){filterMode=FilterMode.Point};var colors=new Color[64*64];for(int i=0;i<colors.Length;i++)colors[i]=new Color(.64f,.43f,.31f,1);
            Action<int,int,int,int,Color> rect=(x,y,w,h,c)=>{for(int row=y;row<y+h;row++)for(int col=x;col<x+w;col++)colors[(63-row)*64+col]=c;};
            rect(0,0,32,8,new Color(.19f,.12f,.08f));rect(8,8,8,8,new Color(.67f,.46f,.34f));rect(8,8,8,2,new Color(.18f,.11f,.08f));rect(9,11,2,1,Color.white);rect(13,11,2,1,Color.white);rect(10,11,1,1,new Color(.2f,.38f,.8f));rect(13,11,1,1,new Color(.2f,.38f,.8f));rect(11,14,3,1,new Color(.3f,.16f,.12f));
            rect(16,16,24,16,new Color(.05f,.58f,.62f));rect(40,16,16,6,new Color(.05f,.58f,.62f));rect(32,48,16,6,new Color(.05f,.58f,.62f));rect(0,16,16,16,new Color(.23f,.25f,.61f));rect(16,48,16,16,new Color(.23f,.25f,.61f));skin.SetPixels(colors);skin.Apply(false,false);textures[key]=skin;return skin;
        }
        internal static Material Cutout(UnityEngine.Texture texture) {if(materials.TryGetValue(texture,out var value))return value;value=new Material(Shader.Find("Unlit/Transparent Cutout")??Shader.Find("Sprites/Default")){mainTexture=texture};if(value.HasProperty("_Cutoff"))value.SetFloat("_Cutoff",.1f);materials[texture]=value;return value;}
        internal static Mesh Extruded(Texture2D texture) {
            string key=texture.GetInstanceID().ToString();if(generatedMeshes.TryGetValue(key,out var cached))return cached;
            var points=new List<Vector3>();var coords=new List<Vector2>();var triangles=new List<int>();var shades=new List<Color>();var pixels=texture.GetPixels32();int width=texture.width,height=texture.height;
            Func<int,int,bool> opaque=(x,y)=>x>=0&&y>=0&&x<width&&y<height&&pixels[y*width+x].a>25;
            Action<Vector3[],Vector2[],float> face=(vertices,uv,shade)=>{int start=points.Count;points.AddRange(vertices);coords.AddRange(uv);for(int i=0;i<4;i++)shades.Add(new Color(shade,shade,shade,1));triangles.AddRange(new[]{start,start+1,start+2,start,start+2,start+3});};
            const float z=.03125f;face(new[]{new Vector3(-.5f,-.5f,-z),new Vector3(.5f,-.5f,-z),new Vector3(.5f,.5f,-z),new Vector3(-.5f,.5f,-z)},new[]{Vector2.zero,Vector2.right,Vector2.one,Vector2.up},1);
            face(new[]{new Vector3(.5f,-.5f,z),new Vector3(-.5f,-.5f,z),new Vector3(-.5f,.5f,z),new Vector3(.5f,.5f,z)},new[]{Vector2.right,Vector2.zero,Vector2.up,Vector2.one},.85f);
            for(int y=0;y<height;y++)for(int x=0;x<width;x++)if(opaque(x,y)) {
                float x0=x/(float)width-.5f,x1=(x+1f)/width-.5f,y0=y/(float)height-.5f,y1=(y+1f)/height-.5f;var sample=new Vector2((x+.5f)/width,(y+.5f)/height);var uv=new[]{sample,sample,sample,sample};
                if(!opaque(x-1,y))face(new[]{new Vector3(x0,y0,z),new Vector3(x0,y0,-z),new Vector3(x0,y1,-z),new Vector3(x0,y1,z)},uv,.7f);
                if(!opaque(x+1,y))face(new[]{new Vector3(x1,y0,-z),new Vector3(x1,y0,z),new Vector3(x1,y1,z),new Vector3(x1,y1,-z)},uv,.8f);
                if(!opaque(x,y-1))face(new[]{new Vector3(x0,y0,z),new Vector3(x1,y0,z),new Vector3(x1,y0,-z),new Vector3(x0,y0,-z)},uv,.6f);
                if(!opaque(x,y+1))face(new[]{new Vector3(x0,y1,-z),new Vector3(x1,y1,-z),new Vector3(x1,y1,z),new Vector3(x0,y1,z)},uv,1);
            }
            var mesh=new Mesh{name="Native generated item extrusion"};mesh.SetVertices(points);mesh.SetUVs(0,coords);mesh.SetColors(shades);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();generatedMeshes[key]=mesh;return mesh;
        }
        internal static Texture2D Icon(string id,string assets) {
            var source=Texture(id,assets);if(source==null||!NativeCatalog.Get(id).Block||id=="minecraft:water"||id=="minecraft:lava")return source;
            var model=NativeItemModels.Icon(id);if(model!=null)return model;
            string key=id+"#native_icon";if(textures.TryGetValue(key,out var cached)&&cached!=null)return cached;var output=new Texture2D(32,32,TextureFormat.RGBA32,false){filterMode=FilterMode.Point};var pixels=new Color[1024];
            Action<Vector2,Vector2,Vector2,Color> triangle=(a,b,c,shade)=>{float area=(b.x-a.x)*(c.y-a.y)-(b.y-a.y)*(c.x-a.x);for(int y=0;y<32;y++)for(int x=0;x<32;x++){float u=((x+.5f-a.x)*(c.y-a.y)-(y+.5f-a.y)*(c.x-a.x))/area,v=((b.x-a.x)*(y+.5f-a.y)-(b.y-a.y)*(x+.5f-a.x))/area;if(u<0||v<0||u+v>1)continue;var tex=source.GetPixel(x%source.width,y%Mathf.Min(source.height,source.width));pixels[y*32+x]=tex*shade;}};
            Action<Vector2,Vector2,Vector2,Vector2,float> quad=(a,b,c,d,shade)=>{var color=new Color(shade,shade,shade,1);triangle(a,b,c,color);triangle(a,c,d,color);};
            quad(new Vector2(2,22),new Vector2(16,14),new Vector2(16,0),new Vector2(2,8),.7f);quad(new Vector2(16,14),new Vector2(30,22),new Vector2(30,8),new Vector2(16,0),.85f);quad(new Vector2(2,22),new Vector2(16,30),new Vector2(30,22),new Vector2(16,14),1);
            output.SetPixels(pixels);output.Apply(false,false);textures[key]=output;return output;
        }
        internal static bool Item(Transform holder,string id,int layer,int order,bool leftHand) {
            foreach(Transform child in holder){child.gameObject.SetActive(false);UnityEngine.Object.Destroy(child.gameObject);}if(string.IsNullOrEmpty(id)||id=="minecraft:air"){holder.gameObject.SetActive(false);return true;}holder.gameObject.SetActive(true);
            var root=new GameObject("Native item "+id);root.transform.SetParent(holder,false);bool grip=holder.name.Contains("HeldItem")||holder.name.Contains("Offhand");if(grip)root.transform.localPosition=new Vector3(leftHand?-.0625f:.0625f,-.6f,-.1f);Texture2D texture=Texture(id);
            if(!NativeCatalog.Get(id).Block&&!id.Contains("shield")&&!id.Contains("trident")&&!id.Contains("arrow")){
                if(grip){root.transform.localRotation=Quaternion.Euler(0,leftHand?90:-90,leftHand?-55:55);root.transform.localScale=Vector3.one*.85f;root.transform.localPosition+=new Vector3(0,.22f,0);}
                MinecraftMeshes.Renderer(root,Extruded(texture),texture,layer,order);return true;
            }
            if((id.StartsWith("minecraft:shield",StringComparison.Ordinal)||id=="minecraft:trident")&&Root!=null&&File.Exists(Path.Combine(Root,id.Contains("shield")?"shield_model.png":"trident_model.png")))return MinecraftMeshes.Builtin(root.transform,id,Root,layer,order);
            Action<string,Vector3,Vector3> part=(name,size,position)=>{var go=new GameObject(name);go.transform.SetParent(root.transform,false);go.transform.localPosition=position;var mesh=MinecraftMeshes.Cuboid(size.x,size.y,size.z,0,0,false,texture);if(NativeCatalog.Get(id).Block){var uv=mesh.uv;for(int i=0;i<uv.Length;i++)uv[i]=i%4==0?Vector2.zero:i%4==1?Vector2.right:i%4==2?Vector2.one:Vector2.up;mesh.uv=uv;}var renderer=MinecraftMeshes.Renderer(go,mesh,texture,layer,order);renderer.sharedMaterial=Cutout(texture);};
            if(NativeCatalog.Get(id).Block)part("Block",new Vector3(6,6,6),Vector3.zero);
            else if(id.Contains("sword")||id.Contains("zenith")){part("Grip",new Vector3(2,5,2),Vector3.zero);part("Guard",new Vector3(7,1,2),new Vector3(0,.13f,0));part("Blade",new Vector3(2,12,1),new Vector3(0,.5f,0));}
            else if(id.Contains("pickaxe")||id.Contains("axe")||id.Contains("shovel")||id.Contains("hoe")){part("Handle",new Vector3(1.5f,13,1.5f),new Vector3(0,.2f,0));part("Tool head",new Vector3(id.Contains("shovel")?4:10,3,2),new Vector3(0,.6f,0));}
            else if(id.Contains("bow")){part("Upper limb",new Vector3(2,6,2),new Vector3(.1f,.3f,0));part("Lower limb",new Vector3(2,6,2),new Vector3(.1f,-.3f,0));part("String",new Vector3(.2f,12,.2f),new Vector3(-.1f,0,0));}
            else if(id.Contains("shield"))part("Shield",new Vector3(10,15,2),new Vector3(0,.05f,0));
            else if(id.Contains("trident")||id.Contains("arrow")){part("Shaft",new Vector3(1,20,1),new Vector3(0,.3f,0));for(int i=-1;i<=1;i++)part("Prong",new Vector3(1,5,1),new Vector3(i*.12f,1,0));}
            else part("Item",new Vector3(6,7,2),Vector3.zero);return true;
        }
        internal static Sprite SwordSprite(){var image=Texture("hollowcraft_bridge:zenith");return Sprite.Create(image,new Rect(0,0,image.width,image.height),new Vector2(.5f,.5f),1/.0375f);}
    }
}
