using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace HollowCraft
{
    internal static class EnemySystem
    {
        private sealed class Adapter {internal HealthManager Enemy;internal Breakable Breakable;internal bool Resource,Rewarded;internal int NativeMax,LastNative,Generation;internal float Max,Health;}
        private static readonly Dictionary<int,Adapter> enemies=new Dictionary<int,Adapter>();
        private static readonly FieldInfo brokenField=typeof(Breakable).GetField("isBroken",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
        private static readonly FieldInfo evasionField=typeof(HealthManager).GetField("evasionByHitRemaining",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
        private static readonly FieldInfo specialDeathField=typeof(HealthManager).GetField("hasSpecialDeath",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
        // Six points per iron-sword hit: three normal hits for ordinary enemies.
        // Tough enemies and bosses retain extra health above the common 60 native HP band.
        private static float ConvertedHealth(int native) => Math.Min(1024,18+Math.Max(0,native-60)*.6f);
        internal static void Clear() {enemies.Clear();}
        internal static int Register(HealthManager enemy) {
            int id=enemy.GetInstanceID();
            if(!enemies.ContainsKey(id)) {
                float max=ConvertedHealth(enemy.hp);
                enemies[id]=new Adapter {Enemy=enemy,NativeMax=Math.Max(1,enemy.hp),LastNative=enemy.hp,Max=max,Health=max};
            }
            return id;
        }
        internal static int Register(Breakable value) {
            int id=value.GetInstanceID();if(!enemies.ContainsKey(id)) enemies[id]=new Adapter {Breakable=value,Resource=true,NativeMax=1,LastNative=1,Max=1,Health=1};return id;
        }
        internal static void ShieldRecoil(int id,Vector2 velocity) {
            if(!enemies.TryGetValue(id,out Adapter adapter)||adapter.Enemy==null||adapter.Enemy.isDead)return;
            var body=adapter.Enemy.GetComponent<Rigidbody2D>();
            if(body!=null&&body.bodyType==RigidbodyType2D.Dynamic&&body.constraints!=RigidbodyConstraints2D.FreezeAll)
                body.linearVelocity+=velocity*20;
        }
        internal static object[] Observe(HeroController hero,int required=0) {
            foreach(var pair in enemies) {var entry=pair.Value;if(entry.Health<=0&&!entry.Rewarded&&(entry.Resource ? entry.Breakable==null||!entry.Breakable.gameObject.activeInHierarchy||(bool)(brokenField?.GetValue(entry.Breakable)??false) : entry.Enemy==null||entry.Enemy.isDead)) {entry.Rewarded=true;HollowCraftRuntime.Instance.Send(new {type="nativeDeath",scene=HollowCraftRuntime.Instance.Room,id=pair.Key,generation=entry.Generation});}}
            var observed=new HashSet<int>();var result=new List<object>();
            foreach(var collider in Physics2D.OverlapCircleAll(hero.transform.position,20)) {
                var enemy=collider.GetComponentInParent<HealthManager>();if(enemy==null || enemy.isDead || enemy.hp<=0) continue;
                int id=Register(enemy);if(!observed.Add(id)) continue;
                var body=enemy.GetComponent<Collider2D>()??collider;Vector3 point=body.bounds.center;
                var adapter=enemies[id];
                if(enemy.hp>adapter.LastNative) {adapter.NativeMax=Math.Max(adapter.NativeMax,enemy.hp);adapter.Max=ConvertedHealth(adapter.NativeMax);adapter.Health=adapter.Max*enemy.hp/adapter.NativeMax;adapter.Generation++;}
                adapter.LastNative=enemy.hp;
                string kind=enemy.name.IndexOf("ghost",StringComparison.OrdinalIgnoreCase)>=0||enemy.name.IndexOf("shade",StringComparison.OrdinalIgnoreCase)>=0 ? "undead":"arthropod";
                result.Add(new {id,maxHealth=adapter.Max,health=adapter.Health,generation=adapter.Generation,kind,name=enemy.name,x=point.x,y=body!=null ? body.bounds.min.y : point.y,width=body!=null ? body.bounds.size.x:.6f,height=body!=null ? body.bounds.size.y:1.95f,invulnerable=enemy.IsInvincible});
            }
            foreach(var collider in Physics2D.OverlapCircleAll(hero.transform.position,12)) {
                var breakable=collider.GetComponentInParent<Breakable>();if(breakable==null || !breakable.gameObject.activeInHierarchy) continue;
                int id=Register(breakable);if(!observed.Add(id)) continue;
                result.Add(new {id,maxHealth=1,health=enemies[id].Health,generation=0,kind="resource",name=breakable.name,x=breakable.transform.position.x,y=collider.bounds.min.y,width=collider.bounds.size.x,height=collider.bounds.size.y});
            }
            if(required!=0&&!observed.Contains(required)&&enemies.TryGetValue(required,out Adapter extra)) {
                var actor=extra.Resource?extra.Breakable?.gameObject:extra.Enemy?.gameObject;
                if(actor!=null&&actor.activeInHierarchy&&(extra.Resource||!extra.Enemy.isDead)) {
                    var body=actor.GetComponent<Collider2D>()??actor.GetComponentInChildren<Collider2D>();
                    if(body!=null) {
                        string kind=extra.Resource?"resource":actor.name.IndexOf("ghost",StringComparison.OrdinalIgnoreCase)>=0||actor.name.IndexOf("shade",StringComparison.OrdinalIgnoreCase)>=0?"undead":"arthropod";
                        result.Add(new {id=required,maxHealth=extra.Max,health=extra.Health,generation=extra.Generation,kind,name=actor.name,x=body.bounds.center.x,y=body.bounds.min.y,width=body.bounds.size.x,height=body.bounds.size.y,invulnerable=extra.Enemy!=null&&extra.Enemy.IsInvincible});
                    }
                }
            }
            return result.ToArray();
        }
        internal static void Apply(JArray values,HeroController hero) {
            if(values==null || hero==null) return;
            foreach(JObject value in values) {
                int id=(int)value["id"];if(!enemies.TryGetValue(id,out Adapter adapter)) continue;
                if(((int?)value["generation"] ?? 0)!=adapter.Generation) continue;
                if(adapter.Enemy!=null&&!adapter.Enemy.isDead) {var effect=adapter.Enemy.GetComponent<NativeMobEffects>()??adapter.Enemy.gameObject.AddComponent<NativeMobEffects>();effect.Factor=Mathf.Clamp((float?)value["moveFactor"] ?? 1,.01f,4);effect.Burning=(bool?)value["onFire"] ?? false;}
                float health=(float)value["health"];
                if(health>adapter.Health+.001f && adapter.Enemy!=null&&!adapter.Enemy.isDead) {adapter.Enemy.hp=Mathf.CeilToInt(health/adapter.Max*adapter.NativeMax);adapter.LastNative=adapter.Enemy.hp;adapter.Health=health;continue;}
                // A last hit can change a tiny positive float to exactly zero. Always
                // forward that death even when the change is below the normal tolerance.
                if(health>=adapter.Health-.001f && !(health<=0 && adapter.Health>0)) continue;
                if(Math.Abs((float)value["maxHealth"]-adapter.Max)>.001f) continue;
                if(adapter.Breakable!=null) {
                    if(health<=0) {string source=(string)value["damageKind"];bool blast=source=="explosion"||source=="explosion.player"||source=="fireworks";var type=blast?AttackTypes.Spell:AttackTypes.Nail;var descriptor=HollowCraftRuntime.Instance.Combat.NativeDamageSource(hero,999,0,type);HitTaker.Hit(adapter.Breakable.gameObject,new HitInstance {Source=descriptor,AttackType=type,DamageDealt=999,Multiplier=1,IgnoreInvulnerable=false},3);adapter.Health=health;}
                    if(health<=0&&!adapter.Breakable.gameObject.activeInHierarchy) HollowCraftRuntime.Instance.Send(new {type="nativeDeath",scene=HollowCraftRuntime.Instance.Room,id,generation=adapter.Generation});
                    continue;
                }
                if(adapter.Enemy==null || adapter.Enemy.isDead) continue;
                var enemy=adapter.Enemy;int before=enemy.hp;int desired=Mathf.CeilToInt(Math.Max(0,health)/adapter.Max*adapter.NativeMax);
                int damage=Math.Max(0,enemy.hp-desired);adapter.Health=health;
                if(damage<=0) continue;
                Vector2 direction=((Vector2)enemy.transform.position-(Vector2)hero.transform.position).normalized;
                string kind=(string)value["damageKind"];
                bool ranged=kind=="arrow"||kind=="trident"||kind=="thrown"||kind=="indirectMagic"||kind=="magic"||kind=="explosion.player"||kind=="explosion"||kind=="fireworks";
                if(!ranged&&direction.sqrMagnitude<.000001f)direction=HollowCraftRuntime.Instance.Combat.Aim;
                var attackType=ranged?AttackTypes.Spell:AttackTypes.Nail;float angle=Mathf.Atan2(direction.y,direction.x)*Mathf.Rad2Deg;
                var sourceObject=HollowCraftRuntime.Instance.Combat.NativeDamageSource(hero,damage,angle,attackType);
                // MC already accepted this hit using its cooldown and hurt immunity.
                // Clear only HK's extra evasion timer; scripted shields/phases still go through Hit.
                evasionField?.SetValue(enemy,0f);
                enemy.Hit(new HitInstance {Source=sourceObject,AttackType=attackType,DamageDealt=damage,Multiplier=1,
                    Direction=angle,MagnitudeMultiplier=1.2f,IgnoreInvulnerable=true});
                // Native recoil timers must not heal an already accepted MC hit. A genuine
                // boss phase refill increases HP above the pre-hit value and stays native-owned.
                bool specialDeath=(bool)(specialDeathField?.GetValue(enemy)??true);
                if(!enemy.isDead&&!enemy.IsInvincible&&enemy.hp>desired&&enemy.hp<=before&&(desired>0||!specialDeath)) {
                    enemy.hp=desired;
                    // A rejected/one-point native lethal hit used to refill the MC proxy
                    // as if every ordinary enemy had a new boss phase, making it immortal.
                    if(desired==0)enemy.Die(angle,attackType,true);
                }
                Debug.Log("[HollowCraft] Native combat id="+id+" name="+enemy.name+" MC="+health+" native="+before+"->"+enemy.hp+" dead="+enemy.isDead+" blocked="+enemy.IsInvincible);
                adapter.LastNative=enemy.hp;
                if(health<=0&&enemy.isDead) HollowCraftRuntime.Instance.Send(new {type="nativeDeath",scene=HollowCraftRuntime.Instance.Room,id,generation=adapter.Generation});
                if(!enemy.isDead&&(enemy.hp>before||health<=0&&enemy.hp>0)) {adapter.Health=adapter.Max*enemy.hp/adapter.NativeMax;adapter.Generation++;}
                var body=enemy.GetComponent<Rigidbody2D>();if(body!=null && !enemy.isDead && body.bodyType==RigidbodyType2D.Dynamic) body.linearVelocity+=direction*3;
            }
        }
    }
}
