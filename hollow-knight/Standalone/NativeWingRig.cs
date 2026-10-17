using UnityEngine;
namespace HollowCraft.Standalone
{
    internal sealed class NativeWingRig
    {
        private readonly GameObject root;private readonly Transform left,right,leftFan,rightFan;private float fold=1;
        internal NativeWingRig(Transform torso,int layer,int order) {
            root=new GameObject("HollowCraft.NativeWingRig");root.transform.SetParent(torso,false);root.transform.localPosition=new Vector3(0,-.1f,.22f);
            var wings=new Transform[2];var fans=new Transform[2];Texture2D blue=NativeAssets.Texture("minecraft:lapis_lazuli"),cyan=NativeAssets.Texture("minecraft:diamond");
            for(int side=0;side<2;side++){int sign=side==0?-1:1;var wing=new GameObject("Wing hinge").transform;wing.SetParent(root.transform,false);wing.localPosition=new Vector3(sign*.15f,0,0);wings[side]=wing;
                Add(wing,"Upper",new Vector3(10,2,2),new Vector3(sign*.3f,0,0),blue,layer,order);
                var fan=new GameObject("Outer wing").transform;fan.SetParent(wing,false);fan.localPosition=new Vector3(sign*.55f,0,0);fans[side]=fan;
                for(int i=0;i<7;i++){float length=15-Mathf.Abs(i-2)*1.1f;var feather=new GameObject("Feather "+i).transform;feather.SetParent(fan,false);feather.localPosition=new Vector3(sign*i*.05f,-i*.025f,0);feather.localRotation=Quaternion.Euler(0,sign*(i-3)*2,sign*(18-i*12));Add(feather,"Shell",new Vector3(length,2,1.6f),new Vector3(sign*length/32,0,0),blue,layer,order);Add(feather,"Core",new Vector3(length*.85f,.8f,1.7f),new Vector3(sign*length/32,0,0),cyan,layer,order);}
            }left=wings[0];right=wings[1];leftFan=fans[0];rightFan=fans[1];root.SetActive(false);
        }
        private static void Add(Transform parent,string name,Vector3 size,Vector3 at,Texture2D texture,int layer,int order){var go=new GameObject(name);go.transform.SetParent(parent,false);go.transform.localPosition=at;var renderer=MinecraftMeshes.Renderer(go,MinecraftMeshes.Cuboid(size.x,size.y,size.z,0,0,false,texture),texture,layer,order+1);renderer.sharedMaterial=NativeAssets.Cutout(texture);}
        internal void Pose(string item) {
            bool enabled=item==TerrariaEquipment.Wings;root.SetActive(enabled);if(!enabled)return;
            string state=TerrariaEquipment.WingState;float desired=state=="fly"?.1f:state=="glide"?0:state=="fall"?.35f:1;fold=Mathf.Lerp(fold,desired,1-Mathf.Exp(-Time.deltaTime*14));float beat=state=="fly"?Mathf.Sin(TerrariaEquipment.WingPhase*1.6f)*12:0;
            left.localRotation=Quaternion.Euler(-6+fold*10,18+fold*37+beat,-16+fold*77);right.localRotation=Quaternion.Euler(-6+fold*10,-18-fold*37-beat,16-fold*77);leftFan.localRotation=Quaternion.Euler(0,fold*12,fold*-25);rightFan.localRotation=Quaternion.Euler(0,-fold*12,fold*25);
        }
    }
}
