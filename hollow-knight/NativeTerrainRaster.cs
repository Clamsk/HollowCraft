using System;
using System.Collections.Generic;
using System.Linq;

namespace HollowCraft
{
    internal struct TerrainPoint {internal double X,Y;internal TerrainPoint(double x,double y){X=x;Y=y;}}
    // Edges have no filled interior. Never infer solid space from their bounds
    // or from ClosestPoint, which can invert the floor into a ceiling.
    internal sealed class NativeTerrainRaster
    {
        private sealed class Contour {internal TerrainPoint[] Points;internal double Radius;internal bool Filled,Closed;}
        private readonly List<Contour> contours=new List<Contour>();
        private readonly Dictionary<int,List<double[]>> columns=new Dictionary<int,List<double[]>>();
        internal void Polygon(TerrainPoint[] points){if(points.Length>=3)contours.Add(new Contour{Points=points,Filled=true});}
        internal void Edge(TerrainPoint[] points,double radius=.006,bool closed=false){if(points.Length>=2)contours.Add(new Contour{Points=points,Radius=Math.Max(.006,radius),Closed=closed});}
        private static List<double> Crossings(TerrainPoint[] p,double x) {
            var result=new List<double>();for(int i=0;i<p.Length;i++){var a=p[i];var b=p[(i+1)%p.Length];if((a.X<=x&&b.X>x)||(b.X<=x&&a.X>x))result.Add(a.Y+(b.Y-a.Y)*(x-a.X)/(b.X-a.X));}result.Sort();return result;
        }
        private List<double[]> Column(int x) {
            if(columns.TryGetValue(x,out var cached))return cached;var result=new List<double[]>();const int slices=16;
            foreach(var contour in contours)for(int slice=0;slice<slices;slice++){
                double left=x+slice/(double)slices,right=x+(slice+1)/(double)slices;
                if(contour.Filled){
                    left=Math.Max(left,contour.Points.Min(p=>p.X));right=Math.Min(right,contour.Points.Max(p=>p.X));if(right-left<.0000001)continue;
                    var middle=Crossings(contour.Points,(left+right)/2);var lo=Crossings(contour.Points,left+.0000001);var hi=Crossings(contour.Points,right-.0000001);
                    for(int i=0;i+1<middle.Count;i+=2){double bottom=middle[i],top=middle[i+1];if(lo.Count==middle.Count){bottom=Math.Min(bottom,lo[i]);top=Math.Max(top,lo[i+1]);}if(hi.Count==middle.Count){bottom=Math.Min(bottom,hi[i]);top=Math.Max(top,hi[i+1]);}result.Add(new[]{left,bottom,right,top});}
                }else for(int i=0;i<contour.Points.Length-(contour.Closed?0:1);i++){
                    var a=contour.Points[i];var b=contour.Points[(i+1)%contour.Points.Length];double r=contour.Radius;
                    double x0=Math.Max(left,Math.Min(a.X,b.X)-r),x1=Math.Min(right,Math.Max(a.X,b.X)+r);if(x1<=x0)continue;double y0,y1;
                    if(Math.Abs(a.X-b.X)<.0000001){y0=Math.Min(a.Y,b.Y)-r;y1=Math.Max(a.Y,b.Y)+r;}
                    else {double t0=Math.Max(0,Math.Min(1,(x0-a.X)/(b.X-a.X))),t1=Math.Max(0,Math.Min(1,(x1-a.X)/(b.X-a.X)));double ya=a.Y+(b.Y-a.Y)*t0,yb=a.Y+(b.Y-a.Y)*t1;y0=Math.Min(ya,yb)-r;y1=Math.Max(ya,yb)+r;}
                    result.Add(new[]{x0,y0,x1,y1});
                }
            }columns[x]=result;return result;
        }
        internal List<double[]> Cell(int x,int y) {
            var result=new List<double[]>();foreach(var r in Column(x)){
                double bottom=Math.Max(y,r[1]),top=Math.Min(y+1,r[3]);if(top-bottom<.000001)continue;var next=new[]{Math.Max(0,r[0]-x),bottom-y,Math.Min(1,r[2]-x),top-y};
                var merge=result.FirstOrDefault(p=>Math.Abs(p[2]-next[0])<.000001&&Math.Abs(p[1]-next[1])<.000001&&Math.Abs(p[3]-next[3])<.000001);if(merge!=null)merge[2]=next[2];else result.Add(next);
            }return result;
        }
        internal static bool Covers(IEnumerable<double[]> rectangles,int axis,double start=0,double end=1) {
            double covered=start;foreach(var r in rectangles.OrderBy(r=>r[axis])){if(r[axis]>covered+.00001)return false;covered=Math.Max(covered,r[axis+2]);if(covered>=end-.00001)return true;}return false;
        }
        internal static bool Full(IEnumerable<double[]> rectangles) {
            var data=rectangles.ToArray();if(data.Length==0)return false;var cuts=data.SelectMany(r=>new[]{r[0],r[2]}).Concat(new[]{0.0,1}).Distinct().OrderBy(v=>v).ToArray();
            for(int i=0;i+1<cuts.Length;i++){double x=(cuts[i]+cuts[i+1])/2;if(x<0||x>1||cuts[i+1]-cuts[i]<.000001)continue;if(!Covers(data.Where(r=>r[0]<=x&&r[2]>=x),1))return false;}return true;
        }
    }
}
