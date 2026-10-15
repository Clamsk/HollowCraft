using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace HollowCraft
{
    internal sealed class ProjectileView : MonoBehaviour
    {
        private Vector3 from,to;
        private Vector2 velocity;
        private float received,retireAt=-1;
        private bool initialized,grounded,noClip,nativeCollision=true;
        private bool stepRequired;
        private int age=-1,lastStepAge=-1,impactRequest;
        private static int nextImpactRequest;
        private float nextStepRetry;
        private string room,kind;
        private int entity;
        private string uuid;
        private Vector3 serverPoint,impactPoint;
        private bool awaitingImpact;
        private float retryAt;
        private float impactAt;
        private object hitCommand;
        private MeshRenderer[] meshes;
        private int pendingTarget;
        private Collider2D pendingOneHit;
        private Vector2 pendingMotion;
        private readonly HashSet<int> hitTargets=new HashSet<int>();
        private void Awake() {meshes=GetComponentsInChildren<MeshRenderer>();}
        internal void Push(JObject value,string scene) {
            room=scene;kind=(string)value["kind"];entity=(int)value["entity"];uuid=(string)value["uuid"];
            var target=new Vector3((float)value["x"],(float)value["y"],-.15f);
            bool landed=(bool?)value["grounded"]??false;
            int currentAge=(int?)value["age"]??-1;
            bool changed=!initialized||age!=currentAge||serverPoint!=target||grounded!=landed;
            age=currentAge;stepRequired=(bool?)value["stepRequired"]??false;
            noClip=(bool?)value["noClip"]??false;nativeCollision=(bool?)value["nativeCollision"]??true;
            if(noClip)awaitingImpact=false;
            if(landed)awaitingImpact=false;
            if(initialized&&!landed&&!awaitingImpact)Sweep(serverPoint,target);
            serverPoint=target;
            if(!initialized){transform.position=target;initialized=true;}
            if(changed){from=transform.position;to=target;received=Time.unscaledTime;}retireAt=-1;
            velocity=new Vector2((float?)value["vx"]??0,(float?)value["vy"]??0);grounded=(bool?)value["grounded"]??false;
            var nextVelocity=new Vector2((float?)value["nextVx"]??velocity.x,(float?)value["nextVy"]??velocity.y);
            if(!landed&&!awaitingImpact)Sweep(target,target+new Vector3(nextVelocity.x,nextVelocity.y,0),true);
            if(stepRequired&&!awaitingImpact&&(lastStepAge!=age||Time.unscaledTime>=nextStepRetry)) {
                lastStepAge=age;nextStepRetry=Time.unscaledTime+.2f;
                HollowCraftRuntime.Instance?.QueueNativeStep(entity,uuid,age,room);
            }
        }
        internal void Retire(){retireAt=Time.unscaledTime+.08f;}
        internal void Resolve(JObject message) {
            if(message["uuid"]!=null&&(string)message["uuid"]!=uuid||message["request"]!=null&&(int)message["request"]!=impactRequest)return;
            if((bool?)message["accepted"]==true&&pendingTarget!=0)hitTargets.Add(pendingTarget);
            if((bool?)message["accepted"]==true&&pendingOneHit!=null&&HeroController.instance!=null) {
                NativeSceneProps.Strike(pendingOneHit,pendingOneHit.gameObject,HeroController.instance,pendingMotion);
                pendingOneHit=null;
            }
            if((bool?)message["removed"]==true) {HollowCraftRuntime.Instance?.RetireProjectile(entity,uuid);return;}
            if(noClip) {awaitingImpact=false;return;}
            if((bool?)message["accepted"]==true&&(bool?)message["grounded"]==true) {grounded=true;awaitingImpact=false;from=to=impactPoint;velocity=Vector2.zero;}
            else awaitingImpact=false;
        }
        private void Sweep(Vector3 start,Vector3 end,bool predicted=false) {
            if(awaitingImpact||grounded||noClip||!nativeCollision||retireAt>=0||string.IsNullOrEmpty(uuid)||kind=="minecraft:falling_block")return;
            Vector2 delta=end-start;if(delta.sqrMagnitude<.000001f)return;
            var hits=Physics2D.CircleCastAll(start,kind=="minecraft:ender_pearl"?.125f:.08f,delta.normalized,delta.magnitude);
            Array.Sort(hits,(a,b)=>a.distance.CompareTo(b.distance));
            foreach(var hit in hits) {
                var collider=hit.collider;if(collider==null||collider.GetComponent<CreatureBody>()!=null||collider.GetComponentInParent<HeroController>()!=null)continue;
                var node=collider.transform;while(node!=null&&!node.name.StartsWith("HollowCraft.Block"))node=node.parent;
                if(node!=null) {if(!collider.isTrigger)return;continue;}
                var enemy=EnemySystem.Resolve(collider);
                var breakable=collider.GetComponentInParent<Breakable>();
                bool arrow=kind=="minecraft:arrow"||kind=="minecraft:spectral_arrow";
                var oneHit=arrow&&enemy==null&&breakable==null?collider.GetComponentInParent<ScuttlerControl>():null;
                var prop=arrow&&enemy==null?NativeSceneProps.Find(collider):null;
                if(prop!=null&&breakable==null) {
                    int propId=prop.GetInstanceID();if(hitTargets.Add(propId)) {
                        var propHero=HeroController.instance;if(propHero!=null)NativeSceneProps.Strike(collider,prop,propHero,delta);
                    }
                    // Trigger-only grass/poles are native interactions, never arrow anchors.
                    continue;
                }
                // Disabled/dead health responders are not inert solid walls.
                if(enemy==null&&collider.GetComponentInParent<HealthManager>()!=null)continue;
                int targetId=enemy!=null?enemy.GetInstanceID():breakable!=null?breakable.GetInstanceID():oneHit!=null?oneHit.GetInstanceID():0;
                if(targetId!=0&&hitTargets.Contains(targetId))continue;
                if(enemy==null&&breakable==null&&oneHit==null&&(collider.isTrigger||collider.gameObject.layer!=LayerMask.NameToLayer("Terrain")&&collider.gameObject.layer!=LayerMask.NameToLayer("Default")))continue;
                if(enemy!=null&&enemy.isDead)continue;
                if(hit.distance<=.001f&&Vector2.Dot(hit.normal,delta)>=0)continue;
                var runtime=HollowCraftRuntime.Instance;var hero=HeroController.instance;if(runtime==null||hero==null)return;
                Vector2 normal=hit.normal.sqrMagnitude>.01f?hit.normal:-delta.normalized;
                Vector2 point=hit.point,landing=kind=="minecraft:ender_pearl"?NativeLanding.FromSurface(hero,point,normal):NativeLanding.CurrentFeet(hero);
                impactPoint=new Vector3(hit.centroid.x,hit.centroid.y,-.15f);awaitingImpact=true;retryAt=Time.unscaledTime+.4f;
                impactAt=Time.unscaledTime+(predicted?.05f:0);
                pendingTarget=targetId;
                pendingOneHit=oneHit!=null?collider:null;pendingMotion=delta;
                impactRequest=++nextImpactRequest;runtime.RevokeNativeStep(entity);
                int target=enemy!=null?EnemySystem.Register(enemy):breakable!=null?EnemySystem.Register(breakable):0;
                hitCommand=new {type="nativeProjectileHit",scene=room,entity,uuid,age,request=impactRequest,x=point.x,y=point.y,normalX=normal.x,normalY=normal.y,
                    landingX=landing.x,landingY=landing.y,target,nativeOneHit=oneHit!=null,passThrough=arrow&&breakable!=null&&enemy==null,enemies=EnemySystem.Observe(hero,target)};
                runtime.Send(hitCommand);return;
            }
        }
        private void Update() {
            if(HollowCraftRuntime.Instance==null||HollowCraftRuntime.Instance.Room!=room||(retireAt>=0&&Time.unscaledTime>=retireAt)){Destroy(gameObject);return;}
            if(HollowCraftRuntime.Instance.SimulationPaused)return;
            float elapsed=Time.unscaledTime-received;
            transform.position=Vector3.Lerp(from,to,Mathf.Clamp01(elapsed/.05f));
            if(!grounded&&retireAt<0&&elapsed>.05f)transform.position+=new Vector3(velocity.x,velocity.y,0)*20*Mathf.Min(.05f,elapsed-.05f);
            if(!awaitingImpact)Sweep(from,transform.position);
            if(awaitingImpact) {
                if(Time.unscaledTime>=impactAt)transform.position=impactPoint;
                if(retireAt<0&&Time.unscaledTime>=retryAt) {retryAt=Time.unscaledTime+.4f;HollowCraftRuntime.Instance.Send(hitCommand);}
            }
            if(kind!="minecraft:falling_block"&&velocity.sqrMagnitude>.001f)transform.rotation=Quaternion.Euler(0,0,Mathf.Atan2(velocity.y,velocity.x)*Mathf.Rad2Deg-(kind=="minecraft:trident"?90:0));
            foreach(var renderer in meshes)if(renderer!=null)MinecraftMeshes.SortFaces(renderer);
        }
    }
}
