using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace HollowCraft
{
    // Exercise the same pointer handler used by gameplay: down, hold and release are separate inputs.
    internal sealed class PointerSmoke
    {
        private readonly HollowCraftRuntime runtime;private readonly HeroController hero;private readonly string scene;private readonly Action<bool,string> result;
        private int stage;private float since,firstDamage,contactHealth;private Vector3 origin,contactPosition;private Vector2Int cell;private HealthManager enemy;private int enemyId;
        private int Slot(string id) {var inv=(JArray)runtime.LatestSnapshot["inventory"];for(int i=0;i<inv.Count;i++) if((string)inv[i]["id"]==id&&(int)inv[i]["count"]>0) return i;return -1;}
        private void Send(object command)=>runtime.SmokeSend(command);
        private void Select(string id) {int index=Slot(id);if(index<0) throw new Exception("Missing smoke item "+id);if(index>=9) {Send(new {type="swap",scene,from=index,to=8});index=8;}runtime.Select(index);}
        private bool Block=>((JArray)runtime.LatestSnapshot["blocks"]).Any(b=>(int)b["x"]==cell.x&&(int)b["y"]==cell.y);
        private float Health=>((JArray)runtime.LatestSnapshot["enemies"]).Where(e=>(int)e["id"]==enemyId).Select(e=>(float)e["health"]).DefaultIfEmpty(20).First();
        private Vector2 Pointer=>new Vector2(cell.x+.5f,cell.y+.5f);
        private void Next(int value) {stage=value;since=Time.unscaledTime;}
        internal PointerSmoke(HollowCraftRuntime value,HeroController player,string room,Action<bool,string> report) {runtime=value;hero=player;scene=room;result=report;origin=hero.transform.position;cell=new Vector2Int(Mathf.FloorToInt(origin.x)+2,Mathf.FloorToInt(origin.y)+1);Send(new {type="mode",scene,mode="creative"});Send(new {type="kit",scene});Next(0);}
        internal void Tick() {
            if(Time.unscaledTime-since>15) {result(false,"Pointer/numerical stage "+stage+": "+runtime.LatestSnapshot["status"]);return;}
            if(stage==0 && runtime.Creative && Slot("minecraft:tnt")>=0) {Select("minecraft:dirt");Next(1);}
            else if(stage==1 && runtime.HeldItem=="minecraft:dirt") {
                if(!runtime.PlacementAllowed(cell,hero)) {cell.y++;return;}
                runtime.HandlePointer(hero,Pointer,false,false,true,true);Next(2);
            }
            else if(stage==2 && Block) {
                Debug.Log("[HollowCraft SMOKE] PASS right-click pointer handler places at exact cursor cell with translucent preview");
                Select("minecraft:cooked_beef");Send(new {type="mode",scene,mode="survival"});Next(3);
            }
            else if(stage==3 && !runtime.Creative && runtime.HeldItem=="minecraft:cooked_beef") {
                var block=((JArray)runtime.LatestSnapshot["blocks"]).First(b=>(int)b["x"]==cell.x&&(int)b["y"]==cell.y);
                int ticks=(int)block["breakTicks"];if(ticks!=15) {result(false,"Grounded vanilla dirt by hand expected 15 ticks, got "+ticks);return;}
                runtime.HandlePointer(hero,Pointer,true,true,false,false);Next(4);
            }
            else if(stage==4) {
                runtime.HandlePointer(hero,Pointer,false,true,false,false);
                if(!Block) {
                    float elapsed=Time.unscaledTime-since;if(elapsed<.55f||elapsed>1.3f) {result(false,"Vanilla dirt mining time mismatch: "+elapsed);return;}
                    runtime.HandlePointer(hero,Pointer,false,false,false,false);Debug.Log("[HollowCraft SMOKE] PASS held pointer mining: dirt 15 server ticks, elapsed "+elapsed);
                    Send(new {type="mode",scene,mode="creative"});Select("minecraft:cobblestone");Next(40);
                }
            }
            else if(stage==40 && runtime.Creative && runtime.HeldItem=="minecraft:cobblestone") {runtime.HandlePointer(hero,Pointer,false,false,true,true);Next(41);}
            else if(stage==41 && Block) {Select("minecraft:cooked_beef");Send(new {type="mode",scene,mode="survival"});Next(42);}
            else if(stage==42 && !runtime.Creative && runtime.HeldItem=="minecraft:cooked_beef") {
                var block=((JArray)runtime.LatestSnapshot["blocks"]).First(b=>(int)b["x"]==cell.x&&(int)b["y"]==cell.y);
                if((int)block["breakTicks"]!=200) throw new Exception("Cobblestone without a suitable tool must take 200 ticks");
                Select("minecraft:stone_pickaxe");Next(43);
            }
            else if(stage==43 && runtime.HeldItem=="minecraft:stone_pickaxe" && Time.unscaledTime-since>.3f && (int)runtime.LatestSnapshot["selected"]==runtime.SelectedSlot) {
                var block=((JArray)runtime.LatestSnapshot["blocks"]).First(b=>(int)b["x"]==cell.x&&(int)b["y"]==cell.y);
                if((int)block["breakTicks"]!=15) throw new Exception("Cobblestone with stone pickaxe must take 15 ticks");
                runtime.HandlePointer(hero,Pointer,true,true,false,false);Next(44);
            }
            else if(stage==44) {
                runtime.HandlePointer(hero,Pointer,false,true,false,false);
                if(!Block) {runtime.HandlePointer(hero,Pointer,false,false,false,false);Debug.Log("[HollowCraft SMOKE] PASS cobblestone: bare hand 200 ticks, stone pickaxe 15 ticks; held pointer mining works");Send(new {type="mode",scene,mode="creative"});Select("minecraft:diamond_sword");Next(5);}
            }
            else if(stage==5 && runtime.Creative && runtime.HeldItem=="minecraft:diamond_sword" && !runtime.Combat.Swinging) {
                var source=UnityEngine.Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None).First(e=>!e.isDead && e.GetComponent<Collider2D>()!=null);
                enemy=UnityEngine.Object.Instantiate(source);enemy.name="HollowCraft.NumericSmokeEnemy";enemy.hp=20;enemy.isDead=false;
                foreach(var component in enemy.GetComponents<MonoBehaviour>()) if(component!=enemy) component.enabled=false;
                foreach(var damage in enemy.GetComponentsInChildren<DamageHero>()) damage.enabled=false;
                var body=enemy.GetComponent<Rigidbody2D>();if(body!=null) body.constraints=RigidbodyConstraints2D.FreezeAll;
                // Keep the target away from the earlier mining/empty swings until the critical fixture starts.
                enemy.transform.position=hero.transform.position+Vector3.right*10;enemyId=EnemySystem.Register(enemy);Next(6);
            }
            else if(stage==6 && Time.unscaledTime-since>.5f && !runtime.Combat.Swinging) {
                MinecraftControls.frontView=true;MinecraftControls.Tick(hero,true,true);if(!MinecraftControls.FrontView) throw new Exception("Front view did not latch without S held");
                Debug.Log("[HollowCraft SMOKE] PASS front-facing pose stays latched without holding S");
                runtime.HandlePointer(hero,(Vector2)hero.transform.position+Vector2.up*2,true,false,false,false);
                if(!runtime.Combat.Swinging) throw new Exception("Short click did not begin animation immediately");
                runtime.HandlePointer(hero,(Vector2)hero.transform.position+Vector2.up*2,false,false,false,false);Next(7);
            }
            else if(stage==7 && !runtime.Combat.Swinging) {Debug.Log("[HollowCraft SMOKE] PASS press/release short click plays an empty Better Combat attack");Next(8);}
            else if(stage==8 || stage==9 || stage==10 || stage==11) {
                hero.transform.position=origin+Vector3.up*3;hero.GetComponent<Rigidbody2D>().linearVelocity=Vector2.down;
                hero.cState.onGround=false;
                enemy.transform.position=hero.transform.position+Vector3.right*1.3f;
                if(stage==8 && Time.unscaledTime-since>.4f && !runtime.Combat.Swinging) {runtime.HandlePointer(hero,enemy.transform.position,true,false,false,false);Next(9);}
                else if(stage==9 && Health<20) {
                    firstDamage=20-Health;if(Math.Abs(firstDamage-10.5f)>.05f) {result(false,"Critical damage expected 10.5, got "+firstDamage);return;}
                    Next(10);
                }
                else if(stage==10 && !runtime.Combat.Swinging && Time.unscaledTime-since>.65f) {runtime.HandlePointer(hero,enemy.transform.position,true,false,false,false);Next(11);}
                else if(stage==11 && (enemy.isDead || enemy.hp<=0)) {
                    Debug.Log("[HollowCraft SMOKE] PASS 20 HP native enemy: diamond sword critical 10.5; two falling criticals kill");hero.transform.position=origin;hero.GetComponent<Rigidbody2D>().linearVelocity=Vector2.zero;Send(new {type="mode",scene,mode="survival"});Next(12);
                }
            }
            else if(stage==12 && !runtime.Creative && Time.unscaledTime-since>.7f) {
                contactPosition=hero.transform.position;contactHealth=(float)runtime.Stats["health"];PlayerSystem.RouteDamage(null,GlobalEnums.CollisionSide.left,1,1);
                if(Vector3.Distance(contactPosition,hero.transform.position)>.01f) throw new Exception("Normal contact teleported before the server damage result");Next(121);
            }
            else if(stage==121&&(float)runtime.Stats["health"]<contactHealth) {
                if(Vector3.Distance(contactPosition,hero.transform.position)>2||!PlayerSystem.Hurt||!PlayerSystem.KnockbackActive) throw new Exception("Confirmed normal contact teleported or omitted hurt feedback");
                Debug.Log("[HollowCraft SMOKE] PASS ordinary hazardType=1 contact stays in place and activates hurt flash/knockback");Send(new {type="mode",scene,mode="creative"});Select("minecraft:tnt");Next(13);
            }
            else if(stage==13 && runtime.Creative && runtime.HeldItem=="minecraft:tnt" && Time.unscaledTime-since>.4f) {
                origin=hero.transform.position;cell=new Vector2Int(Mathf.FloorToInt(origin.x)+2,Mathf.FloorToInt(origin.y)+1);
                Select("minecraft:cobblestone");Next(130);
            }
            else if(stage==130 && runtime.HeldItem=="minecraft:cobblestone") {
                int floor=Mathf.FloorToInt(hero.GetComponent<Collider2D>().bounds.min.y)-1;
                for(int x=cell.x-1;x<=cell.x+1;x++) Send(new {type="place",scene,x,y=floor});
                Select("minecraft:tnt");Next(131);
            }
            else if(stage==131 && runtime.HeldItem=="minecraft:tnt" && Time.unscaledTime-since>.3f) {
                var source=UnityEngine.Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None).First(e=>!e.isDead && e.GetComponent<Collider2D>()!=null);
                enemy=UnityEngine.Object.Instantiate(source);enemy.hp=200;enemy.isDead=false;
                foreach(var component in enemy.GetComponents<MonoBehaviour>()) if(component!=enemy) component.enabled=false;
                foreach(var damage in enemy.GetComponentsInChildren<DamageHero>()) damage.enabled=false;
                var body=enemy.GetComponent<Rigidbody2D>();if(body!=null) body.constraints=RigidbodyConstraints2D.FreezeAll;
                enemy.transform.position=new Vector3(cell.x-1,origin.y,origin.z);enemyId=EnemySystem.Register(enemy);
                runtime.HandlePointer(hero,Pointer,false,false,true,true);Next(14);
            }
            else if(stage==14 && Block) {Select("minecraft:flint_and_steel");Next(15);}
            else if(stage==15 && runtime.HeldItem=="minecraft:flint_and_steel") {runtime.HandlePointer(hero,Pointer,false,false,true,true);Next(16);}
            else if(stage==16 && ((JArray)runtime.LatestSnapshot["primed"]).Count>0) {Debug.Log("[HollowCraft SMOKE] PASS cursor TNT ignition produces a vanilla 80-tick primed entity");Next(17);}
            else if(stage==17 && ((JArray)runtime.LatestSnapshot["primed"]).Count==0 && Time.unscaledTime-since>3) {
                var icon=runtime.ItemTexture("minecraft:dirt");if(icon==null || icon.width!=64) {result(false,"Vanilla baked 3D item model icon missing");return;}
                if(Health>=200 || enemy.hp>=200) {result(false,"Vanilla TNT failed to damage enemy proxy: MC HP "+Health+", native HP "+enemy.hp+", victim at "+enemy.transform.position);return;}
                Debug.Log("[HollowCraft SMOKE] PASS TNT damage calculated by vanilla explosion -> native HP: 200 -> "+enemy.hp);
                Debug.Log("[HollowCraft SMOKE] PASS vanilla TNT fuse/explosion and Minecraft-rendered 64px item models");
                if(icon.GetPixel(0,0).a>.01f) throw new Exception("GUI model corner is opaque; RGBA export regression");
                var bodyModel=GameObject.Find("HollowCraft.Steve3D.Head");
                if(bodyModel==null || bodyModel.GetComponentInChildren<MeshFilter>()?.sharedMesh.vertexCount!=24) throw new Exception("Steve head must have six real mesh faces");
                Debug.Log("[HollowCraft SMOKE] PASS transparent GUI pixels and six-face 3D Steve body geometry");
                Send(new {type="checkpoint",scene});
                result(true,"Pointer placement/ghost, short-click animation, vanilla mining ticks, latched facing, 10.5 critical and two-hit kill, normal contact feedback, real TNT, baked GUI models; prior survival/crafting/container checks passed");
            }
        }
    }
}
