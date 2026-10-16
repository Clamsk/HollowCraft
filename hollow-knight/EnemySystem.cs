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
        private sealed class Adapter {internal HealthManager Enemy;internal Breakable Breakable;internal Collider2D Body;internal bool Resource,Rewarded;internal int NativeMax,LastNative,Generation,BlockSerial,ContactSerial;internal float Max,Health;}
        private static readonly Dictionary<int,Adapter> enemies=new Dictionary<int,Adapter>();
        private static readonly HashSet<int> nearby=new HashSet<int>();
        private static Collider2D[] discovery=new Collider2D[128];
        private static float nextDiscovery;
        private static readonly FieldInfo brokenField=typeof(Breakable).GetField("isBroken",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
        private static readonly FieldInfo evasionField=typeof(HealthManager).GetField("evasionByHitRemaining",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
        private static readonly FieldInfo specialDeathField=typeof(HealthManager).GetField("hasSpecialDeath",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
        // Six points per iron-sword hit: three normal hits for ordinary enemies.
        // Tough enemies and bosses retain extra health above the common 60 native HP band.
        private static float ConvertedHealth(int native) => Math.Min(1024,18+Math.Max(0,native-60)*.6f);
        internal static void Clear() {enemies.Clear();nearby.Clear();nextDiscovery=0;NativeBattle.Clear();NativeBossPhases.Clear();NativeScriptedAttacks.Clear();NativeFallTracker.Reset();BedrockBarrier.Clear();WorldCreatures.Clear();}
        internal static GameObject Actor(int id)=>enemies.TryGetValue(id,out var adapter)?adapter.Enemy!=null?adapter.Enemy.gameObject:adapter.Breakable!=null?adapter.Breakable.gameObject:null:null;
        internal static Collider2D Body(int id)=>enemies.TryGetValue(id,out var adapter)?adapter.Body:null;
        // Match HitTaker's nearest responder, but never pick a disabled phase component.
        internal static HealthManager Resolve(Collider2D collider) {
            for(var node=collider.transform;node!=null;node=node.parent)
                foreach(var enemy in node.GetComponents<HealthManager>())
                    if(!enemy.isDead&&enemy.hp>0&&(enemy.enabled||NativeScriptedAttacks.NeedsNailContact(enemy)))return enemy;
            return null;
        }
        private static int BlockMask(HealthManager enemy,AttackTypes type) {
            int mask=0;for(int direction=0;direction<4;direction++)if(enemy.IsBlockingByDirection(direction,type))mask|=1<<direction;return mask;
        }
        private static object Entry(int id,Adapter adapter,Collider2D body) {
            var enemy=adapter.Enemy;
            var actor=enemy!=null?enemy.gameObject:adapter.Breakable.gameObject;
            return new {id,maxHealth=adapter.Max,health=adapter.Health,generation=adapter.Generation,
                kind=adapter.Resource?"resource":enemy.name.IndexOf("ghost",StringComparison.OrdinalIgnoreCase)>=0||enemy.name.IndexOf("shade",StringComparison.OrdinalIgnoreCase)>=0?"undead":"arthropod",
                name=adapter.Resource?adapter.Breakable.name:enemy.name,actor=body.attachedRigidbody!=null?body.attachedRigidbody.transform.GetInstanceID():actor.GetInstanceID(),x=body.bounds.center.x,y=body.bounds.min.y,width=body.bounds.size.x,height=body.bounds.size.y,
                phaseFloor=enemy!=null?Math.Max(0,(NativeBossPhases.Floor(enemy,adapter.NativeMax)-.01f)/adapter.NativeMax*adapter.Max):0,
                nativeHurtTicks=enemy!=null&&NativeBossPhases.Phased(enemy)?4:0,nativeContact=enemy!=null&&NativeScriptedAttacks.NeedsNailContact(enemy),
                nailBlock=enemy!=null?BlockMask(enemy,AttackTypes.Nail):0,spellBlock=enemy!=null?BlockMask(enemy,AttackTypes.Spell):0};
        }
        internal static int Register(HealthManager enemy) {
            int id=enemy.GetInstanceID();
            if(!enemies.ContainsKey(id)) {
                NativeBattle.Track(enemy);BedrockBarrier.Track(enemy);
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
        internal static object[] Observe(HeroController hero,int required=0,bool discover=false) {
            foreach(var pair in enemies) {var entry=pair.Value;if(entry.Health<=0&&!entry.Rewarded&&(entry.Resource ? entry.Breakable==null||!entry.Breakable.gameObject.activeInHierarchy||(bool)(brokenField?.GetValue(entry.Breakable)??false) : entry.Enemy==null||entry.Enemy.isDead)) {entry.Rewarded=true;HollowCraftRuntime.Instance.Send(new {type="nativeDeath",scene=HollowCraftRuntime.Instance.Room,id=pair.Key,generation=entry.Generation});}}
            // Discovery is independent of the 20 Hz transform/guard stream. Attacks force
            // immediate discovery, so a newly spawned brood cannot miss its first hit.
            if(Time.unscaledTime>=nextDiscovery||required!=0||discover) {
                nextDiscovery=Time.unscaledTime+.15f;nearby.Clear();var filter=new ContactFilter2D().NoFilter();
                float radius=HollowCraftRuntime.Instance?.HeldItem==TerrariaEquipment.Zenith?48:20;
                int count=Physics2D.OverlapCircle(hero.transform.position,radius,filter,discovery);
                while(count==discovery.Length&&discovery.Length<4096) {Array.Resize(ref discovery,discovery.Length*2);count=Physics2D.OverlapCircle(hero.transform.position,radius,filter,discovery);}
                for(int i=0;i<count;i++) {
                    var collider=discovery[i];if(collider==null)continue;
                    var enemy=Resolve(collider);
                    if(enemy!=null) {int id=Register(enemy);if(nearby.Add(id))enemies[id].Body=enemy.GetComponents<Collider2D>().FirstOrDefault(c=>c.enabled&&c.gameObject.activeInHierarchy)??collider;continue;}
                    if(((Vector2)collider.bounds.ClosestPoint(hero.transform.position)-(Vector2)hero.transform.position).sqrMagnitude>144)continue;
                    var value=collider.GetComponentInParent<Breakable>();if(value==null||!value.gameObject.activeInHierarchy)continue;
                    int key=Register(value);if(nearby.Add(key))enemies[key].Body=collider;
                }
                Array.Clear(discovery,0,count);
            }
            var observed=new HashSet<int>(nearby);if(required!=0)observed.Add(required);
            foreach(var pair in enemies)if(pair.Value.Enemy!=null&&NativeScriptedAttacks.NeedsNailContact(pair.Value.Enemy))observed.Add(pair.Key);
            foreach(int id in WorldCreatures.Captured)observed.Add(id);
            var result=new List<object>(observed.Count);
            foreach(int id in observed) {
                if(!enemies.TryGetValue(id,out var adapter))continue;
                var actor=adapter.Resource?adapter.Breakable?.gameObject:adapter.Enemy?.gameObject;
                if(actor==null||!actor.activeInHierarchy||!adapter.Resource&&((!adapter.Enemy.enabled&&!NativeScriptedAttacks.NeedsNailContact(adapter.Enemy))||adapter.Enemy.isDead||adapter.Enemy.hp<=0))continue;
                var body=adapter.Body;
                if(body==null||!body.enabled||!body.gameObject.activeInHierarchy)adapter.Body=body=actor.GetComponentsInChildren<Collider2D>().FirstOrDefault(c=>c.enabled&&c.gameObject.activeInHierarchy&&(adapter.Resource||Resolve(c)==adapter.Enemy));
                if(body==null)continue;
                if(!adapter.Resource) {
                    var enemy=adapter.Enemy;
                    if(enemy.hp>adapter.LastNative||adapter.Health<=0) {adapter.NativeMax=Math.Max(adapter.NativeMax,enemy.hp);adapter.Max=ConvertedHealth(adapter.NativeMax);adapter.Health=adapter.Max*enemy.hp/adapter.NativeMax;adapter.Generation++;adapter.BlockSerial=adapter.ContactSerial=0;}
                    adapter.LastNative=enemy.hp;
                }
                result.Add(Entry(id,adapter,body));
            }
            result.AddRange(WorldCreatures.FriendlyActors(hero));return result.ToArray();
        }
        internal static void Apply(JArray values,HeroController hero) {
            if(values==null || hero==null) return;
            foreach(JObject value in values) {
                int id=(int)value["id"];if(!enemies.TryGetValue(id,out Adapter adapter)) continue;
                if(((int?)value["generation"] ?? 0)!=adapter.Generation) continue;
                int contactSerial=(int?)value["contactSerial"]??0;
                if(contactSerial>adapter.ContactSerial) {
                    adapter.ContactSerial=contactSerial;
                    NativeScriptedAttacks.NailContact(adapter.Enemy,hero,(float?)value["contactAngle"]??0);
                }
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
                int floor=NativeBossPhases.Floor(enemy,adapter.NativeMax);bool gated=desired<floor;desired=Math.Max(desired,floor);
                int damage=Math.Max(0,enemy.hp-desired);adapter.Health=health;
                if(damage<=0) {
                    // A stale guard packet may let MC spend HP while the native
                    // phase is already waiting. Reconcile even when no native hit
                    // is delivered; otherwise both sides keep different HP forever.
                    if(gated) {adapter.Health=adapter.Max*enemy.hp/adapter.NativeMax;adapter.Generation++;adapter.BlockSerial=adapter.ContactSerial=0;}
                    continue;
                }
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
                Debug.Log("[HollowCraft] Native combat id="+id+" name="+enemy.name+" MC="+health+" native="+before+"->"+enemy.hp+" dead="+enemy.isDead+" blocked="+blocked+" phaseFloor="+floor);
                NativeBossPhases.Damaged(enemy,before);
                adapter.LastNative=enemy.hp;
                if(health<=0&&enemy.isDead) HollowCraftRuntime.Instance.Send(new {type="nativeDeath",scene=HollowCraftRuntime.Instance.Room,id,generation=adapter.Generation});
                if(!enemy.isDead&&(gated||blocked||enemy.hp>before||health<=0&&enemy.hp>0)) {adapter.Health=adapter.Max*enemy.hp/adapter.NativeMax;adapter.Generation++;adapter.BlockSerial=adapter.ContactSerial=0;}
                var body=enemy.GetComponent<Rigidbody2D>();if(body!=null && !enemy.isDead && body.bodyType==RigidbodyType2D.Dynamic) body.linearVelocity+=direction*3;
            }
        }
    }
}
