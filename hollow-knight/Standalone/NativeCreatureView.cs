using Newtonsoft.Json.Linq;
using UnityEngine;
namespace HollowCraft.Standalone
{
    // Local authored models. Exact imported vanilla entity rigs remain a tracked migration item.
    internal sealed class NativeCreatureView : MonoBehaviour
    {
        private string kind;private GameObject model;private Transform[] legs;private float phase;
        internal void Apply(JObject data) {
            string next=(string)data["kind"];bool boat=(bool?)data["boat"]==true;
            if(next!=kind||model==null){kind=next;if(model!=null)Destroy(model);model=new GameObject("Native model "+next);model.transform.SetParent(transform,false);float height=(float?)data["height"]??1;model.transform.localPosition=new Vector3(0,-height/2,-.05f);
                var texture=NativeAssets.Texture(boat?"minecraft:oak_planks":next.Contains("zombie")||next.Contains("creeper")?"minecraft:grass_block":next.Contains("skeleton")?"minecraft:iron_ingot":"minecraft:dirt");
                System.Func<string,Vector3,Vector3,Transform> part=(name,size,at)=>{var go=new GameObject(name);go.transform.SetParent(model.transform,false);go.transform.localPosition=at;var renderer=MinecraftMeshes.Renderer(go,MinecraftMeshes.Cuboid(size.x,size.y,size.z,0,0,false,texture),texture,0,25);renderer.sharedMaterial=NativeAssets.Cutout(texture);return go.transform;};
                if(boat){part("Hull",new Vector3(22,2,12),new Vector3(0,.06f,0));part("Bow",new Vector3(2,7,12),new Vector3(.65f,.25f,0));part("Stern",new Vector3(2,7,12),new Vector3(-.65f,.25f,0));part("Side",new Vector3(22,7,1),new Vector3(0,.25f,.35f));part("Side",new Vector3(22,7,1),new Vector3(0,.25f,-.35f));legs=null;}
                else {bool humanoid=next.Contains("zombie")||next.Contains("skeleton")||next.Contains("villager");float bodyY=humanoid?height*.65f:height*.6f;part("Body",new Vector3(humanoid?8:12,humanoid?12:8,humanoid?4:7),new Vector3(0,bodyY,0));part("Head",new Vector3(8,8,8),new Vector3(humanoid?0:.35f,height*.88f,0));legs=new Transform[humanoid?2:4];for(int i=0;i<legs.Length;i++)legs[i]=part("Leg",new Vector3(3,7,3),new Vector3(i%2==0?-.22f:.22f,.23f,i<2?-.18f:.18f));if(humanoid){part("Arm",new Vector3(3,11,3),new Vector3(-.34f,bodyY,.1f));part("Arm",new Vector3(3,11,3),new Vector3(.34f,bodyY,.1f));}}
            }
            if(!boat){model.transform.localRotation=Quaternion.Euler(0,(int?)data["facing"]<0?180:0,0);phase+=Time.deltaTime*10;if(legs!=null)for(int i=0;i<legs.Length;i++)legs[i].localRotation=Quaternion.Euler(Mathf.Sin(phase+i*Mathf.PI)*25,0,0);}
        }
    }
}
