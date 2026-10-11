using System;
using System.Linq;
using System.IO;
using System.Reflection;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace HollowCraft
{
    internal sealed class ControlsSmoke
    {
        private readonly HollowCraftRuntime runtime;private readonly HeroController hero;private readonly string scene;private readonly Action<bool,string> report;
        private int stage;private float since,health;private Vector3 origin;private Vector2Int cell;private float surface;private bool hurtSeen;private GameObject thinFloor;
        private Breakable nativeGate;private Quaternion attackPose;private int attackId;private Transform hand;private Vector3 grip;
        private void Send(object value)=>runtime.SmokeSend(value);
        private void Next(int value){stage=value;since=Time.unscaledTime;}
        private JObject Block()=>((JArray)runtime.LatestSnapshot["blocks"]).OfType<JObject>().FirstOrDefault(b=>(int)b["x"]==cell.x&&(int)b["y"]==cell.y);
        private void Item(string id) {Send(new {type="creativeItem",scene,id,count=1,slot=8});runtime.Select(8);}
        private void Right()=>runtime.HandlePointer(hero,new Vector2(cell.x+.5f,cell.y+.5f),false,false,true,true);
        internal ControlsSmoke(HollowCraftRuntime value,HeroController player,string room,Action<bool,string> result) {
            runtime=value;hero=player;scene=room;report=result;origin=hero.transform.position;
            float feet=hero.GetComponent<Collider2D>().bounds.min.y;
            int x=Mathf.FloorToInt(origin.x)+2;
            var floor=Physics2D.RaycastAll(new Vector2(x+.5f,feet+1),Vector2.down,3,LayerMask.GetMask("Terrain","Default")).First(h=>h.collider!=null&&!h.collider.isTrigger&&h.collider.GetComponentInParent<HeroController>()==null&&h.collider.GetComponentInParent<HealthManager>()==null);
            surface=floor.point.y;cell=new Vector2Int(x,Mathf.FloorToInt(surface+.08f));
            Debug.Log("[HollowCraft SMOKE] Ground surface="+surface+" cell="+cell+" collider="+floor.collider.name+" bounds="+floor.collider.bounds);
            foreach(var breakable in UnityEngine.Object.FindObjectsByType<Breakable>(FindObjectsSortMode.None)) {
                var collider=breakable.GetComponent<Collider2D>();
                if(collider==null||!breakable.gameObject.activeInHierarchy)continue;
                Debug.Log("[HollowCraft SMOKE] Native breakable="+breakable.name+" bounds="+collider.bounds+" DamageHero="+(breakable.GetComponent<DamageHero>()!=null)+" damageFSM="+FSMUtility.ContainsFSM(breakable.gameObject,"damages_hero"));
            }
            Send(new {type="mode",scene,mode="creative"});Item("minecraft:dirt");Next(0);
        }
        internal void Tick() {
            if(Time.unscaledTime-since>20)throw new Exception("Controls stage "+stage+" status="+runtime.LatestSnapshot["status"]+" block="+Block()+" stats="+runtime.Stats);
            if(stage==0&&runtime.Creative&&runtime.HeldItem=="minecraft:dirt"&&Time.unscaledTime-since>.5f) {
                if(!runtime.PlacementAllowed(cell,hero))throw new Exception("Native ground is still marked as an occupied full cube");runtime.HandlePointer(hero,new Vector2(cell.x+.5f,surface-.25f),false,false,true,true);Next(1);
            } else if(stage==1&&Block()!=null) {
                var go=GameObject.Find("HollowCraft.Block.minecraft:dirt");
                if(go==null||Math.Abs(go.GetComponent<Collider2D>().bounds.min.y-surface)>.081f)throw new Exception("Placed ground block does not meet native surface");
                Debug.Log("[HollowCraft SMOKE] PASS pointer placement directly on native floor without adding a support block");Send(new {type="mine",scene,x=cell.x,y=cell.y,heldTicks=1});Next(2);
            } else if(stage==2&&Block()==null) {
                Debug.Log("[HollowCraft SMOKE] PASS ground block remains mineable over native platform proxy");
                thinFloor=new GameObject("HollowCraft.SmokeFractionalPlatform");thinFloor.layer=LayerMask.NameToLayer("Terrain");thinFloor.transform.position=new Vector3(cell.x+.5f,surface,0);thinFloor.AddComponent<BoxCollider2D>().size=new Vector2(1,.075f);Next(18);
            } else if(stage==18&&Time.unscaledTime-since>.5f) {
                if(!runtime.PlacementAllowed(cell,hero))throw new Exception("Thin platform edge still blocks cursor cell");Right();Next(19);
            } else if(stage==19&&Block()!=null) {
                Debug.Log("[HollowCraft SMOKE] PASS fractional native platform top +.0375 does not become a full cube or displace placement");
                Send(new {type="mine",scene,x=cell.x,y=cell.y,heldTicks=1});UnityEngine.Object.Destroy(thinFloor);Next(20);
            } else if(stage==20&&Block()==null&&Time.unscaledTime-since>.4f) {Item("minecraft:oak_door");Next(3);
            } else if(stage==3&&runtime.HeldItem=="minecraft:oak_door"&&Time.unscaledTime-since>.4f) {Right();Next(4);
            } else if(stage==4&&Block()!=null) {
                Send(new {type="mode",scene,mode="survival"});hero.transform.position=new Vector3(cell.x+.05f,origin.y,origin.z);hero.GetComponent<Rigidbody2D>().linearVelocity=Vector2.zero;health=(float)runtime.Stats["health"];Next(5);
            } else if(stage==5&&!runtime.Creative&&Time.unscaledTime-since>2) {
                if((float)runtime.Stats["health"]!=health)throw new Exception("Standing at closed door caused damage");
                Debug.Log("[HollowCraft SMOKE] PASS two seconds against closed door in survival without phantom suffocation");Right();Next(6);
            } else if(stage==6&&(bool?)Block()?["open"]==true) {
                hero.transform.position=new Vector3(cell.x+.5f,origin.y,origin.z);hero.GetComponent<Rigidbody2D>().linearVelocity=Vector2.zero;Next(7);
            } else if(stage==7&&Time.unscaledTime-since>1) {
                if((float)runtime.Stats["health"]!=health)throw new Exception("Standing inside open door caused damage");
                Debug.Log("[HollowCraft SMOKE] PASS open door passage has no projected MC wall damage");hero.transform.position=origin;
                Send(new {type="damage",scene,amount=3,kind="projectile",enemy=0,attackerX=origin.x-1,attackerY=origin.y});
                Send(new {type="damage",scene,amount=3,kind="projectile",enemy=0,attackerX=origin.x-1,attackerY=origin.y});Next(8);
            } else if(stage==8) {
                hurtSeen|=PlayerSystem.Hurt;
                if(Time.unscaledTime-since>.3f) {
                    if(Math.Abs(health-(float)runtime.Stats["health"]-3)>.01f||!hurtSeen)throw new Exception("MC hurt flash/invulnerability mismatch");
                    var body=hero.GetComponent<Rigidbody2D>();if(hero.transform.position.x<=origin.x+.01f||Math.Abs(body.linearVelocity.x)>8.1f||body.linearVelocity.y>8.1f)throw new Exception("Hurt knockback missing or multiplied above vanilla .4 blocks/tick");
                    Debug.Log("[HollowCraft SMOKE] PASS MC invulnerability rejects repeated equal damage, opaque hurt feedback and single vanilla impulse");
                    runtime.Modes.Tick(runtime,true,true,false,true);Next(9);
                }
            } else if(stage==9&&runtime.Modes.Open) {
                if(!runtime.GameplayBlocked)throw new Exception("Mode menu failed to block gameplay input");
                ScreenCapture.CaptureScreenshot(Path.Combine(HollowCraftBootstrap.SaveRoot(),"hollowcraft-mode-menu.png"));Next(21);
            } else if(stage==21&&Time.unscaledTime-since>.2f) {
                runtime.Modes.Tick(runtime,false,false,false,true);Next(10);
            } else if(stage==10&&runtime.Mode=="creative") {
                runtime.Modes.Tick(runtime,true,true,false,true);runtime.Modes.Tick(runtime,true,true,false,true);runtime.Modes.Tick(runtime,false,false,false,true);Next(11);
            } else if(stage==11&&runtime.Mode=="adventure") {
                if(runtime.PlacementAllowed(cell+Vector2Int.up*2,hero))throw new Exception("Adventure allowed ordinary block placement");
                runtime.ChangeMode("spectator");Next(12);
            } else if(stage==12&&runtime.Spectator&&Time.unscaledTime-since>.3f) {
                if(hero.GetComponent<Rigidbody2D>().bodyType!=RigidbodyType2D.Kinematic||!hero.GetComponent<Collider2D>().isTrigger)throw new Exception("Spectator failed to disable native solid collision");
                health=(float)runtime.Stats["health"];PlayerSystem.RouteDamage(null,GlobalEnums.CollisionSide.left,5,1);Next(13);
            } else if(stage==13&&Time.unscaledTime-since>.4f) {
                if((float)runtime.Stats["health"]<health)throw new Exception("Spectator took native damage");runtime.ChangeMode("survival");Next(14);
            } else if(stage==14&&runtime.Mode=="survival"&&Time.unscaledTime-since>.3f) {
                if(hero.GetComponent<Rigidbody2D>().bodyType!=RigidbodyType2D.Dynamic||hero.GetComponent<Collider2D>().isTrigger)throw new Exception("Leaving spectator failed to restore player collision");
                runtime.Modes.Tick(runtime,true,true,false,true);runtime.Modes.Tick(runtime,true,false,true,true);
                if(runtime.Modes.Open||!runtime.Modes.ConsumedEscape||runtime.Mode!="survival")throw new Exception("Escape did not cancel mode chooser");
                Debug.Log("[HollowCraft SMOKE] PASS F3/F4 open, cycle, release commit, Escape cancel; all four authoritative modes and spectator collision restore");
                runtime.ChangeMode("creative");Item("minecraft:diamond_sword");Next(15);
            } else if(stage==15&&runtime.HeldItem=="minecraft:diamond_sword"&&Time.unscaledTime-since>.8f) {
                var held=GameObject.Find("HollowCraft.HeldItem3D");if(held==null||held.transform.localPosition!=Vector3.zero||Quaternion.Angle(held.transform.localRotation,Quaternion.identity)>.01f||held.GetComponentsInChildren<MeshRenderer>().Length==0)throw new Exception("Baked vanilla grip missing or applied twice");
                MinecraftControls.frontView=false;MinecraftControls.Facing=1;hero.transform.position=origin;
                ScreenCapture.CaptureScreenshot(Path.Combine(HollowCraftBootstrap.SaveRoot(),"hollowcraft-corrected-grip.png"));
                Next(16);
            } else if(stage==16&&Time.unscaledTime-since>.2f) {
                MinecraftControls.TestCrouch=true;Next(17);
            } else if(stage==17&&Time.unscaledTime-since>.3f) {
                var torso=GameObject.Find("HollowCraft.Steve3D.Torso").transform;var leg=GameObject.Find("HollowCraft.Steve3D.Right leg").transform;
                if(Quaternion.Angle(torso.localRotation,Quaternion.Euler(-.5f*Mathf.Rad2Deg,0,0))>.02f||Math.Abs(leg.localPosition.z-.25f)>.01f||Math.Abs(hero.GetComponent<Collider2D>().bounds.size.y-1.5f)>.01f)throw new Exception("Vanilla crouch pivots/hip offset/collider mismatch");
                ScreenCapture.CaptureScreenshot(Path.Combine(HollowCraftBootstrap.SaveRoot(),"hollowcraft-corrected-crouch.png"));
                Debug.Log("[HollowCraft SMOKE] PASS vanilla crouch body .5 radians, hip Z4 pixels, correct pivots and fully baked hand mesh");Next(22);
            } else if(stage==22&&Time.unscaledTime-since>.3f) {MinecraftControls.TestCrouch=null;MinecraftControls.TestMovement=1;MinecraftControls.TestSprint=true;Next(23);
            } else if(stage==23&&Time.unscaledTime-since>.35f) {
                if(hero.transform.position.x<=origin.x+.5f||hero.GetComponent<Rigidbody2D>().linearVelocity.x<5.4f)throw new Exception("Native movement hook failed to apply vanilla sprint speed");
                ScreenCapture.CaptureScreenshot(Path.Combine(HollowCraftBootstrap.SaveRoot(),"hollowcraft-corrected-run.png"));Next(24);
            } else if(stage==24&&Time.unscaledTime-since>.3f) {
                MinecraftControls.TestMovement=null;MinecraftControls.TestSprint=null;
                Debug.Log("[HollowCraft SMOKE] PASS native Move hook drives vanilla sprint displacement and animated limb pivots");
                hero.GetComponent<Rigidbody2D>().linearVelocity=Vector2.zero;hero.transform.position=origin;
                Item("minecraft:dirt");runtime.Combat.Cancel();Next(36);
            } else if(stage==36&&runtime.HeldItem=="minecraft:dirt"&&Time.unscaledTime-since>.4f) {runtime.ChangeMode("survival");Next(25);
            } else if(stage==25&&runtime.Mode=="survival"&&Time.unscaledTime-since>.5f) {
                var eye=GameCursor.Eye(hero);cell=new Vector2Int(Mathf.FloorToInt(eye.x)+4,Mathf.FloorToInt(eye.y)+1);
                Next(26);
            } else if(stage==26&&runtime.HeldItem=="minecraft:dirt"&&Time.unscaledTime-since>.4f) {
                if(!runtime.PlacementAllowed(cell,hero)||GameCursor.BlockInReach(cell+Vector2Int.right*2,hero))throw new Exception("Survival block reach does not allow 4.5 / reject 6 blocks");
                Send(new {type="place",scene,x=cell.x+2,y=cell.y});Next(27);
            } else if(stage==27&&Time.unscaledTime-since>.4f) {
                if(((JArray)runtime.LatestSnapshot["blocks"]).Any(b=>(int)b["x"]==cell.x+2&&(int)b["y"]==cell.y))throw new Exception("Server accepted an out-of-reach survival block");
                Send(new {type="place",scene,x=cell.x,y=cell.y});Next(28);
            } else if(stage==28&&Block()!=null) {
                Debug.Log("[HollowCraft SMOKE] PASS survival block reach accepts beyond old 3-block cap and server rejects beyond 4.5; eye origin shared");
                Send(new {type="mine",scene,x=cell.x,y=cell.y,heldTicks=100});runtime.ChangeMode("creative");Next(37);
            } else if(stage==37&&runtime.Creative) {Item("minecraft:diamond_sword");Next(38);
            } else if(stage==38&&runtime.HeldItem=="minecraft:diamond_sword"&&Time.unscaledTime-since>.5f) {runtime.ChangeMode("survival");Next(29);
            } else if(stage==29&&runtime.Mode=="survival"&&Time.unscaledTime-since>.5f) {
                hand=GameObject.Find("HollowCraft.HeldItem3D").transform;grip=hand.localPosition;
                runtime.HandlePointer(hero,GameCursor.Eye(hero)+Vector2.right*2,true,false,false,false);Next(30);
            } else if(stage==30&&runtime.Combat.Swinging&&runtime.Combat.Progress>.25f) {
                attackPose=hand.parent.localRotation;attackId=(int)runtime.Combat.Current["attack"];
                if(attackId==0)return;
                if(hand.parent.name!="HollowCraft.Steve3D.Right arm"||hand.localPosition!=grip||Quaternion.Angle(hand.localRotation,Quaternion.identity)>.01f)throw new Exception("Held item detached from arm during swing");
                ScreenCapture.CaptureScreenshot(Path.Combine(HollowCraftBootstrap.SaveRoot(),"hollowcraft-attached-swing.png"));Next(31);
            } else if(stage==31&&!runtime.Combat.Swinging&&Time.unscaledTime-since>.4f) {
                if(Quaternion.Angle(attackPose,hand.parent.localRotation)<15||hand.localPosition!=grip)throw new Exception("Single click failed to return from its attack pose");
                runtime.HandlePointer(hero,GameCursor.Eye(hero)+Vector2.right*2,true,false,false,false);Next(32);
            } else if(stage==32&&runtime.Combat.Swinging&&runtime.Combat.Progress>.15f&&(int)runtime.Combat.Current["attack"]>attackId) {
                attackId=(int)runtime.Combat.Current["attack"];
                runtime.HandlePointer(hero,GameCursor.Eye(hero)+Vector2.right*2,true,false,false,false);Next(33);
            } else if(stage==33&&runtime.Combat.Swinging&&(int)runtime.Combat.Current["attack"]>attackId) {
                attackId=(int)runtime.Combat.Current["attack"];Next(34);
            } else if(stage==34&&!runtime.Combat.Swinging&&Time.unscaledTime-since>1) {
                if((int)runtime.Combat.Current["attack"]!=attackId)throw new Exception("Queued clicks repeated forever");
                Debug.Log("[HollowCraft SMOKE] PASS single click swings and recovers; queued second click makes one more authoritative swing and stops; item remains attached throughout");
                nativeGate=UnityEngine.Object.FindObjectsByType<Breakable>(FindObjectsSortMode.None).Where(b=>b.gameObject.activeInHierarchy&&b.GetComponent<Collider2D>()!=null&&(b.name.IndexOf("wall",StringComparison.OrdinalIgnoreCase)>=0||b.name.IndexOf("door",StringComparison.OrdinalIgnoreCase)>=0)&&b.GetComponent<DamageHero>()==null&&!FSMUtility.ContainsFSM(b.gameObject,"damages_hero")).OrderBy(b=>Vector2.Distance(origin,b.transform.position)).FirstOrDefault();
                if(nativeGate==null)throw new Exception("No native breakable wall available for contact regression");
                var box=nativeGate.GetComponent<Collider2D>().bounds;var player=hero.GetComponent<Collider2D>().bounds;
                hero.transform.position+=new Vector3(box.min.x-.28f-player.center.x,Mathf.Max(box.min.y,10)+player.extents.y-player.center.y,0);
                hero.GetComponent<Rigidbody2D>().linearVelocity=Vector2.zero;health=(float)runtime.Stats["health"];Next(35);
            } else if(stage==35&&Time.unscaledTime-since>2) {
                if((float)runtime.Stats["health"]<health)throw new Exception("Native breakable wall contact damaged Steve: "+nativeGate.name);
                Debug.Log("[HollowCraft SMOKE] PASS native breakable wall contact two seconds without damage: "+nativeGate.name+"; original prefab has no DamageHero or damages_hero FSM");
                var box=nativeGate.GetComponent<Collider2D>().bounds;hero.transform.position+=new Vector3(box.min.x-3.1f-hero.transform.position.x,0,0);
                hero.GetComponent<Rigidbody2D>().linearVelocity=Vector2.zero;Next(39);
            } else if(stage==39&&Time.unscaledTime-since>.4f) {
                runtime.Combat.Request(scene,hero,nativeGate.GetComponent<Collider2D>().bounds.center);Next(40);
            } else if(stage==40&&!runtime.Combat.Swinging&&Time.unscaledTime-since>.9f) {
                if((bool)typeof(Breakable).GetField("isBroken",BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public).GetValue(nativeGate))throw new Exception("Survival attack broke native gate beyond 3 blocks");
                var box=nativeGate.GetComponent<Collider2D>().bounds;hero.transform.position+=new Vector3(box.min.x-1-hero.transform.position.x,0,0);hero.GetComponent<Rigidbody2D>().linearVelocity=Vector2.zero;Next(41);
            } else if(stage==41&&Time.unscaledTime-since>.4f) {
                runtime.Combat.Request(scene,hero,nativeGate.GetComponent<Collider2D>().bounds.center);Next(42);
            } else if(stage==42&&(bool)typeof(Breakable).GetField("isBroken",BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public).GetValue(nativeGate)) {
                Debug.Log("[HollowCraft SMOKE] PASS survival attack cannot break native gate at 3.1 blocks but breaks within reach through native HitTaker and vanilla proxy damage");
                hero.transform.position=origin;hero.GetComponent<Rigidbody2D>().linearVelocity=Vector2.zero;Send(new {type="checkpoint",scene});
                report(true,"Native/fractional floor placement; MC and native breakable door no contact damage; MC immunity/knockback; four game modes; vanilla poses and attached grip; single/queued swings recover; vanilla block and attack reach client/server verified");
            }
        }
    }
}

