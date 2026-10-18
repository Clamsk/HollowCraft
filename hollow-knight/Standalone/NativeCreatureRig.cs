using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace HollowCraft.Standalone
{
    // Runtime builds only Unity geometry from static descriptions, never Java objects.
    internal sealed class NativeCreatureRig
    {
        private struct Bone {internal Transform Transform;internal string Name;internal Vector3 Angles;}
        private static readonly Dictionary<string,Mesh> meshes=new Dictionary<string,Mesh>();
        private readonly List<Bone> bones=new List<Bone>();private readonly NativeMobDefinition definition;
        internal int ImageSize;private readonly Transform anchor;
        private static Vector3 Vector(JToken value,Vector3 fallback)=>value is JArray a&&a.Count>=3?new Vector3((float)a[0],(float)a[1],(float)a[2]):fallback;
        private static Quaternion Rotation(Vector3 angles){var q=NativeRigSpace.Rotation(angles.x,angles.y,angles.z);return new Quaternion(q.X,q.Y,q.Z,q.W);}
        internal NativeCreatureRig(Transform parent,NativeMobDefinition definition,float height) {
            this.definition=definition;anchor=new GameObject("Vanilla articulated parts").transform;anchor.SetParent(parent,false);anchor.localPosition=new Vector3(0,1.5f,0);
            Build(anchor,"root",definition.Model,definition.Texture,definition.Id+"/main");
            foreach(JObject layer in definition.Layers)Build(anchor,"overlay",(JObject)layer["model"],(string)layer["texture"],definition.Id+"/"+(string)layer["texture"]);
            Pose(8,false,new JObject());
            Bounds bounds=new Bounds();bool found=false;foreach(var renderer in anchor.GetComponentsInChildren<MeshRenderer>()){if(!found){bounds=renderer.bounds;found=true;}else bounds.Encapsulate(renderer.bounds);}
            if(found&&bounds.size.y>.001f) {
                float size=Mathf.Clamp(height/bounds.size.y,.1f,8);float floor=bounds.min.y-parent.position.y;
                anchor.localScale=Vector3.one*size;anchor.localPosition=new Vector3(0,(1.5f-floor)*size,0);
                float extent=Math.Max(height,Math.Max(bounds.size.x,bounds.size.z)*size);
                ImageSize=Mathf.Clamp(Mathf.NextPowerOfTwo(Mathf.CeilToInt(extent*64+40)),128,512);
            }else ImageSize=160;
        }
        private void Build(Transform parent,string name,JObject source,string texture,string path) {
            var bone=new GameObject(name).transform;bone.SetParent(parent,false);Vector3 pivot=Vector(source["pivot"],Vector3.zero),angles=Vector(source["rotation"],Vector3.zero);
            bone.localPosition=new Vector3(-pivot.x,-pivot.y,pivot.z)/16;bone.localRotation=Rotation(angles);bone.localScale=Vector(source["scale"],Vector3.one);bones.Add(new Bone{Transform=bone,Name=name.ToLowerInvariant(),Angles=angles});
            if(source["faces"] is JArray faces&&faces.Count>0) {
                if(!meshes.TryGetValue(path,out var mesh)) {
                    var points=new List<Vector3>();var uv=new List<Vector2>();var colors=new List<Color>();var indices=new List<int>();
                    foreach(JArray face in faces) {
                        int start=points.Count;foreach(JArray vertex in face){points.Add(new Vector3(-(float)vertex[0],-(float)vertex[1],(float)vertex[2])/16);uv.Add(new Vector2((float)vertex[3],1-(float)vertex[4]));}
                        var normal=Vector3.Cross(points[start+1]-points[start],points[start+2]-points[start]).normalized;float shade=Math.Abs(normal.y)>.5f?normal.y>0?1:.55f:Math.Abs(normal.x)>.5f?.7f:.85f;
                        for(int i=0;i<face.Count;i++)colors.Add(new Color(shade,shade,shade,1));for(int i=1;i+1<face.Count;i++)indices.AddRange(new[]{start,start+i,start+i+1});
                    }
                    mesh=new Mesh{name="Vanilla mob model part "+path};mesh.SetVertices(points);mesh.SetUVs(0,uv);mesh.SetColors(colors);mesh.SetTriangles(indices,0);mesh.RecalculateBounds();meshes[path]=mesh;
                }
                MinecraftMeshes.Renderer(bone.gameObject,mesh,NativeAssets.Texture(texture),0,25).enabled=false;
            }
            foreach(var child in source["children"] as JObject??new JObject())Build(bone,child.Key,(JObject)child.Value,texture,path+"/"+child.Key);
        }
        internal void Pose(int frame,bool sitting,JObject data) {
            float phase=frame==8?0:frame*Mathf.PI/4;bool moving=frame!=8;
            foreach(var bone in bones) {
                string name=bone.Name;var a=bone.Angles;
                bool hidden=(name.Contains("saddle")||name.Contains("rein"))&&(bool?)data["saddled"]!=true||name.Contains("chest")&&(bool?)data["chested"]!=true||name.Contains("baby")&&(bool?)data["baby"]!=true||name=="croaking_body"||name=="egg_belly"||name=="overlay"&&(bool?)data["sheared"]==true;
                bone.Transform.gameObject.SetActive(!hidden);if(hidden)continue;
                if(name.Contains("leg")||name.Contains("hind")||name.Contains("front")) {
                    float opposite=name.Contains("left")?Mathf.PI:0;if(name.Contains("hind"))opposite+=Mathf.PI;
                    a.x+=sitting?-1.15f:moving?Mathf.Cos(phase+opposite)*.65f:0;
                }else if(name.Contains("arm")&&!name.Contains("warm")) {
                    if(definition.Id.Contains("zombie")||definition.Id.EndsWith(":husk"))a.x=-1.4f;
                    else a.x+=sitting?-.6f:moving?Mathf.Cos(phase+(name.Contains("left")?0:Mathf.PI))*.5f:0;
                    if((int?)data["attacking"]>0)a.x=-1.35f;
                }else if(name.Contains("wing")&&definition.Motion=="fly")a.z+=(name.Contains("left")?1:-1)*(moving?Mathf.Sin(phase)*.6f:.12f);
                else if(name.Contains("tentacle"))a.x+=moving?Mathf.Sin(phase)*.3f:0;
                bone.Transform.localRotation=Rotation(a);
            }
        }
    }
}
