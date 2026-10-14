using System;
using UnityEngine;
using GlobalEnums;

namespace HollowCraft
{
    public static class PlayerSystem
    {
        public static bool SuppressKnightJumpRelease() => MinecraftControls.Active && HollowCraftRuntime.Instance?.InGame==true && HeroController.instance?.controlReqlinquished!=true;
        private static float nextHud;
        private static HeroController hero;
        private static Vector3 spawn;
        private static bool hasSpawn,wasDead;
        private static float hurtUntil,knockUntil,knockX,lastHealth=-1;
        private static float heartsUntil;
        private static int lastDamageSource;
        private static float nextDamageLog;
        internal static float PreviousHealth {get;private set;}
        internal static bool HeartFlash=>Time.time<heartsUntil&&Mathf.CeilToInt((heartsUntil-Time.time)*20/3)%2==1;
        internal static bool Hurt => Time.time<hurtUntil;
        internal static bool KnockbackActive => Time.time<knockUntil;
        internal static float KnockbackX => knockX;
        internal static void EnterScene(HeroController value) {hero=value;spawn=value.transform.position;hasSpawn=false;wasDead=false;lastHealth=-1;hurtUntil=knockUntil=0;}
        internal static void EntryPositionChanged(HeroController value) {hero=value;spawn=value.transform.position;hasSpawn=MinecraftControls.Grounded(value);}
        internal static void Tick(HeroController value)
        {
            hero=value;var runtime=HollowCraftRuntime.Instance;if(runtime==null) return;
            float health=(float?)runtime.Stats?["health"] ?? -1;
            if(lastHealth>=0 && health<lastHealth) {PreviousHealth=lastHealth;heartsUntil=Time.time+1;if(Time.time>=hurtUntil)HurtFeedback((int?)runtime.Stats?["hurtTicks"]??10);}
            lastHealth=health;
            if(!hasSpawn && hero.cState.onGround) {spawn=hero.transform.position;hasSpawn=true;}
            if(wasDead && !runtime.Dead) {
                hero.GetComponent<Rigidbody2D>().linearVelocity=Vector2.zero;
                MinecraftEnvironment.StopMotion();
                if(!BossRecording.Respawn()&&!CampaignBridge.Respawn()) {hero.transform.position=spawn;hero.RegainControl();hero.AcceptInput();}
            }
            wasDead=runtime.Dead;
            if(runtime.Dead) {hero.GetComponent<Rigidbody2D>().linearVelocity=Vector2.zero;return;}
            if(hero.transform.position.y<-60 && hasSpawn) {runtime.Send(new {type="damage",scene=runtime.Room,amount=40});hero.transform.position=spawn;}
            if(Time.unscaledTime>=nextHud)
            {
                nextHud=Time.unscaledTime+0.25f;
                var cameras=UnityEngine.Object.FindFirstObjectByType<GameCameras>();
                if(cameras!=null && cameras.hudCamera!=null) cameras.hudCamera.enabled=false;
                // Story acquisition flags belong to the campaign. Movement hooks suppress Knight
                // abilities without erasing the flags used by cutscenes and world progression.
            }
        }
        // Return true to replace the Knight's masks, soul, damage/death and charm processing.
        public static bool RouteDamage(GameObject source,CollisionSide side,int amount,int hazardType)
        {
            var runtime=HollowCraftRuntime.Instance;if(runtime==null) return false;
            if(amount<=0 || runtime.Dead || runtime.Creative || runtime.Spectator) return true;
            var nativeHero=HeroController.instance;
            if(nativeHero!=null)hero=nativeHero;
            if(hero==null)return true;
            // Preserve native transition/story immunity before replacing mask damage with MC damage.
            if(nativeHero!=null) {
                if(nativeHero.damageMode==DamageMode.NO_DAMAGE||nativeHero.transitionState!=HeroTransitionState.WAITING_TO_TRANSITION
                    ||nativeHero.cState.dead||nativeHero.cState.hazardDeath||PlayerData.instance?.isInvincible==true||BossSceneController.IsTransitioning)return true;
                if(hazardType==1&&(nativeHero.damageMode==DamageMode.HAZARD_ONLY||nativeHero.cState.invulnerable||nativeHero.cState.recoiling
                    ||nativeHero.cState.shadowDashing||nativeHero.parryInvulnTimer>0))return true;
            }
            // TakeDamage uses 1 for ordinary contact; 2+ are spikes, acid, lava and pits.
            var enemy=source!=null ? source.GetComponentInParent<HealthManager>() : null;
            int sourceId=source!=null?source.GetInstanceID():0;
            if(sourceId!=lastDamageSource||Time.unscaledTime>=nextDamageLog) {
                lastDamageSource=sourceId;nextDamageLog=Time.unscaledTime+2;
                Debug.Log("[HollowCraft] Native damage source="+(source!=null?source.name:"script")+" amount="+amount+" hazard="+hazardType+" enemy="+(enemy!=null));
            }
            var point=source!=null ? source.transform.position : hero.transform.position+Vector3.left;
            var velocity=hero!=null?hero.GetComponent<Rigidbody2D>().linearVelocity/20:Vector2.zero;
            int enemyId=enemy!=null?EnemySystem.Register(enemy):0;
            // Contact can arrive before the next pose update discovers a spawned enemy.
            // Include its geometry now so vanilla shields have an actual attacker source.
            runtime.Send(new {type="damage",scene=runtime.Room,amount=Math.Max(1,amount)*3,kind=hazardType>=2 ? "hazard" : enemy==null ? "projectile":"mob",enemy=enemyId,
                enemies=EnemySystem.Observe(hero,enemyId),attackerX=point.x,attackerY=point.y,velocityX=velocity.x,velocityY=velocity.y});
            if(hero!=null)
            {
                var body=hero.GetComponent<Rigidbody2D>();
                if(hazardType>=2 && hasSpawn) {hero.transform.position=spawn;if(body!=null) body.linearVelocity=Vector2.zero;MinecraftEnvironment.StopMotion();}
            }
            return true;
        }
        internal static void HurtFeedback(int ticks) {hurtUntil=Math.Max(hurtUntil,Time.time+Mathf.Clamp(ticks,0,10)*.05f);}
        internal static void ApplyImpulse(HeroController player,Vector2 velocity) {
            if(!HollowCraftRuntime.Instance.InGame||HollowCraftRuntime.Instance.Spectator||HollowCraftRuntime.Instance.Dead)return;hero=player;var body=player.GetComponent<Rigidbody2D>();
            if(body==null||body.bodyType!=RigidbodyType2D.Dynamic)return;
            // Vanilla already halves previous horizontal velocity and caps grounded lift at .4.
            // Replace with that result once; adding it used to multiply repeated impacts.
            knockUntil=Time.time+.25f;knockX=velocity.x*20;body.linearVelocity=new Vector2(knockX,velocity.y*20);
            MinecraftEnvironment.SetVerticalImpulse(velocity.y);
        }
        internal static void DecayKnockback(float elapsed,bool grounded) {if(KnockbackActive)knockX*=Mathf.Pow(grounded?.546f:.91f,elapsed/.05f);}
        public static bool SuppressKnightResources() => HollowCraftRuntime.Instance!=null;
        public static bool SuppressKnightAbilities() => MinecraftControls.Active;
        public static bool MinecraftJump(HeroController player) => MinecraftEnvironment.TakeJump(player);
        public static bool SuppressKnightLanding() => MinecraftControls.Active&&!CampaignSession.IntroPending;
        public static void BenchCheckpoint() => CampaignBridge.BenchCheckpoint();
    }
}
