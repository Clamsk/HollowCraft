using System;
using System.Linq;
using UnityEngine;

namespace HollowCraft
{
    internal static class NativeTerrainSampler
    {
        private static TerrainPoint[] World(Collider2D collider,Vector2[] points){return points.Select(p=>{var v=collider.transform.TransformPoint(p+collider.offset);return new TerrainPoint(v.x,v.y);}).ToArray();}
        internal static NativeTerrainRaster Read(Collider2D collider) {
            var result=new NativeTerrainRaster();
            if(collider is EdgeCollider2D edge){result.Edge(World(edge,edge.points),edge.edgeRadius*Math.Max(Math.Abs(edge.transform.lossyScale.x),Math.Abs(edge.transform.lossyScale.y)));return result;}
            if(collider is PolygonCollider2D polygon){for(int p=0;p<polygon.pathCount;p++)result.Polygon(World(polygon,polygon.GetPath(p)));return result;}
            if(collider is BoxCollider2D box){var size=box.size*.5f;result.Polygon(World(box,new[]{new Vector2(-size.x,-size.y),new Vector2(size.x,-size.y),new Vector2(size.x,size.y),new Vector2(-size.x,size.y)}));return result;}
            if(collider is CompositeCollider2D composite){for(int p=0;p<composite.pathCount;p++){var points=new Vector2[composite.GetPathPointCount(p)];composite.GetPath(p,points);if(composite.geometryType==CompositeCollider2D.GeometryType.Polygons)result.Polygon(World(composite,points));else result.Edge(World(composite,points),composite.edgeRadius,true);}return result;}
            if(collider is CircleCollider2D circle){result.Polygon(World(circle,Enumerable.Range(0,32).Select(i=>new Vector2(Mathf.Cos(i*Mathf.PI/16),Mathf.Sin(i*Mathf.PI/16))*circle.radius).ToArray()));return result;}
            var bounds=collider.bounds;result.Polygon(new[]{new TerrainPoint(bounds.min.x,bounds.min.y),new TerrainPoint(bounds.max.x,bounds.min.y),new TerrainPoint(bounds.max.x,bounds.max.y),new TerrainPoint(bounds.min.x,bounds.max.y)});return result;
        }
    }
}
