using System;
using System.Collections.Generic;
using System.Linq;
using InControl;
using UnityEngine;

namespace HollowCraft
{
    public static class MinecraftControls
    {
        public static bool Active { get; private set; }
        public static bool InterfaceOpen { get; private set; }
        public static bool Crouching { get; private set; }
        public static bool FrontView => Active && frontView;
        internal static bool frontView;
        internal static int Facing=1;
        internal static bool? TestCrouch;
        internal static float? TestMovement;
        internal static bool? TestSprint;
        internal static bool Sprinting => !Crouching && (TestSprint??Input.GetKey(KeyCode.LeftControl)) && (HollowCraftRuntime.Instance.Creative || ((int?)HollowCraftRuntime.Instance.Stats?["food"]??20)>6);
        private static HeroController hero;
        private static HeroActions actions;
        private static readonly Dictionary<PlayerAction,BindingSource[]> saved = new Dictionary<PlayerAction,BindingSource[]>();
        private static BoxCollider2D body;
        private static Vector2 standingSize,standingOffset;
        private static Vector2 nativeSize,nativeOffset;
        private static bool bodyConfigured;
        private static bool priorEnabled;
        private static readonly RaycastHit2D[] groundHits=new RaycastHit2D[16];
        private static readonly float[] groundOffsets={0,-.7f,.7f};
        internal static bool Grounded(HeroController value) {
            var collider=value.GetComponent<Collider2D>();if(collider==null) return value.cState.onGround;
            var rb=value.GetComponent<Rigidbody2D>();if(rb!=null&&rb.linearVelocity.y>.1f)return false;
            var bounds=collider.bounds;
            var filter=new ContactFilter2D();filter.SetLayerMask(LayerMask.GetMask("Terrain","Default"));filter.useTriggers=false;
            foreach(float offset in groundOffsets) {
                int count=Physics2D.Raycast(new Vector2(bounds.center.x+offset*bounds.extents.x,bounds.min.y+.02f),Vector2.down,filter,groundHits,.055f);
                for(int i=0;i<count;i++) {var hit=groundHits[i];if(hit.collider!=null&&hit.collider.GetComponentInParent<HeroController>()==null&&hit.collider.GetComponentInParent<HealthManager>()==null)return true;}
            }
            return false;
        }

