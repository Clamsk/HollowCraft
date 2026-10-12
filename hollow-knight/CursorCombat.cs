using System;
using System.Collections.Generic;
using System.Reflection;
using HutongGames.PlayMaker;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace HollowCraft
{
    internal sealed class CursorCombat
    {
        internal JObject Profile;
        internal JObject Current {get;private set;}
        internal float Progress => Current==null ? 1 : Mathf.Clamp01((Time.time-started)/(float)Current["duration"]);
        internal Vector2 Aim {get;private set;}=Vector2.right;
        internal bool Swinging => Current!=null && Progress<1;
        internal int AnimationSerial {get;private set;}
        private float started;
        private bool struck;
        private readonly Action<object> send;
        private string room;
        private int predictedCombo;
        private readonly Queue<Vector2> queuedClicks=new Queue<Vector2>();
        private int requestId;
        private bool awaiting;
        private float nextRetry;
        private bool Ready => !awaiting&&!Swinging&&(Current==null||struck);
        private string predictedWeapon;
        private float lastRequest;
        private BoxCollider2D nativeSlash;
        private static readonly FieldInfo damageFsmField=typeof(PlayMakerFSM).GetField("fsm",BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public);
        private void DamageDescriptor(int damage,float direction,AttackTypes attack=AttackTypes.Nail) {
            var fsm=FSMUtility.LocateFSM(nativeSlash.gameObject,"damages_enemy");
            if(fsm==null) {
                // ReceivedDamage gates read this FSM, rather than HitInstance or
                // the nail tag alone. Keep it inert: it only describes this MC hit.
                nativeSlash.gameObject.SetActive(false);
                fsm=nativeSlash.gameObject.AddComponent<PlayMakerFSM>();
                damageFsmField.SetValue(fsm,new Fsm());fsm.Fsm.Reset(fsm);fsm.FsmName="damages_enemy";
                fsm.Fsm.Variables=new FsmVariables {
                    IntVariables=new[]{new FsmInt {Name="attackType",Value=(int)AttackTypes.Nail},new FsmInt {Name="damageDealt",Value=damage}},
                    FloatVariables=new[]{new FsmFloat {Name="direction",Value=direction}}
                };
                fsm.Fsm.States=new[]{new FsmState(fsm.Fsm) {Name="Idle"}};fsm.Fsm.StartState="Idle";
                // FindFsmOnGameObject searches PlayMaker's enabled FSM registry.
                // Disabling this inert descriptor made native grass/poles miss it.
                fsm.enabled=true;nativeSlash.gameObject.SetActive(true);
            }
            fsm.FsmVariables.GetFsmInt("attackType").Value=(int)attack;
            fsm.FsmVariables.GetFsmInt("damageDealt").Value=damage;
            fsm.FsmVariables.GetFsmFloat("direction").Value=direction;
        }
        internal GameObject NativeDamageSource(HeroController hero,int damage,float direction,AttackTypes attack) {
            if(nativeSlash==null) {
                var source=new GameObject("HollowCraft.NativeAttackDescriptor");source.tag="Nail Attack";source.layer=2;
                UnityEngine.Object.DontDestroyOnLoad(source);
                nativeSlash=source.AddComponent<BoxCollider2D>();nativeSlash.isTrigger=true;nativeSlash.size=Vector2.one*.05f;nativeSlash.enabled=false;
            }
            DamageDescriptor(damage,direction,attack);nativeSlash.transform.position=hero.transform.position;
            return nativeSlash.gameObject;
        }
        internal CursorCombat(Action<object> sender) {send=sender;}
        internal void Begin(JObject message,string scene)
        {
            if(message["request"]!=null&&(int)message["request"]!=requestId)return;
            if((int?)message["attack"]>0&&(int?)Current?["attack"]==(int?)message["attack"])return;
            if((int?)message["attack"]>0)awaiting=false;
            bool predicted=Current!=null && (int?)Current["attack"]==0;
            Current=message;if(!predicted) {started=Time.time;AnimationSerial++;}struck=false;room=scene;
            Aim=new Vector2((float)message["aimX"],(float)message["aimY"]).normalized;
            if(Aim.sqrMagnitude<0.5f) Aim=Vector2.right;
        }
        internal void Cancel() {
            if((Current!=null&&!struck||awaiting)&&!string.IsNullOrEmpty(room))send(new {type="cancelAttack",scene=room,request=requestId});
            Current=null;struck=true;awaiting=false;queuedClicks.Clear();requestId++;
        }
        internal void Reject(JObject message) {
            if((int?)message["request"]!=requestId)return;
            awaiting=false;Current=null;struck=true;
        }
        internal bool HasEnemy(HeroController hero,Vector2 mouse)
        {
            float range=GameCursor.AttackReach;var eye=GameCursor.Eye(hero);
            foreach(var collider in Physics2D.OverlapCircleAll(mouse,0.25f))
                if(collider.GetComponentInParent<HealthManager>()!=null && Vector2.Distance(eye,collider.ClosestPoint(eye))<=range) return true;
            return false;
        }
        internal void Request(string scene,HeroController hero,Vector2 mouse,bool queueClick=false)
        {
            if(HollowCraftRuntime.Instance.Spectator)return;
            if((bool?)HollowCraftRuntime.Instance?.Stats?["using"]==true) {
                if(!queueClick)return;
                send(new {type="itemUse",scene,cancel=true});
            }
            // A prediction that has finished still waits for its matching server impact.
            // Otherwise its late acknowledgement could overwrite the next click's animation.
            Vector2 aim=mouse.x<hero.transform.position.x?Vector2.left:Vector2.right;
            if(!Ready) {if(queueClick)queuedClicks.Enqueue(aim);return;}
            room=scene;
            MinecraftControls.Facing=aim.x<0?-1:1;MinecraftControls.frontView=false;
            var moves=Profile?["weapon"]?["attacks"] as JArray;
            string weapon=(string)Profile?["item"]??"";
            if(weapon!=predictedWeapon||Time.time-lastRequest>1.5f)predictedCombo=0;
            predictedWeapon=weapon;lastRequest=Time.time;requestId++;
            if(moves!=null && moves.Count>0) {
                var move=moves[predictedCombo++%moves.Count];
                Begin(new JObject { ["attack"]=0,["duration"]=1/Math.Max(.2,(float?)Profile["speed"] ?? 4),["upswing"]=(float?)move["upswing"] ?? .3f,
                    ["range"]=GameCursor.AttackReach,["damage"]=(float?)Profile["damage"] ?? 1,["move"]=move.DeepClone(),["aimX"]=aim.x,["aimY"]=aim.y},scene);
            }
            Aim=aim;awaiting=true;nextRetry=Time.unscaledTime+.4f;
            send(new {type="swing",scene,request=requestId,aimX=aim.x,aimY=aim.y});
        }
        internal void Tick(HeroController hero)
        {
            if(awaiting&&Time.unscaledTime>=nextRetry) {
                nextRetry=Time.unscaledTime+.4f;
                send(new {type="swing",scene=room,request=requestId,aimX=Aim.x,aimY=Aim.y});
            }
            if(Ready&&queuedClicks.Count>0&&!HollowCraftRuntime.Instance.GameplayBlocked) {
                var side=queuedClicks.Dequeue();Request(room,hero,(Vector2)hero.transform.position+side,true);
            }
            if(Current==null || (int?)Current["attack"]==0 || struck || Progress<(float)Current["upswing"]) return;
            struck=true;
            float range=(float)Current["range"];
            Vector2 origin=GameCursor.Eye(hero);
            var playerCollider=hero.GetComponent<Collider2D>();
            var hit=new HashSet<GameObject>();
            var enemies=new HashSet<int>();bool terrainHit=false;
            foreach(var collider in Physics2D.OverlapCircleAll(origin,range))
            {
                var enemy=collider.GetComponentInParent<HealthManager>();
                if(collider.transform.IsChildOf(hero.transform) || collider.name.StartsWith("HollowCraft.Block")) continue;
                var breakable=collider.GetComponentInParent<Breakable>();
                GameObject target=enemy!=null ? enemy.gameObject : breakable!=null ? breakable.gameObject : collider.gameObject;
                if((enemy!=null && enemy.isDead) || hit.Contains(target)) continue;
                Vector2 point=collider.ClosestPoint(origin), delta=point-origin;
                // The swing covers the player's actual shape as well as the chosen side.
                // A flyer sharing that space must remain hittable from either direction.
                bool overlapping=false;
                if(enemy!=null && playerCollider!=null && playerCollider.enabled) {
                    var contact=playerCollider.Distance(collider);
                    overlapping=contact.isValid && (contact.isOverlapped || contact.distance<=0);
                }
                if(!overlapping) {
                    if(delta.x*Aim.x<-.02f) continue;
                    // ClosestPoint returns the origin when inside a target. A zero-length
                    // ray must not manufacture an obstruction at its starting point.
                    if(delta.sqrMagnitude>.000001f) {
                        var wall=Physics2D.Raycast(origin,delta.normalized,delta.magnitude,LayerMask.GetMask("Terrain"));
                        if(wall && wall.collider!=collider && !wall.transform.IsChildOf(target.transform)) continue;
                    }
                }
                hit.Add(target);
                if(enemy!=null) {enemies.Add(EnemySystem.Register(enemy));continue;}
                if(breakable!=null) {enemies.Add(EnemySystem.Register(breakable));terrainHit=true;continue;}
                NativeDamageSource(hero,Math.Max(1,Mathf.RoundToInt((float)Current["damage"])),Mathf.Atan2(Aim.y,Aim.x)*Mathf.Rad2Deg,AttackTypes.Nail);
                nativeSlash.transform.position=new Vector3(point.x,point.y,hero.transform.position.z);nativeSlash.enabled=true;
                var instance=new HitInstance {Source=nativeSlash.gameObject,AttackType=AttackTypes.Nail,
                    DamageDealt=Math.Max(1,Mathf.RoundToInt((float)Current["damage"])),
                    Direction=Mathf.Atan2(Aim.y,Aim.x)*Mathf.Rad2Deg,
                    MagnitudeMultiplier=1.2f,Multiplier=1,IgnoreInvulnerable=false};
                HitTaker.Hit(collider.gameObject,instance,3);
                // Trigger-based grass/props require the original nail tag as well as HitTaker.
                collider.gameObject.SendMessage("OnTriggerEnter2D",nativeSlash,SendMessageOptions.DontRequireReceiver);
                nativeSlash.enabled=false;
                FSMUtility.SendEventToGameObject(target,"TAKE DAMAGE",false);
                if(enemy!=null && !enemy.isDead) {
                    var body=enemy.GetComponent<Rigidbody2D>();
                    if(body!=null && body.bodyType==RigidbodyType2D.Dynamic && body.constraints!=RigidbodyConstraints2D.FreezeAll)
                        body.linearVelocity+=Aim*3;
                }
            }
            var heroBody=hero.GetComponent<Rigidbody2D>();
            send(new {type="hit",scene=room,attack=(int)Current["attack"],targets=new List<int>(enemies),terrain=terrainHit,
                playerX=hero.transform.position.x,playerY=hero.GetComponent<Collider2D>().bounds.min.y,grounded=MinecraftControls.Grounded(hero),
                enemies=EnemySystem.Observe(hero),
                falling=!MinecraftControls.Grounded(hero) && heroBody!=null && heroBody.linearVelocity.y<-.1f && !MinecraftControls.Sprinting});
        }
    }
}
