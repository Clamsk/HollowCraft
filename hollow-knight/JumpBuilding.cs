using System;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace HollowCraft
{
    // One support surface and one placement request per actual jump. Cursor motion
    // and the regular item-use cooldown do not change an already armed target.
    internal sealed class JumpBuilding
    {
        private readonly HollowCraftRuntime runtime;
        private readonly RaycastHit2D[] hits=new RaycastHit2D[32];
        private Vector2Int cell;
        private float supportY,jumpedAt,intentUntil;
        private bool nativeSupport,active,used,intent;
        private Request request;
        private sealed class Request {
            internal string Id,Item;
            internal Vector2Int Cell;
            internal bool Foundation,Sent;
            internal float Support,Deadline,RetryAt;
            internal object Packet;
        }
        internal JumpBuilding(HollowCraftRuntime runtime) {this.runtime=runtime;}
        internal bool CanUse=>runtime.TowerItem!=null&&!runtime.GameplayBlocked&&!runtime.Spectator&&runtime.Mode!="adventure"&&!MinecraftEnvironment.Flying&&!MinecraftEnvironment.Gliding;
        internal bool AutoJump=>CanUse&&Application.isFocused&&Input.GetKey(KeyCode.B)&&request==null;
        private bool Underfoot(HeroController hero,Vector2 mouse) {
            var body=hero.GetComponent<Collider2D>();
            return body!=null&&Math.Abs(mouse.x-body.bounds.center.x)<=1.25f&&mouse.y<=body.bounds.min.y+.8f;
        }
        private bool Support(HeroController hero,out float top,out bool native) {
            top=float.NegativeInfinity;native=false;
            var body=hero.GetComponent<Collider2D>();if(body==null)return false;
            var bounds=body.bounds;
            var filter=new ContactFilter2D();filter.SetLayerMask(LayerMask.GetMask("Terrain","Default"));filter.useTriggers=false;
            foreach(float offset in new[]{0f,-.7f,.7f}) {
                int count=Physics2D.Raycast(new Vector2(bounds.center.x+offset*bounds.extents.x,bounds.min.y+.08f),Vector2.down,filter,hits,.18f);
                for(int i=0;i<count;i++) {
                    var hit=hits[i];var collider=hit.collider;
                    if(collider==null||hit.normal.y<.5f||collider.GetComponentInParent<HeroController>()!=null||collider.GetComponentInParent<HealthManager>()!=null||hit.point.y<=top)continue;
                    top=hit.point.y;native=true;
                    for(var node=collider.transform;node!=null;node=node.parent)if(node.name.StartsWith("HollowCraft.Block")) {native=false;break;}
                }
            }
            return !float.IsNegativeInfinity(top);
        }
        internal void BeginJump(HeroController hero) {
            active=false;used=false;
            if(request!=null)return;
            if(!Support(hero,out supportY,out nativeSupport)) {supportY=hero.GetComponent<Collider2D>().bounds.min.y;nativeSupport=false;}
            // A thin foundation may embed below a fractional native surface. Its
            // top is the next voxel edge, so the first jump never needs two blocks.
            cell=new Vector2Int(Mathf.FloorToInt(hero.GetComponent<Collider2D>().bounds.center.x),Mathf.FloorToInt(supportY+.025f));
            active=true;jumpedAt=Time.unscaledTime;
            bool pointed=GameCursor.WorldTarget(hero,out Vector2 mouse,out _)&&Underfoot(hero,mouse);
            if(CanUse&&(Input.GetKey(KeyCode.B)||(intent&&Time.unscaledTime<=intentUntil)||runtime.BuildRightHeld&&pointed))Arm();
            intent=false;
        }
        private void Arm() {
            if(request!=null||used||!active||!CanUse)return;
            request=new Request {Id=Guid.NewGuid().ToString("N"),Item=(string)runtime.TowerItem["id"],Cell=cell,Foundation=nativeSupport,Support=supportY,Deadline=jumpedAt+1.3f};
            used=true;
            Debug.Log("[HollowCraft] Tower armed cell="+cell+" support="+supportY+" native="+nativeSupport);
        }
        internal bool Pointer(HeroController hero,Vector2 mouse) {
            if(!CanUse||!Underfoot(hero,mouse))return false;
            if(request!=null)return true;
            if(MinecraftControls.Grounded(hero)) {intent=true;intentUntil=Time.unscaledTime+.6f;return true;}
            if(!active)return false;
            Arm();return true;
        }
        internal bool Preview(HeroController hero,Vector2 mouse,out Vector2Int target,out bool foundation,out float top) {
            target=cell;foundation=nativeSupport;top=supportY;
            if(request!=null) {target=request.Cell;foundation=request.Foundation;top=request.Support;return true;}
            return active&&!used&&CanUse&&Underfoot(hero,mouse);
        }
        internal void Tick(HeroController hero) {
            if(!CanUse||!Application.isFocused) {Cancel();return;}
            if(active&&Time.unscaledTime-jumpedAt>.1f&&MinecraftControls.Grounded(hero)) {active=false;intent=false;if(request!=null&&!request.Sent)Cancel();}
            var job=request;if(job==null)return;
            if((string)runtime.TowerItem?["id"]!=job.Item) {Cancel();return;}
            if(Time.unscaledTime>job.Deadline) {runtime.RejectTower(job.Cell);request=null;Debug.Log("[HollowCraft] Tower expired before confirmation");return;}
            if(job.Sent) {
                if(Time.unscaledTime>=job.RetryAt) {job.RetryAt=Time.unscaledTime+.2f;runtime.Send(job.Packet);}
                return;
            }
            if(!runtime.TowerPlacementAllowed(job.Cell,hero,job.Foundation,job.Support))return;
            var body=hero.GetComponent<Collider2D>();
            // The prediction uses a full collision cube, not the preview's .98 box.
            if(body.bounds.min.y<job.Cell.y+1.015f)return;
            runtime.PrepareTower(hero,job.Cell,runtime.TowerItem);
            job.Packet=new {type="towerPlace",scene=runtime.Room,request=job.Id,x=job.Cell.x,y=job.Cell.y,
                nativeFoundation=job.Foundation,supportY=job.Support,playerX=hero.transform.position.x,playerY=body.bounds.min.y};
            job.Sent=true;job.Deadline=Time.unscaledTime+1.5f;job.RetryAt=Time.unscaledTime+.2f;
            runtime.Send(job.Packet);
            Debug.Log("[HollowCraft] Tower submitted request="+job.Id+" cell="+job.Cell+" feet="+body.bounds.min.y);
        }
        internal void Resolve(JObject reply) {
            if(request==null||(string)reply["request"]!=request.Id)return;
            bool accepted=(bool?)reply["accepted"]==true;
            if(!accepted)runtime.RejectTower(request.Cell);
            Debug.Log("[HollowCraft] Tower confirmed accepted="+accepted+" reason="+(string)reply["reason"]);
            request=null;
        }
        internal void Cancel() {
            if(request!=null&&request.Sent)runtime.RejectTower(request.Cell);
            request=null;active=false;intent=false;used=false;
        }
    }
}
