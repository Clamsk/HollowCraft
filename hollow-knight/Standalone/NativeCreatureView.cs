using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace HollowCraft.Standalone
{
    // Authored vanilla-shaped rigs with imported skins. Quantized poses are shared
    // as depth-tested sprites, avoiding a raster pass for every mob every frame.
    internal sealed class NativeCreatureView : MonoBehaviour
    {
        private static readonly Dictionary<string,Sprite> frames=new Dictionary<string,Sprite>();private static readonly NativeModelView raster=new NativeModelView();
        private string kind;private GameObject model;private Transform[] legs,arms;private SpriteRenderer display;private float phase,lastX,modelHeight;private bool first=true;private Texture2D texture;private NativeCreatureRig imported;
        private static readonly Dictionary<string,float> lastUsed=new Dictionary<string,float>();private static long cachedPixels;
        private Transform Part(string name,float w,float h,float d,float u,float v,Vector3 pivot,Vector3 from,float inflate=0) {
            var bone=new GameObject(name).transform;bone.SetParent(model.transform,false);bone.localPosition=new Vector3(pivot.x,24-pivot.y,pivot.z)/16;
            var mesh=new GameObject("Geometry");mesh.transform.SetParent(bone,false);mesh.transform.localPosition=new Vector3(from.x+w/2,-from.y-h/2,from.z+d/2)/16;
            MinecraftMeshes.Renderer(mesh,MinecraftMeshes.Cuboid(w,h,d,u,v,false,texture,inflate),texture,0,25).enabled=false;return bone;
        }
        private void Create(string id,bool boat,float height) {
            DisposeModel();model=new GameObject("Native textured rig "+id);model.transform.SetParent(transform,false);model.transform.localPosition=new Vector3(0,-height/2,-.05f);kind=id;first=true;legs=arms=null;
            modelHeight=height;
            var blueprint=NativeMobCatalog.Find(id);if(!boat&&blueprint!=null){imported=new NativeCreatureRig(model.transform,blueprint,height);CreateDisplay();return;}
            string name=id.Substring(id.IndexOf(':')+1),file=boat?"boat_"+name.Replace("_chest_boat","").Replace("_boat","").Replace("_chest_raft","").Replace("_raft",""):name=="chicken"?"chicken":name+"_"+name;texture=NativeAssets.Texture("mob_"+file);
            if(boat){Part("Floor",16,3,28,0,0,new Vector3(0,24,0),new Vector3(-8,-3,-14));Part("Port",2,6,28,0,19,new Vector3(-8,21,0),new Vector3(-2,-3,-14));Part("Starboard",2,6,28,0,19,new Vector3(8,21,0),new Vector3(0,-3,-14));Part("Bow",16,6,2,0,27,new Vector3(0,21,-14),new Vector3(-8,-3,-2));Part("Stern",16,6,2,0,27,new Vector3(0,21,14),new Vector3(-8,-3,0));}
            else if(name=="zombie"||name=="skeleton"||name=="villager") {
                Part("Head",8,8,8,0,0,Vector3.zero,new Vector3(-4,-8,-4));Part("Torso",8,12,4,16,16,Vector3.zero,new Vector3(-4,0,-2));
                float legWidth=name=="skeleton"?2:4;legs=new[]{Part("Right leg",legWidth,12,legWidth,0,16,new Vector3(-1.9f,12,0),new Vector3(-legWidth/2,0,-legWidth/2)),Part("Left leg",legWidth,12,legWidth,name=="zombie"?16:0,name=="zombie"?48:16,new Vector3(1.9f,12,0),new Vector3(-legWidth/2,0,-legWidth/2))};
                if(name=="villager"){Part("Nose",2,4,2,24,0,Vector3.zero,new Vector3(-1,-3,-6));Part("Robe",8,18,6,16,20,Vector3.zero,new Vector3(-4,0,-3),.025f);Part("Folded arms",8,4,4,40,38,new Vector3(0,3,-3),new Vector3(-4,0,-4));}
                else {float width=name=="skeleton"?2:4;arms=new[]{Part("Right arm",width,12,width,40,16,new Vector3(-5,2,0),new Vector3(-width*.75f,-2,-width*.5f)),Part("Left arm",width,12,width,name=="zombie"?32:40,name=="zombie"?48:16,new Vector3(5,2,0),new Vector3(-width*.25f,-2,-width*.5f))};}
            } else if(name=="creeper") {
                Part("Head",8,8,8,0,0,new Vector3(0,6,0),new Vector3(-4,-8,-4));Part("Body",8,12,4,16,16,new Vector3(0,6,0),new Vector3(-4,0,-2));legs=new Transform[4];for(int i=0;i<4;i++)legs[i]=Part("Foot",4,6,4,0,16,new Vector3(i%2==0?-2:2,18,i<2?-4:4),new Vector3(-2,0,-2));
            } else if(name=="spider") {
                Part("Head",8,8,8,32,4,new Vector3(0,15,-3),new Vector3(-4,-4,-8));Part("Thorax",6,6,6,0,0,new Vector3(0,15,0),new Vector3(-3,-3,-3));Part("Abdomen",10,8,12,0,12,new Vector3(0,15,9),new Vector3(-5,-4,-6));legs=new Transform[8];for(int i=0;i<8;i++){bool left=i<4;legs[i]=Part("Spider leg",16,2,2,18,0,new Vector3(left?-4:4,15,(i%4)-1),new Vector3(left?-15:-1,-1,-1));legs[i].localRotation=Quaternion.Euler(0,(i%4-1.5f)*30,left?-40:40);}
            } else if(name=="chicken") {
                Part("Head",4,6,3,0,0,new Vector3(0,15,-4),new Vector3(-2,-6,-2));Part("Beak",4,2,2,14,0,new Vector3(0,15,-4),new Vector3(-2,-4,-4));Part("Wattle",2,2,2,14,4,new Vector3(0,15,-4),new Vector3(-1,-2,-3));Part("Body",6,8,6,0,9,new Vector3(0,16,0),new Vector3(-3,-4,-3));legs=new[]{Part("Leg",3,5,3,26,0,new Vector3(-2,19,1),new Vector3(-1,0,-3)),Part("Leg",3,5,3,26,0,new Vector3(2,19,1),new Vector3(-1,0,-3))};Part("Wing",1,4,6,24,13,new Vector3(-4,13,0),new Vector3(0,0,-3));Part("Wing",1,4,6,24,13,new Vector3(4,13,0),new Vector3(-1,0,-3));
            } else {
                bool cow=name=="cow",sheep=name=="sheep";float length=cow||sheep?12:6;Part("Head",sheep?6:8,sheep?6:8,cow?6:8,0,0,new Vector3(0,sheep?6:cow?4:12,-8),new Vector3(sheep?-3:-4,sheep?-3:-4,-6));var body=Part("Body",cow?12:sheep?8:10,cow?18:16,cow?10:8,cow?18:28,8,new Vector3(0,sheep||cow?5:11,2),new Vector3(cow?-6:sheep?-4:-5,-10,-7));body.localRotation=Quaternion.Euler(-90,0,0);legs=new Transform[4];for(int i=0;i<4;i++)legs[i]=Part("Leg",4,length,4,0,16,new Vector3(i%2==0?-3:3,24-length,i<2?-5:7),new Vector3(-2,0,-2));
            }
            CreateDisplay();
        }
        private void CreateDisplay(){var shown=new GameObject("Native cached creature sprite");shown.transform.SetParent(model.transform,false);display=shown.AddComponent<SpriteRenderer>();display.sortingOrder=25;}
        internal void Apply(JObject data) {
            string next=(string)data["kind"];bool boat=(bool?)data["boat"]==true;float height=(float?)data["height"]??1;if(next!=kind||model==null||Math.Abs(height-modelHeight)>.01f)Create(next,boat,height);
            float px=(float)data["x"],distance=first?0:Mathf.Abs(px-lastX);phase+=distance*4;first=false;lastX=px;var spec=NativeMobCatalog.Find(kind);int frame=boat?0:spec?.Motion=="fly"||spec?.Motion=="swim"?Mathf.FloorToInt(Time.time*12)%8:distance<.001f?8:Mathf.FloorToInt(phase)%8,facing=(int?)data["facing"]<0?-1:1;bool sitting=data["vehicle"]!=null;string key=kind+"/"+facing+"/"+frame+"/"+sitting;
            key+="/"+((int?)data["attacking"]>0)+"/"+((bool?)data["sheared"]==true)+"/"+((bool?)data["baby"]==true);
            if(!frames.TryGetValue(key,out var sprite)) {
                imported?.Pose(frame,sitting,data);
                if(legs!=null)for(int i=0;i<legs.Length;i++){if(kind=="minecraft:spider")continue;legs[i].localRotation=Quaternion.Euler(sitting?65:frame==8?0:Mathf.Cos(frame*Mathf.PI/4+i*Mathf.PI)*35,0,0);}if(arms!=null)for(int i=0;i<arms.Length;i++)arms[i].localRotation=Quaternion.Euler(kind=="minecraft:zombie"?80:frame==8?0:Mathf.Cos(frame*Mathf.PI/4+i*Mathf.PI)*25,0,0);
                var matrix=Matrix4x4.Rotate(Quaternion.Euler(0,-facing*75,0))*model.transform.worldToLocalMatrix;int size=imported?.ImageSize??160;var image=raster.Capture(model,size,matrix,64,16);sprite=Sprite.Create(image,new Rect(0,0,size,size),new Vector2(.5f,16f/size),64);TrimFrames((long)size*size);frames[key]=sprite;cachedPixels+=(long)size*size;
            }lastUsed[key]=Time.unscaledTime;display.sprite=sprite;
        }
        private static void TrimFrames(long incoming){while(cachedPixels+incoming>8*1024*1024){string oldest=null;float age=Time.unscaledTime-.5f;foreach(var pair in lastUsed)if(pair.Value<age){oldest=pair.Key;age=pair.Value;}if(oldest==null)break;var sprite=frames[oldest];cachedPixels-=(long)sprite.texture.width*sprite.texture.height;Destroy(sprite.texture);Destroy(sprite);frames.Remove(oldest);lastUsed.Remove(oldest);}}
        internal static void ClearFrames(){foreach(var sprite in frames.Values){if(sprite!=null){Destroy(sprite.texture);Destroy(sprite);}}frames.Clear();lastUsed.Clear();cachedPixels=0;}
        private void DisposeModel(){if(model==null)return;if(imported==null)foreach(var filter in model.GetComponentsInChildren<MeshFilter>(true))if(filter.sharedMesh!=null)Destroy(filter.sharedMesh);Destroy(model);model=null;imported=null;}
        private void OnDestroy(){DisposeModel();}
    }
}
