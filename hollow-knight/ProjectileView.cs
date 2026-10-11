using System;
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
        private int entity;
        private string uuid;
        private Vector3 serverPoint,impactPoint;
        private bool awaitingImpact;
        private float retryAt;
        private object hitCommand;
        private MeshRenderer[] meshes;
        private void Awake() {meshes=GetComponentsInChildren<MeshRenderer>();}
        internal void Push(JObject value,string scene) {
            room=scene;kind=(string)value["kind"];entity=(int)value["entity"];uuid=(string)value["uuid"];
            var target=new Vector3((float)value["x"],(float)value["y"],-.15f);
            bool landed=(bool?)value["grounded"]??false;
            if(landed)awaitingImpact=false;
            if(initialized&&!landed&&!awaitingImpact)Sweep(serverPoint,target);
            serverPoint=target;
            if(!initialized){transform.position=target;initialized=true;}
            from=transform.position;to=target;received=Time.unscaledTime;retireAt=-1;
            velocity=new Vector2((float?)value["vx"]??0,(float?)value["vy"]??0);grounded=(bool?)value["grounded"]??false;
        }
        internal void Retire(){retireAt=Time.unscaledTime+.08f;}
        internal void Resolve(JObject message) {
            if((bool?)message["removed"]==true) {Retire();return;}
            if((bool?)message["accepted"]==true&&(bool?)message["grounded"]==true) {grounded=true;awaitingImpact=false;from=to=impactPoint;velocity=Vector2.zero;}
            else if((bool?)message["accepted"]!=true)awaitingImpact=false;
        }
        private void Sweep(Vector3 start,Vector3 end) {
            if(awaitingImpact||grounded||retireAt>=0||string.IsNullOrEmpty(uuid)||kind=="minecraft:falling_block"||kind=="minecraft:firework_rocket")return;
            Vector2 delta=end-start;if(delta.sqrMagnitude<.000001f)return;
            foreach(var hit in Physics2D.CircleCastAll(start,kind=="minecraft:ender_pearl"?.125f:.08f,delta.normalized,delta.magnitude)) {
                var collider=hit.collider;if(collider==null||collider.name.StartsWith("HollowCraft.Block")||collider.GetComponentInParent<HeroController>()!=null)continue;
                var enemy=collider.GetComponentInParent<HealthManager>();
                if(enemy==null&&(collider.isTrigger||collider.gameObject.layer!=LayerMask.NameToLayer("Terrain")&&collider.gameObject.layer!=LayerMask.NameToLayer("Default")))continue;
                if(enemy!=null&&enemy.isDead)continue;
                if(hit.distance<=.001f&&Vector2.Dot(hit.normal,delta)>=0)continue;
                var runtime=HollowCraftRuntime.Instance;var hero=HeroController.instance;if(runtime==null||hero==null)return;
                Vector2 normal=hit.normal.sqrMagnitude>.01f?hit.normal:-delta.normalized;
                Vector2 point=hit.point,landing=PearlLanding(point,normal,hero);
                impactPoint=new Vector3(hit.centroid.x,hit.centroid.y,-.15f);awaitingImpact=true;retryAt=Time.unscaledTime+.4f;
                hitCommand=new {type="nativeProjectileHit",scene=room,entity,uuid,x=point.x,y=point.y,normalX=normal.x,normalY=normal.y,
                    landingX=landing.x,landingY=landing.y,target=enemy!=null?EnemySystem.Register(enemy):0,enemies=EnemySystem.Observe(hero)};
                runtime.Send(hitCommand);return;
            }
        }
        private static Vector2 PearlLanding(Vector2 point,Vector2 normal,HeroController hero) {
            var body=hero.GetComponent<Collider2D>();var size=new Vector2(body.bounds.size.x,body.bounds.size.y);
            var baseFeet=point+new Vector2(normal.x*(size.x*.5f+.03f),normal.y>0?.03f:normal.y<0?-size.y-.03f:0);
            for(int step=0;step<16;step++) {
                var feet=baseFeet+normal*(step*.1f);bool occupied=false;
                foreach(var collider in Physics2D.OverlapBoxAll(feet+Vector2.up*size.y*.5f,size*.98f,0,LayerMask.GetMask("Terrain","Default")))
                    if(!collider.isTrigger&&collider.GetComponentInParent<HeroController>()==null&&collider.GetComponentInParent<HealthManager>()==null) {occupied=true;break;}
                if(!occupied)return feet;
            }
            return new Vector2(hero.transform.position.x,body.bounds.min.y);
        }
        private void Update() {
            if(HollowCraftRuntime.Instance==null||HollowCraftRuntime.Instance.Room!=room||(retireAt>=0&&Time.unscaledTime>=retireAt)){Destroy(gameObject);return;}
            float elapsed=Time.unscaledTime-received;
            transform.position=Vector3.Lerp(from,to,Mathf.Clamp01(elapsed/.05f));
            if(!grounded&&retireAt<0&&elapsed>.05f)transform.position+=new Vector3(velocity.x,velocity.y,0)*20*Mathf.Min(.05f,elapsed-.05f);
            if(!awaitingImpact)Sweep(from,transform.position);
            if(awaitingImpact) {
                transform.position=impactPoint;
                if(retireAt<0&&Time.unscaledTime>=retryAt) {retryAt=Time.unscaledTime+.4f;HollowCraftRuntime.Instance.Send(hitCommand);}
            }
            if(kind!="minecraft:falling_block"&&velocity.sqrMagnitude>.001f)transform.rotation=Quaternion.Euler(0,0,Mathf.Atan2(velocity.y,velocity.x)*Mathf.Rad2Deg-(kind=="minecraft:trident"?90:0));
            foreach(var renderer in meshes)if(renderer!=null)MinecraftMeshes.SortFaces(renderer);
        }
    }
}
