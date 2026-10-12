using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace HollowCraft
{
    [DefaultExecutionOrder(5000)]
    internal sealed class MinecraftEnvironment : MonoBehaviour
    {
        internal static bool Gliding;
        internal static bool Flying;
        internal static float? JumpOriginFeet;
        internal static Vector2 FlightVelocity;
        internal static Vector2? TestAim;
        private static MinecraftEnvironment instance;
        private HeroController hero;private Rigidbody2D body;private float gravity,jump,step;
        private float normalPhase,motionY,lastAppliedY;
        private float motionX,walkPhase,walkVelocity,lastAppliedX,fluidY,fluidPhase,fluidVelocity;
        private bool ownHorizontal,fluidActive;
        private readonly RaycastHit2D[] wallHits=new RaycastHit2D[16];
        private Collider2D movementCollider;
        private bool ownGravity;
        private float nextJump,lastJump;
        private float lastSpacePress=-1000,flightY,flightPhase,flightMoveY;
        private bool spectatorBody;private bool previousTrigger;private RigidbodyType2D previousBodyType;
        private int Effect(string id) {var effects=HollowCraftRuntime.Instance?.Stats?["effects"] as JArray;var effect=effects?.FirstOrDefault(e=>(string)e["id"]=="minecraft:"+id);return effect==null?-1:(int)effect["amplifier"];}
        private void Awake() {instance=this;}
        internal static void SetVerticalImpulse(float velocity) {if(instance!=null) {instance.motionY=velocity;instance.lastAppliedY=velocity*20;instance.normalPhase=0;instance.ownGravity=true;instance.fluidActive=false;}}
        internal static void StopMotion() {SetVerticalImpulse(0);Gliding=false;Flying=false;JumpOriginFeet=null;FlightVelocity=Vector2.zero;if(instance!=null) {instance.step=0;instance.nextJump=0;instance.lastSpacePress=-1000;instance.flightY=instance.flightPhase=0;instance.motionX=instance.walkPhase=instance.walkVelocity=instance.lastAppliedX=0;instance.fluidActive=false;}}
        internal static float? HorizontalSpeed=>instance!=null&&instance.ownHorizontal?instance.lastAppliedX:(float?)null;
        internal static void SceneChanged() {StopMotion();if(instance!=null) {instance.ownGravity=false;instance.ownHorizontal=false;}}
        internal static void SuspendNativeMotion() {
            if(instance==null)return;
            // Restore before AffectedByGravity(false) records prevGravityScale.
            // Otherwise HK records zero and cannot restore gravity on arrival.
            instance.Restore();SceneChanged();instance.hero=null;instance.body=null;instance.movementCollider=null;
        }
        internal static bool TakeJump(HeroController player)
        {
            var runtime=HollowCraftRuntime.Instance;
            if(instance==null||!MinecraftControls.Active||runtime==null) return false;
            if(runtime.GameplayBlocked||player.controlReqlinquished)return true;
            if(Flying||runtime.Spectator)return true;
            bool water=(bool?)runtime.Stats?["water"]==true,lava=(bool?)runtime.Stats?["lava"]==true;
            if((water||lava)&&((float?)runtime.Stats?[water?"waterHeight":"lavaHeight"]??1)>((float?)runtime.Stats?["swimHeight"]??.4f))return true;
            if(MinecraftControls.Grounded(player)&&Time.time>=instance.nextJump) {
                int boost=instance.Effect("jump_boost");float velocity=(float?)runtime.Stats?["jumpVelocity"]??.42f+(boost>=0?.1f*(boost+1):0);
                // Player-requested building assistance: the ordinary 20 Hz arc peaks
                // at 1.5 blocks (0.4634 impulse), while potion boost remains additive.
                float potion=boost>=0?.1f*(boost+1):0;velocity=(velocity-potion)*(.4634f/.42f)+potion;
                instance.nextJump=Time.time+.5f;
                instance.lastJump=Time.time;
                runtime.JumpBuild?.BeginJump(player);
                JumpOriginFeet=player.GetComponent<Collider2D>().bounds.min.y;
                instance.motionY=velocity;instance.normalPhase=0;instance.ownGravity=true;instance.lastAppliedY=velocity*20;
                instance.fluidActive=false;
                var rb=player.GetComponent<Rigidbody2D>();rb.gravityScale=0;rb.linearVelocity=new Vector2(rb.linearVelocity.x,velocity*20);
                if(MinecraftControls.Sprinting) {instance.motionX+=MinecraftControls.Facing*.2f;instance.walkVelocity+=MinecraftControls.Facing*.2f;instance.lastAppliedX=instance.walkVelocity*20;}
                player.cState.onGround=false;player.cState.jumping=false;player.cState.falling=false;player.cState.recoiling=false;
            }
            return true;
        }
        internal static bool BeginGlide(HeroController player)
        {
            var runtime=HollowCraftRuntime.Instance;
            if(instance==null||runtime==null||runtime.Dead||MinecraftControls.Grounded(player)||(bool?)runtime.Stats?["water"]==true||(bool?)runtime.Stats?["lava"]==true||(string)runtime.Stats?["equipment"]?[1]?["id"]!="minecraft:elytra") return false;
            Gliding=true;FlightVelocity=player.GetComponent<Rigidbody2D>().linearVelocity/20;if(Math.Abs(FlightVelocity.x)<.1f) FlightVelocity.x=MinecraftControls.Facing*.3f;return true;
        }
        private void Update()
        {
            var runtime=HollowCraftRuntime.Instance;if(runtime==null||!runtime.InGame) return;
            var current=HeroController.instance;if(current==null) return;
            if(hero!=current) {Restore();hero=current;body=hero.GetComponent<Rigidbody2D>();movementCollider=hero.GetComponent<Collider2D>();gravity=hero.DEFAULT_GRAVITY;jump=hero.JUMP_SPEED;Gliding=Flying=false;JumpOriginFeet=null;lastSpacePress=-1000;ownHorizontal=false;fluidActive=false;walkPhase=0;}
            if(!runtime.Creative&&Flying)SetFlying(false);
            if(runtime.SimulationPaused) {lastSpacePress=-1000;if(runtime.Dead)Gliding=Flying=false;return;}
            bool test=Environment.GetEnvironmentVariable("HOLLOWCRAFT_SMOKE")=="1";
            if(!test&&Application.isFocused&&!runtime.GameplayBlocked) {
                if(runtime.Creative&&((bool?)runtime.Stats?["allowFlying"]??true)&&Input.GetKeyDown(KeyCode.Space)) {
                    if(Time.unscaledTime-lastSpacePress<=.35f) {SetFlying(!Flying);lastSpacePress=-1000;}
                    else lastSpacePress=Time.unscaledTime;
                }
                if(Flying&&Time.time-lastJump>.08f&&MinecraftControls.Grounded(hero)&&!Input.GetKey(KeyCode.Space))SetFlying(false);
                if(JumpOriginFeet.HasValue&&Time.time-lastJump>.08f&&MinecraftControls.Grounded(hero))JumpOriginFeet=null;
                bool buildJump=runtime.JumpBuild?.AutoJump==true;
                if(!Input.GetKey(KeyCode.Space)&&!buildJump)nextJump=0;
                else if(!Flying&&MinecraftControls.Grounded(hero))TakeJump(hero);
                if(!Flying&&Input.GetKeyDown(KeyCode.Space)&&!MinecraftControls.Grounded(hero)&&Time.time-lastJump>.08f) BeginGlide(hero);
                if(Input.GetKeyDown(KeyCode.LeftShift))Gliding=false;
            } else lastSpacePress=-1000;
            int boost=Effect("jump_boost");hero.JUMP_SPEED=jump*(boost>=0?(.42f+.1f*(boost+1))/.42f:1);
            if(MinecraftControls.Grounded(hero)||(bool?)runtime.Stats?["water"]==true||(string)runtime.Stats?["equipment"]?[1]?["id"]!="minecraft:elytra") Gliding=false;
        }
        private void FixedUpdate()
        {
            var runtime=HollowCraftRuntime.Instance;if(hero==null||body==null||runtime==null||!runtime.InGame||runtime.SimulationPaused) return;
            if(runtime.NativeControlLocked) {ownHorizontal=false;return;}
            if(runtime.Spectator) {
                var collider=hero.GetComponent<Collider2D>();
                if(!spectatorBody) {previousTrigger=collider.isTrigger;previousBodyType=body.bodyType;collider.isTrigger=true;body.bodyType=RigidbodyType2D.Kinematic;spectatorBody=true;Gliding=false;ownHorizontal=false;}
                float x=Input.GetKey(KeyCode.A)?-1:Input.GetKey(KeyCode.D)?1:0,y=Input.GetKey(KeyCode.Space)?1:Input.GetKey(KeyCode.LeftShift)||Input.GetKey(KeyCode.RightShift)?-1:0;
                body.gravityScale=0;body.linearVelocity=runtime.GameplayBlocked||!Application.isFocused?Vector2.zero:new Vector2(x,y)*(Input.GetKey(KeyCode.LeftControl)?15:10);return;
            }
            if(spectatorBody) {hero.GetComponent<Collider2D>().isTrigger=previousTrigger;body.bodyType=previousBodyType;spectatorBody=false;body.linearVelocity=Vector2.zero;ownGravity=false;}
            PlayerSystem.DecayKnockback(Time.fixedDeltaTime,MinecraftControls.Grounded(hero));
            bool water=(bool?)runtime.Stats?["water"] ?? false,lava=(bool?)runtime.Stats?["lava"]??false,climbing=(bool?)runtime.Stats?["climbing"] ?? false;
            int levitation=Effect("levitation");
            HorizontalStep(runtime,water,lava);
            if(Flying) {
                Gliding=false;fluidActive=false;body.gravityScale=0;
                bool input=Application.isFocused&&!runtime.GameplayBlocked;
                float vertical=input?((Input.GetKey(KeyCode.Space)?1:0)-(Input.GetKey(KeyCode.LeftShift)||Input.GetKey(KeyCode.RightShift)?1:0)):0;
                float speed=(float?)runtime.Stats?["flySpeed"]??.05f;
                float remaining=Time.fixedDeltaTime,displacement=0;
                if(flightY>0&&TouchingCeiling())flightY=0;
                if(flightY<0&&MinecraftControls.Grounded(hero))flightY=0;
                while(remaining>.00001f) {
                    if(flightPhase<.00001f)flightMoveY=flightY+vertical*speed*3;
                    float segment=Math.Min(remaining,.05f-flightPhase);displacement+=flightMoveY*20*segment;remaining-=segment;flightPhase+=segment;
                    if(flightPhase>=.04999f) {flightPhase=0;flightY=flightMoveY*.6f;}
                }
                lastAppliedY=displacement/Time.fixedDeltaTime;body.linearVelocity=new Vector2(body.linearVelocity.x,lastAppliedY);motionY=flightY;normalPhase=0;ownGravity=true;
            } else if(Gliding) {
                body.gravityScale=0;step+=Time.fixedDeltaTime;
                while(step>=.05f) {step-=.05f;Vector2 target=TestAim??(Vector2.right*MinecraftControls.Facing);
                    if(TestAim==null&&GameCursor.WorldTarget(hero,out Vector2 cursor,out _)) target=(cursor-(Vector2)hero.transform.position).normalized;
                    FlightVelocity=StepFlight(FlightVelocity,target,(bool?)runtime.Stats?["rocket"] ?? false);
                }
                // HK's contacts still resolve collision; only the vanilla gliding velocity replaces locomotion.
                body.linearVelocity=FlightVelocity*20;
            } else if((water||lava)&&levitation<0) {
                if(!fluidActive) {fluidY=body.linearVelocity.y/20;fluidPhase=0;fluidActive=true;}
                body.gravityScale=0;float remaining=Time.fixedDeltaTime,displacement=0;
                while(remaining>.00001f) {
                    if(fluidPhase<.00001f) {if(Application.isFocused&&!runtime.GameplayBlocked&&Input.GetKey(KeyCode.Space))fluidY+=.04f;fluidVelocity=fluidY;}
                    float segment=Math.Min(remaining,.05f-fluidPhase);displacement+=fluidVelocity*20*segment;remaining-=segment;fluidPhase+=segment;
                    if(fluidPhase>=.04999f) {fluidPhase=0;float gravityStep=Effect("slow_falling")>=0&&fluidY<=0?.01f:.08f;bool shallow=((float?)runtime.Stats?["lavaHeight"]??1)<=((float?)runtime.Stats?["swimHeight"]??.4f);fluidY=fluidVelocity*(water?.8f:shallow?.8f:.5f)-gravityStep/(water?16:4);}
                }
                lastAppliedY=displacement/Time.fixedDeltaTime;body.linearVelocity=new Vector2(body.linearVelocity.x,lastAppliedY);motionY=fluidY;normalPhase=0;ownGravity=true;
            } else if(climbing||levitation>=0) {
                fluidActive=false;
                body.gravityScale=0;var velocity=body.linearVelocity;
                if(levitation>=0) velocity.y=Mathf.Lerp(velocity.y,(levitation+1),1-Mathf.Pow(.8f,Time.fixedDeltaTime/.05f));
                else if(climbing) velocity.y=Input.GetKey(KeyCode.W)||Input.GetKey(KeyCode.Space)?3:Input.GetKey(KeyCode.S)?-3:Math.Max(-3,velocity.y);
                else {velocity.y=Mathf.Max(-2,velocity.y-Time.fixedDeltaTime*3);if(Input.GetKey(KeyCode.Space)) velocity.y=3.5f;}
                body.linearVelocity=velocity;
            } else {
                fluidActive=false;
                // Integrate each 20 Hz vanilla gravity step across Unity's physics intervals.
                // This preserves the 1.25-block jump without the Knight's held-jump acceleration.
                // Keep the MC motion accumulator across native animation updates. Only
                // contacts and explicit MC knockback may stop or replace a jump.
                if(!ownGravity) {motionY=body.linearVelocity.y/20;normalPhase=0;ownGravity=true;}
                // HK can zero Y during its own animation/state update. Only an actual
                // ceiling contact may cancel the MC ascent; otherwise a full jump is lost.
                if(motionY>0&&TouchingCeiling()) {motionY=0;normalPhase=0;}
                if(MinecraftControls.Grounded(hero)&&motionY<0) {motionY=0;normalPhase=0;}
                float remaining=Time.fixedDeltaTime,displacement=0;
                while(remaining>.00001f) {
                    float segment=Math.Min(remaining,.05f-normalPhase);displacement+=motionY*20*segment;remaining-=segment;normalPhase+=segment;
                    if(normalPhase>=.04999f) {normalPhase=0;motionY=(motionY-(Effect("slow_falling")>=0&&motionY<0?.01f:.08f))*.98f;}
                }
                body.gravityScale=0;lastAppliedY=displacement/Time.fixedDeltaTime;body.linearVelocity=new Vector2(body.linearVelocity.x,lastAppliedY);step=0;
            }
        }
        private void HorizontalStep(HollowCraftRuntime runtime,bool water,bool lava) {
            if(Gliding) {ownHorizontal=false;return;}
            if(!ownHorizontal) {motionX=body.linearVelocity.x/20;walkVelocity=motionX;walkPhase=0;ownHorizontal=true;}
            bool grounded=MinecraftControls.Grounded(hero),sprint=MinecraftControls.Sprinting;
            float input=MinecraftControls.TestMovement??(Application.isFocused&&!runtime.GameplayBlocked?(Input.GetKey(KeyCode.A)?-1:0)+(Input.GetKey(KeyCode.D)?1:0):0);
            if(MinecraftControls.Crouching&&!Flying)input*=.3f;
            if((bool?)runtime.Stats?["using"]==true)input*=.2f;
            input=Mathf.Clamp(input,-1,1)*.98f;
            float speed=(float?)runtime.Stats?["speed"]??.1f;
            speed=speed/((bool?)runtime.Stats?["sprinting"]==true?1.3f:1)*(sprint?1.3f:1);
            float friction=Mathf.Clamp((float?)runtime.Stats?["slipperiness"]??.6f,.1f,1),drag=grounded?friction*.91f:.91f;
            float acceleration=grounded?speed*.21600002f/(friction*friction*friction):sprint?.026f:.02f;
            if(Flying) {drag=.91f;acceleration=((float?)runtime.Stats?["flySpeed"]??.05f)*(sprint?2:1);}
            else if(water) {float depth=Math.Min(3,(int?)runtime.Stats?["depthStrider"]??0)*(grounded?1:.5f)/3;drag=Mathf.Lerp(sprint?.9f:.8f,.54600006f,depth);acceleration=Mathf.Lerp(.02f,speed,depth);if(Effect("dolphins_grace")>=0)drag=.96f;}
            else if(lava) {drag=.5f;acceleration=.02f;}
            float multiplier=Flying?1:Mathf.Clamp((float?)runtime.Stats?["velocityMultiplier"]??1,0,4);
            float remaining=Time.fixedDeltaTime,displacement=0;
            while(remaining>.00001f) {
                if(walkPhase<.00001f) {if(Math.Abs(motionX)<.003f)motionX=0;walkVelocity=motionX+acceleration*input;}
                float segment=Math.Min(remaining,.05f-walkPhase);displacement+=walkVelocity*20*segment;remaining-=segment;walkPhase+=segment;
                if(walkPhase>=.04999f) {walkPhase=0;motionX=walkVelocity*drag*multiplier;}
            }
            if(PlayerSystem.KnockbackActive) {motionX=walkVelocity=PlayerSystem.KnockbackX/20;walkPhase=0;lastAppliedX=PlayerSystem.KnockbackX;}
            else lastAppliedX=displacement/Time.fixedDeltaTime;
            float safe=MinecraftControls.SafeHorizontalSpeed(lastAppliedX);
            if(movementCollider!=null&&Math.Abs(safe)>.01f) {
                var filter=new ContactFilter2D();filter.SetLayerMask(LayerMask.GetMask("Terrain","Default"));filter.useTriggers=false;
                int count=movementCollider.Cast(Vector2.right*Math.Sign(safe),filter,wallHits,.002f);
                for(int i=0;i<count;i++)if(wallHits[i].collider!=null&&wallHits[i].collider.GetComponentInParent<HeroController>()==null&&wallHits[i].normal.x*Math.Sign(safe)<-.5f) {safe=0;break;}
            }
            if(safe!=lastAppliedX) {motionX=walkVelocity=0;walkPhase=0;lastAppliedX=safe;}
            body.linearVelocity=new Vector2(lastAppliedX,body.linearVelocity.y);
        }
        private bool TouchingCeiling() {
            if(movementCollider==null)return false;
            var filter=new ContactFilter2D();filter.SetLayerMask(LayerMask.GetMask("Terrain","Default"));filter.useTriggers=false;
            int count=movementCollider.Cast(Vector2.up,filter,wallHits,.025f);
            float head=movementCollider.bounds.max.y;
            // Casts can report an initial floor overlap with a synthetic downward
            // normal. Require a hit at head height, so takeoff cannot look like a ceiling.
            for(int i=0;i<count;i++)if(wallHits[i].collider!=null&&Math.Abs(wallHits[i].point.y-head)<.055f&&wallHits[i].collider.GetComponentInParent<HeroController>()==null&&wallHits[i].collider.GetComponentInParent<HealthManager>()==null&&wallHits[i].normal.y<-.5f)return true;
            return false;
        }
        private void SetFlying(bool value) {
            Flying=value;Gliding=false;JumpOriginFeet=null;flightPhase=0;flightY=value?body.linearVelocity.y/20:0;
            if(!value) {motionY=flightY=0;normalPhase=0;ownGravity=true;body.linearVelocity=new Vector2(body.linearVelocity.x,0);}
            HollowCraftRuntime.Instance?.Send(new {type="flight",scene=HollowCraftRuntime.Instance.Room,flying=value});
        }
        // Minecraft 1.20.1 LivingEntity.travel's elytra equations projected onto the XY plane, at 20 Hz.
        internal static Vector2 StepFlight(Vector2 velocity,Vector2 look,bool rocket)
        {
            if(look.sqrMagnitude<.001f) look=Vector2.right;look.Normalize();float horizontal=Math.Abs(look.x),speed=Math.Abs(velocity.x),cos2=horizontal*horizontal;
            velocity.y+=.08f*(-1+.75f*cos2);
            if(velocity.y<0&&horizontal>0) {float lift=velocity.y*-.1f*cos2;velocity+=new Vector2(Math.Sign(look.x)*lift,lift);}
            if(look.y>0&&horizontal>0) {float climb=speed*look.y*.04f;velocity+=new Vector2(-Math.Sign(look.x)*climb,climb*3.2f);}
            if(horizontal>0) velocity.x+=(Math.Sign(look.x)*speed-velocity.x)*.1f;
            if(rocket) velocity+=look*.1f+(look*1.5f-velocity)*.5f;
            return new Vector2(velocity.x*.99f,velocity.y*.98f);
        }
        private void Restore() {if(hero!=null) {hero.JUMP_SPEED=jump;if(spectatorBody)hero.GetComponent<Collider2D>().isTrigger=previousTrigger;}if(body!=null) {body.gravityScale=gravity;if(spectatorBody)body.bodyType=previousBodyType;}spectatorBody=false;}
        private void OnDestroy() {Restore();Gliding=Flying=false;JumpOriginFeet=null;instance=null;TestAim=null;}
    }
}
