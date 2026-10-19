using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace HollowCraft.Standalone
{
    // The sealed 2D plane uses vanilla fluid amounts (1..8)/9 and shared corner
    // heights. Native HK collision rectangles clip the visible fluid, not its UVs.
    internal static class NativeFluidGeometry
    {
        internal static string Kind(JObject cell)=>(bool?)cell?["waterlogged"]==true?"minecraft:water":(string)cell?["id"];
        internal static bool IsLiquid(JObject cell)=>Kind(cell)=="minecraft:water"||Kind(cell)=="minecraft:lava";
        private static double[][] Floors(Dictionary<string,JArray> terrain,int x,int y) {
            var data=terrain!=null&&terrain.TryGetValue(NativeRoom.Key(x,y),out var shapes)?shapes.OfType<JArray>().Where(r=>r.Count>=6&&(double)r[5]<.99999).Select(r=>new[]{(double)r[2],(double)r[3],(double)r[4],(double)r[5]}).ToArray():new double[0][];
            return NativeTerrainRaster.Covers(data,0)?data:new double[0][];
        }
        private static double Floor(Dictionary<string,JArray> terrain,int x,int y,double at=.5){return Floors(terrain,x,y).Where(r=>r[0]<=at+.000001&&r[2]>=at-.000001).Select(r=>r[3]).DefaultIfEmpty(0).Max();}
        private static double LocalHeight(NativeRoom room,Dictionary<string,JArray> terrain,JObject cell,double at){double floor=Floor(terrain,(int)cell["x"],(int)cell["y"],at);return floor+(1-floor)*Height(room,cell);}
        internal static double Height(NativeRoom room,JObject cell) {
            if(!IsLiquid(cell))return 0;int x=(int)cell["x"],y=(int)cell["y"];
            if(room.Blocks.TryGetValue(NativeRoom.Key(x,y+1),out var above)&&Kind(above)==Kind(cell))return 1;
            int level=(bool?)cell["source"]==true||(bool?)cell["waterlogged"]==true||(bool?)cell["falling"]==true?0:Math.Max(0,Math.Min(7,(int?)cell["level"]??7));return (8-level)/9.0;
        }
        internal static IEnumerable<double[]> Obstacles(NativeRoom room,Dictionary<string,JArray> terrain,int x,int y) {
            if(terrain!=null&&terrain.TryGetValue(NativeRoom.Key(x,y),out var native))foreach(JArray a in native){if(a.Count<6)yield return new[]{0.0,0,1,1};else yield return new[]{(double)a[2],(double)a[3],(double)a[4],(double)a[5]};}
            if(room.Blocks.TryGetValue(NativeRoom.Key(x,y),out var cell)&&!new[]{"minecraft:water","minecraft:lava"}.Contains((string)cell["id"]))foreach(JArray a in NativeRoom.Shapes(cell))yield return new[]{(double)a[0],(double)a[1],(double)a[2],(double)a[3]};
        }
        private static double Corner(NativeRoom room,Dictionary<string,JArray> terrain,JObject cell,int direction) {
            double own=LocalHeight(room,terrain,cell,direction<0?0:1);int x=(int)cell["x"]+direction,y=(int)cell["y"];
            if(room.Blocks.TryGetValue(NativeRoom.Key(x,y),out var side)&&Kind(side)==Kind(cell)){double other=LocalHeight(room,terrain,side,direction<0?1:0);if(own>=1||other>=1)return 1;double a=own>=.8?10:1,b=other>=.8?10:1;return (a*own+b*other)/(a+b);}
            if(Obstacles(room,terrain,x,y).Any(r=>r[3]>.99&&r[1]<.01))return own;
            return (own*(own>=.8?10:1)+Floor(terrain,x,y,direction<0?1:0))/(own>=.8?11:2);
        }
        internal static double[] Tops(NativeRoom room,Dictionary<string,JArray> terrain,JObject cell)=>new[]{Corner(room,terrain,cell,-1),Corner(room,terrain,cell,1)};
        private static List<double[]> Clip(List<double[]> polygon,int axis,double edge,bool greater) {
            var result=new List<double[]>();if(polygon.Count==0)return result;var a=polygon[polygon.Count-1];bool insideA=greater?a[axis]>=edge-1e-9:a[axis]<=edge+1e-9;
            foreach(var b in polygon){bool insideB=greater?b[axis]>=edge-1e-9:b[axis]<=edge+1e-9;if(insideA!=insideB){double t=(edge-a[axis])/(b[axis]-a[axis]);result.Add(new[]{a[0]+(b[0]-a[0])*t,a[1]+(b[1]-a[1])*t});}if(insideB)result.Add(b);a=b;insideA=insideB;}return result;
        }
        private static bool Area(List<double[]> polygon) {double sum=0;for(int i=0;i<polygon.Count;i++){var a=polygon[i];var b=polygon[(i+1)%polygon.Count];sum+=a[0]*b[1]-a[1]*b[0];}return Math.Abs(sum)>.000001;}
        internal static bool Intersects(NativeRoom room,Dictionary<string,JArray> terrain,JObject cell,double left,double bottom,double right,double top) {
            double x=(int)cell["x"],y=(int)cell["y"];
            foreach(JArray polygon in Polygons(room,terrain,cell)) {
                var clipped=polygon.OfType<JArray>().Select(p=>new[]{(double)p[0],(double)p[1]}).ToList();
                clipped=Clip(Clip(Clip(Clip(clipped,0,left-x,true),0,right-x,false),1,bottom-y,true),1,top-y,false);
                if(Area(clipped))return true;
            }return false;
        }
        internal static JArray Polygons(NativeRoom room,Dictionary<string,JArray> terrain,JObject cell) {
            var top=Tops(room,terrain,cell);var floors=Floors(terrain,(int)cell["x"],(int)cell["y"]);var cuts=floors.SelectMany(r=>new[]{r[0],r[2]}).Concat(new[]{0.0,1}).Distinct().OrderBy(v=>v).ToArray();var polygons=new List<List<double[]>>();
            for(int i=0;i+1<cuts.Length;i++){double left=cuts[i],right=cuts[i+1],bottom=floors.Where(r=>r[0]<right&&r[2]>left).Select(r=>r[3]).DefaultIfEmpty(0).Max();polygons.Add(new List<double[]>{new[]{left,bottom},new[]{right,bottom},new[]{right,Math.Max(bottom,top[0]+(top[1]-top[0])*right)},new[]{left,Math.Max(bottom,top[0]+(top[1]-top[0])*left)}});}
            foreach(var r in Obstacles(room,terrain,(int)cell["x"],(int)cell["y"])) {
                var next=new List<List<double[]>>();foreach(var p in polygons){var middle=Clip(Clip(p,0,r[0],true),0,r[2],false);foreach(var part in new[]{Clip(p,0,r[0],false),Clip(p,0,r[2],true),Clip(middle,1,r[1],false),Clip(middle,1,r[3],true)})if(Area(part))next.Add(part);}polygons=next;
            }
            return new JArray(polygons.Select(p=>new JArray(p.Select(v=>new JArray(v[0],v[1])))));
        }
        internal static void Describe(NativeRoom room,Dictionary<string,JArray> terrain,JObject cell,JObject result) {
            int x=(int)cell["x"],y=(int)cell["y"];string key=NativeRoom.Key(x,y);var signature=new System.Text.StringBuilder();
            for(int dx=-1;dx<=1;dx++)for(int dy=0;dy<=1;dy++){
                if(room.Blocks.TryGetValue(NativeRoom.Key(x+dx,y+dy),out var n))signature.Append(n["id"]).Append('/').Append(n["source"]).Append('/').Append(n["level"]).Append('/').Append(n["falling"]).Append('/').Append(n["waterlogged"]).Append('/').Append(n["top"]).Append('/').Append(n["double"]).Append('/').Append(n["open"]).Append('/').Append(n["pistonHead"]).Append('/').Append(n["dx"]).Append('/').Append(n["dy"]);
                signature.Append('|');if(dy==0&&terrain!=null&&terrain.TryGetValue(NativeRoom.Key(x+dx,y),out var shapes))signature.Append(shapes.ToString(Newtonsoft.Json.Formatting.None));
            }
            string input=signature.ToString();if(!room.FluidVisuals.TryGetValue(key,out var cached)||cached.Item1!=input){
                var data=new JObject();var top=Tops(room,terrain,cell);data["height"]=Math.Max(top[0],top[1]);data["fluidGeometry"]=Polygons(room,terrain,cell);data["fluidKind"]=Kind(cell);
                bool falling=(bool?)cell["falling"]==true;double horizontal=top[0]-top[1];data["fluidFlowX"]=Math.Abs(horizontal)<.00001?0:Math.Sign(horizontal);data["fluidFlowY"]=falling?-1:0;
                bool flowing=falling||(bool?)cell["source"]!=true&&(bool?)cell["waterlogged"]!=true;data["fluidTexture"]=Kind(cell).Substring(10)+(flowing?"_flow":"_still");
                string state="/fluid/"+data["fluidTexture"]+"/"+data["fluidGeometry"].ToString(Newtonsoft.Json.Formatting.None)+"/"+data["fluidFlowX"]+"/"+data["fluidFlowY"];
                if(room.FluidVisuals.Count>2048)room.FluidVisuals.Clear();room.FluidVisuals[key]=cached=Tuple.Create(input,data,state);
            }
            foreach(var property in cached.Item2)result[property.Key]=property.Value.DeepClone();result["state"]=(string)result["state"]+cached.Item3;
        }
    }
}
