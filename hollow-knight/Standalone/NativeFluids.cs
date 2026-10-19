using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace HollowCraft.Standalone
{
    internal sealed partial class NativeGame
    {
        private static bool Liquid(string id)=>id=="minecraft:water"||id=="minecraft:lava";
        private static bool WaterCell(JObject cell)=>(string)cell?["id"]=="minecraft:water"||(bool?)cell?["waterlogged"]==true;
        private static bool LiquidCell(JObject cell)=>Liquid((string)cell?["id"])||(bool?)cell?["waterlogged"]==true;
        private static string LiquidKind(JObject cell)=>WaterCell(cell)?"minecraft:water":(string)cell?["id"];
        private static bool LiquidSource(JObject cell)=>(bool?)cell?["source"]==true||(bool?)cell?["waterlogged"]==true;
        private static int LiquidLevel(JObject cell)=>LiquidSource(cell)||(bool?)cell?["falling"]==true?0:Math.Max(0,Math.Min(7,(int?)cell?["level"]??7));
        private static readonly int[][] Neighbors={new[]{-1,0},new[]{1,0},new[]{0,-1},new[]{0,1}};
        private bool FluidPassable(int bx,int by,string id) {
            if(by< -256||by>4096||Math.Abs(bx)>4096||terrainKnown.Count>0&&!terrainKnown.Contains(NativeRoom.Key(bx,by))||TerrainFull(bx,by))return false;
            if(!Room.Blocks.TryGetValue(NativeRoom.Key(bx,by),out var cell))return true;
            string other=(string)cell["id"];return Liquid(other)||id=="minecraft:water"&&other.EndsWith("_slab",StringComparison.Ordinal)&&(bool?)cell["double"]!=true||!NativeCatalog.Get(other).Solid&&!new[]{"minecraft:ladder","minecraft:vine","minecraft:nether_portal"}.Contains(other);
        }
        private bool FluidCross(int bx,int by,int dx,int dy,string id) {
            if(!FluidPassable(bx+dx,by+dy,id))return false;
            // A thin platform can be wholly inside a voxel. Treat its horizontal
            // span as a floor even though TerrainFull correctly returns false.
            return !NativeFluidGeometry.Obstacles(Room,terrain,bx,by).Any(r=>dy<0?r[0]<.001&&r[2]>.999&&r[3]>.001&&(r[1]<.001||r[3]<.999):dx!=0&&r[1]<.001&&r[3]>.999&&r[2]-r[0]>.001);
        }
        private int FluidDropDistance(int bx,int by,int direction,string id,Dictionary<string,JObject> old,int limit) {
            for(int distance=0;distance<=limit;distance++){
                if(FluidDown(bx,by,id))return distance;
                if(!FluidCross(bx,by,direction,0,id))break;bx+=direction;
                if(old.TryGetValue(NativeRoom.Key(bx,by),out var source)&&LiquidKind(source)==id&&LiquidSource(source))break;
            }return 1000;
        }
        private int FluidDirections(JObject cell,Dictionary<string,JObject> old) {
            int bx=(int)cell["x"],by=(int)cell["y"];string id=LiquidKind(cell);
            if(FluidDown(bx,by,id))return 0;
            int left=FluidCross(bx,by,-1,0,id)?FluidDropDistance(bx-1,by,-1,id,old,id=="minecraft:water"?3:1):1001;
            int right=FluidCross(bx,by,1,0,id)?FluidDropDistance(bx+1,by,1,id,old,id=="minecraft:water"?3:1):1001;
            return (left<=right&&left<1001?1:0)|(right<=left&&right<1001?2:0);
        }
        private bool FluidDown(int bx,int by,string id)=>FluidCross(bx,by,0,-1,id)&&!(Room.Blocks.TryGetValue(NativeRoom.Key(bx,by-1),out var below)&&LiquidKind(below)==id&&LiquidSource(below));
        private double FluidDepth(string id) {
            double depth=0;for(int bx=(int)Math.Floor(x-.299);bx<=(int)Math.Floor(x+.299);bx++)for(int by=(int)Math.Floor(y);by<=(int)Math.Floor(y+1.79);by++){
                if(!Room.Blocks.TryGetValue(NativeRoom.Key(bx,by),out var cell)||LiquidKind(cell)!="minecraft:"+id)continue;
                var tops=NativeFluidGeometry.Tops(Room,terrain,cell);double fraction=Math.Max(0,Math.Min(1,x-bx)),surface=by+tops[0]+(tops[1]-tops[0])*fraction;if(surface>y+.001)depth=Math.Max(depth,surface-y);
            }return Math.Min(1.8,depth);
        }
        private bool FluidPoint(double px,double py,string id) {
            int bx=(int)Math.Floor(px),by=(int)Math.Floor(py);if(!Room.Blocks.TryGetValue(NativeRoom.Key(bx,by),out var cell)||LiquidKind(cell)!="minecraft:"+id)return false;
            var top=NativeFluidGeometry.Tops(Room,terrain,cell);return py-by<top[0]+(top[1]-top[0])*(px-bx)&&!NativeFluidGeometry.Obstacles(Room,terrain,bx,by).Any(r=>px-bx>=r[0]&&px-bx<=r[2]&&py-by>=r[1]&&py-by<=r[3]);
        }
        // A cell is evaluated against the previous generation; sources are never
        // erased and recreated. Separate 5/30 tick clocks preserve lava's delay.
        private void FluidStep() {
            if(tick%5!=0||terrainSurveyRequired&&terrainKnown.Count==0)return;
            var old=Room.Blocks.Where(p=>LiquidCell(p.Value)&&Math.Abs((int)p.Value["x"]-x)<=38&&Math.Abs((int)p.Value["y"]-y)<=30).ToDictionary(p=>p.Key,p=>(JObject)p.Value.DeepClone());
            var candidates=new Dictionary<string,int[]>();
            foreach(var cell in old.Values){int bx=(int)cell["x"],by=(int)cell["y"];candidates[NativeRoom.Key(bx,by)]=new[]{bx,by};foreach(var d in Neighbors)candidates[NativeRoom.Key(bx+d[0],by+d[1])]=new[]{bx+d[0],by+d[1]};}
            var changes=new Dictionary<string,JObject>();int updates=0;
            var directions=old.ToDictionary(p=>p.Key,p=>FluidDirections(p.Value,old));
            foreach(var entry in candidates.OrderBy(p=>p.Key,StringComparer.Ordinal)) {
                int bx=entry.Value[0],by=entry.Value[1];string key=entry.Key;
                if(Math.Abs(bx-x)>36||Math.Abs(by-y)>28||updates>=8192||terrainKnown.Count>0&&!terrainKnown.Contains(key))continue;
                old.TryGetValue(key,out var current);
                if(Room.Blocks.TryGetValue(key,out var occupied)&&!Liquid((string)occupied["id"])&&(WaterCell(occupied)||NativeCatalog.Get((string)occupied["id"]).Solid&&!((string)occupied["id"]).EndsWith("_slab",StringComparison.Ordinal)))continue;
                string kind=current==null?null:LiquidKind(current);
                // Lava touching water solidifies; downward lava into water makes stone.
                if(kind=="minecraft:lava"&&Neighbors.Any(d=>d[1]>=0&&old.TryGetValue(NativeRoom.Key(bx+d[0],by+d[1]),out var n)&&WaterCell(n))) {changes[key]=NativeRoom.Cell(bx,by,LiquidSource(current)?"obsidian":"cobblestone");updates++;continue;}
                if(kind=="minecraft:water"&&old.TryGetValue(NativeRoom.Key(bx,by+1),out var aboveLava)&&LiquidKind(aboveLava)=="minecraft:lava"){changes[key]=NativeRoom.Cell(bx,by,"stone");updates++;continue;}
                if(kind!=null&&LiquidSource(current))continue;
                JObject next=null;
                foreach(string id in new[]{"minecraft:water","minecraft:lava"}) {
                    if(kind!=null&&kind!=id||tick%(id=="minecraft:water"?5:30)!=0||!FluidPassable(bx,by,id))continue;
                    int level=8,sources=0;bool falling=false;
                    if(old.TryGetValue(NativeRoom.Key(bx,by+1),out var above)&&LiquidKind(above)==id&&FluidCross(bx,by+1,0,-1,id)){level=0;falling=true;}
                    foreach(int direction in new[]{-1,1})if(old.TryGetValue(NativeRoom.Key(bx+direction,by),out var side)&&LiquidKind(side)==id&&FluidCross(bx+direction,by,-direction,0,id)){
                        if(LiquidSource(side))sources++;bool supported=!FluidCross(bx+direction,by,0,-1,id)||old.TryGetValue(NativeRoom.Key(bx+direction,by-1),out var support)&&LiquidKind(support)==id&&LiquidSource(support);
                        if(supported&&(directions[NativeRoom.Key(bx+direction,by)]&(direction<0?2:1))!=0){int candidate=LiquidLevel(side)+(id=="minecraft:lava"?2:1);if(candidate<level){level=candidate;falling=false;}}
                    }
                    bool renew=id=="minecraft:water"&&sources>=2&&(Solid(bx,by-1)||old.TryGetValue(NativeRoom.Key(bx,by-1),out var below)&&WaterCell(below)&&LiquidSource(below));
                    if(renew){level=0;falling=false;}
                    if(level<=7){next=NativeRoom.Cell(bx,by,id,renew,level);next["falling"]=falling;}
                    if(next!=null)break;
                }
                if(next!=null){if(current==null||LiquidLevel(current)!=LiquidLevel(next)||(bool?)current["falling"]!=(bool?)next["falling"]||LiquidSource(current)!=LiquidSource(next))changes[key]=next;}
                else if(current!=null&&tick%(kind=="minecraft:lava"?30:5)==0)changes[key]=null;
                updates++;
            }
            foreach(var change in changes){if(change.Value==null)Room.Blocks.Remove(change.Key);else {
                if(Room.Blocks.TryGetValue(change.Key,out var washed)&&!Liquid((string)washed["id"])){
                    if(((string)washed["id"]).EndsWith("_slab",StringComparison.Ordinal)&&(string)change.Value["id"]=="minecraft:water"){washed["waterlogged"]=true;continue;}
                    DropAt(NativeStack.New((string)washed["id"]),(int)washed["x"]+.5,(int)washed["y"]+.5);
                }Room.Blocks[change.Key]=change.Value;
            }}
            if(tick%5==0)foreach(var sponge in Room.Blocks.Values.Where(c=>(string)c["id"]=="minecraft:sponge").ToArray())AbsorbSponge(sponge);
        }
        private void AbsorbSponge(JObject sponge) {
            var queue=new Queue<int[]>();var seen=new HashSet<string>();queue.Enqueue(new[]{(int)sponge["x"],(int)sponge["y"],0});int count=0;
            while(queue.Count>0&&count<65){var at=queue.Dequeue();foreach(var d in Neighbors){int bx=at[0]+d[0],by=at[1]+d[1];string key=NativeRoom.Key(bx,by);if(!seen.Add(key)||!Room.Blocks.TryGetValue(key,out var cell)||!WaterCell(cell))continue;if((bool?)cell["waterlogged"]==true)cell["waterlogged"]=false;else Room.Blocks.Remove(key);count++;if(at[2]<5)queue.Enqueue(new[]{bx,by,at[2]+1});if(count>=65)break;}}
            if(count>0)sponge["id"]="minecraft:wet_sponge";
        }
        private double[] FluidCurrent(double px,double py) {
            if(!Room.Blocks.TryGetValue(NativeRoom.Key((int)Math.Floor(px),(int)Math.Floor(py)),out var cell)||!LiquidCell(cell))return new[]{0.0,0.0};
            int bx=(int)cell["x"],by=(int)cell["y"],level=LiquidLevel(cell);double horizontal=0;
            foreach(int d in new[]{-1,1}) {if(Room.Blocks.TryGetValue(NativeRoom.Key(bx+d,by),out var side)&&LiquidKind(side)==LiquidKind(cell))horizontal+=d*(LiquidLevel(side)-level);else if(FluidPassable(bx+d,by,LiquidKind(cell)))horizontal+=d*(8-level);}
            return new[]{Math.Sign(horizontal)*.014,(bool?)cell["falling"]==true?-.014:0};
        }
    }
}
