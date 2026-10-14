using System;
using System.Collections.Generic;
using UnityEngine;

namespace HollowCraft
{
    // Native charges can use trigger bodies, kinematic bodies or direct transforms.
    // Sweep the entire body against only bedrock before physics and after animation/FSM movement.
    [DefaultExecutionOrder(32000)]
    internal sealed class BedrockBarrier : MonoBehaviour
    {
        private sealed class Actor {
            internal Transform Root;internal Rigidbody2D Body;internal Vector2 Previous;
            internal readonly List<HealthManager> Health=new List<HealthManager>();
            internal readonly List<Collider2D> Colliders=new List<Collider2D>();
            internal bool HadBody;internal float NextLog;
        }
        private static readonly Dictionary<GameObject,Bounds> walls=new Dictionary<GameObject,Bounds>();
        private static readonly Dictionary<int,Actor> actors=new Dictionary<int,Actor>();
        private static readonly List<int> retired=new List<int>();
        private static float nextScan;
        private const float Skin=.003f;
        internal static void Add(GameObject block) {
            walls[block]=new Bounds(block.transform.position,Vector3.one);Scan();
        }
        internal static void Remove(GameObject block) {walls.Remove(block);if(walls.Count==0)actors.Clear();}
        internal static void Clear() {walls.Clear();actors.Clear();nextScan=0;}
        internal static void Track(HealthManager enemy) {
            if(walls.Count==0||enemy==null||enemy.GetComponentInParent<HeroController>()!=null)return;
            var body=enemy.GetComponentInParent<Rigidbody2D>();
            var root=body!=null?body.transform:enemy.transform;int id=root.GetInstanceID();
            if(!actors.TryGetValue(id,out var actor))actors[id]=actor=new Actor {Root=root,Body=body,Previous=root.position};
            if(!actor.Health.Contains(enemy))actor.Health.Add(enemy);
            foreach(var collider in enemy.GetComponents<Collider2D>())if(!actor.Colliders.Contains(collider))actor.Colliders.Add(collider);
            if(!actor.Colliders.Exists(collider=>collider!=null&&collider.enabled&&collider.gameObject.activeInHierarchy))foreach(var collider in enemy.GetComponentsInChildren<Collider2D>())
                if(EnemySystem.Resolve(collider)==enemy&&!actor.Colliders.Contains(collider))actor.Colliders.Add(collider);
        }
        private static void Scan() {
            if(walls.Count==0)return;
            nextScan=Time.unscaledTime+.25f;
            foreach(var enemy in UnityEngine.Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None))Track(enemy);
        }
        private void FixedUpdate() {Tick(true);}
        private void LateUpdate() {Tick(false);}
        private static void Tick(bool predict) {
            var runtime=HollowCraftRuntime.Instance;
            if(walls.Count==0||runtime==null||!runtime.InGame||runtime.SimulationPaused)return;
            if(Time.unscaledTime>=nextScan)Scan();
            retired.Clear();bool moved=false;
            foreach(var pair in actors) {
                var actor=pair.Value;
                if(actor.Root==null) {retired.Add(pair.Key);continue;}
                moved|=Reconcile(actor,predict);
            }
            foreach(int id in retired)actors.Remove(id);
            if(moved)Physics2D.SyncTransforms();
        }
        private static bool BoundsOf(Actor actor,out Bounds bounds) {
            bounds=new Bounds();bool found=false,alive=false;
            foreach(var health in actor.Health)if(health!=null&&health.enabled&&!health.isDead&&health.hp>0) {alive=true;break;}
            if(!alive||!actor.Root.gameObject.activeInHierarchy)return false;
            foreach(var collider in actor.Colliders) {
                if(collider==null||!collider.enabled||!collider.gameObject.activeInHierarchy)continue;
                var health=EnemySystem.Resolve(collider);if(health==null||!actor.Health.Contains(health))continue;
                if(!found) {bounds=collider.bounds;found=true;}else bounds.Encapsulate(collider.bounds);
            }
            return found;
        }
        private static bool Reconcile(Actor actor,bool predict) {
            Vector2 position=actor.Root.position;
            if(!BoundsOf(actor,out var current)) {actor.Previous=position;actor.HadBody=false;return false;}
            // Hidden/inactive phase teleports have no hurt body to sweep. Re-enabled
            // bodies are checked for overlap before becoming a physical attacker.
            Vector2 motion=actor.HadBody?position-actor.Previous:Vector2.zero;
            var start=current;start.center-=new Vector3(motion.x,motion.y,0);
            Vector2 safe=Sweep(start,motion,out Vector2 normal);
            bool moved=(safe-motion).sqrMagnitude>.00000001f;
            if(moved) {
                position=actor.Previous+safe;SetPosition(actor,position);current.center+=new Vector3(safe.x-motion.x,safe.y-motion.y,0);
                StopInto(actor,normal);Notify(actor,normal);
                if(actor.Root==null)return true;
            }
            // A new phase/spawn can appear inside a wall. Push along the shortest
            // escape from the complete wall set, rather than leaving a trapped body.
            Vector2 escape=Escape(current);
            if(escape.sqrMagnitude>.00000001f) {
                position+=escape;SetPosition(actor,position);current.center+=new Vector3(escape.x,escape.y,0);
                StopInto(actor,escape.normalized);moved=true;
            }
            actor.Previous=position;actor.HadBody=true;
            if(predict&&actor.Body!=null&&actor.Body.simulated) {
                Vector2 step=actor.Body.linearVelocity*Time.fixedDeltaTime;
                safe=Sweep(current,step,out normal);
                if((safe-step).sqrMagnitude>.00000001f) {
                    actor.Body.linearVelocity=safe/Time.fixedDeltaTime;
                    if(safe.sqrMagnitude<Skin*Skin)Notify(actor,normal);
                }
            }
            return moved;
        }
        private static void SetPosition(Actor actor,Vector2 position) {
            Vector3 value=actor.Root.position;value.x=position.x;value.y=position.y;actor.Root.position=value;
            if(actor.Body!=null)actor.Body.position=position;
        }
        private static void StopInto(Actor actor,Vector2 normal) {
            if(actor.Body==null)return;Vector2 velocity=actor.Body.linearVelocity;
            float incoming=Vector2.Dot(velocity,normal);if(incoming<0)actor.Body.linearVelocity=velocity-normal*incoming;
        }
        private static void Notify(Actor actor,Vector2 normal) {
            if(Time.unscaledTime>=actor.NextLog) {
                actor.NextLog=Time.unscaledTime+1;
                Debug.Log("[HollowCraft] Bedrock blocked native movement actor="+actor.Root.name+" normal="+normal);
            }
            foreach(var fsm in actor.Root.GetComponentsInChildren<PlayMakerFSM>()) {
                var state=fsm.Fsm?.ActiveState;if(!fsm.enabled||state?.Transitions==null)continue;
                foreach(var transition in state.Transitions) {
                    string name=transition.EventName;
                    bool matches=Math.Abs(normal.x)>.5f
                        ?name=="WALL"||name=="HIT WALL"||name==(normal.x>0?"WALL LEFT":"WALL RIGHT")
                        :normal.y>0?name=="LAND"||name=="LANDED"||name=="HIT FLOOR":name=="HIT CEILING"||name=="CEILING";
                    if(matches) {fsm.SendEvent(name);break;}
                }
            }
        }
        internal static bool BlocksContact(GameObject source,HeroController hero) {
            if(walls.Count==0||source==null||hero==null)return false;
            var health=source.GetComponentInParent<HealthManager>();
            if(health!=null) {
                Track(health);var body=health.GetComponentInParent<Rigidbody2D>();var root=body!=null?body.transform:health.transform;
                if(actors.TryGetValue(root.GetInstanceID(),out var actor)&&Reconcile(actor,false))Physics2D.SyncTransforms();
            }
            var collider=source.GetComponent<Collider2D>();var player=hero.GetComponent<Collider2D>();
            Vector2 start=collider!=null?(Vector2)collider.bounds.center:(Vector2)source.transform.position;
            Vector2 end=player!=null?(Vector2)player.bounds.center:(Vector2)hero.transform.position;
            foreach(var wall in walls.Values)if(Entry(start,end-start,wall,Vector2.zero,out float time,out _)&&time<=1)return true;
            return false;
        }
        private static Vector2 Sweep(Bounds body,Vector2 motion,out Vector2 normal) {
            Vector2 result=Vector2.zero,remaining=motion;normal=Vector2.zero;
            for(int slide=0;slide<3&&remaining.sqrMagnitude>.000000001f;slide++) {
                float first=1;Vector2 hit=Vector2.zero;
                foreach(var wall in walls.Values)if(Entry((Vector2)body.center+result,remaining,wall,body.extents,out float time,out Vector2 side)&&time<first) {first=time;hit=side;}
                if(hit==Vector2.zero) {result+=remaining;break;}
                float length=remaining.magnitude;float safe=Math.Max(0,first-Skin/Math.Max(Skin,length));
                result+=remaining*safe;remaining*=1-safe;normal=hit;
                float into=Vector2.Dot(remaining,hit);if(into<0)remaining-=hit*into;
            }
            return result;
        }
        private static bool Entry(Vector2 start,Vector2 motion,Bounds wall,Vector2 half,out float time,out Vector2 normal) {
            Vector2 min=(Vector2)wall.min-half,max=(Vector2)wall.max+half;
            float enter=float.NegativeInfinity,leave=float.PositiveInfinity;normal=Vector2.zero;time=0;
            for(int axis=0;axis<2;axis++) {
                float speed=axis==0?motion.x:motion.y,value=axis==0?start.x:start.y,low=axis==0?min.x:min.y,high=axis==0?max.x:max.y;
                if(Math.Abs(speed)<.000001f) {if(value<=low+.00001f||value>=high-.00001f)return false;continue;}
                float a=(low-value)/speed,b=(high-value)/speed;
                Vector2 side=axis==0?new Vector2(-Math.Sign(speed),0):new Vector2(0,-Math.Sign(speed));
                float near=Math.Min(a,b),far=Math.Max(a,b);
                if(near>enter) {enter=near;normal=side;}leave=Math.Min(leave,far);
                if(enter>leave)return false;
            }
            if(enter<-.00001f||leave<0||enter>1)return false;
            time=Math.Max(0,enter);return normal!=Vector2.zero;
        }
        private static Vector2 Escape(Bounds body) {
            Vector2 escape=Vector2.zero;
            for(int iteration=0;iteration<8;iteration++) {
                bool overlap=false;
                foreach(var wall in walls.Values) {
                    Vector2 center=(Vector2)body.center+escape,half=body.extents,min=(Vector2)wall.min-half,max=(Vector2)wall.max+half;
                    if(center.x<=min.x+.00001f||center.x>=max.x-.00001f||center.y<=min.y+.00001f||center.y>=max.y-.00001f)continue;
                    // Resolve against the whole set: a candidate must clear every wall.
                    Vector2[] options={new Vector2(min.x-center.x-Skin,0),new Vector2(max.x-center.x+Skin,0),new Vector2(0,min.y-center.y-Skin),new Vector2(0,max.y-center.y+Skin)};
                    Vector2 best=Vector2.zero;float shortest=float.PositiveInfinity;
                    foreach(Vector2 option in options) {
                        bool clear=true;Vector2 next=center+option;
                        foreach(var other in walls.Values) {
                            Vector2 lo=(Vector2)other.min-half,hi=(Vector2)other.max+half;
                            if(next.x>lo.x+.00001f&&next.x<hi.x-.00001f&&next.y>lo.y+.00001f&&next.y<hi.y-.00001f) {clear=false;break;}
                        }
                        if(clear&&option.sqrMagnitude<shortest) {best=option;shortest=option.sqrMagnitude;}
                    }
                    if(best==Vector2.zero) {
                        // Include outside edges of adjacent cells in the next search.
                        Bounds union=wall;foreach(var other in walls.Values)if(union.Intersects(other))union.Encapsulate(other);
                        options=new[]{new Vector2(union.min.x-half.x-center.x-Skin,0),new Vector2(union.max.x+half.x-center.x+Skin,0),new Vector2(0,union.min.y-half.y-center.y-Skin),new Vector2(0,union.max.y+half.y-center.y+Skin)};
                        foreach(var option in options)if(option.sqrMagnitude<shortest) {best=option;shortest=option.sqrMagnitude;}
                    }
                    escape+=best;overlap=true;break;
                }
                if(!overlap)break;
            }
            return escape;
        }
    }
    internal sealed class BedrockWall : MonoBehaviour {
        private void OnEnable() {BedrockBarrier.Add(gameObject);}
        private void OnDisable() {BedrockBarrier.Remove(gameObject);}
    }
}
