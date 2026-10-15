using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace HollowCraft
{
    internal sealed class CreatureBody : MonoBehaviour {
        internal int Id;internal string Uuid;internal bool Boat;internal float Health;
        internal BoxCollider2D Shape;
        internal object Target=>new {entity=Id,uuid=Uuid};
    }
    // Query bodies accompany Minecraft's real transparent entity-render frames.
    [DefaultExecutionOrder(31900)]
    internal sealed class WorldCreatures : MonoBehaviour
    {
        private sealed class Friendly {internal GameObject Actor;internal Collider2D Body;internal Renderer Sprite;}
        private sealed class Seat {internal int Id;internal Transform Root;internal Rigidbody2D Body;internal HealthManager Health;internal Vector2 Point,Offset,Velocity;internal float Gravity;internal string MovingState;internal PlayMakerFSM[] Machines;}
        private static readonly Dictionary<int,CreatureBody> bodies=new Dictionary<int,CreatureBody>();
        private static readonly Dictionary<int,Friendly> friendly=new Dictionary<int,Friendly>();
        private static readonly Dictionary<int,Seat> seats=new Dictionary<int,Seat>();
        private static readonly string[] names={"npc","elderbug","hornet","quirrel","cloth","cornifer","bretta","tiso","zote","myla","sly","mato","oro","sheo","iselda","salubra","leg eater","willoh","divine","emilitia","nailsmith","hunter","stag"};
        private static float nextFriendly;
        private static JObject rider;
        internal static bool Riding=>rider!=null&&HollowCraftRuntime.Instance?.InGame==true&&HollowCraftRuntime.Instance.Dead==false;
        internal static IEnumerable<int> Captured=>seats.Values.Where(s=>s.Root!=null&&(s.Health==null||!s.Health.isDead)).Select(s=>s.Id);
        internal static IEnumerable<Vector2Int> SampleCells(int x,int y) {
            var cells=new HashSet<Vector2Int>();
            for(int cx=x-8;cx<=x+8;cx++)for(int cy=y-6;cy<=y+6;cy++)cells.Add(new Vector2Int(cx,cy));
            foreach(var body in bodies.Values) {
                if(body==null||Math.Abs(body.transform.position.x-x)>26||Math.Abs(body.transform.position.y-y)>19)continue;
                var box=body.Shape.bounds;
                int x0=Math.Max(x-32,Mathf.FloorToInt(box.min.x)-3),x1=Math.Min(x+32,Mathf.CeilToInt(box.max.x)+3);
                int y0=Math.Max(y-24,Mathf.FloorToInt(box.min.y)-3),y1=Math.Min(y+24,Mathf.CeilToInt(box.max.y)+3);
                for(int cx=x0;cx<=x1;cx++)for(int cy=y0;cy<=y1;cy++)cells.Add(new Vector2Int(cx,cy));
            }
            return cells;
        }
        internal static void Apply(JObject message) {
            var present=new HashSet<int>();
            foreach(JObject value in message["creatures"] as JArray??new JArray()) {
                int id=(int)value["entity"];string uuid=(string)value["uuid"];present.Add(id);
                if(bodies.TryGetValue(id,out var body)&&body.Uuid!=uuid) {UnityEngine.Object.Destroy(body.gameObject);bodies.Remove(id);body=null;}
                if(body==null) {
                    var go=new GameObject("HollowCraft.Creature."+(string)value["kind"]);UnityEngine.Object.DontDestroyOnLoad(go);
                    body=go.AddComponent<CreatureBody>();body.Id=id;body.Uuid=uuid;body.Shape=go.AddComponent<BoxCollider2D>();bodies[id]=body;
                }
                body.Boat=(bool?)value["boat"]==true;body.Health=(float?)value["health"]??1;
                float width=Mathf.Clamp((float?)value["width"]??.6f,.1f,32),height=Mathf.Clamp((float?)value["height"]??1,.1f,32);
                body.transform.position=new Vector3((float)value["x"],(float)value["y"]+height*.5f,0);
                body.Shape.size=new Vector2(width,height);body.Shape.isTrigger=!body.Boat;body.Shape.enabled=body.Health>0;
                body.gameObject.layer=Math.Max(0,LayerMask.NameToLayer(body.Boat?"Terrain":"Enemies"));
            }
            foreach(int id in bodies.Keys.Where(id=>!present.Contains(id)).ToArray()) {bodies[id].gameObject.SetActive(false);UnityEngine.Object.Destroy(bodies[id].gameObject);bodies.Remove(id);}
            var active=new HashSet<int>();
            foreach(JObject value in message["enemies"] as JArray??new JArray()) {
                var anchor=value["seat"] as JObject;if(anchor==null||(float?)value["health"]<=0)continue;
                int id=(int)value["id"];GameObject actor=EnemySystem.Actor(id);
                if(actor==null&&friendly.TryGetValue(id,out var npc))actor=npc.Actor;
                if(actor==null||!actor.activeInHierarchy)continue;
                var health=actor.GetComponent<HealthManager>();if(health!=null&&health.isDead)continue;
                var body=actor.GetComponentInParent<Rigidbody2D>();var root=body!=null?body.transform:actor.transform;
                int key=root.GetInstanceID();active.Add(key);
                if(!seats.TryGetValue(key,out var seat)) {
                    var collision=actor.GetComponentsInChildren<Collider2D>().FirstOrDefault(c=>c.enabled&&c.gameObject.activeInHierarchy);
                    Bounds bounds=collision!=null?collision.bounds:new Bounds(root.position,Vector3.one);
                    if(friendly.TryGetValue(id,out var person)&&person.Sprite!=null)bounds=person.Sprite.bounds;
                    seats[key]=seat=new Seat {Id=id,Root=root,Body=body,Health=health,Gravity=body!=null?body.gravityScale:0,Machines=root.GetComponents<PlayMakerFSM>(),
                        Offset=new Vector2(root.position.x-bounds.center.x,root.position.y-bounds.min.y)};
                    Debug.Log("[HollowCraft] Native actor boarded boat: "+root.name);
                }
                seat.Point=new Vector2((float)anchor["x"],(float)anchor["y"]);
            }
            foreach(int id in seats.Keys.Where(id=>!active.Contains(id)).ToArray()) {Release(seats[id]);seats.Remove(id);}
            rider=message["rider"] as JObject;Constrain();Physics2D.SyncTransforms();
        }
        private void FixedUpdate() {Constrain();}
        private void LateUpdate() {Constrain();}
        private static void Constrain() {
            if(HollowCraftRuntime.Instance?.InGame!=true)return;
            foreach(var seat in seats.Values)Pin(seat);
            if(Riding&&HeroController.instance!=null) {
                var hero=HeroController.instance;var shape=hero.GetComponent<Collider2D>();float offset=hero.transform.position.y-shape.bounds.min.y;
                var position=hero.transform.position;position.x=(float)rider["x"];position.y=(float)rider["y"]+offset;hero.transform.position=position;
                hero.GetComponent<Rigidbody2D>().linearVelocity=Vector2.zero;NativeFallTracker.Reset();
            }
        }
        private static void Pin(Seat seat) {
            if(seat.Root==null||seat.Health!=null&&seat.Health.isDead)return;
            var position=seat.Root.position;position.x=seat.Point.x+seat.Offset.x;position.y=seat.Point.y+seat.Offset.y;seat.Root.position=position;
            if(seat.Body!=null) {
                if(seat.Body.gravityScale!=0)seat.Gravity=seat.Body.gravityScale;
                if(seat.Body.linearVelocity.sqrMagnitude>.001f) {seat.Velocity=seat.Body.linearVelocity;seat.MovingState=State(seat);}
                seat.Body.position=position;seat.Body.gravityScale=0;seat.Body.linearVelocity=Vector2.zero;
            }
        }
        private static string State(Seat seat)=>string.Join("|",seat.Machines.Where(f=>f!=null).Select(f=>f.ActiveStateName));
        private static void Release(Seat seat) {
            if(seat.Body==null)return;seat.Body.gravityScale=seat.Gravity;
            if((seat.Health==null||!seat.Health.isDead)&&seat.MovingState==State(seat))seat.Body.linearVelocity=seat.Velocity;
        }
        internal static bool Seated(HealthManager enemy) {
            if(enemy==null)return false;var body=enemy.GetComponentInParent<Rigidbody2D>();return seats.ContainsKey((body!=null?body.transform:enemy.transform).GetInstanceID());
        }
        internal static bool ConstrainSource(GameObject source) {
            if(source==null)return false;
            for(var node=source.transform;node!=null;node=node.parent)if(seats.TryGetValue(node.GetInstanceID(),out var seat)) {Pin(seat);Physics2D.SyncTransforms();return true;}
            return false;
        }
        internal static List<object> FriendlyActors(HeroController hero) {
            if(Time.unscaledTime>=nextFriendly) {
                nextFriendly=Time.unscaledTime+.5f;
                foreach(var collider in Physics2D.OverlapCircleAll(hero.transform.position,24)) {
                    if(collider.GetComponentInParent<HeroController>()!=null||collider.GetComponentInParent<HealthManager>()!=null)continue;
                    GameObject actor=collider.GetComponentInParent<ScuttlerControl>()?.gameObject;
                    if(actor==null)for(var node=collider.transform;node!=null;node=node.parent) {
                        if(names.Any(part=>node.name.IndexOf(part,StringComparison.OrdinalIgnoreCase)>=0)&&CampaignBridge.ConversationFsm(node.gameObject)!=null) {actor=node.gameObject;break;}
                    }
                    if(actor==null)continue;
                    var sprite=actor.GetComponent<Renderer>()??actor.GetComponentInChildren<Renderer>();
                    var box=sprite!=null?sprite.bounds:collider.bounds;if(box.size.x>10||box.size.y>12)continue;
                    friendly[actor.GetInstanceID()]=new Friendly {Actor=actor,Body=collider,Sprite=sprite};
                }
            }
            var result=new List<object>();
            foreach(var pair in friendly.ToArray()) {
                var actor=pair.Value.Actor;
                if(actor==null||!actor.activeInHierarchy||pair.Value.Sprite==null&&pair.Value.Body==null) {friendly.Remove(pair.Key);continue;}
                var box=pair.Value.Sprite!=null?pair.Value.Sprite.bounds:pair.Value.Body.bounds;
                if(Vector2.Distance(hero.transform.position,box.center)>24&&!Captured.Contains(pair.Key))continue;
                result.Add(new {id=pair.Key,actor=pair.Key,maxHealth=20,health=20,generation=0,kind="npc",name=actor.name,
                    x=box.center.x,y=box.min.y,width=Math.Max(.2f,box.size.x),height=Math.Max(.2f,box.size.y),nailBlock=15,spellBlock=15});
            }
            return result;
        }
        internal static void Clear() {
            foreach(var seat in seats.Values)Release(seat);seats.Clear();friendly.Clear();rider=null;nextFriendly=0;
            foreach(var body in bodies.Values)if(body!=null) {body.gameObject.SetActive(false);UnityEngine.Object.Destroy(body.gameObject);}bodies.Clear();
        }
    }
}
