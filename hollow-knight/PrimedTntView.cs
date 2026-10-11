using System;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace HollowCraft
{
    // Sweeps the actual TNT-sized hull against HK's native floor/walls before each MC tick.
    internal sealed class PrimedTntView : MonoBehaviour
    {
        private static readonly RaycastHit2D[] hits=new RaycastHit2D[64];
        private static readonly Collider2D[] overlaps=new Collider2D[64];
        private int age=-1,lastStepAge=-1;
        private string uuid,room;
        private Vector3 from,to;
        private float received,nextRetry;
        private bool initialized;
        internal void Push(JObject value,string scene) {
            int current=(int?)value["age"]??-1;string identity=(string)value["uuid"];
            if(uuid==identity&&current<age)return;
            if(uuid!=identity) {initialized=false;lastStepAge=-1;}
            uuid=identity;room=scene;
            float width=Mathf.Clamp((float?)value["width"]??.98f,.1f,2),height=Mathf.Clamp((float?)value["height"]??.98f,.1f,2);
            var feet=new Vector2((float)value["x"],(float)value["y"]);
            var position=new Vector3(feet.x,feet.y+height*.5f,-.1f);
            if(!initialized) {transform.position=position;initialized=true;}
            if(age!=current||to!=position) {from=transform.position;to=position;received=Time.unscaledTime;}
            age=current;
            GetComponent<SpriteRenderer>().color=(int)value["fuse"]%10<5?Color.white:new Color(1,.55f,.55f);
            var runtime=HollowCraftRuntime.Instance;
            if(runtime==null||runtime.SimulationPaused||(bool?)value["stepRequired"]!=true||string.IsNullOrEmpty(uuid)||(lastStepAge==age&&Time.unscaledTime<nextRetry))return;
            lastStepAge=age;nextRetry=Time.unscaledTime+.2f;
            var size=new Vector2(width-.004f,height-.004f);
            float lift=0;
            if(!Clear(feet+Vector2.up*height*.5f,size)) {
                for(int i=1;i<=20;i++)if(Clear(feet+Vector2.up*(height*.5f+i*.05f),size)) {lift=i*.05f;break;}
            }
            var centre=feet+Vector2.up*(height*.5f+lift);
            float dy=Clip(centre,size,Vector2.up,(float?)value["nextY"]??0);
            float dx=Clip(centre+Vector2.up*dy,size,Vector2.right,(float?)value["nextX"]??0);
            runtime.QueueTntStep(new {entity=(int)value["id"],uuid,age,dx,dy,lift},(int)value["id"],room);
        }
        private static bool Native(Collider2D collider) {
            if(collider==null||collider.isTrigger||collider.GetComponentInParent<HeroController>()!=null||collider.GetComponentInParent<HealthManager>()!=null)return false;
            for(var node=collider.transform;node!=null;node=node.parent)if(node.name.StartsWith("HollowCraft.Block"))return false;
            return true;
        }
        private static ContactFilter2D Filter() {var filter=new ContactFilter2D();filter.SetLayerMask(LayerMask.GetMask("Terrain","Default"));filter.useTriggers=false;return filter;}
        private static bool Clear(Vector2 centre,Vector2 size) {
            int count=Physics2D.OverlapBox(centre,size,0,Filter(),overlaps);
            for(int i=0;i<count;i++)if(Native(overlaps[i]))return false;
            return count<overlaps.Length;
        }
        private static float Clip(Vector2 centre,Vector2 size,Vector2 axis,float move) {
            if(Math.Abs(move)<.000001f)return 0;
            var direction=axis*Math.Sign(move);float distance=Math.Abs(move),allowed=distance;
            int count=Physics2D.BoxCast(centre,size,0,direction,Filter(),hits,distance+.002f);
            for(int i=0;i<count;i++) {
                var hit=hits[i];if(!Native(hit.collider)||Vector2.Dot(hit.normal,direction)>=-.01f)continue;
                allowed=Math.Min(allowed,Math.Max(0,hit.distance-.002f));
            }
            return Math.Sign(move)*allowed;
        }
        private void Update() {
            var runtime=HollowCraftRuntime.Instance;if(runtime==null||runtime.Room!=room||runtime.SimulationPaused)return;
            transform.position=Vector3.Lerp(from,to,Mathf.Clamp01((Time.unscaledTime-received)/.05f));
        }
    }
}
