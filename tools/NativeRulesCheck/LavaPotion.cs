using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using HollowCraft.Standalone;

internal static partial class Program
{
    private static void LavaPotion(string root,string assets) {
        using(var game=WorldCase(root,assets,"native-lava")) {
            var room=CurrentRoom(game);for(int bx=-3;bx<8;bx++)Put(room,bx,0,"stone");Put(room,0,1,"lava");
            var touching=new JObject{["id"]=9401,["generation"]=1,["kind"]="arthropod",["health"]=100,["maxHealth"]=100,["x"]=1.05,["y"]=1.1,["width"]=.5,["height"]=2};
            var airborne=(JObject)touching.DeepClone();airborne["id"]=9402;airborne["x"]=.5;airborne["y"]=1.95;
            var resistant=(JObject)touching.DeepClone();resistant["id"]=9403;resistant["nativeEffects"]=new JObject{["fire_resistance"]=new JObject{["ticks"]=200,["level"]=0}};
            var npc=(JObject)touching.DeepClone();npc["id"]=9404;npc["kind"]="npc";
            Send(game,"world","native-lava","pose","enemies",new JArray(touching,airborne,resistant,npc));Latest(game);Advance(game,9);var state=Latest(game);var values=(JArray)state["enemies"];
            Func<int,JToken> actor=id=>values.Single(e=>(int)e["id"]==id);
            Check((double)actor(9401)["health"]==96&&(bool)actor(9401)["onFire"],"Lava overlaps a native enemy's body edge even when its origin is outside the source cell");
            Check((double)actor(9402)["health"]==100&&(bool?)actor(9402)["onFire"]!=true,"An enemy above the actual partial fluid surface stays dry");
            Check((double)actor(9403)["health"]==100,"Native fire resistance prevents lava damage");
            Check((double)actor(9404)["health"]==100,"Lava does not turn friendly native NPCs into combat targets");
            room.Blocks.Remove("0:1");touching["x"]=3;
            Send(game,"world","native-lava","pose","enemies",new JArray(touching));Latest(game);Advance(game,10);state=Latest(game);
            Check((double)state["enemies"][0]["health"]==95&&(bool)state["enemies"][0]["onFire"],"Native enemies continue burning after leaving lava");
            Put(room,3,1,"water");Advance(game,1);state=Latest(game);
            Check((int)state["enemies"][0]["fireTicks"]==0&&!(bool)state["enemies"][0]["onFire"],"Water extinguishes native enemy flames immediately");
            var clipped=new NativeRoom();Put(clipped,0,0,"lava");var terrain=new System.Collections.Generic.Dictionary<string,JArray>{{"0:0",new JArray{new JArray(0,0,0,0,1,.6)}}};
            Check(!NativeFluidGeometry.Intersects(clipped,terrain,clipped.Blocks["0:0"],.2,.1,.8,.4),"Terrain clipped out of a fluid mesh cannot cause lava contact damage");
            Check(NativeFluidGeometry.Intersects(clipped,terrain,clipped.Blocks["0:0"],.2,.65,.8,.75),"Exposed lava above a partial native platform causes contact damage");
        }
        foreach(string kind in new[]{"lava","onFire","inFire","magic","wither"})using(var game=WorldCase(root,assets,"no-pull-"+kind)) {
            Send(game,"world","no-pull-"+kind,"damage","amount",4,"kind",kind,"attackerX",5,"attackerY",1);var events=Drain(game);var state=events.Last(e=>(string)e["type"]=="snapshot");
            Check((double)state["stats"]["health"]<20&&events.Any(e=>(string)e["type"]=="hurt")&&!events.Any(e=>(string)e["type"]=="impulse"),"Environmental "+kind+" damage retains hurt feedback without directional pull or lift");
        }
        using(var game=WorldCase(root,assets,"contact-recoil")) {
            Send(game,"world","contact-recoil","damage","amount",4,"kind","mob","attackerX",4,"attackerY",1);
            Check(Drain(game).Any(e=>(string)e["type"]=="impulse"&&(double)e["x"]>0),"Ordinary enemy contact still pushes away from the attacker");
        }
        foreach(string fluid in new[]{"water","lava"})foreach(bool source in new[]{true,false})using(var game=WorldCase(root,assets,"cover-"+fluid+source)) {
            string campaign="cover-"+fluid+source;var room=CurrentRoom(game);room.Blocks["3:1"]=NativeRoom.Cell(3,1,fluid,source,source?0:4);game.Inventory.Selected=3;
            Send(game,"world",campaign,"interact","x",3,"y",1,"placeX",3,"placeY",1,"canPlace",true);Latest(game);
            Check((string)room.Blocks["3:1"]["id"]=="minecraft:cobblestone"&&game.Inventory.Hand.Count==63,"Survival placement replaces "+(source?"source ":"flowing ")+fluid+" and consumes exactly one block");
            Advance(game,65);Latest(game);Check((string)room.Blocks["3:1"]["id"]=="minecraft:cobblestone","Fluid updates do not restore a source replaced by a solid block");
            Send(game,"world",campaign,"place","x",3,"y",1);Latest(game);Check(game.Inventory.Hand.Count==63,"Replacing fluid does not allow overwriting the resulting solid block");
        }
        using(var game=WorldCase(root,assets,"wet-slab")) {
            var room=CurrentRoom(game);Put(room,3,1,"oak_slab");room.Blocks["3:1"]["waterlogged"]=true;game.Inventory.Selected=3;
            Send(game,"world","wet-slab","place","x",3,"y",1);Latest(game);Check((string)room.Blocks["3:1"]["id"]=="minecraft:oak_slab"&&game.Inventory.Hand.Count==64,"Waterlogged solid blocks remain occupied instead of being replaced as pure water");
        }
        using(var game=WorldCase(root,assets,"potion-splash")) {
            var target=new JObject{["id"]=9501,["generation"]=1,["kind"]="arthropod",["health"]=100,["maxHealth"]=100,["x"]=2,["y"]=1,["width"]=1,["height"]=2};
            Send(game,"world","potion-splash","pose","enemies",new JArray(target));Latest(game);
            game.Inventory.Selected=0;game.Inventory.Slots[0]=NativeStack.New("splash_potion");game.Inventory.Hand.Data["Potion"]="minecraft:strong_harming";game.Inventory.Hand.Data["CustomPotionColor"]=0x3377cc;
            Send(game,"world","potion-splash","itemUse","start",true,"x",3,"y",1,"aimX",-1);var events=Drain(game);var state=events.Last(e=>(string)e["type"]=="snapshot");var shot=state["projectiles"].Single();
            var throwKeys=((JArray)NativeData.Sounds["minecraft:entity.splash_potion.throw"]).Values<string>().ToArray();var breakKeys=((JArray)NativeData.Sounds["minecraft:entity.splash_potion.break"]).Values<string>().ToArray();
            Check(events.Any(e=>(string)e["type"]=="mcSound"&&(bool?)e["play"]==true&&throwKeys.Contains((string)e["key"])),"Throwing a potion emits its original throw sound");
            Action impact=()=>Send(game,"world","potion-splash","nativeProjectileHit","entity",(int)shot["entity"],"uuid",(string)shot["uuid"],"request",50,"target",9501,"x",2,"y",1);
            impact();events=Drain(game);state=events.Last(e=>(string)e["type"]=="snapshot");var splash=events.Single(e=>(string)e["type"]=="potionSplash");
            Check((int)splash["color"]==0x3377cc&&(bool)splash["instant"]&&(double)splash["x"]==2,"Instant potion impact carries its actual world position and custom particle color");
            Check(events.Count(e=>(string)e["type"]=="mcSound"&&(bool?)e["play"]==true&&breakKeys.Contains((string)e["key"]))==1,"Potion glass breaking plays exactly once on impact");
            Check((double)state["enemies"][0]["health"]==88&&state["projectiles"].Count()==0,"Direct-hit harming II applies once and removes the thrown bottle");
            impact();events=Drain(game);Check(!events.Any(e=>(string)e["type"]=="potionSplash"||(string)e["type"]=="mcSound"&&(bool?)e["play"]==true),"Duplicate impact acknowledgements cannot replay potion splash particles or sounds");
            game.Inventory.Slots[0]=NativeStack.New("lingering_potion");game.Inventory.Hand.Data["Potion"]="minecraft:poison";
            Send(game,"world","potion-splash","itemUse","start",true,"x",3,"y",1,"aimX",-1);state=Latest(game);shot=state["projectiles"].Single();
            Send(game,"world","potion-splash","nativeProjectileHit","entity",(int)shot["entity"],"uuid",(string)shot["uuid"],"request",51,"x",2,"y",1);events=Drain(game);state=events.Last(e=>(string)e["type"]=="snapshot");
            Check(events.Any(e=>(string)e["type"]=="potionSplash"&&(bool)e["lingering"])&&state["clouds"].Count()==1,"Lingering potions show the initial splash and retain their persistent effect cloud");
        }
        Check(Directory.GetFiles(assets,"particle_*.png").Length>=20,"Original potion effect/spell/splash animation frames are available locally");
        Check(NativeData.Sounds["minecraft:entity.lingering_potion.throw"] is JArray,"Installed audio manifest includes the lingering potion throw event");
    }
}
