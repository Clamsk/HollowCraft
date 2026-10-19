using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using HollowCraft;
using HollowCraft.Standalone;

internal static partial class Program
{
    private static void NativeGround(string root,string assets) {
        var fixture=JObject.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"fixtures","tutorial-edge-floor.json")));
        var shape=new NativeTerrainRaster();shape.Edge(((JArray)fixture["points"]).OfType<JArray>().Select(v=>new TerrainPoint((double)v[0],(double)v[1])).ToArray());
        var surface=shape.Cell(48,10);Check(surface.Count>0&&surface.All(r=>r[1]>=0&&r[3]<.02),"The real tutorial contour produces a thin floor in the cell above its edge");
        Check(!NativeTerrainRaster.Full(surface)&&shape.Cell(48,11).Count==0,"An EdgeCollider bounds box does not fill the sky above the tutorial floor");
        var known=new JArray();var collision=new JArray();var terrain=new Dictionary<string,JArray>();for(int bx=38;bx<=58;bx++)for(int by=7;by<=14;by++){
            known.Add(new JArray(bx,by));foreach(var r in shape.Cell(bx,by)){var v=new JArray(bx,by,r[0],r[1],r[2],r[3]);collision.Add(v);string key=NativeRoom.Key(bx,by);if(!terrain.TryGetValue(key,out var list))terrain[key]=list=new JArray();list.Add(v);}
        }
        using(var game=WorldCase(root,assets,"real-tutorial-floor")){
            Send(game,"world","real-tutorial-floor","pose","exactX",48,"exactY",10,"terrain",collision,"terrainKnown",known);Latest(game);var room=CurrentRoom(game);Put(room,48,12,"water");Advance(game,60);
            for(int distance=1;distance<=7;distance++)foreach(int sign in new[]{-1,1})Check(room.Blocks.TryGetValue((48+sign*distance)+":10",out var flow)&&(int)flow["level"]==distance,"Real tutorial floor spreads water "+distance+" cells toward "+sign);
            Check(!room.Blocks.Values.Any(c=>NativeFluidGeometry.IsLiquid(c)&&(int)c["y"]<10),"Water cannot descend through the real zero-thickness tutorial floor");
            var polygons=NativeFluidGeometry.Polygons(room,terrain,room.Blocks["49:10"]);Check(polygons.Count>0&&polygons.OfType<JArray>().SelectMany(p=>p.OfType<JArray>()).All(p=>(double)p[1]>=.0059),"Real-floor flowing water remains above the collision line");
        }
        using(var game=WorldCase(root,assets,"legacy-tutorial-column")){
            Send(game,"world","legacy-tutorial-column","pose","exactX",48,"exactY",10,"terrain",collision,"terrainKnown",known);Latest(game);var room=CurrentRoom(game);Put(room,48,12,"water");for(int by=8;by<=11;by++){var c=NativeRoom.Cell(48,by,"water",false,0);c["falling"]=true;room.Blocks[NativeRoom.Key(48,by)]=c;}Advance(game,200);
            Check(room.Blocks.ContainsKey("45:10")&&room.Blocks.ContainsKey("51:10"),"A saved stuck water column resumes lateral spreading after the sampler fix");Check(!room.Blocks.Values.Any(c=>NativeFluidGeometry.IsLiquid(c)&&(int)c["y"]<10),"Previously leaked water below the floor recedes without erasing the source");
        }
        var polygon=new NativeTerrainRaster();polygon.Polygon(new[]{new TerrainPoint(-12,-2),new TerrainPoint(12,-2),new TerrainPoint(12,.8),new TerrainPoint(-12,.8)});Check(!NativeTerrainRaster.Full(polygon.Cell(0,0))&&polygon.Cell(0,0).All(r=>r[3]<=.80001),"A polygon containing the voxel center is still sampled as a partial floor");
        var slope=new NativeTerrainRaster();slope.Edge(new[]{new TerrainPoint(-12,.2),new TerrainPoint(12,.8)});var slopeCollision=new JArray();var slopeTerrain=new Dictionary<string,JArray>();for(int bx=-10;bx<=10;bx++)for(int by=-1;by<=2;by++)foreach(var r in slope.Cell(bx,by)){var v=new JArray(bx,by,r[0],r[1],r[2],r[3]);slopeCollision.Add(v);string key=NativeRoom.Key(bx,by);if(!slopeTerrain.TryGetValue(key,out var list))slopeTerrain[key]=list=new JArray();list.Add(v);}
        using(var game=WorldCase(root,assets,"sloped-native-edge")){
            Send(game,"world","sloped-native-edge","pose","terrain",slopeCollision);Latest(game);var room=CurrentRoom(game);Put(room,0,2,"water");Advance(game,60);Check(room.Blocks.ContainsKey("-5:0")&&room.Blocks.ContainsKey("5:0"),"A sloping edge composed of multiple strips supports flow in both directions");Check(!room.Blocks.Values.Any(c=>NativeFluidGeometry.IsLiquid(c)&&(int)c["y"]<0),"Strip coverage blocks downward leakage across a sloping floor");
            var shapes=NativeFluidGeometry.Polygons(room,slopeTerrain,room.Blocks["2:0"]);Check(shapes.Count>0&&shapes.OfType<JArray>().SelectMany(p=>p.OfType<JArray>()).All(p=>(double)p[1]>=.2+((2+(double)p[0]+12)/24)*.6),"Sloped-floor geometry leaves no water below the true terrain line");
        }
        var wall=new NativeTerrainRaster();wall.Edge(new[]{new TerrainPoint(.4,0),new TerrainPoint(.4,1)});Check(!NativeTerrainRaster.Full(wall.Cell(0,0))&&NativeTerrainRaster.Covers(wall.Cell(0,0),1),"A vertical edge blocks a side channel without filling its entire voxel");
        Check(NativeHandModels.Render("minecraft:trident",true,12)=="minecraft:trident_throwing"&&NativeHandModels.Render("minecraft:trident",false,0)=="minecraft:trident","Trident charging selects the original throwing display and restores idle on release");
        var throwing=JObject.Parse(File.ReadAllText(Path.Combine(assets,"models","minecraft","item","trident_throwing.json")));
        Check((double)throwing["display"]["thirdperson_righthand"]["rotation"][2]==180&&(double)throwing["display"]["thirdperson_lefthand"]["rotation"][2]==180,"Both original trident throwing hand displays reverse the idle shaft orientation");
        Check(NativeHandModels.SpearPitch(0)==180&&NativeHandModels.SpearPitch(30)==210,"Charging pose points the spear forward at cursor elevation rather than eighteen degrees down");
    }
}
