using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using HollowCraft.Standalone;

internal static partial class Program
{
    private static void FluidFood(string root,string assets) {
        var eatKeys=new HashSet<string>(((JArray)NativeData.Sounds["minecraft:entity.generic.eat"]).Values<string>());
        var drinkKeys=new HashSet<string>(((JArray)NativeData.Sounds["minecraft:entity.generic.drink"]).Values<string>());
        var burpKeys=new HashSet<string>(((JArray)NativeData.Sounds["minecraft:entity.player.burp"]).Values<string>());
        Func<JObject,HashSet<string>,bool> sound=(e,keys)=>(string)e["type"]=="mcSound"&&(bool?)e["play"]==true&&keys.Contains((string)e["key"]??"");
        using(var game=WorldCase(root,assets,"food-sound")){
            game.Inventory.Selected=2;Send(game,"world","food-sound","itemUse","start",true,"x",5,"y",1);Drain(game);Advance(game,6);Check(!Drain(game).Any(e=>sound(e,eatKeys)),"Eating retains the initial windup before chewing");
            Advance(game,1);Check(Drain(game).Count(e=>sound(e,eatKeys))==1,"First chewing sound occurs at the eighth use tick");
            Advance(game,24);var events=Drain(game);Check(events.Count(e=>sound(e,eatKeys))==6&&events.Count(e=>sound(e,burpKeys))==1&&game.Inventory.Hand.Count==63,"Chewing repeats throughout the meal and burps once on consumption");
            Send(game,"world","food-sound","itemUse","start",true,"x",5,"y",1);Drain(game);Advance(game,8);Drain(game);Send(game,"world","food-sound","itemUse","cancel",true);Drain(game);Advance(game,40);
            Check(!Drain(game).Any(e=>sound(e,eatKeys)||sound(e,burpKeys))&&game.Inventory.Hand.Count==63,"Releasing use cancels further chewing and consumption");
            game.Inventory.Slots[2]=NativeStack.New("milk_bucket");Send(game,"world","food-sound","itemUse","start",true,"x",5,"y",1);Drain(game);Advance(game,31);events=Drain(game);Check(events.Count(e=>sound(e,drinkKeys))==7&&game.Inventory.Hand.Id=="minecraft:bucket","Milk drinks throughout the use animation and returns its bucket");
        }
        using(var game=WorldCase(root,assets,"fluid-levels")){
            var room=CurrentRoom(game);for(int i=-12;i<=12;i++)Put(room,i,0,"stone");Put(room,0,1,"water");Advance(game,45);
            for(int i=1;i<=7;i++)Check(room.Blocks.TryGetValue(i+":1",out var c)&&(int)c["level"]==i,"Water attenuates to level "+i+" at distance "+i);
            Check(!room.Blocks.ContainsKey("8:1"),"Water stops after seven horizontal cells");Check(Math.Abs(NativeFluidGeometry.Height(room,room.Blocks["0:1"])-8/9.0)<1e-8,"Exposed source height follows the vanilla eight-ninths rule");
            for(int i=0;i<7;i++){var a=NativeFluidGeometry.Tops(room,null,room.Blocks[i+":1"]);var b=NativeFluidGeometry.Tops(room,null,room.Blocks[(i+1)+":1"]);Check(Math.Abs(a[1]-b[0])<1e-8,"Adjacent liquid corners form one continuous surface at "+i);}
            var first=room.BlocksJson(0,1).OfType<JObject>().Single(c=>(int)c["x"]==1&&(int)c["y"]==1);Check((string)first["fluidTexture"]=="water_flow"&&first["fluidGeometry"] is JArray polys&&polys.Count>0&&polys[0] is JArray,"Flow snapshots carry clipped polygons and the flowing texture");
            room.Blocks["1:1"]["level"]=6;var second=room.BlocksJson(0,1).OfType<JObject>().Single(c=>(int)c["x"]==1&&(int)c["y"]==1);Check((string)first["state"]!=(string)second["state"],"Water-level changes invalidate the actual frontend render state");
        }
        using(var game=WorldCase(root,assets,"lava-levels")){
            var room=CurrentRoom(game);for(int i=-12;i<=12;i++)Put(room,i,0,"stone");Put(room,0,1,"lava");Advance(game,130);
            for(int i=1;i<=3;i++)Check(room.Blocks.TryGetValue(i+":1",out var c)&&(int)c["level"]==i*2,"Lava loses two levels per horizontal cell at "+i);
            Check(!room.Blocks.ContainsKey("4:1"),"Overworld-style lava stops after three horizontal cells");
        }
        using(var game=WorldCase(root,assets,"fluid-outlet")){
            var room=CurrentRoom(game);for(int i=-10;i<=10;i++)if(i!=2)Put(room,i,0,"stone");Put(room,0,1,"water");Advance(game,10);Check(room.Blocks.ContainsKey("1:1")&&!room.Blocks.ContainsKey("-1:1"),"Water prefers the closest downward outlet instead of spreading equally");
        }
        using(var game=WorldCase(root,assets,"fluid-thin-floor")){
            var room=CurrentRoom(game);var collision=new JArray();for(int i=-10;i<=10;i++)collision.Add(new JArray(i,0,0,.45,1,.55));
            Send(game,"world","fluid-thin-floor","pose","terrain",collision);Latest(game);Put(room,0,2,"water");Advance(game,80);
            Check(room.Blocks.ContainsKey("1:0")&&!room.Blocks.Values.Any(c=>(int)c["y"]<0&&NativeFluidGeometry.IsLiquid(c)),"A native thin ledge supports lateral flow and prevents downward leakage");
            var terrain=new Dictionary<string,JArray>();foreach(JArray c in collision)terrain[NativeRoom.Key((int)c[0],0)]=new JArray{c};
            foreach(int bx in new[]{0,1,5}){var polygons=NativeFluidGeometry.Polygons(room,terrain,room.Blocks[bx+":0"]);Check(polygons.Count>0&&polygons.OfType<JArray>().SelectMany(p=>p.OfType<JArray>()).All(p=>(double)p[1]>=.55-1e-8),"Partial-height flow stays above the thin platform at "+bx);}
        }
        using(var game=WorldCase(root,assets,"fluid-known-terrain")){
            var room=CurrentRoom(game);var known=new JArray();var collision=new JArray();for(int bx=-2;bx<=2;bx++){for(int by=0;by<=2;by++)known.Add(new JArray(bx,by));collision.Add(new JArray(bx,0,0,0,1,1));}
            Send(game,"world","fluid-known-terrain","pose","terrain",collision,"terrainKnown",known);Latest(game);Put(room,0,1,"water");Advance(game,45);Check(!room.Blocks.ContainsKey("3:1")&&!room.Blocks.ContainsKey("-3:1"),"Unprobed native terrain is not assumed to be air");
            known=new JArray();collision=new JArray();for(int bx=3;bx<=4;bx++){for(int by=0;by<=2;by++)known.Add(new JArray(bx,by));collision.Add(new JArray(bx,0,0,0,1,1));}
            Send(game,"world","fluid-known-terrain","pose","terrain",collision,"terrainKnown",known);Latest(game);Advance(game,20);Check(room.Blocks.ContainsKey("3:1")&&room.Blocks.ContainsKey("4:1"),"Flow resumes into newly surveyed cells without forgetting the earlier floor");
        }
        using(var game=WorldCase(root,assets,"fluid-arrival")){
            Send(game,"world","fluid-arrival","scene","nativeTerrain",true);Latest(game);var room=CurrentRoom(game);Put(room,0,2,"water");Advance(game,15);Check(room.Blocks.Count==1,"Entering a native scene freezes saved fluids until the first terrain survey");
            Send(game,"world","fluid-arrival","pose","terrain",new JArray(),"terrainKnown",new JArray{new JArray(0,1),new JArray(0,2)});Latest(game);Advance(game,5);Check(room.Blocks.ContainsKey("0:1"),"Initial survey enables fluid updates after arrival");
        }
        var shapes=NativeRoom.Shapes(NativeRoom.Cell(0,0,"oak_slab"));Check(shapes.Count==1&&shapes[0] is JArray box&&box.Count==4,"Half-slab collision remains an array of rectangles rather than a flattened numeric array");
        var slabRoom=new NativeRoom();var slab=NativeRoom.Cell(0,0,"oak_slab");slab["waterlogged"]=true;slabRoom.Blocks["0:0"]=slab;var wet=NativeFluidGeometry.Polygons(slabRoom,null,slab);Check(wet.Count>0&&wet.OfType<JArray>().SelectMany(p=>p.OfType<JArray>()).All(p=>(double)p[1]>=.5),"Waterlogged bottom slab displays water only in its unoccupied half");
    }
}
