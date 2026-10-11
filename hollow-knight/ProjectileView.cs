using Newtonsoft.Json.Linq;
using UnityEngine;

namespace HollowCraft
{
    internal sealed class ProjectileView : MonoBehaviour
    {
        private Vector3 from,to;
        private Vector2 velocity;
        private float received,retireAt=-1;
        private bool initialized,grounded;
        private string room,kind;
        internal void Push(JObject value,string scene) {
            room=scene;kind=(string)value["kind"];
            var target=new Vector3((float)value["x"],(float)value["y"],-.15f);
            if(!initialized){transform.position=target;initialized=true;}
            from=transform.position;to=target;received=Time.unscaledTime;retireAt=-1;
            velocity=new Vector2((float?)value["vx"]??0,(float?)value["vy"]??0);grounded=(bool?)value["grounded"]??false;
        }
        internal void Retire(){retireAt=Time.unscaledTime+.08f;}
        private void Update() {
            if(HollowCraftRuntime.Instance==null||HollowCraftRuntime.Instance.Room!=room||(retireAt>=0&&Time.unscaledTime>=retireAt)){Destroy(gameObject);return;}
            float elapsed=Time.unscaledTime-received;
            transform.position=Vector3.Lerp(from,to,Mathf.Clamp01(elapsed/.05f));
            if(!grounded&&retireAt<0&&elapsed>.05f)transform.position+=new Vector3(velocity.x,velocity.y,0)*20*Mathf.Min(.05f,elapsed-.05f);
            if(kind!="minecraft:falling_block"&&velocity.sqrMagnitude>.001f)transform.rotation=Quaternion.Euler(0,0,Mathf.Atan2(velocity.y,velocity.x)*Mathf.Rad2Deg-(kind=="minecraft:trident"?90:0));
            foreach(var renderer in GetComponentsInChildren<MeshRenderer>())MinecraftMeshes.SortFaces(renderer);
        }
    }
}
