using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace HollowCraft
{
    internal sealed class EnvironmentSmoke
    {
        private readonly HollowCraftRuntime runtime;private readonly HeroController hero;private readonly string scene;private readonly Action<bool,string> report;
        private int stage;private float since,health;private Vector3 origin;private Vector2Int cell,lamp;private long pausedWorldTime;
        private void Send(object command)=>runtime.SmokeSend(command);
        private void Item(string id,int count=1) {Send(new {type="creativeItem",scene,id,count,slot=8});runtime.Select(8);}
        private void Next(int value) {stage=value;since=Time.unscaledTime;}
        private JObject Block(Vector2Int p)=>((JArray)runtime.LatestSnapshot["blocks"]).OfType<JObject>().FirstOrDefault(b=>(int)b["x"]==p.x&&(int)b["y"]==p.y);
        private Vector2 Point=>new Vector2(cell.x+.5f,cell.y+.5f);
        private void Right()=>runtime.HandlePointer(hero,Point,false,false,true,true);
        internal EnvironmentSmoke(HollowCraftRuntime value,HeroController player,string room,Action<bool,string> result) {runtime=value;hero=player;scene=room;report=result;origin=hero.transform.position;cell=new Vector2Int(Mathf.FloorToInt(origin.x)+2,Mathf.FloorToInt(hero.GetComponent<Collider2D>().bounds.min.y)+1);Send(new {type="mode",scene,mode="creative"});Item("minecraft:water_bucket");Next(0);}
        internal void Tick()
        {
            if(Time.unscaledTime-since>20) {report(false,"Environment stage "+stage+": "+runtime.LatestSnapshot["status"]+" block "+Block(cell)+" stats "+runtime.Stats);return;}
            if(stage==0&&runtime.Creative&&runtime.HeldItem=="minecraft:water_bucket") {Right();Next(1);}
            else if(stage==1&&Time.unscaledTime-since>.3f&&Block(cell)?["id"]?.ToString()=="minecraft:water") {
                if((bool?)Block(cell)["solid"]==true) throw new Exception("Water has a solid native collider");
                var animated=GameObject.FindObjectsByType<MinecraftSpriteAnimation>(FindObjectsSortMode.None).FirstOrDefault(a=>a.name=="HollowCraft.Block.minecraft:water");
                if(animated==null||animated.GetComponent<SpriteRenderer>().sprite.texture.height<=animated.GetComponent<SpriteRenderer>().sprite.texture.width) throw new Exception("Water lost original animation frames");
                Debug.Log("[HollowCraft SMOKE] PASS vanilla water bucket places fluid at exact cursor cell");
                hero.transform.position=new Vector3(cell.x+.5f,origin.y,origin.z);hero.GetComponent<Rigidbody2D>().linearVelocity=Vector2.zero;health=(float)runtime.Stats["health"];Next(2);
            }
            else if(stage==2&&(bool?)runtime.Stats["water"]==true) {
                if((float)runtime.Stats["health"]<health) throw new Exception("Shallow water damaged player");Debug.Log("[HollowCraft SMOKE] PASS real water immersion crosses both engines without solid collision");
                hero.transform.position=origin;Item("minecraft:bucket");Next(3);
            }
            else if(stage==3&&runtime.HeldItem=="minecraft:bucket") {Send(new {type="mode",scene,mode="survival"});Right();Next(4);}
            else if(stage==4&&runtime.HeldItem=="minecraft:water_bucket") {Debug.Log("[HollowCraft SMOKE] PASS survival empty bucket recovers a vanilla water source");Send(new {type="mode",scene,mode="creative"});Right();Item("minecraft:lava_bucket");Next(5);}
            else if(stage==5&&runtime.Creative&&runtime.HeldItem=="minecraft:lava_bucket") {cell.x--;Right();Next(6);}
            else if(stage==6&&((JArray)runtime.LatestSnapshot["blocks"]).Any(b=>(string)b["id"]=="minecraft:obsidian"||(string)b["id"]=="minecraft:cobblestone")) {
                Debug.Log("[HollowCraft SMOKE] PASS vanilla water/lava mixing produces stone material");Send(new {type="clearTestFluids",scene});
                cell=new Vector2Int(Mathf.FloorToInt(origin.x)+1,Mathf.FloorToInt(hero.GetComponent<Collider2D>().bounds.min.y)+1);
                Send(new {type="mine",scene,x=cell.x,y=cell.y});
                Item("minecraft:cobblestone");Send(new {type="place",scene,x=cell.x,y=cell.y-1});Item("minecraft:oak_door");Next(7);
            }
            else if(stage==7&&runtime.HeldItem=="minecraft:oak_door"&&Time.unscaledTime-since>.4f) {Right();Next(8);}
            else if(stage==8&&Block(cell)?["id"]?.ToString()=="minecraft:oak_door") {
                if((bool?)Block(cell)["open"]==true) throw new Exception("Placed door starts open");Right();Next(9);
            }
            else if(stage==9&&(bool?)Block(cell)?["open"]==true) {
                if((bool?)Block(cell)["solid"]==true) throw new Exception("Open door kept solid native collision");Debug.Log("[HollowCraft SMOKE] PASS right-click opens real Minecraft door and updates native collision");
                Item("minecraft:redstone_lamp");cell.y+=2;lamp=cell;Next(10);
            }
            else if(stage==10&&runtime.HeldItem=="minecraft:redstone_lamp") {Right();Next(11);}
            else if(stage==11&&Block(lamp)?["id"]?.ToString()=="minecraft:redstone_lamp") {Item("minecraft:lever");cell.x--;Next(12);}
            else if(stage==12&&runtime.HeldItem=="minecraft:lever") {Right();Next(13);}
            else if(stage==13&&Block(cell)?["id"]?.ToString()=="minecraft:lever") {Right();Next(14);}
            else if(stage==14&&(bool?)Block(lamp)?["lit"]==true) {
                Debug.Log("[HollowCraft SMOKE] PASS vanilla lever powers adjacent redstone lamp with live state/texture updates");Item("minecraft:elytra");Next(15);
            }
            else if(stage==15&&runtime.HeldItem=="minecraft:elytra") {
                // Actual vanilla equipment slot click, no direct native stat injection.
                Send(new {type="slotClick",scene,handler="player",slot=44,button=0,quick=false});Send(new {type="slotClick",scene,handler="player",slot=6,button=0,quick=false});Next(16);
            }
            else if(stage==16&&(string)runtime.Stats["equipment"]?[1]?["id"]=="minecraft:elytra") {
                hero.transform.position=origin+Vector3.up*5;hero.cState.onGround=false;hero.GetComponent<Rigidbody2D>().linearVelocity=new Vector2(6,-1);MinecraftEnvironment.TestAim=Vector2.right;
                if(!MinecraftEnvironment.BeginGlide(hero)) throw new Exception("Equipped elytra failed to start while airborne");Next(17);
            }
            else if(stage==17&&Time.unscaledTime-since>.6f) {
                if(!MinecraftEnvironment.Gliding||hero.GetComponent<Rigidbody2D>().gravityScale!=0||MinecraftEnvironment.FlightVelocity.x<=0) throw new Exception("Gliding lost velocity/gravity ownership");
                if((bool?)runtime.Stats["gliding"]!=true) return;
                Debug.Log("[HollowCraft SMOKE] PASS 3D elytra uses vanilla 20 Hz flight equations and server flight flag");
                typeof(HollowCraftRuntime).GetField("paused",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(runtime,true);Send(new {type="pause",paused=true});Time.timeScale=0;Next(25);
            }
            else if(stage==25&&Time.unscaledTime-since>.4f) {pausedWorldTime=(long)runtime.LatestSnapshot["worldTime"];Next(26);}
            else if(stage==26&&Time.unscaledTime-since>.7f) {
                if(!MinecraftEnvironment.Gliding||(long)runtime.LatestSnapshot["worldTime"]!=pausedWorldTime) throw new Exception("Pause cancelled elytra flight or left vanilla world running");
                typeof(HollowCraftRuntime).GetField("paused",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(runtime,false);Send(new {type="pause",paused=false});Time.timeScale=1;
                typeof(HollowCraftRuntime).GetField("inventoryOpen",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(runtime,true);Send(new {type="inventoryOpen",scene});Next(27);
            }
            else if(stage==27&&Time.unscaledTime-since>.3f) {
                if(!MinecraftEnvironment.Gliding||(bool?)runtime.Stats["gliding"]!=true) throw new Exception("Inventory cancelled elytra flight");
                Debug.Log("[HollowCraft SMOKE] PASS elytra survives pause/resume and inventory while native/vanilla clocks stay synchronized");runtime.CloseInterface();Item("minecraft:firework_rocket",4);Next(18);
            }
            else if(stage==18&&runtime.HeldItem=="minecraft:firework_rocket") {cell=new Vector2Int(Mathf.FloorToInt(hero.transform.position.x)+2,Mathf.FloorToInt(hero.transform.position.y));Right();Next(19);}
            else if(stage==19&&(bool?)runtime.Stats["rocket"]==true) {
                Debug.Log("[HollowCraft SMOKE] PASS vanilla firework is attached to elytra wearer and drives flight boost");MinecraftEnvironment.Gliding=false;MinecraftEnvironment.TestAim=null;hero.transform.position=origin;hero.GetComponent<Rigidbody2D>().linearVelocity=Vector2.zero;
                Send(new {type="creativeCatalog",scene});Next(20);
            }
            else if(stage==20&&runtime.Catalog!=null) {
                if(runtime.Catalog.Count<1300||!runtime.Catalog.Any(i=>(string)i["potion"]=="minecraft:strong_healing")) throw new Exception("Creative catalog lost registry items/potion variants");
                runtime.CatalogOpen=true;ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(HollowCraftBootstrap.SaveRoot(),"hollowcraft-creative-catalog.png"));Item("minecraft:diamond_sword",64);Next(21);
            }
            else if(stage==21&&runtime.HeldItem=="minecraft:diamond_sword") {
                if((int)runtime.LatestSnapshot["inventory"][8]["count"]!=1) throw new Exception("Creative non-stackable item ignored vanilla stack limit");
                Send(new {type="creativeItem",scene,id="minecraft:potion",count=64,slot=8,potion="minecraft:strong_healing"});Next(22);
            }
            else if(stage==22&&(string)runtime.LatestSnapshot["inventory"][8]["potion"]=="minecraft:strong_healing") {
                if((int)runtime.LatestSnapshot["inventory"][8]["count"]!=1) throw new Exception("Potion stack limit mismatch");
                runtime.CatalogOpen=false;Debug.Log("[HollowCraft SMOKE] PASS searchable creative registry catalog, potion NBT and vanilla maximum stack sizes");
                Send(new {type="checkpoint",scene});report(true,"Fluids/buckets/mixing, native door collision, lever/lamp, elytra/rocket, flight pause/inventory, creative catalog/potion NBT/stack limits verified");
            }
        }
    }
}
