using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace HollowCraft.Standalone
{
    // Reads static vanilla model JSON. The hand matrix follows the same sequence
    // as the bridge's ItemSceneModels exporter, including left-hand mirroring.
    internal static class NativeItemModels
    {
        private sealed class Model {internal JObject Textures=new JObject(),Display=new JObject();internal JArray Elements;internal bool Builtin;}
        private sealed class Part {internal Mesh Mesh;internal Texture2D Texture;}
        private sealed class Builder {internal readonly List<Vector3> Points=new List<Vector3>();internal readonly List<Vector2> UV=new List<Vector2>();internal readonly List<Color> Colors=new List<Color>();internal readonly List<int> Indices=new List<int>();}
        private static readonly Dictionary<string,Model> models=new Dictionary<string,Model>();
        private static readonly Dictionary<string,List<Part>> meshes=new Dictionary<string,List<Part>>();
        private static readonly Dictionary<string,Texture2D> icons=new Dictionary<string,Texture2D>();private static readonly NativeModelView iconRaster=new NativeModelView();
        private static Model Read(string id,int depth=0) {
            if(depth>20||id.Contains("..")||id.Contains('\\'))return null;if(!id.Contains(":"))id="minecraft:"+id;
            if(models.TryGetValue(id,out var cached))return cached;string[] split=id.Split(':');if(split.Length!=2)return null;
            string path=Path.Combine(NativeAssets.Root??"","models",split[0],split[1]+".json");if(!File.Exists(path))return null;
            var json=JObject.Parse(File.ReadAllText(path));string parent=(string)json["parent"]??"";var inherited=parent.Length>0?Read(parent,depth+1):null;
            var result=new Model{Textures=inherited!=null?(JObject)inherited.Textures.DeepClone():new JObject(),Display=inherited!=null?(JObject)inherited.Display.DeepClone():new JObject(),Elements=json["elements"] as JArray??inherited?.Elements,Builtin=parent=="builtin/entity"||inherited?.Builtin==true};
            foreach(var entry in json["textures"] as JObject??new JObject())result.Textures[entry.Key]=entry.Value.DeepClone();foreach(var entry in json["display"] as JObject??new JObject())result.Display[entry.Key]=entry.Value.DeepClone();models[id]=result;return result;
        }
        private static Vector3 Vector(JToken token,Vector3 fallback)=>token is JArray a&&a.Count>=3?new Vector3((float)a[0],(float)a[1],(float)a[2]):fallback;
        private static Quaternion Rotation(Vector3 degrees)=>Quaternion.AngleAxis(degrees.x,Vector3.right)*Quaternion.AngleAxis(degrees.y,Vector3.up)*Quaternion.AngleAxis(degrees.z,Vector3.forward);
        private static Matrix4x4 Transform(Model model,bool grip,bool left,bool gui=false) {
            string kind=gui?"gui":grip?left?"thirdperson_lefthand":"thirdperson_righthand":"ground";var display=model.Display[kind] as JObject;if(display==null&&left&&grip)display=model.Display["thirdperson_righthand"] as JObject;
            Vector3 offset=Vector(display?["translation"],Vector3.zero)/16,angles=Vector(display?["rotation"],Vector3.zero),scale=Vector(display?["scale"],Vector3.one);
            if(left){offset.x=-offset.x;angles.y=-angles.y;angles.z=-angles.z;}
            var transform=Matrix4x4.TRS(offset,Rotation(angles),scale);
            return grip?Matrix4x4.Rotate(Quaternion.AngleAxis(-90,Vector3.right))*Matrix4x4.Rotate(Quaternion.AngleAxis(180,Vector3.up))*Matrix4x4.Translate(new Vector3((left?-1:1)/16f,.125f,-.625f))*transform:transform;
        }
        private static Texture2D Image(Model model,string key) {
            for(int i=0;i<16&&key!=null&&key.StartsWith("#",StringComparison.Ordinal);i++)key=(string)model.Textures[key.Substring(1)];if(string.IsNullOrEmpty(key))return null;
            string name=key.Split('/').Last().Split(':').Last();return NativeAssets.Texture("minecraft:"+name);
        }
        private static Mesh Bake(Mesh source,Matrix4x4 transform,bool gui=false) {var mesh=UnityEngine.Object.Instantiate(source);var points=mesh.vertices;for(int i=0;i<points.Length;i++){var p=transform.MultiplyPoint3x4(points[i]);points[i]=gui?new Vector3(p.x,p.y,-p.z):new Vector3(-p.x,-p.y,p.z);}mesh.vertices=points;mesh.RecalculateBounds();return mesh;}
        internal static bool Draw(Transform holder,string id,int layer,int order,bool left,bool gui=false) {
            if(string.IsNullOrEmpty(id)||!id.StartsWith("minecraft:",StringComparison.Ordinal))return false;
            string item=id.Substring(10),modelId="minecraft:item/"+(item=="trident"?"trident_in_hand":item);var model=Read(modelId);if(model==null)return false;
            bool grip=holder.name=="HollowCraft.HeldItem3D"||holder.name=="HollowCraft.Offhand3D";var transform=Transform(model,grip,left,gui);if(gui&&model.Builtin)return false;
            if(model.Builtin){if(!item.StartsWith("shield",StringComparison.Ordinal)&&item!="trident"&&item!="chest")return false;var matrix=grip?transform*Matrix4x4.Translate(new Vector3(-.5f,-.5f,-.5f))*Matrix4x4.Scale(new Vector3(1,-1,-1)):Matrix4x4.identity;var values=new JArray();for(int column=0;column<4;column++)for(int row=0;row<4;row++)values.Add(matrix[row,column]);return MinecraftMeshes.Builtin(holder,id,NativeAssets.Root,layer,order,grip?values:null);}
            string key=id+":"+grip+":"+left+":"+gui;
            if(!meshes.TryGetValue(key,out var parts)) {
                parts=new List<Part>();
                if(model.Elements==null||model.Elements.Count==0){foreach(var entry in model.Textures.Properties().Where(p=>p.Name.StartsWith("layer",StringComparison.Ordinal))){var image=Image(model,(string)entry.Value);if(image!=null)parts.Add(new Part{Texture=image,Mesh=Bake(NativeAssets.Extruded(image),transform,gui)});}}
                else {
                    var groups=new Dictionary<Texture2D,Builder>();
                    foreach(JObject element in model.Elements) {
                        var min=Vector(element["from"],Vector3.zero)/16-Vector3.one*.5f;var max=Vector(element["to"],Vector3.one*16)/16-Vector3.one*.5f;
                        var rotate=element["rotation"] as JObject;var origin=Vector(rotate?["origin"],Vector3.one*8)/16-Vector3.one*.5f;float angle=(float?)rotate?["angle"]??0;string axis=(string)rotate?["axis"]??"y";Vector3 direction=axis=="x"?Vector3.right:axis=="z"?Vector3.forward:Vector3.up;
                        var rescale=Vector3.one;if((bool?)rotate?["rescale"]==true){float factor=1/Mathf.Max(.01f,Mathf.Cos(angle*Mathf.Deg2Rad));rescale=axis=="x"?new Vector3(1,factor,factor):axis=="z"?new Vector3(factor,factor,1):new Vector3(factor,1,factor);}
                        var local=Matrix4x4.Translate(origin)*Matrix4x4.TRS(Vector3.zero,Quaternion.AngleAxis(angle,direction),rescale)*Matrix4x4.Translate(-origin);
                        foreach(var face in element["faces"] as JObject??new JObject()) {
                            var data=(JObject)face.Value;var image=Image(model,(string)data["texture"]);if(image==null)continue;if(!groups.TryGetValue(image,out var builder))groups[image]=builder=new Builder();Vector3[] p;
                            switch(face.Key){case "north":p=new[]{new Vector3(max.x,min.y,min.z),new Vector3(min.x,min.y,min.z),new Vector3(min.x,max.y,min.z),new Vector3(max.x,max.y,min.z)};break;case "south":p=new[]{new Vector3(min.x,min.y,max.z),new Vector3(max.x,min.y,max.z),new Vector3(max.x,max.y,max.z),new Vector3(min.x,max.y,max.z)};break;case "east":p=new[]{new Vector3(max.x,min.y,max.z),new Vector3(max.x,min.y,min.z),new Vector3(max.x,max.y,min.z),new Vector3(max.x,max.y,max.z)};break;case "west":p=new[]{new Vector3(min.x,min.y,min.z),new Vector3(min.x,min.y,max.z),new Vector3(min.x,max.y,max.z),new Vector3(min.x,max.y,min.z)};break;case "up":p=new[]{new Vector3(min.x,max.y,min.z),new Vector3(max.x,max.y,min.z),new Vector3(max.x,max.y,max.z),new Vector3(min.x,max.y,max.z)};break;default:p=new[]{new Vector3(min.x,min.y,max.z),new Vector3(max.x,min.y,max.z),new Vector3(max.x,min.y,min.z),new Vector3(min.x,min.y,min.z)};break;}
                            var tex=data["uv"] as JArray??new JArray(0,0,16,16);float u0=(float)tex[0]/16,u1=(float)tex[2]/16,v0=1-(float)tex[3]/16,v1=1-(float)tex[1]/16;var uv=new[]{new Vector2(u0,v0),new Vector2(u1,v0),new Vector2(u1,v1),new Vector2(u0,v1)};int offset=((int?)data["rotation"]??0)/90,start=builder.Points.Count;float shade=face.Key=="up"?1:face.Key=="down"?.5f:face.Key=="east"||face.Key=="west"?.6f:.8f;
                            Color color=(int?)data["tintindex"]>=0?id.Contains("leaves")?new Color(.282f,.71f,.094f):new Color(.569f,.741f,.349f):Color.white;
                            for(int i=0;i<4;i++){var point=(transform*local).MultiplyPoint3x4(p[i]);builder.Points.Add(gui?new Vector3(point.x,point.y,-point.z):new Vector3(-point.x,-point.y,point.z));builder.UV.Add(uv[(i+offset)%4]);builder.Colors.Add(color*new Color(shade,shade,shade,1));}builder.Indices.AddRange(new[]{start,start+1,start+2,start,start+2,start+3});
                        }
                    }
                    foreach(var group in groups){var b=group.Value;var mesh=new Mesh{name="Native vanilla JSON model "+id};mesh.SetVertices(b.Points);mesh.SetUVs(0,b.UV);mesh.SetColors(b.Colors);mesh.SetTriangles(b.Indices,0);mesh.RecalculateBounds();parts.Add(new Part{Mesh=mesh,Texture=group.Key});}
                }
                if(parts.Count==0)return false;meshes[key]=parts;
            }
            foreach(Transform child in holder){child.gameObject.SetActive(false);UnityEngine.Object.Destroy(child.gameObject);}holder.gameObject.SetActive(true);foreach(var part in parts){var go=new GameObject("Native vanilla held model "+id);go.transform.SetParent(holder,false);MinecraftMeshes.Renderer(go,part.Mesh,part.Texture,layer,order);}return true;
        }
        internal static Texture2D Icon(string id) {
            if(icons.TryGetValue(id,out var cached))return cached;var holder=new GameObject("HollowCraft.GuiItem");
            try{if(!Draw(holder.transform,id,0,0,false,true))return null;var image=iconRaster.Capture(holder,32,Matrix4x4.identity,32,16);icons[id]=image;return image;}
            finally{holder.SetActive(false);UnityEngine.Object.Destroy(holder);}
        }
    }
}
