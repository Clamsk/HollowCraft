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
        internal static Vector2 FlightVelocity;
        internal static Vector2? TestAim;
        private static MinecraftEnvironment instance;
        private HeroController hero;private Rigidbody2D body;private float gravity,jump,step;
        private float normalPhase,motionY,lastAppliedY;
        private bool ownGravity;
        private float nextJump,lastJump;
        private bool spectatorBody;private bool previousTrigger;private RigidbodyType2D previousBodyType;
        private int Effect(string id) {var effects=HollowCraftRuntime.Instance?.Stats?["effects"] as JArray;var effect=effects?.FirstOrDefault(e=>(string)e["id"]=="minecraft:"+id);return effect==null?-1:(int)effect["amplifier"];}
        private void Awake() {instance=this;}
        internal static bool TakeJump(HeroController player)
        {
            var runtime=HollowCraftRuntime.Instance;
            if(instance==null||!MinecraftControls.Active||runtime==null) return false;
            if(runtime.GameplayBlocked||player.controlReqlinquished)return true;
            if(MinecraftControls.Grounded(player)&&Time.time>=instance.nextJump) {
                int boost=instance.Effect("jump_boost");float velocity=.42f+(boost>=0?.1f*(boost+1):0);
                instance.nextJump=Time.time+.5f;
                instance.lastJump=Time.time;
                instance.motionY=velocity;instance.normalPhase=0;instance.ownGravity=true;instance.lastAppliedY=velocity*20;
                var rb=player.GetComponent<Rigidbody2D>();rb.gravityScale=0;rb.linearVelocity=new Vector2(rb.linearVelocity.x,velocity*20);
                player.cState.onGround=false;player.cState.jumping=false;player.cState.falling=false;player.cState.recoiling=false;
            }
            return true;
        }
        internal static bool BeginGlide(HeroController player)
        {
            var runtime=HollowCraftRuntime.Instance;
            if(instance==null||runtime==null||runtime.Dead||MinecraftControls.Grounded(player)||(bool?)runtime.Stats?["water"]==true||(string)runtime.Stats?["equipment"]?[1]?["id"]!="minecraft:elytra") return false;
            Gliding=true;FlightVelocity=player.GetComponent<Rigidbody2D>().linearVelocity/20;if(Math.Abs(FlightVelocity.x)<.1f) FlightVelocity.x=MinecraftControls.Facing*.3f;return true;
        }
        private void Update()
        {
            var runtime=HollowCraftRuntime.Instance;if(runtime==null||!runtime.InGame) return;
            var current=HeroController.instance;if(current==null) return;
            if(hero!=current) {Restore();hero=current;body=hero.GetComponent<Rigidbody2D>();gravity=body.gravityScale;jump=hero.JUMP_SPEED;Gliding=false;}
            if(runtime.SimulationPaused) {if(runtime.Dead) Gliding=false;return;}
            bool test=Environment.GetEnvironmentVariable("HOLLOWCRAFT_SMOKE")=="1";
            if(!test&&Application.isFocused&&!runtime.GameplayBlocked) {
                if(!Input.GetKey(KeyCode.Space))nextJump=0;
                else if(MinecraftControls.Grounded(hero))TakeJump(hero);
                if(Input.GetKeyDown(KeyCode.Space)&&!MinecraftControls.Grounded(hero)&&Time.time-lastJump>.08f) BeginGlide(hero);
                if(Input.GetKeyDown(KeyCode.LeftShift)) Gliding=false;
            }
            int boost=Effect("jump_boost");hero.JUMP_SPEED=jump*(boost>=0?(.42f+.1f*(boost+1))/.42f:1);
            if(MinecraftControls.Grounded(hero)||(bool?)runtime.Stats?["water"]==true||(string)runtime.Stats?["equipment"]?[1]?["id"]!="minecraft:elytra") Gliding=false;
        }
        private void FixedUpdate()
        {
            var runtime=HollowCraftRuntime.Instance;if(hero==null||body==null||runtime==null||!runtime.InGame||runtime.SimulationPaused||hero.controlReqlinquished) return;
            if(runtime.Spectator) {
                var collider=hero.GetComponent<Collider2D>();
                if(!spectatorBody) {previousTrigger=collider.isTrigger;previousBodyType=body.bodyType;collider.isTrigger=true;body.bodyType=RigidbodyType2D.Kinematic;spectatorBody=true;Gliding=false;}
                float x=Input.GetKey(KeyCode.A)?-1:Input.GetKey(KeyCode.D)?1:0,y=Input.GetKey(KeyCode.Space)?1:Input.GetKey(KeyCode.LeftShift)||Input.GetKey(KeyCode.RightShift)?-1:0;
                body.gravityScale=0;body.linearVelocity=runtime.GameplayBlocked||!Application.isFocused?Vector2.zero:new Vector2(x,y)*(Input.GetKey(KeyCode.LeftControl)?15:10);return;
            }
            if(spectatorBody) {hero.GetComponent<Collider2D>().isTrigger=previousTrigger;body.bodyType=previousBodyType;spectatorBody=false;body.linearVelocity=Vector2.zero;ownGravity=false;}
            PlayerSystem.DecayKnockback(Time.fixedDeltaTime,MinecraftControls.Grounded(hero));
            bool water=(bool?)runtime.Stats?["water"] ?? false,climbing=(bool?)runtime.Stats?["climbing"] ?? false;
            int levitation=Effect("levitation");
            if(Gliding) {
                body.gravityScale=0;step+=Time.fixedDeltaTime;
                while(step>=.05f) {step-=.05f;Vector2 target=TestAim??(Vector2.right*MinecraftControls.Facing);
                    if(TestAim==null&&GameCursor.WorldTarget(hero,out Vector2 cursor,out _)) target=(cursor-(Vector2)hero.transform.position).normalized;
                    FlightVelocity=StepFlight(FlightVelocity,target,(bool?)runtime.Stats?["rocket"] ?? false);
                }
                // HK's contacts still resolve collision; only the vanilla gliding velocity replaces locomotion.
                body.linearVelocity=FlightVelocity*20;
            } else if(water||climbing||levitation>=0) {
                body.gravityScale=0;var velocity=body.linearVelocity;
                if(levitation>=0) velocity.y=Mathf.Lerp(velocity.y,(levitation+1),1-Mathf.Pow(.8f,Time.fixedDeltaTime/.05f));
                else if(climbing) velocity.y=Input.GetKey(KeyCode.W)||Input.GetKey(KeyCode.Space)?3:Input.GetKey(KeyCode.S)?-3:Math.Max(-3,velocity.y);
                else {velocity.y=Mathf.Max(-2,velocity.y-Time.fixedDeltaTime*3);if(Input.GetKey(KeyCode.Space)) velocity.y=3.5f;}
                body.linearVelocity=velocity;
            } else {
                // Integrate each 20 Hz vanilla gravity step across Unity's physics intervals.
                // This preserves the 1.25-block jump without the Knight's held-jump acceleration.
                if(!ownGravity||Math.Abs(body.linearVelocity.y-lastAppliedY)>.05f) {motionY=body.linearVelocity.y/20;normalPhase=0;ownGravity=true;}
                if(MinecraftControls.Grounded(hero)&&motionY<0) {motionY=0;normalPhase=0;}
                float remaining=Time.fixedDeltaTime,displacement=0;
                while(remaining>.00001f) {
                    float segment=Math.Min(remaining,.05f-normalPhase);displacement+=motionY*20*segment;remaining-=segment;normalPhase+=segment;
                    if(normalPhase>=.04999f) {normalPhase=0;motionY=(motionY-(Effect("slow_falling")>=0&&motionY<0?.01f:.08f))*.98f;}
                }
                body.gravityScale=0;lastAppliedY=displacement/Time.fixedDeltaTime;body.linearVelocity=new Vector2(body.linearVelocity.x,lastAppliedY);step=0;
            }
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
        private void OnDestroy() {Restore();Gliding=false;instance=null;TestAim=null;}
    }
}
