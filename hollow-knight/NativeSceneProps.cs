using System.Collections.Generic;
using HutongGames.PlayMaker;
using UnityEngine;

namespace HollowCraft
{
    internal static class NativeSceneProps
    {
        private static readonly HashSet<string> cuttable=new HashSet<string> {
            "Grass","GrassCut","GrassBehaviour","GrassSpriteBehaviour","TownGrass",
            "Breakable","BreakablePole","BreakablePoleSimple","BreakableObject","BreakableInfectedVine"
        };
        internal static GameObject Find(Collider2D collider) {
            for(var node=collider.transform;node!=null;node=node.parent) {
                if(node.GetComponent<HealthManager>()!=null)return null;
                foreach(var component in node.GetComponents<MonoBehaviour>())
                    if(component!=null&&cuttable.Contains(component.GetType().Name))return node.gameObject;
            }
            return null;
        }
        internal static void Strike(Collider2D collider,GameObject prop,HeroController hero,Vector2 motion) {
            float angle=Mathf.Atan2(motion.y,motion.x)*Mathf.Rad2Deg;
            var source=HollowCraftRuntime.Instance.Combat.NativeDamageSource(hero,1,angle,AttackTypes.Nail);
            source.transform.position=collider.ClosestPoint(hero.transform.position);
            var trigger=source.GetComponent<BoxCollider2D>();trigger.enabled=true;
            try {
                HitTaker.Hit(collider.gameObject,new HitInstance {Source=source,AttackType=AttackTypes.Nail,DamageDealt=1,Multiplier=1,Direction=angle},3);
                collider.gameObject.SendMessage("OnTriggerEnter2D",trigger,SendMessageOptions.DontRequireReceiver);
                if(prop!=collider.gameObject)prop.SendMessage("OnTriggerEnter2D",trigger,SendMessageOptions.DontRequireReceiver);
                FSMUtility.SendEventToGameObject(prop,"TAKE DAMAGE",false);
            } finally {trigger.enabled=false;}
        }
    }
}
