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
        private sealed class Adapter {internal HealthManager Enemy;internal Breakable Breakable;internal bool Resource,Rewarded;internal int NativeMax,LastNative,Generation,BlockSerial;internal float Max,Health;}
        private static readonly Dictionary<int,Adapter> enemies=new Dictionary<int,Adapter>();
        private static readonly FieldInfo brokenField=typeof(Breakable).GetField("isBroken",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
        private static readonly FieldInfo evasionField=typeof(HealthManager).GetField("evasionByHitRemaining",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
        private static readonly FieldInfo specialDeathField=typeof(HealthManager).GetField("hasSpecialDeath",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
        // Six points per iron-sword hit: three normal hits for ordinary enemies.
        // Tough enemies and bosses retain extra health above the common 60 native HP band.
        private static float ConvertedHealth(int native) => Math.Min(1024,18+Math.Max(0,native-60)*.6f);
        internal static void Clear() {enemies.Clear();NativeBattle.Clear();NativeFallTracker.Reset();}
        // Match HitTaker's nearest responder, but never pick a disabled phase component.
        internal static HealthManager Resolve(Collider2D collider) {
            for(var node=collider.transform;node!=null;node=node.parent)
                foreach(var enemy in node.GetComponents<HealthManager>())
                    if(enemy.enabled&&!enemy.isDead&&enemy.hp>0)return enemy;
            return null;
        }
        private static int BlockMask(HealthManager enemy,AttackTypes type) {
            int mask=0;for(int direction=0;direction<4;direction++)if(enemy.IsBlockingByDirection(direction,type))mask|=1<<direction;return mask;
        }
        private static object Entry(int id,Adapter adapter,Collider2D body) {
            var enemy=adapter.Enemy;
            return new {id,maxHealth=adapter.Max,health=adapter.Health,generation=adapter.Generation,
                kind=adapter.Resource?"resource":enemy.name.IndexOf("ghost",StringComparison.OrdinalIgnoreCase)>=0||enemy.name.IndexOf("shade",StringComparison.OrdinalIgnoreCase)>=0?"undead":"arthropod",
                name=adapter.Resource?adapter.Breakable.name:enemy.name,x=body.bounds.center.x,y=body.bounds.min.y,width=body.bounds.size.x,height=body.bounds.size.y,
                nailBlock=enemy!=null?BlockMask(enemy,AttackTypes.Nail):0,spellBlock=enemy!=null?BlockMask(enemy,AttackTypes.Spell):0};
        }
        internal static int Register(HealthManager enemy) {
            NativeBattle.Track(enemy);
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
                var enemy=Resolve(collider);if(enemy==null) continue;
                int id=Register(enemy);if(!observed.Add(id)) continue;
                var body=enemy.GetComponents<Collider2D>().FirstOrDefault(c=>c.enabled&&c.gameObject.activeInHierarchy)??collider;
                var adapter=enemies[id];
                if(enemy.hp>adapter.LastNative||adapter.Health<=0) {adapter.NativeMax=Math.Max(adapter.NativeMax,enemy.hp);adapter.Max=ConvertedHealth(adapter.NativeMax);adapter.Health=adapter.Max*enemy.hp/adapter.NativeMax;adapter.Generation++;adapter.BlockSerial=0;}
                adapter.LastNative=enemy.hp;
                result.Add(Entry(id,adapter,body));
            }
            foreach(var collider in Physics2D.OverlapCircleAll(hero.transform.position,12)) {
                var breakable=collider.GetComponentInParent<Breakable>();if(breakable==null || !breakable.gameObject.activeInHierarchy) continue;
                int id=Register(breakable);if(!observed.Add(id)) continue;
                result.Add(new {id,maxHealth=1,health=enemies[id].Health,generation=0,kind="resource",name=breakable.name,x=breakable.transform.position.x,y=collider.bounds.min.y,width=collider.bounds.size.x,height=collider.bounds.size.y});
            }
            if(required!=0&&!observed.Contains(required)&&enemies.TryGetValue(required,out Adapter extra)) {
                var actor=extra.Resource?extra.Breakable?.gameObject:extra.Enemy?.gameObject;
                if(actor!=null&&actor.activeInHierarchy&&(extra.Resource||extra.Enemy.enabled&&!extra.Enemy.isDead&&extra.Enemy.hp>0)) {
                    var body=actor.GetComponentsInChildren<Collider2D>().FirstOrDefault(c=>c.enabled&&c.gameObject.activeInHierarchy&&(extra.Resource||Resolve(c)==extra.Enemy));
                    if(body!=null) {
                        result.Add(Entry(required,extra,body));
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
                int blockSerial=(int?)value["blockSerial"]??0;
                if(blockSerial>adapter.BlockSerial&&adapter.Enemy!=null&&!adapter.Enemy.isDead) {
                    adapter.BlockSerial=blockSerial;float blockedAngle=(float?)value["blockedAngle"]??0;
                    var blockedType=(bool?)value["blockedSpell"]==true?AttackTypes.Spell:AttackTypes.Nail;
                    if(adapter.Enemy.IsBlockingByDirection(DirectionUtils.GetCardinalDirection(blockedAngle),blockedType)) {
                        evasionField?.SetValue(adapter.Enemy,0f);
                        adapter.Enemy.Hit(new HitInstance {Source=HollowCraftRuntime.Instance.Combat.NativeDamageSource(hero,1,blockedAngle,blockedType),
                            AttackType=blockedType,DamageDealt=1,Multiplier=1,Direction=blockedAngle});
                    }
                }
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
                bool ranged=kind=="arrow"||kind=="trident"||kind=="thrown"||kind=="indirectMagic"||kind=="magic"||kind=="explosion.player"||kind=="explosion"||kind=="fireworks"||kind=="inFire"||kind=="onFire"||kind=="lava"||kind=="hotFloor"||kind=="wither";
                if(!ranged)direction=HollowCraftRuntime.Instance.Combat.Aim;
                var attackType=ranged?AttackTypes.Spell:AttackTypes.Nail;
                float angle=(float?)value["damageAngle"]??Mathf.Atan2(direction.y,direction.x)*Mathf.Rad2Deg;
                direction=new Vector2(Mathf.Cos(angle*Mathf.Deg2Rad),Mathf.Sin(angle*Mathf.Deg2Rad));
                var sourceObject=HollowCraftRuntime.Instance.Combat.NativeDamageSource(hero,damage,angle,attackType);
                // MC already accepted this hit using its cooldown and hurt immunity.
                // Clear only HK's extra evasion timer; scripted shields/phases still go through Hit.
                evasionField?.SetValue(enemy,0f);
                enemy.Hit(new HitInstance {Source=sourceObject,AttackType=attackType,DamageDealt=damage,Multiplier=1,
                    Direction=angle,MagnitudeMultiplier=1.2f,IgnoreInvulnerable=true});
                // Native recoil timers must not heal an already accepted MC hit. A genuine
                // boss phase refill increases HP above the pre-hit value and stays native-owned.
                bool specialDeath=(bool)(specialDeathField?.GetValue(enemy)??true);
                bool blocked=enemy.IsBlockingByDirection(DirectionUtils.GetCardinalDirection(angle),attackType);
                if(!enemy.isDead&&!blocked&&enemy.hp>desired&&enemy.hp<=before&&(desired>0||!specialDeath)) {
                    enemy.hp=desired;
                    // A rejected/one-point native lethal hit used to refill the MC proxy
                    // as if every ordinary enemy had a new boss phase, making it immortal.
                    if(desired==0)enemy.Die(angle,attackType,true);
                }
                Debug.Log("[HollowCraft] Native combat id="+id+" name="+enemy.name+" MC="+health+" native="+before+"->"+enemy.hp+" dead="+enemy.isDead+" blocked="+blocked);
                adapter.LastNative=enemy.hp;
                if(health<=0&&enemy.isDead) HollowCraftRuntime.Instance.Send(new {type="nativeDeath",scene=HollowCraftRuntime.Instance.Room,id,generation=adapter.Generation});
                if(!enemy.isDead&&(enemy.hp>before||health<=0&&enemy.hp>0)) {adapter.Health=adapter.Max*enemy.hp/adapter.NativeMax;adapter.Generation++;adapter.BlockSerial=0;}
                var body=enemy.GetComponent<Rigidbody2D>();if(body!=null && !enemy.isDead && body.bodyType==RigidbodyType2D.Dynamic) body.linearVelocity+=direction*3;
            }
        }
    }
}
