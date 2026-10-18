using System;
using System.Collections.Generic;
using UnityEngine;
namespace HollowCraft.Standalone
{
    // Direct port of the bridge's authored StardustWingsFeature rig. Fixed
    // three-segment plumes are baked per hinge: eight renderers instead of 92.
    internal sealed class NativeWingRig
    {
        private readonly GameObject root;private readonly Transform left,right,leftFan,rightFan;private float fold=1;
        private readonly List<Mesh> owned=new List<Mesh>();
        private static Vector3 Pivot(float x,float y,float z)=>new Vector3(-x,-y,z)/16;
        private static Quaternion Rotation(float x,float y,float z){var q=NativeRigSpace.Rotation(x,y,z);return new Quaternion(q.X,q.Y,q.Z,q.W);}
        private static Matrix4x4 At(float x,float y,float z,float pitch=0,float yaw=0,float roll=0)=>Matrix4x4.TRS(Pivot(x,y,z),Rotation(pitch,yaw,roll),Vector3.one);
        private static Transform Bone(Transform parent,string name,float x,float y,float z) {var bone=new GameObject(name).transform;bone.SetParent(parent,false);bone.localPosition=Pivot(x,y,z);return bone;}
        internal NativeWingRig(Transform torso,int layer,int order) {
            root=new GameObject("HollowCraft.NativeWingRig");root.transform.SetParent(torso,false);
            var hinges=new Transform[2];var fans=new Transform[2];
            for(int side=0;side<2;side++) {
                int sign=side==0?-1:1;var wing=Bone(root.transform,"Bridge wing hinge "+sign,sign*2.5f,2.1f,3.4f);hinges[side]=wing;wing.localScale=Vector3.one*1.45f;
                var fan=Bone(wing,"Bridge outer fan "+sign,sign*6.6f,.2f,.2f);fans[side]=fan;
                foreach(bool glow in new[]{false,true}) {
                    var shoulder=new List<CombineInstance>();Box(shoulder,sign,0,-.85f,-.9f,7,1.7f,1.8f,glow,Matrix4x4.identity);owned.Add(Bake(wing,shoulder,glow,layer,order));
                    var feathers=new List<CombineInstance>();Box(feathers,sign,0,-.7f,-.7f,5.5f,1.4f,1.4f,glow,Matrix4x4.identity);
                    for(int i=0;i<7;i++) {
                        float length=10.5f-Math.Abs(i-2)*.9f,width=1.8f-i*.08f;
                        var plume=At(sign*(.4f+i*.62f),i*.23f,i*.09f,0,sign*(i-3)*.045f,sign*(-.32f+i*.205f));
                        Box(feathers,sign,0,0,-.6f,length*.55f,width,1.2f,glow,plume);
                        var curve=plume*At(sign*length*.52f,.05f,0,0,sign*.08f,sign*.22f);Box(feathers,sign,0,0,-.5f,length*.3f,width*.8f,1,glow,curve);
                        var tip=curve*At(sign*length*.27f,.04f,0,0,sign*.08f,sign*.3f);Box(feathers,sign,0,0,-.4f,length*.22f,width*.48f,.8f,glow,tip);
                    }
                    owned.Add(Bake(fan,feathers,glow,layer,order));
                }
            }
            left=hinges[0];right=hinges[1];leftFan=fans[0];rightFan=fans[1];root.SetActive(false);
        }
        private static void Box(List<CombineInstance> pieces,int sign,float x,float y,float z,float width,float height,float depth,bool glow,Matrix4x4 transform) {
            float inset=glow?.22f:0,w=Math.Max(.15f,width-inset*2),h=Math.Max(.14f,height-inset*2),d=depth+(glow?.07f:0);
            float minX=sign<0?-x-width+inset:x+inset,minY=y+inset,minZ=z-(glow?.035f:0);
            pieces.Add(new CombineInstance{mesh=MinecraftMeshes.Cuboid(w,h,d,0,0,false,Texture2D.whiteTexture),transform=transform*Matrix4x4.Translate(Pivot(minX+w/2,minY+h/2,minZ+d/2))});
        }
        private static Mesh Bake(Transform parent,List<CombineInstance> pieces,bool glow,int layer,int order) {
            var mesh=new Mesh{name="Bridge Stardust wing "+(glow?"luminous core":"blue shell")};mesh.CombineMeshes(pieces.ToArray(),true,true);
            var color=glow?new Color(.2f,.85f,1,1):new Color(.055f,.27f,.78f,1);var colors=mesh.colors;
            for(int i=0;i<colors.Length;i++)colors[i]=glow?color:colors[i]*color;mesh.colors=colors;
            foreach(var part in pieces)UnityEngine.Object.Destroy(part.mesh);
            var go=new GameObject(mesh.name);go.transform.SetParent(parent,false);MinecraftMeshes.Renderer(go,mesh,Texture2D.whiteTexture,layer,order+1);return mesh;
        }
        internal void Pose(string item) {
            bool enabled=item==TerrariaEquipment.Wings;root.SetActive(enabled);if(!enabled)return;
            bool flying=TerrariaEquipment.WingState=="fly",gliding=TerrariaEquipment.WingState=="glide",air=TerrariaEquipment.WingState!="ground"&&TerrariaEquipment.WingState!="none";
            var runtime=HollowCraftRuntime.Instance;float weight=runtime?.SimulationPaused==true?0:1-Mathf.Exp(-Time.deltaTime*14);
            fold=Mathf.Lerp(fold,flying?.1f:gliding?0:air?.35f:1,weight);
            float flap=flying?Mathf.Sin(TerrariaEquipment.WingPhase*1.6f)*.48f:gliding?Mathf.Sin(Time.time*20*.12f)*.045f:0;
            float yaw=.3f+fold*.65f+flap*.22f,roll=.28f-fold*1.35f,pitch=.1f-fold*.18f;
            left.localRotation=Rotation(pitch,yaw,roll);right.localRotation=Rotation(pitch,-yaw,-roll);
            float fanYaw=fold*.2f+flap*.12f;leftFan.localRotation=Rotation(0,fanYaw,fold*.55f);rightFan.localRotation=Rotation(0,-fanYaw,-fold*.55f);
        }
        internal void Dispose(){foreach(var mesh in owned)if(mesh!=null)UnityEngine.Object.Destroy(mesh);owned.Clear();if(root!=null)UnityEngine.Object.Destroy(root);}
    }
}
