using System;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace HollowCraft
{
    // Integration checks run only under the isolated smoke launcher, against the actual games.
    internal sealed class ExtendedSmoke
    {
        private readonly HollowCraftRuntime runtime;
        private readonly string scene;
        private readonly Vector2Int cell;
        private readonly Action<bool,string> report;
        private int stage,planksBefore,firstAttack,lastCombo,swings,resourceCount;
        private float deadline,nextRequest;
        private HeroController hero;
        private HealthManager targetEnemy;
        private int enemyBefore;
        private float healthBefore;
        private bool additional;
        private int foodBefore,breadBefore,exhaustPoses;
        private Breakable terrainTarget;
        internal ExtendedSmoke(HollowCraftRuntime value,string room,Vector2Int target,Action<bool,string> result)
        {runtime=value;scene=room;cell=target;report=result;deadline=Time.unscaledTime+45;}
        private JArray Inventory => (JArray)runtime.LatestSnapshot["inventory"];
        private JObject Container => runtime.LatestSnapshot["container"] as JObject;
        private int Slot(string id) {for(int i=0;i<Inventory.Count;i++) if((string)Inventory[i]["id"]==id && (int)Inventory[i]["count"]>0) return i;return -1;}
        private int Count(string id) => Inventory.Where(i=>(string)i["id"]==id).Sum(i=>(int)i["count"]);
        private int ResourceCount()=>Inventory.Sum(i=>(int)i["count"])+((JArray)runtime.LatestSnapshot["drops"]).Sum(i=>(int)i["count"]);
        private void Send(object command) => runtime.SmokeSend(command);
        private void Advance(int value,float timeout=30) {stage=value;deadline=Time.unscaledTime+timeout;}
        private void Place(int slot) {Send(new {type="select",scene,slot});Send(new {type="place",scene,x=cell.x,y=cell.y});}
        private bool IsBlock(string id) => ((JArray)runtime.LatestSnapshot["blocks"]).Any(b=>(int)b["x"]==cell.x && (int)b["y"]==cell.y && (string)b["id"]==id);
        internal void Tick(HeroController value)
        {
            hero=value;
            if(Time.unscaledTime>deadline) {report(false,"Extended stage "+stage+": "+(string)runtime.LatestSnapshot["status"]);return;}
            if(stage==0) {
                runtime.CloseInterface();int log=Slot("minecraft:oak_log"),serverSlot=log<9 ? log+36 : log;
                planksBefore=Count("minecraft:oak_planks");
                Send(new {type="slotClick",scene,handler="player",slot=serverSlot,button=0});
                Send(new {type="slotClick",scene,handler="player",slot=1,button=1});
                Send(new {type="slotClick",scene,handler="player",slot=serverSlot,button=0});Advance(100);
            }
            else if(stage==100 && (string)runtime.LatestSnapshot["playerCraft"]?[0]?["id"]=="minecraft:oak_planks") {
                Send(new {type="slotClick",scene,handler="player",slot=0,button=0,quick=true});Advance(101);
            }
            else if(stage==101 && Count("minecraft:oak_planks")==planksBefore+4) {
                Debug.Log("[HollowCraft SMOKE] PASS Minecraft 2x2 GUI slot pickup, split, recipe result and shift-craft");
                Place(Slot("minecraft:crafting_table"));Advance(102);
            }
            else if(stage==102 && IsBlock("minecraft:crafting_table")) {Send(new {type="use",scene,x=cell.x,y=cell.y});Advance(103);}
            else if(stage==103 && Container!=null && (string)Container["id"]=="minecraft:crafting_table") {
                if(((JArray)Container["slots"]).Count!=10) {report(false,"Workbench lacks real 3x3 screen handler");return;}
                int source=Slot("minecraft:oak_planks");int serverSlot=(int)((JArray)Container["inventorySlots"]).First(m=>(int)m["inventory"]==source)["slot"];
                planksBefore=Count("minecraft:oak_planks");Send(new {type="slotClick",scene,handler="container",slot=serverSlot,button=0});
                foreach(int slot in new[]{1,2,3,4,6,7,8,9}) Send(new {type="slotClick",scene,handler="container",slot,button=1});
                Send(new {type="slotClick",scene,handler="container",slot=serverSlot,button=0});Advance(104);
            }
            else if(stage==104 && Container!=null && (string)Container["slots"]?[0]?["id"]=="minecraft:chest") {
                if(Environment.GetEnvironmentVariable("HOLLOWCRAFT_CAPTURE")=="1") ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(HollowCraftBootstrap.SaveRoot(),"hollowcraft-workbench.png"));
                Send(new {type="slotClick",scene,handler="container",slot=0,button=0,quick=true});Advance(105);
            }
            else if(stage==105 && Count("minecraft:oak_planks")==planksBefore-8 && Count("minecraft:chest")>0) {
                Debug.Log("[HollowCraft SMOKE] PASS 3x3 workbench GUI: eight planks -> chest through real slot clicks");
                runtime.CloseInterface();Send(new {type="mine",scene,x=cell.x,y=cell.y,heldTicks=100});Send(new {type="kit",scene});Advance(1);
            }
            else if(stage==1 && Slot("minecraft:chest")>=0)
            {
                int slot=Slot("minecraft:chest");Send(new {type="swap",scene,from=slot,to=0});Place(0);Advance(2);
            }
            else if(stage==2 && IsBlock("minecraft:chest")) {Send(new {type="use",scene,x=cell.x,y=cell.y});Advance(3);}
            else if(stage==3 && Container!=null && (string)Container["id"]=="minecraft:chest")
            {
                if(((JArray)Container["slots"]).Count!=27) {report(false,"Chest lacks 27 real slots");return;}
                planksBefore=Count("minecraft:oak_planks");Send(new {type="containerMove",scene,direction="deposit",slot=Slot("minecraft:oak_planks")});Advance(4);
            }
            else if(stage==4 && Container!=null && Count("minecraft:oak_planks")<planksBefore)
            {
                var stored=((JArray)Container["slots"]).FirstOrDefault(i=>(string)i["id"]=="minecraft:oak_planks" && (int)i["count"]>0);
                if(stored==null) {report(false,"Deposited planks absent in chest");return;}
                Send(new {type="containerMove",scene,direction="take",slot=(int)stored["slot"]});Advance(5);
            }
            else if(stage==5 && Count("minecraft:oak_planks")==planksBefore)
            {
                Debug.Log("[HollowCraft SMOKE] PASS real chest: deposit and take preserve inventory counts");
                runtime.CloseInterface();Send(new {type="mine",scene,x=cell.x,y=cell.y,heldTicks=100});Place(Slot("minecraft:furnace"));Advance(6);
            }
            else if(stage==6 && IsBlock("minecraft:furnace")) {Send(new {type="use",scene,x=cell.x,y=cell.y});Advance(7);}
            else if(stage==7 && Container!=null && (string)Container["id"]=="minecraft:furnace")
            {
                Send(new {type="containerMove",scene,direction="deposit",slot=Slot("minecraft:coal")});
                Send(new {type="containerMove",scene,direction="deposit",slot=Slot("minecraft:raw_iron")});Advance(8,45);
            }
            else if(stage==8 && Container!=null && ((JArray)Container["slots"]).Any(i=>(string)i["id"]=="minecraft:iron_ingot" && (int)i["count"]>0))
            {
                Debug.Log("[HollowCraft SMOKE] PASS vanilla furnace: fuel and raw iron produce iron ingot");
                if(Environment.GetEnvironmentVariable("HOLLOWCRAFT_CAPTURE")=="1") ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(HollowCraftBootstrap.SaveRoot(),"hollowcraft-furnace.png"));
                runtime.CloseInterface();Send(new {type="mine",scene,x=cell.x,y=cell.y,heldTicks=100});
                int sword=Slot("minecraft:diamond_sword");Send(new {type="swap",scene,from=sword,to=0});Send(new {type="select",scene,slot=0});Advance(9);
            }
            else if(stage==9 && (string)runtime.Combat.Profile?["item"]=="minecraft:diamond_sword")
            {
                if((string)runtime.Combat.Profile["source"]!="Better Combat 1.8.5" || ((JArray)runtime.Combat.Profile["weapon"]["attacks"]).Count!=3) {report(false,"Better Combat sword registry not loaded");return;}
                Debug.Log("[HollowCraft SMOKE] PASS installed Better Combat registry: sword has 3 attacks");
                TestControls();
                targetEnemy=UnityEngine.Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None).FirstOrDefault(e=>!e.isDead && e.GetComponent<Collider2D>()!=null);
                if(targetEnemy==null) {report(false,"Tutorial contains no test enemy");return;}
                targetEnemy.hp=100;enemyBefore=100;
                EnemySystem.Clear();
                foreach(var ai in targetEnemy.GetComponents<MonoBehaviour>()) if(ai!=targetEnemy) ai.enabled=false;
                var enemyBody=targetEnemy.GetComponent<Rigidbody2D>();if(enemyBody!=null) enemyBody.constraints=RigidbodyConstraints2D.FreezeAll;
                foreach(var damage in targetEnemy.GetComponentsInChildren<DamageHero>()) damage.enabled=false;
                targetEnemy.transform.position=hero.transform.position+new Vector3(1.3f,0,0);
                Advance(10);
            }
            else if(stage==10)
            {
                var attack=runtime.Combat.Current;
                if(attack!=null && (int)attack["attack"]>0 && (int)attack["attack"]!=firstAttack)
                {
                    firstAttack=(int)attack["attack"];int combo=(int)attack["combo"];
                    if(combo!=lastCombo+1) {report(false,"Combo sequence skipped");return;}
                    lastCombo=combo;swings++;
                    if(swings==3) {Debug.Log("[HollowCraft SMOKE] PASS server cooldown and 3-hit Better Combat combo");Advance(11);return;}
                }
                if(Time.unscaledTime>=nextRequest && !runtime.Combat.Swinging)
                {runtime.Combat.Request(scene,hero,(Vector2)hero.transform.position+Vector2.right*2);nextRequest=Time.unscaledTime+0.2f;}
            }
            else if(stage==11 && !runtime.Combat.Swinging)
            {
                if(targetEnemy.hp>=enemyBefore) {report(false,"Cursor attack did not damage real Hollow Knight enemy");return;}
                Debug.Log("[HollowCraft SMOKE] PASS cursor combat -> native HealthManager: HP "+enemyBefore+" -> "+targetEnemy.hp);
                targetEnemy.transform.position=hero.transform.position+new Vector3(20,0,0);
                Send(new {type="mode",scene,mode="survival"});
                int armor=Slot("minecraft:iron_chestplate");if(armor<0) {report(false,"Missing armor for survival test");return;}
                Send(new {type="slotClick",scene,handler="player",slot=armor<9 ? armor+36 : armor,button=0,quick=true});Advance(12);
            }
            else if(stage==12 && !runtime.Creative && (int?)runtime.Stats?["armor"]>=6)
            {
                healthBefore=(float)runtime.Stats["health"];
                PlayerSystem.RouteDamage(targetEnemy.gameObject,GlobalEnums.CollisionSide.left,4,0);
                Advance(13);
            }
            else if(stage==13 && (float)runtime.Stats["health"]<healthBefore)
            {
                float loss=healthBefore-(float)runtime.Stats["health"];
                if(Math.Abs(loss-11.424f)>.05f) {report(false,"Minecraft armor formula mismatch: "+loss);return;}
                Debug.Log("[HollowCraft SMOKE] PASS native TakeDamage -> Minecraft health and iron armor: loss "+loss);
                int bread=Slot("minecraft:bread");if(bread<0) {report(false,"No bread to test offhand");return;}
                Send(new {type="select",scene,slot=0});Send(new {type="swap",scene,from=bread,to=0});Send(new {type="offhand",scene});Advance(14);
            }
            else if(stage==14 && (string)runtime.Stats?["offhand"]?["id"]=="minecraft:bread")
            {
                Debug.Log("[HollowCraft SMOKE] PASS real offhand inventory swap");
                Send(new {type="offhand",scene});Advance(15);
            }
            else if(stage==15 && (string)Inventory[0]["id"]=="minecraft:bread")
            {
                if(!additional) {additional=true;firstAttack=(int?)runtime.Combat.Current?["attack"] ?? 0;runtime.Combat.Request(scene,hero,(Vector2)hero.transform.position+Vector2.left*2);}
                Advance(16);
            }
            else if(stage==16 && runtime.Combat.Current!=null && (int)runtime.Combat.Current["attack"]!=firstAttack && !runtime.Combat.Swinging)
            {
                Debug.Log("[HollowCraft SMOKE] PASS empty Better Combat swing in survival");
                terrainTarget=UnityEngine.Object.FindObjectsByType<Breakable>(FindObjectsSortMode.None).FirstOrDefault(b=>b.gameObject.activeInHierarchy && b.GetComponent<Collider2D>()!=null && !(bool)typeof(Breakable).GetField("isBroken",BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public).GetValue(b));
                if(terrainTarget==null) {report(false,"Tutorial has no intact native breakable to verify terrain interaction");return;}
                terrainTarget.transform.position=hero.transform.position+new Vector3(1.2f,0,0);
                resourceCount=ResourceCount();firstAttack=(int)runtime.Combat.Current["attack"];Advance(17);
            }
            else if(stage==17)
            {
                if(Time.unscaledTime>=nextRequest && !runtime.Combat.Swinging) {runtime.Combat.Request(scene,hero,(Vector2)hero.transform.position+Vector2.right*2);nextRequest=Time.unscaledTime+0.5f;}
                if(terrainTarget==null || (bool)typeof(Breakable).GetField("isBroken",BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public).GetValue(terrainTarget)) {
                    Debug.Log("[HollowCraft SMOKE] PASS Better Combat swing breaks native Hollow Knight terrain prop via HitTaker");Advance(171);
                }
            }
            else if(stage==171&&ResourceCount()>resourceCount) {Debug.Log("[HollowCraft SMOKE] PASS native prop death acknowledgment creates real Minecraft survival resources exactly after destruction");Advance(18);}
            else if(stage==18)
            {
                if(exhaustPoses<160 && Time.unscaledTime>=nextRequest) {
                    for(int i=0;i<16;i++) {exhaustPoses++;float x=hero.transform.position.x+(exhaustPoses%2==0 ? 2 : -2);Send(new {type="pose",scene,x=Mathf.FloorToInt(x),y=Mathf.FloorToInt(hero.transform.position.y),exactX=x,exactY=hero.transform.position.y,sprinting=true,grounded=true});}
                    nextRequest=Time.unscaledTime+0.12f;
                }
                if((int)runtime.Stats["food"]<20) {
                    Debug.Log("[HollowCraft SMOKE] PASS sprint pose exhaustion lowers Minecraft hunger");foodBefore=(int)runtime.Stats["food"];breadBefore=Count("minecraft:bread");Send(new {type="eat",scene});Advance(19);
                }
            }
            else if(stage==19 && Count("minecraft:bread")==breadBefore-1 && (int)runtime.Stats["food"]>foodBefore)
            {
                Debug.Log("[HollowCraft SMOKE] PASS food consumption restores hunger and consumes real bread");
                Send(new {type="pose",scene,x=Mathf.FloorToInt(hero.transform.position.x),y=Mathf.FloorToInt(hero.transform.position.y),exactX=hero.transform.position.x,exactY=hero.GetComponent<Collider2D>().bounds.min.y,centerY=hero.transform.position.y,grounded=hero.cState.onGround,sprinting=false});
                Send(new {type="damage",scene,amount=40});Advance(20);
            }
            else if(stage==20 && runtime.Dead)
            {
                Debug.Log("[HollowCraft SMOKE] PASS Minecraft death replaces Knight masks/death");Send(new {type="respawn",scene});Advance(21);
            }
            else if(stage==21 && !runtime.Dead && (float)runtime.Stats["health"]==20)
            {
                Debug.Log("[HollowCraft SMOKE] PASS Minecraft server respawn restores health and native movement");
                Advance(22,30);
            }
            else if(stage==22&&pointerSmoke==null&&Count("minecraft:bread")==0) {
                // Death throws vanilla stacks sideways. Walk to the bread instead of
                // requiring one randomly scattered stack to land at the respawn point.
                var bread=((JArray)runtime.LatestSnapshot["drops"]).FirstOrDefault(d=>(string)d["id"]=="minecraft:bread");
                if(bread!=null&&MinecraftControls.Grounded(hero)) {var point=hero.transform.position;point.x=Mathf.MoveTowards(point.x,(float)bread["x"],4.317f*Time.deltaTime);hero.transform.position=point;}
            }
            else if(stage==22 && Count("minecraft:bread")>0)
            {
                if(pointerSmoke==null) {Debug.Log("[HollowCraft SMOKE] PASS death drops remain in 2D scene and are picked up after respawn");pointerSmoke=new PointerSmoke(runtime,hero,scene,report);deadline=Time.unscaledTime+90;}
                pointerSmoke.Tick();
            }
        }
        private PointerSmoke pointerSmoke;
        private void TestControls()
        {
            Debug.Log("[HollowCraft SMOKE] Ground colliders: "+string.Join(", ",Physics2D.RaycastAll(hero.transform.position,Vector2.down,4).Where(h=>h.collider!=null && h.collider.GetComponentInParent<HeroController>()==null).Select(h=>h.collider.name+" layer="+h.collider.gameObject.layer)));
            typeof(InputHandler).GetMethod("SetCursorEnabled",BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic).Invoke(InputHandler.Instance,new object[]{false});
            if(Cursor.lockState!=CursorLockMode.None) throw new Exception("Game input relocked cursor");
            Vector2 origin=hero.transform.position;
            Vector2 right=GameCursor.ClampWorld(origin+Vector2.right*20,origin),left=GameCursor.ClampWorld(origin-Vector2.right*20,origin);
            float expected=runtime.Creative?6:4.5f;
            if(Math.Abs(Vector2.Distance(origin,right)-expected)>0.001f || right.x<=left.x) throw new Exception("Vanilla mode reach free cursor clamp failed");
            Debug.Log("[HollowCraft SMOKE] PASS native cursor unlock and vanilla mode reach radius");
            var actions=InputHandler.Instance.inputActions;
            if(!actions.left.HasBinding(new InControl.KeyBindingSource(InControl.Key.A)) || !actions.right.HasBinding(new InControl.KeyBindingSource(InControl.Key.D)) || !actions.jump.HasBinding(new InControl.KeyBindingSource(InControl.Key.Space))) throw new Exception("Minecraft input remap missing");
            var body=hero.GetComponent<BoxCollider2D>();if(body==null) throw new Exception("Expected hero box collider");
            float feet=body.bounds.min.y,height=body.size.y;
            if(Math.Abs(body.bounds.size.x-.6f)>.01f||Math.Abs(body.bounds.size.y-1.8f)>.01f) throw new Exception("Standing hitbox must match vanilla Steve 0.6 x 1.8 blocks");
            var method=typeof(MinecraftControls).GetMethod("SetCrouch",BindingFlags.NonPublic|BindingFlags.Static);
            method.Invoke(null,new object[]{true});
            if(body.size.y>=height || Math.Abs(body.bounds.min.y-feet)>0.02f) throw new Exception("Crouch must shrink body while retaining feet");
            if(Math.Abs(body.bounds.size.y-1.5f)>.01f) throw new Exception("Crouching hitbox must match vanilla 1.5 blocks");
            method.Invoke(null,new object[]{false});
            if(Math.Abs(body.size.y-height)>0.02f) throw new Exception("Standing collision was not restored");
            Debug.Log("[HollowCraft SMOKE] PASS A/D/Space bindings and crouch collider keeps feet fixed");
        }
    }
}
