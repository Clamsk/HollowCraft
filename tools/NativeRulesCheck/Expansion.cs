using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using HollowCraft.Standalone;

internal static partial class Program
{
    private static void NativeExpansion(string root,string assets) {
        using(var game=new NativeGame(Path.Combine(root,"native-expansion"),assets)) {
            const string scene="expansion";string campaign="gift";Send(game,scene,campaign,"scene","exactX",0,"exactY",1);Latest(game);
            var floor=new JArray();for(int i=-5;i<=8;i++)floor.Add(new JArray(i,0,0,0,1,1));
            Send(game,scene,campaign,"chat","text","/cLaMsK");var state=Latest(game);
            Check(game.Inventory.Slots.Any(i=>i.Id=="hollowcraft_bridge:zenith")&&game.Inventory.Slots.Any(i=>i.Id=="hollowcraft_bridge:stardust_wings"),"Clamsk command grants both native easter-egg items");
            game.Inventory.Slots[0]=NativeStack.New("hollowcraft_bridge:stardust_wings");Send(game,scene,campaign,"itemUse","start",true);state=Latest(game);
            Check((string)state["stats"]["equipment"][1]["id"]=="hollowcraft_bridge:stardust_wings","Stardust Wings equip into chest armor slot through normal item use");
            game.Inventory.Slots[0]=NativeStack.New("hollowcraft_bridge:zenith");
            var boss=new JObject{["id"]=7001,["generation"]=1,["health"]=1000,["maxHealth"]=1000,["phaseFloor"]=500,["nativeHurtTicks"]=4,["x"]=1,["y"]=1,["width"]=1,["height"]=2};
            Send(game,scene,campaign,"pose","exactX",0,"exactY",1,"enemies",new JArray(boss));Latest(game);
            Send(game,scene,campaign,"zenithUse","held",true,"x",1,"y",2);Latest(game);for(int i=0;i<30;i++)game.Pump(.05);state=Latest(game);
            Check((double)state["enemies"][0]["health"]>=500&&(double)state["enemies"][0]["health"]<1000,"Zenith deals damage while preserving the next native boss phase floor");

            campaign="models";Send(game,scene,campaign,"scene","exactX",0,"exactY",1);Latest(game);Send(game,scene,campaign,"mode","mode","creative");Latest(game);
            Check(NativeMobCatalog.Types.Count==75,"Native model catalog covers 75 spawnable species");int faces=0;
            foreach(var spec in NativeMobCatalog.Types.Values) {
                ValidateModel(spec.Model,ref faces);Check(File.Exists(Path.Combine(assets,spec.Texture+".png")),"Native model texture resolves: "+spec.Id);
                foreach(JObject layer in spec.Layers)Check(File.Exists(Path.Combine(assets,(string)layer["texture"]+".png")),"Native overlay texture resolves: "+spec.Id);
                var egg=NativeCatalog.Items.Values.FirstOrDefault(i=>i.Entity==spec.Id);Check(egg!=null,"Every enabled creature has a real spawn egg: "+spec.Id);
                game.Inventory.Slots[0]=NativeStack.New(egg.Id);Send(game,scene,campaign,"itemUse","start",true,"x",2,"y",1,"aimX",1);state=Latest(game);
                Check(((JArray)state["creatures"]).Any(m=>(string)m["kind"]==spec.Id&&m["uuid"]!=null&&(double)m["health"]>0),"Creative egg spawns live local entity: "+spec.Id);
            }
            Check(faces>2500,"Model descriptions contain articulated geometry rather than placeholder boxes");

            campaign="zenith-mobs";Send(game,scene,campaign,"scene","exactX",0,"exactY",1);Latest(game);Send(game,scene,campaign,"mode","mode","creative");Latest(game);
            Send(game,scene,campaign,"pose","exactX",0,"exactY",1,"terrain",floor);Latest(game);
            game.Inventory.Slots[0]=NativeStack.New("zombie_spawn_egg");Send(game,scene,campaign,"itemUse","start",true,"x",1,"y",1);state=Latest(game);int mobId=(int)state["creatures"][0]["entity"];
            game.Inventory.Slots[0]=NativeStack.New("hollowcraft_bridge:zenith");Send(game,scene,campaign,"zenithUse","held",true,"x",1.5,"y",2);Latest(game);for(int i=0;i<25;i++)game.Pump(.05);state=Latest(game);
            Check(!((JArray)state["creatures"]).Any(v=>(int)v["entity"]==mobId&&(double)v["health"]>0),"Zenith swept blades hit Minecraft mobs as well as HK enemy adapters");

            campaign="archer";Send(game,scene,campaign,"scene","exactX",0,"exactY",1);Latest(game);Send(game,scene,campaign,"mode","mode","creative");Latest(game);
            Send(game,scene,campaign,"pose","exactX",0,"exactY",1,"terrain",floor);Latest(game);
            game.Inventory.Slots[0]=NativeStack.New("skeleton_spawn_egg");Send(game,scene,campaign,"itemUse","start",true,"x",4,"y",1);Latest(game);Send(game,scene,campaign,"mode","mode","survival");state=Latest(game);
            var shot=((JArray)state["projectiles"]).FirstOrDefault(v=>(bool?)v["hostile"]==true);Check(shot!=null,"Skeleton AI fires a visible native projectile");
            int shotId=(int)shot["entity"];for(int i=0;i<12&&shot!=null;i++){Send(game,scene,campaign,"projectileStep","steps",new JArray(new JObject{["entity"]=shotId,["uuid"]=shot["uuid"],["age"]=shot["age"]}));state=Latest(game);shot=((JArray)state["projectiles"]).FirstOrDefault(v=>(int)v["entity"]==shotId);}
            Check((double)state["stats"]["health"]<20&&shot==null,"Hostile arrow swept segment hits the player exactly once");

            campaign="cloud";Send(game,scene,campaign,"scene","exactX",10,"exactY",1);Latest(game);
            var victim=new JObject{["id"]=7101,["generation"]=1,["health"]=100,["maxHealth"]=100,["x"]=0,["y"]=1,["width"]=.6,["height"]=1.8};
            Send(game,scene,campaign,"pose","exactX",10,"exactY",1,"enemies",new JArray(victim));Latest(game);game.Inventory.Slots[0]=NativeStack.New("lingering_potion");game.Inventory.Slots[0].Data["Potion"]="minecraft:strong_harming";
            Send(game,scene,campaign,"itemUse","start",true,"x",10,"y",1,"aimX",-1);state=Latest(game);shot=state["projectiles"][0];Send(game,scene,campaign,"nativeProjectileHit","entity",(int)shot["entity"],"uuid",(string)shot["uuid"],"request",1,"x",0,"y",1);state=Latest(game);
            Check(((JArray)state["clouds"]).Count==1&&(double)state["enemies"][0]["health"]==100,"Lingering potion creates a persistent cloud instead of immediate splash damage");
            for(int i=0;i<9;i++)game.Pump(.05);state=Latest(game);Check((double)state["enemies"][0]["health"]==94,"Cloud waits ten ticks then applies half-strength instant damage");
            for(int i=0;i<5;i++)game.Pump(.05);state=Latest(game);Check((double)state["enemies"][0]["health"]==94&&(double)state["clouds"][0]["radius"]<2.5,"Cloud respects per-target cooldown and radius-on-use");
            Send(game,"away",campaign,"scene","exactX",10,"exactY",1);Latest(game);Send(game,scene,campaign,"scene","exactX",10,"exactY",1);state=Latest(game);Check(((JArray)state["clouds"]).Count==1,"Room changes preserve lingering clouds");

            campaign="milk";Send(game,scene,campaign,"scene","exactX",0,"exactY",1);Latest(game);Send(game,scene,campaign,"mode","mode","creative");Latest(game);
            Send(game,scene,campaign,"pose","exactX",0,"exactY",1,"terrain",floor);Latest(game);game.Inventory.Slots[0]=NativeStack.New("cow_spawn_egg");Send(game,scene,campaign,"itemUse","start",true,"x",1,"y",1);Latest(game);Send(game,scene,campaign,"mode","mode","survival");Latest(game);
            game.Inventory.Slots[0]=NativeStack.New("bucket");Send(game,scene,campaign,"interact","x",1,"y",1,"canPlace",false);state=Latest(game);Check(game.Inventory.Hand.Id=="minecraft:milk_bucket","Right-clicking a cow fills a milk bucket using the normal interaction path");

            campaign="breeding";Send(game,scene,campaign,"scene","exactX",0,"exactY",1);Latest(game);Send(game,scene,campaign,"mode","mode","creative");Latest(game);Send(game,scene,campaign,"pose","exactX",0,"exactY",1,"terrain",floor);Latest(game);
            game.Inventory.Slots[0]=NativeStack.New("cow_spawn_egg");Send(game,scene,campaign,"itemUse","start",true,"x",1,"y",1);Latest(game);Send(game,scene,campaign,"itemUse","start",true,"x",3,"y",1);Latest(game);
            game.Inventory.Slots[0]=NativeStack.New("wheat",64);Send(game,scene,campaign,"interact","x",1,"y",1,"canPlace",false);Latest(game);Send(game,scene,campaign,"interact","x",3,"y",1,"canPlace",false);state=Latest(game);
            Check(((JArray)state["creatures"]).Count==3&&((JArray)state["creatures"]).Any(m=>(bool?)m["baby"]==true&&(double)m["height"]<1),"Feeding nearby adults creates a smaller baby with a growth timer");

            campaign="shearing";Send(game,scene,campaign,"scene","exactX",0,"exactY",1);Latest(game);Send(game,scene,campaign,"mode","mode","creative");Latest(game);Send(game,scene,campaign,"pose","terrain",floor);Latest(game);
            game.Inventory.Slots[0]=NativeStack.New("sheep_spawn_egg");Send(game,scene,campaign,"itemUse","start",true,"x",2,"y",1);Latest(game);Send(game,scene,campaign,"mode","mode","survival");Latest(game);game.Inventory.Slots[0]=NativeStack.New("shears");Send(game,scene,campaign,"interact","x",2,"y",1,"canPlace",false);state=Latest(game);
            Check((bool)state["creatures"][0]["sheared"]&&(int)state["inventory"][0]["damage"]==1&&((JArray)state["drops"]).Any(d=>(string)d["id"]=="minecraft:white_wool"),"Shearing removes the wool layer, drops wool and consumes durability");

            campaign="blast-resistance";Send(game,scene,campaign,"scene","exactX",0,"exactY",1);Latest(game);Send(game,scene,campaign,"mode","mode","creative");Latest(game);
            game.Inventory.Slots[0]=NativeStack.New("obsidian");Send(game,scene,campaign,"place","x",2,"y",1);Latest(game);game.Inventory.Slots[0]=NativeStack.New("bedrock");Send(game,scene,campaign,"place","x",3,"y",1);Latest(game);game.Inventory.Slots[0]=NativeStack.New("tnt");Send(game,scene,campaign,"place","x",1,"y",1);Latest(game);game.Inventory.Slots[0]=NativeStack.New("flint_and_steel");Send(game,scene,campaign,"interact","x",1,"y",1);Latest(game);
            for(int i=0;i<80;i++)game.Pump(.05);state=Latest(game);Check(((JArray)state["primed"]).Count==0&&((JArray)state["blocks"]).Any(b=>(string)b["id"]=="minecraft:obsidian")&&((JArray)state["blocks"]).Any(b=>(string)b["id"]=="minecraft:bedrock"),"TNT ray resistance preserves obsidian and bedrock");

            campaign="undead-cloud";Send(game,scene,campaign,"scene","exactX",10,"exactY",1);Latest(game);
            var undead=new JObject{["id"]=7102,["generation"]=1,["kind"]="undead",["health"]=12,["maxHealth"]=20,["x"]=0,["y"]=1,["width"]=.6,["height"]=1.8};
            Send(game,scene,campaign,"pose","exactX",10,"exactY",1,"enemies",new JArray(undead));Latest(game);game.Inventory.Slots[0]=NativeStack.New("lingering_potion");game.Inventory.Slots[0].Data["Potion"]="minecraft:strong_harming";Send(game,scene,campaign,"itemUse","start",true,"x",10,"y",1,"aimX",-1);state=Latest(game);shot=state["projectiles"][0];Send(game,scene,campaign,"nativeProjectileHit","entity",(int)shot["entity"],"uuid",(string)shot["uuid"],"request",1,"x",0,"y",1);Latest(game);
            for(int i=0;i<9;i++)game.Pump(.05);state=Latest(game);Check((double)state["enemies"][0]["health"]==16,"Instant damage heals undead with the original half-strength cloud potency");

            var lootRandom=new Random(713);var counts=new int[4];var pig=new JObject{["kind"]="minecraft:pig"};for(int i=0;i<3000;i++){var loot=NativeLoot.Roll(pig,0,true,lootRandom);foreach(var item in loot){if(item.Id!="minecraft:porkchop"||item.Count<1||item.Count>3)throw new Exception("Invalid pig loot");counts[item.Count]++;}}
            Check(counts.Skip(1).All(n=>n>800&&n<1200),"Vanilla integer uniform loot counts remain uniformly distributed");
            pig["fireTicks"]=200;Check(NativeLoot.Roll(pig,3,true,lootRandom).All(i=>i.Id=="minecraft:cooked_porkchop"&&i.Count>=1&&i.Count<=6),"On-fire loot smelts food and applies looting without invented drops");
            var sheep=new JObject{["kind"]="minecraft:sheep",["sheared"]=false,["woolColorId"]=0};Check(NativeLoot.Roll(sheep,0,true,lootRandom).Any(i=>i.Id=="minecraft:white_wool"),"Vanilla sheep subtable resolves its unsheared color NBT predicate");sheep["sheared"]=true;Check(NativeLoot.Roll(sheep,0,true,lootRandom).All(i=>!i.Id.EndsWith("_wool",StringComparison.Ordinal)),"Sheared sheep cannot duplicate wool through their death loot");

            campaign="container-blast";Send(game,scene,campaign,"scene","exactX",0,"exactY",1);Latest(game);Send(game,scene,campaign,"mode","mode","creative");Latest(game);
            game.Inventory.Slots[0]=NativeStack.New("tnt");Send(game,scene,campaign,"place","x",1,"y",1);Latest(game);game.Inventory.Slots[0]=NativeStack.New("flint_and_steel");Send(game,scene,campaign,"interact","x",1,"y",1);Latest(game);
            game.Inventory.Slots[0]=NativeStack.New("crafting_table");Send(game,scene,campaign,"place","x",2,"y",1);Latest(game);Send(game,scene,campaign,"interact","x",2,"y",1);state=Latest(game);int sync=(int)state["container"]["syncId"];
            game.Inventory.Slots[9]=NativeStack.New("coal",16);Send(game,scene,campaign,"slotClick","handler","container","syncId",sync,"slot",10,"button",0,"action","PICKUP");Latest(game);Send(game,scene,campaign,"slotClick","handler","container","syncId",sync,"slot",1,"button",0,"action","PICKUP");Latest(game);
            for(int i=0;i<80;i++)game.Pump(.05);state=Latest(game);int coal=game.Inventory.Slots.Where(i=>i.Id=="minecraft:coal").Sum(i=>i.Count)+((JArray)state["drops"]).Where(d=>(string)d["id"]=="minecraft:coal").Sum(d=>(int)d["count"]);
            Check(state["container"]==null&&coal==16,"Exploding an open crafting table closes it without refund/drop duplication");
        }
    }
    private static void ValidateModel(JObject node,ref int faces) {
        foreach(JArray face in node["faces"] as JArray??new JArray()){if(face.Count!=4)throw new Exception("Malformed original model face");foreach(JArray vertex in face)foreach(var number in vertex){double value=(double)number;if(double.IsNaN(value)||double.IsInfinity(value))throw new Exception("Non-finite model vertex");}faces++;}
        foreach(var child in node["children"] as JObject??new JObject())ValidateModel((JObject)child.Value,ref faces);
    }
}
