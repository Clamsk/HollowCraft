using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace HollowCraft.Standalone
{
    internal static class NativeBuiltinItems
    {
        private static JObject descriptions;
        private static readonly Dictionary<string,Mesh> cache=new Dictionary<string,Mesh>();
        private static Vector3 V(JToken value,Vector3 fallback)=>value is JArray a?new Vector3((float)a[0],(float)a[1],(float)a[2]):fallback;
        internal static bool Draw(Transform holder,string id,Matrix4x4 matrix,int layer,int order,bool left) {
            string kind=id.Contains("shield")?"shield":NativeHandModels.Trident(id)?"trident":null;if(kind==null)return false;
            if(descriptions==null){string path=Path.Combine(NativeAssets.Root??"","native-held-models.json");if(!File.Exists(path))return false;descriptions=JObject.Parse(File.ReadAllText(path));}
            var source=descriptions["models"]?[kind] as JObject;if(source==null)return false;string key=id+"/"+left;
            if(!cache.TryGetValue(key,out var mesh)) {
                var points=new List<Vector3>();var uvs=new List<Vector2>();var colors=new List<Color>();var indices=new List<int>();Build(source,matrix,points,uvs,colors,indices);
                mesh=new Mesh{name="Original articulated held "+key};mesh.SetVertices(points);mesh.SetUVs(0,uvs);mesh.SetColors(colors);mesh.SetTriangles(indices,0);mesh.RecalculateBounds();cache[key]=mesh;
            }
            foreach(Transform child in holder){child.gameObject.SetActive(false);UnityEngine.Object.Destroy(child.gameObject);}holder.gameObject.SetActive(true);var model=new GameObject("Original "+kind+" geometry");model.transform.SetParent(holder,false);MinecraftMeshes.Renderer(model,mesh,NativeAssets.Texture(kind+"_model"),layer,order);return true;
        }
        private static void Build(JObject part,Matrix4x4 parent,List<Vector3> points,List<Vector2> uvs,List<Color> colors,List<int> indices) {
            var a=V(part["rotation"],Vector3.zero);var rotation=Quaternion.AngleAxis(a.z*Mathf.Rad2Deg,Vector3.forward)*Quaternion.AngleAxis(a.y*Mathf.Rad2Deg,Vector3.up)*Quaternion.AngleAxis(a.x*Mathf.Rad2Deg,Vector3.right);
            var transform=parent*Matrix4x4.TRS(V(part["pivot"],Vector3.zero)/16,rotation,V(part["scale"],Vector3.one));
            foreach(JArray face in part["faces"] as JArray??new JArray()) {int start=points.Count;foreach(JArray v in face){var p=transform.MultiplyPoint3x4(new Vector3((float)v[0],(float)v[1],(float)v[2])/16);points.Add(new Vector3(-p.x,-p.y,p.z));uvs.Add(new Vector2((float)v[3],1-(float)v[4]));colors.Add(Color.white);}for(int i=1;i+1<face.Count;i++)indices.AddRange(new[]{start,start+i,start+i+1});}
            foreach(var child in part["children"] as JObject??new JObject())Build((JObject)child.Value,transform,points,uvs,colors,indices);
        }
    }
}
