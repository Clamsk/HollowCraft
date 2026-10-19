using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace HollowCraft.Standalone
{
    internal sealed partial class NativeGame
    {
        private int navigationBudget=384;
        private IEnumerable<double[]> ShapesAt(int bx,int by) {
            string key=NativeRoom.Key(bx,by);if(terrain.TryGetValue(key,out var cells))foreach(JArray cell in cells)yield return cell.Count>=6?new[]{bx+(double)cell[2],by+(double)cell[3],bx+(double)cell[4],by+(double)cell[5]}:new[]{(double)bx,by,bx+1,by+1};
            if(!Room.Blocks.TryGetValue(key,out var block))yield break;
            foreach(JArray shape in NativeRoom.Shapes(block))yield return new[]{bx+(double)shape[0],by+(double)shape[1],bx+(double)shape[2],by+(double)shape[3]};
        }
        private bool BodyClear(double px,double py,double width,double height) {
            for(int bx=(int)Math.Floor(px-width/2);bx<=Math.Floor(px+width/2);bx++)for(int by=(int)Math.Floor(py);by<=Math.Floor(py+height);by++)foreach(var b in ShapesAt(bx,by))if(px+width/2>b[0]+.01&&px-width/2<b[2]-.01&&py+height>b[1]+.01&&py<b[3]-.01)return false;return true;
        }
        private bool HasGround(double px,double py,double width)=>!BodyClear(px,py-.08,width,.08);
        private bool ClearSight(double ax,double ay,double bx,double by) {
            int steps=Math.Max(1,(int)Math.Ceiling(Math.Max(Math.Abs(bx-ax),Math.Abs(by-ay))*4));
            for(int i=1;i<steps;i++){double px=ax+(bx-ax)*i/steps,py=ay+(by-ay)*i/steps;foreach(var box in ShapesAt((int)Math.Floor(px),(int)Math.Floor(py)))if(px>box[0]&&px<box[2]&&py>box[1]&&py<box[3])return false;}return true;
        }
        private sealed class NavNode {internal int X,Y,Cost;internal double Score;internal NavNode Parent;}
        private JArray FindPath(JObject mob,double tx,double ty) {
            int sx=(int)Math.Floor(Number(mob,"x")),sy=(int)Math.Round(Number(mob,"y")),gx=(int)Math.Floor(tx),gy=(int)Math.Round(ty);double width=Math.Min(1.8,Number(mob,"width",.6)),height=Number(mob,"height",1.8);
            var start=new NavNode{X=sx,Y=sy};var open=new List<NavNode>{start};var best=new Dictionary<string,int>{{NativeRoom.Key(sx,sy),0}};NavNode closest=start;double closestDistance=double.MaxValue;
            bool floating=NativeMobCatalog.Find((string)mob["kind"])?.Motion=="fly"||NativeMobCatalog.Find((string)mob["kind"])?.Motion=="swim";
            for(int budget=0;budget<96&&navigationBudget>0&&open.Count>0;budget++,navigationBudget--) {
                var node=open.OrderBy(n=>n.Score).First();open.Remove(node);double distance=Math.Abs(node.X-gx)+Math.Abs(node.Y-gy);
                if(distance<closestDistance){closest=node;closestDistance=distance;}if(distance<=1)break;
                foreach(int dx in floating?new[]{-1,0,1}:new[]{-1,1})foreach(int dy in floating?new[]{-1,0,1}:new[]{0,1,-1,-2,-3}) {
                    if(dx==0&&dy==0)continue;int nx=node.X+dx,ny=node.Y+dy;if(Math.Abs(nx-sx)>14||Math.Abs(ny-sy)>8||!BodyClear(nx+.5,ny,width,height))continue;
                    if(!floating&&(!HasGround(nx+.5,ny,width)||!BodyClear(node.X+.5,Math.Max(node.Y,ny),width,height)||!BodyClear(nx+.5,Math.Max(node.Y,ny),width,height)))continue;
                    if(FluidAt(nx+.5,ny+.2,"lava")&&(string)mob["kind"]!="minecraft:strider")continue;
                    int cost=node.Cost+10+Math.Abs(dy)*4;string key=NativeRoom.Key(nx,ny);if(best.TryGetValue(key,out var old)&&old<=cost)continue;best[key]=cost;open.Add(new NavNode{X=nx,Y=ny,Cost=cost,Score=cost+(Math.Abs(nx-gx)+Math.Abs(ny-gy))*10,Parent=node});
                    if(!floating)break;
                }
            }
            var path=new List<JArray>();for(var node=closest;node.Parent!=null;node=node.Parent)path.Add(new JArray(node.X+.5,node.Y));path.Reverse();return new JArray(path);
        }
        private double Navigate(JObject mob,double tx,double ty,double speed) {
            if((bool?)mob["sitting"]==true)return 0;double px=Number(mob,"x"),py=Number(mob,"y");int id=(int)mob["entity"];
            if(tick%20==id%20||mob["path"]==null&&(bool?)mob["blocked"]==true){mob["path"]=FindPath(mob,tx,ty);mob["pathUntil"]=tick+40;}
            var path=mob["path"] as JArray;
            if(path!=null&&Number(mob,"pathUntil")>=tick) {
                while(path.Count>0&&Math.Abs((double)path[0][0]-px)<.22&&Math.Abs((double)path[0][1]-py)<.55)path.RemoveAt(0);
                if(path.Count>0){tx=(double)path[0][0];ty=(double)path[0][1];}
            }
            bool grounded=(bool?)mob["grounded"]==true;
            if(grounded&&ty>py+.4&&Math.Abs(tx-px)<1.7&&BodyClear(px,py+.6,Number(mob,"width",.6),Number(mob,"height",1.8)))mob["vy"]=.42;
            double direction=Math.Sign(tx-px);if(Math.Abs(tx-px)<.2)return 0;
            // Walkers without a valid route stop at ledges instead of suiciding.
            if(grounded&&NativeMobCatalog.Find((string)mob["kind"])?.Motion!="fly"&&NativeMobCatalog.Find((string)mob["kind"])?.Motion!="swim"&&!HasGround(px+direction*.7,py-.8,Number(mob,"width",.6))&&ty>=py-.8)return 0;
            return direction*Math.Abs(speed);
        }
    }
}
