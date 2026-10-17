using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using HollowCraft.Standalone;

internal static class Program
{
    private static int checks;
    private static void Check(bool condition,string reason) {if(!condition)throw new Exception(reason);checks++;}
    private static void Send(NativeGame game,string scene,string campaign,string type,params object[] fields) {var p=new JObject{["type"]=type,["scene"]=scene,["campaign"]=campaign,["visit"]=1};for(int i=0;i<fields.Length;i+=2)p[(string)fields[i]]=fields[i+1]==null?JValue.CreateNull():JToken.FromObject(fields[i+1]);game.Enqueue(p);game.Pump(.05);}
    private static JObject Latest(NativeGame game) {JObject snapshot=null;while(game.TryRead(out var message))if((string)message["type"]=="snapshot")snapshot=message;return snapshot??throw new Exception("No native snapshot");}
    private static List<JObject> Drain(NativeGame game){var result=new List<JObject>();while(game.TryRead(out var value))result.Add(value);return result;}
    private static void Main(string[] args) {
        string root=Path.GetFullPath(args.Length>0?args[0]:"artifacts/native-rule-checks/"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        using(var game=new NativeGame(root)) {
            Send(game,"room-a","save-a","scene","exactX",0,"exactY",2);var state=Latest(game);
            Check((string)state["backend"]=="native-csharp","Native backend identity");Check((string)state["inventory"][0]["id"]=="minecraft:iron_sword","Starter sword");Check((int)state["stats"]["armor"]==15,"Iron armor points");
            Send(game,"room-a","save-a","select","slot",3);Send(game,"room-a","save-a","place","x",2,"y",2);state=Latest(game);Check(((JArray)state["blocks"]).Count==1,"Place block locally");Check((int)state["inventory"][3]["count"]==63,"One block consumed");
            Send(game,"room-a","save-a","towerPlace","x",3,"y",2,"request","same-request");Latest(game);Send(game,"room-a","save-a","towerPlace","x",3,"y",2,"request","same-request");state=Latest(game);Check((int)state["inventory"][3]["count"]==62,"Tower retry does not duplicate consumption");
            Send(game,"room-a","save-a","mode","mode","creative");Send(game,"room-a","save-a","mining","x",2,"y",2,"active",true);state=Latest(game);Check(((JArray)state["blocks"]).Count==1,"Creative mining completes");
            Send(game,"room-a","save-a","select","slot",2);Send(game,"room-a","save-a","mode","mode","survival");Send(game,"room-a","save-a","itemUse","start",true);for(int i=0;i<33;i++)game.Pump(.05);state=Latest(game);Check((float)state["stats"]["absorption"]==4,"Golden apple absorption");Check((int)state["inventory"][2]["count"]==63,"Food consumption once");
            Send(game,"room-a","save-a","slotClick","handler","player","syncId",0,"slot",39,"button",1,"action","PICKUP");state=Latest(game);int before=(int)state["inventory"][3]["count"]+(int)state["carried"]["count"];Check(before==62,"Split stack conserves count");
            Send(game,"room-a","save-a","slotClick","handler","player","syncId",0,"slot",39,"button",0,"action","PICKUP");state=Latest(game);Check((int)state["inventory"][3]["count"]==62&&(int)state["carried"]["count"]==0,"Recombine split stack");
            Send(game,"room-b","save-a","scene","exactX",0,"exactY",2);state=Latest(game);Check(((JArray)state["blocks"]).Count==0,"Room isolation");Check((int)state["inventory"][3]["count"]==62,"Inventory survives room change");
            Send(game,"room-a","save-a","scene","exactX",0,"exactY",2);state=Latest(game);Check(((JArray)state["blocks"]).Count==1,"Room blocks restored");Send(game,"room-a","save-a","checkpoint");Latest(game);
            Send(game,"room-a","save-b","scene","exactX",0,"exactY",2);state=Latest(game);Check((int)state["inventory"][3]["count"]==64&&(int)state["inventory"][2]["count"]==64,"New save resets starter supplies");
            Send(game,"room-a","save-a","scene","exactX",0,"exactY",2);state=Latest(game);Check((int)state["inventory"][3]["count"]==62&&(int)state["inventory"][2]["count"]==63,"Save switching preserves inventory");
            var grid=NativeInventory.Empty(4);grid[0]=NativeStack.New("oak_log");Check(NativeRecipes.Result(grid,2).Count==4,"Log recipe");grid[0]=NativeStack.New("oak_planks");grid[1]=NativeStack.New("spruce_planks");grid[2]=NativeStack.New("birch_planks");grid[3]=NativeStack.New("oak_planks");Check(NativeRecipes.Result(grid,2).Id=="minecraft:crafting_table","Mixed plank recipe");
            Check(!NativeCatalog.Get("bedrock").Solid==false&&NativeCatalog.Get("bedrock").Hardness<0,"Bedrock definition");
        }
        using(var reloaded=new NativeGame(root)){Send(reloaded,"room-a","save-a","scene","exactX",0,"exactY",2);var state=Latest(reloaded);Check((int)state["inventory"][3]["count"]==62,"Native disk reload");Check(((JArray)state["blocks"]).Count==1,"Native world disk reload");}
        Additional(root);
        Console.WriteLine("Native rule checks: "+checks+" passed. No Unity, Minecraft, Java, sockets or gameplay launched.");
    }
    private static void Additional(string root) {
        using(var game=new NativeGame(Path.Combine(root,"rules"))) {
            Send(game,"room","combat","scene","exactX",0,"exactY",2);Latest(game);
            var enemy=new JObject{["id"]=42,["actor"]=42,["generation"]=1,["health"]=18,["maxHealth"]=18,["x"]=1,["y"]=2,["width"]=.6,["height"]=1.8};
            Send(game,"room","combat","pose","exactX",0,"exactY",2,"grounded",true,"enemies",new JArray(enemy));Latest(game);
            Send(game,"room","combat","swing","request",10,"aimX",1);var attack=Drain(game).Single(v=>(string)v["type"]=="attack");
            Send(game,"room","combat","hit","attack",(int)attack["attack"],"targets",new[]{42});var state=Latest(game);Check((double)state["enemies"][0]["health"]==12,"Iron sword deals six damage");
            Send(game,"room","combat","hit","attack",(int)attack["attack"],"targets",new[]{42});state=Latest(game);Check((double)state["enemies"][0]["health"]==12&&(int)state["inventory"][0]["damage"]==1,"Melee retry cannot hit or wear tool twice");
            game.Inventory.Slots[0]=NativeStack.New("iron_pickaxe");game.Inventory.Offhand=NativeStack.New("shield");
            Send(game,"room","combat","itemUse","start",true);for(int i=0;i<5;i++)game.Pump(.05);state=Latest(game);Check((bool)state["stats"]["blocking"]&&(string)state["stats"]["activeHand"]=="offhand","Pickaxe permits offhand shield");
            Send(game,"room","combat","damage","kind","mob","amount",3,"attackerX",1);state=Latest(game);Check((double)state["stats"]["health"]==20&&(int)state["stats"]["offhand"]["damage"]==4,"Shield blocks frontal hit and loses durability");
            Send(game,"room","combat","itemUse","cancel",true);Latest(game);game.Inventory.Offhand=NativeStack.New("totem_of_undying");
            Send(game,"room","combat","damage","kind","fall","amount",1000);var events=Drain(game);state=events.Last(v=>(string)v["type"]=="snapshot");Check(events.Any(v=>(string)v["type"]=="totem")&&(double)state["stats"]["health"]==1&&(double)state["stats"]["absorption"]==8,"Totem prevents lethal fall and emits animation event");
            Send(game,"room","furnace","scene","exactX",0,"exactY",2);Latest(game);game.Inventory.Slots[0]=NativeStack.New("furnace");game.Inventory.Slots[9]=NativeStack.New("raw_iron",2);game.Inventory.Slots[10]=NativeStack.New("coal");
            Send(game,"room","furnace","place","x",2,"y",2);Latest(game);Send(game,"room","furnace","interact","x",2,"y",2);state=Latest(game);int sync=(int)state["container"]["syncId"];
            Send(game,"room","furnace","slotClick","handler","container","syncId",sync,"slot",3,"action","QUICK_MOVE");Latest(game);
            Send(game,"room","furnace","slotClick","handler","container","syncId",sync,"slot",4,"action","QUICK_MOVE");Latest(game);
            for(int i=0;i<100;i++)game.Pump(.05);state=Latest(game);Check((double)state["container"]["cook"]>.4&&(double)state["container"]["cook"]<.6,"Furnace progress advances");
            for(int i=0;i<100;i++)game.Pump(.05);state=Latest(game);Check((string)state["container"]["slots"][2]["id"]=="minecraft:iron_ingot"&&(int)state["container"]["slots"][2]["count"]==1,"Furnace produces output");
            Send(game,"room","new-combat","scene","exactX",0,"exactY",2);Latest(game);Send(game,"room","new-combat","pose","exactX",0,"exactY",2,"grounded",true,"enemies",new JArray(enemy));Latest(game);Send(game,"room","new-combat","swing","request",11,"aimX",1);attack=Drain(game).Single(v=>(string)v["type"]=="attack");Send(game,"room","new-combat","hit","attack",(int)attack["attack"],"targets",new[]{42});state=Latest(game);Check((double)state["enemies"][0]["health"]==12,"Switching saves resets combat cooldown safely");
            Send(game,"room","pearl","scene","exactX",0,"exactY",2);Latest(game);game.Inventory.Slots[0]=NativeStack.New("ender_pearl",16);Send(game,"room","pearl","itemUse","start",true,"x",2,"y",2,"aimX",1);state=Latest(game);var pearl=state["projectiles"][0];
            Send(game,"room","pearl","nativeProjectileHit","entity",(int)pearl["entity"],"uuid",(string)pearl["uuid"],"request",1,"landingX",4,"landingY",2);state=Latest(game);Check((double)state["stats"]["health"]==17,"Pearl has three base damage");
            Send(game,"room","pearl","pose","teleport",0,"exactX",0,"exactY",30);Latest(game);Send(game,"room","pearl","nativeLanding","teleport",0,"landing",1,"distance",28);state=Latest(game);Check((double)state["playerX"]==4&&(double)state["stats"]["health"]==17,"Stale pre-teleport poses and fall events cannot damage or move player");
            Send(game,"Tutorial_01","intro","scene","exactX",0,"exactY",3,"intro",true);Latest(game);
            Send(game,"Tutorial_01","intro","nativeLanding","landing",1,"x",0,"y",3,"distance",4);events=Drain(game);Check(events.Any(v=>(string)v["type"]=="landing"&&(string)v["kind"]=="water"&&(bool)v["intro"]),"First tutorial landing deploys water even below 12 blocks");
            Send(game,"Tutorial_01","intro","nativeLanding","landing",1,"x",0,"y",3,"distance",4);events=Drain(game);Check(!events.Any(v=>(string)v["type"]=="landing"),"Landing retry is idempotent");
            for(int i=0;i<31;i++)game.Pump(.05);events=Drain(game);Check(events.Any(v=>(string)v["type"]=="waterCollected"),"Landing water collects after 1.5 seconds");
            Send(game,"room","tnt","scene","exactX",0,"exactY",2);Latest(game);game.Inventory.Slots[0]=NativeStack.New("tnt");Send(game,"room","tnt","place","x",2,"y",2);Latest(game);game.Inventory.Slots[0]=NativeStack.New("flint_and_steel");Send(game,"room","tnt","interact","x",2,"y",2);state=Latest(game);Check(((JArray)state["primed"]).Count==1,"TNT ignites locally");
            Send(game,"other","tnt","scene","exactX",0,"exactY",2);Latest(game);Send(game,"room","tnt","scene","exactX",0,"exactY",2);state=Latest(game);Check(((JArray)state["primed"]).Count==1,"Primed TNT persists across room visits");
            Send(game,"room","respawn","scene","exactX",0,"exactY",2);Latest(game);Send(game,"room","respawn","damage","amount",1000,"kind","fall");state=Latest(game);Check((bool)state["stats"]["dead"],"Lethal damage reaches death state");Send(game,"room","respawn","pause","paused",true);Drain(game);Send(game,"room","respawn","respawn");state=Latest(game);Check(!(bool)state["stats"]["dead"]&&(double)state["stats"]["health"]==20,"Respawn publishes recovery while simulation is paused for death");
        }
        string corruptRoot=Path.Combine(root,"corrupt");Directory.CreateDirectory(corruptRoot);string path;
        using(var game=new NativeGame(corruptRoot)){Send(game,"room","corrupt","scene");Latest(game);Send(game,"room","corrupt","checkpoint");Latest(game);path=Directory.GetFiles(corruptRoot,"*.json").Single();}
        const string broken="{ intentionally corrupt";File.WriteAllText(path,broken);
        using(var game=new NativeGame(corruptRoot)){bool rejected=false;try{Send(game,"room","corrupt","scene");}catch(Newtonsoft.Json.JsonException){rejected=true;}Check(rejected,"Corrupt save is rejected");}
        Check(File.ReadAllText(path)==broken,"Corrupt save is never overwritten with starter state");
    }
}
