using System;
using UnityEngine;

namespace HollowCraft
{
    // MC terrain is an approximation. Check the native player hull before applying a teleport.
    internal static class NativeLanding
    {
        private static Collider2D[] overlaps=new Collider2D[32];
        internal static Vector2 CurrentFeet(HeroController hero) => new Vector2(hero.transform.position.x,hero.GetComponent<Collider2D>().bounds.min.y);
        internal static Vector2 Resolve(HeroController hero,Vector2 requested,Vector2 normal)
        {
            var body=hero.GetComponent<Collider2D>();
            var size=new Vector2(body.bounds.size.x,body.bounds.size.y);
            if(Clear(requested,size))return requested;
            if(normal.sqrMagnitude<.001f)normal=Vector2.up;else normal.Normalize();
            for(int step=1;step<=30;step++) {
                var candidate=requested+normal*(step*.1f);
                if(Clear(candidate,size))return candidate;
            }
            // Do not search through a wall or silently move to its far side.
            return CurrentFeet(hero);
        }
        internal static Vector2 FromSurface(HeroController hero,Vector2 point,Vector2 normal)
        {
            var size=hero.GetComponent<Collider2D>().bounds.size;
            var feet=point+new Vector2(normal.x*(size.x*.5f+.03f),normal.y>0?.03f:normal.y<0?-size.y-.03f:0);
            return Resolve(hero,feet,normal);
        }
        private static bool Clear(Vector2 feet,Vector2 size)
        {
            if(float.IsNaN(feet.x)||float.IsNaN(feet.y)||float.IsInfinity(feet.x)||float.IsInfinity(feet.y)||Math.Abs(feet.x)>400||feet.y<-60||feet.y>240)return false;
            var filter=new ContactFilter2D();filter.SetLayerMask(LayerMask.GetMask("Terrain","Default"));filter.useTriggers=false;
            int count;
            do {
                count=Physics2D.OverlapBox(feet+Vector2.up*size.y*.5f,size*.98f,0,filter,overlaps);
                if(count<overlaps.Length)break;
                if(overlaps.Length>=256)return false;
                Array.Resize(ref overlaps,overlaps.Length*2);
            } while(true);
            for(int i=0;i<count;i++) {
                var collider=overlaps[i];
                if(collider!=null&&collider.GetComponentInParent<HeroController>()==null&&collider.GetComponentInParent<HealthManager>()==null)return false;
            }
            return true;
        }
    }
}