        internal static void Tick(HeroController value,bool active,bool interfaceOpen)
        {
            if(hero!=value) { Restore(); hero=value; body=hero != null ? hero.GetComponent<BoxCollider2D>() : null;
                if(body!=null) {nativeSize=body.size;nativeOffset=body.offset;standingSize=nativeSize;standingOffset=nativeOffset;bodyConfigured=false;} }
            var handler=InputHandler.Instance;
            if(active && actions==null && handler!=null && handler.inputActions!=null)
            {
                actions=handler.inputActions; priorEnabled=actions.Enabled;
                Bind(actions.left,Key.A); Bind(actions.right,Key.D); Bind(actions.up,Key.W);
                Bind(actions.down,Key.S,Key.DownArrow); Bind(actions.jump,Key.Space);
                Bind(actions.dash); Bind(actions.attack); Bind(actions.openInventory);Bind(actions.pause);
                Bind(actions.evade); Bind(actions.focus); Bind(actions.cast);
                Bind(actions.quickCast); Bind(actions.superDash); Bind(actions.dreamNail,Key.R); Bind(actions.quickMap);
                Debug.Log("[HollowCraft] Minecraft input active: A/D Space Shift; mouse combat");
            }
            if(!active && actions!=null) Restore();
            Active=active; InterfaceOpen=active && interfaceOpen;
            if(actions!=null) actions.Enabled=!InterfaceOpen && Application.isFocused;
            if(active)
            {
                if(body!=null&&!bodyConfigured) {
                    var scale=body.transform.lossyScale;
                    standingSize=new Vector2(.6f/Mathf.Max(.001f,Mathf.Abs(scale.x)),1.8f/Mathf.Max(.001f,Mathf.Abs(scale.y)));
                    standingOffset=nativeOffset+new Vector2(0,(standingSize.y-nativeSize.y)*.5f);
                    bodyConfigured=true;
                }
                GameCursor.ApplyCursor();
                if(!InterfaceOpen && Application.isFocused) {
                    if(Input.GetKeyDown(KeyCode.S)||Input.GetKeyDown(KeyCode.DownArrow)) frontView=true;
                    if(Input.GetKey(KeyCode.A)||Input.GetKey(KeyCode.D)) {frontView=false;Facing=Input.GetKey(KeyCode.A) ? -1 : 1;}
                }
                SetCrouch(!InterfaceOpen&&!MinecraftEnvironment.Flying&&HollowCraftRuntime.Instance?.Spectator!=true&&(TestCrouch??(Application.isFocused && (Input.GetKey(KeyCode.LeftShift)||Input.GetKey(KeyCode.RightShift)))));
            }
        }
        private static void Bind(PlayerAction action,params Key[] keys)
        {
            saved[action]=action.Bindings.ToArray(); action.ClearBindings();
            // AddBinding changes this session only; AddDefaultBinding would change reset defaults too.
            foreach(Key key in keys) action.AddBinding(new KeyBindingSource(key));
        }
        private static void SetCrouch(bool requested)
        {
            // The vanilla seated player's standing collision box intersects its boat.
            // That is not a low ceiling and must not turn into an automatic Shift press.
            if(WorldCreatures.Riding) {Crouching=false;return;}
            if(body==null) {Crouching=requested;return;}
            float height=standingSize.y*(MinecraftEnvironment.Gliding ? .6f/1.8f : requested ? 1.5f/1.8f : 1);
            var scale=body.transform.lossyScale;
            Vector2 feet=NativeLanding.CurrentFeet(hero);
            if(height>body.size.y+.001f&&!NativeLanding.Clear(feet,new Vector2(standingSize.x*Mathf.Abs(scale.x),height*Mathf.Abs(scale.y)))) {
                // An HK entrance can have less headroom than a standing MC player.
                // Keep its safe native height until there is room, instead of expanding into the roof.
                height=Math.Min(body.size.y,standingSize.y*1.5f/1.8f);
            }
            Crouching=requested||(!MinecraftEnvironment.Gliding&&height<standingSize.y-.01f);
            body.size=new Vector2(standingSize.x,height);
            body.offset=standingOffset-new Vector2(0,(standingSize.y-body.size.y)/2);
        }
        // Injected at HeroController.Move's entry, preserving the game's collision and jump controller.
        public static float AdjustMove(float movement)
        {
            if(!Active) return movement;
            var runtime=HollowCraftRuntime.Instance;
            if(runtime==null||!runtime.InGame||runtime.NativeControlLocked)return movement;
            if(WorldCreatures.Riding)return 0;
            if(PlayerSystem.KnockbackActive) return PlayerSystem.KnockbackX/Math.Max(1,hero.RUN_SPEED);
            if(TestMovement.HasValue)movement=TestMovement.Value;
            if((InterfaceOpen || !Application.isFocused)&&!TestMovement.HasValue) return 0;
            if(MinecraftEnvironment.Gliding) return MinecraftEnvironment.FlightVelocity.x*20/Math.Max(1,hero.RUN_SPEED);
            if(MinecraftEnvironment.HorizontalSpeed.HasValue) {
                return SafeHorizontalSpeed(MinecraftEnvironment.HorizontalSpeed.Value)/Math.Max(1,hero.RUN_SPEED);
            }
            bool sprint=Sprinting;
            float speed=(float?)runtime.Stats?["speed"] ?? .1f;
            float effects=Mathf.Clamp(speed/(sprint?.13f:.1f),.05f,6);
            if((bool?)runtime.Stats?["using"]==true) effects*=.2f;
            if(!Crouching) return movement*4.317f/Math.Max(1,hero.RUN_SPEED)*(sprint ? 1.3f : 1)*effects;
            if(hero!=null && hero.cState.onGround && body!=null && Math.Abs(movement)>0.01f)
            {
                Vector2 foot=new Vector2(body.bounds.center.x+Math.Sign(movement)*(body.bounds.extents.x+0.2f),body.bounds.min.y+0.05f);
                if(!Physics2D.Raycast(foot,Vector2.down,0.45f,LayerMask.GetMask("Terrain","Default"))) return 0;
            }
            return movement*4.317f/Math.Max(1,hero.RUN_SPEED)*.3f*effects;
        }
        internal static float SafeHorizontalSpeed(float speed) {
            if(Crouching&&hero!=null&&Grounded(hero)&&body!=null&&Math.Abs(speed)>.01f) {
                Vector2 foot=new Vector2(body.bounds.center.x+Math.Sign(speed)*(body.bounds.extents.x+.2f),body.bounds.min.y+.05f);
                if(!Physics2D.Raycast(foot,Vector2.down,.45f,LayerMask.GetMask("Terrain","Default")))return 0;
            }
            return speed;
        }
        internal static void Restore()
        {
            if(actions!=null) { foreach(var pair in saved) {pair.Key.ClearBindings();foreach(var binding in pair.Value) pair.Key.AddBinding(binding);} actions.Enabled=priorEnabled; }
            saved.Clear(); actions=null; Active=false; InterfaceOpen=false; Crouching=false;frontView=false;TestCrouch=null;TestMovement=null;TestSprint=null;
            if(body!=null) {body.size=nativeSize;body.offset=nativeOffset;bodyConfigured=false;}
        }
    }
}
