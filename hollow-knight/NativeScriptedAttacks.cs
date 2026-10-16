using System;
using System.Collections.Generic;
using System.Linq;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEngine;

namespace HollowCraft
{
    // MC clears HK's attack binding. Radiance's ending still waits for that
    // button through ListenForAttack, after its damageable collider is gone.
    internal static class NativeScriptedAttacks
    {
        private static readonly List<PlayMakerFSM> listeners=new List<PlayMakerFSM>();
        private static string room;
        private static float nextScan;
        internal static void Clear() {listeners.Clear();room=null;nextScan=0;}
        internal static bool IsArena(string scene)=>scene=="Dream_Final"||scene=="Dream_Final_Boss"||scene=="GG_Radiance";
        internal static bool NeedsNailContact(HealthManager enemy) {
            if(!NativeBossPhases.Radiance(enemy)||enemy.isDead||!enemy.gameObject.activeInHierarchy)return false;
            return enemy.GetComponents<PlayMakerFSM>().Any(fsm=>fsm.enabled&&fsm.Fsm.ActiveState?.Actions!=null&&fsm.Fsm.ActiveState.Actions.OfType<Trigger2dEvent>().Any(action=>action.Enabled&&action.collideTag?.Value=="Nail Attack"&&action.trigger.ToString().IndexOf("Enter",StringComparison.OrdinalIgnoreCase)>=0));
        }
        internal static void NailContact(HealthManager enemy,HeroController hero,float angle) {
            if(!NeedsNailContact(enemy))return;
            var source=HollowCraftRuntime.Instance.Combat.NativeDamageSource(hero,1,angle,AttackTypes.Nail);
            Debug.Log("[HollowCraft] Native final nail contact actor="+enemy.name+" HP="+enemy.hp);
            // The server verified the real melee / swept Zenith hitbox. Let the
            // original Trigger2dEvent filter and process its Nail Attack collider.
            enemy.gameObject.SendMessage("OnTriggerEnter2D",source.GetComponent<Collider2D>(),SendMessageOptions.DontRequireReceiver);
        }
        internal static void Input(string scene) {
            string activeScene=UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            if(!IsArena(scene)&&!IsArena(activeScene)) {if(room!=null)Clear();return;}
            bool down=UnityEngine.Input.GetMouseButtonDown(0),held=UnityEngine.Input.GetMouseButton(0),up=UnityEngine.Input.GetMouseButtonUp(0);
            if(!down&&!held&&!up)return;
            if(activeScene!=room||Time.unscaledTime>=nextScan) {
                room=activeScene;nextScan=Time.unscaledTime+.5f;listeners.Clear();
                foreach(var fsm in PlayMakerFSM.FsmList)
                    if(fsm!=null&&fsm.gameObject.scene.name==activeScene&&fsm.Fsm.States.Any(state=>state.Actions!=null&&state.Actions.Any(action=>action is ListenForAttack)))listeners.Add(fsm);
            }
            foreach(var fsm in listeners) {
                if(fsm==null||!fsm.enabled||!fsm.gameObject.activeInHierarchy)continue;
                var state=fsm.Fsm.ActiveState;
                if(state?.Actions==null)continue;
                foreach(var action in state.Actions.OfType<ListenForAttack>()) {
                    if(!action.Enabled)continue;
                    var response=down?action.wasPressed:up?action.wasReleased:held?action.isPressed:action.isNotPressed;
                    if(response==null||string.IsNullOrEmpty(response.Name))continue;
                    // Dispatch only the listener's authored event in its current
                    // state. Never set a boss state, HP, victory or ending flag.
                    if(!(state.Transitions??Array.Empty<FsmTransition>()).Any(transition=>transition.EventName==response.Name))continue;
                    Debug.Log("[HollowCraft] Native scripted attack scene="+scene+" actor="+fsm.name+" fsm="+fsm.FsmName+" state="+state.Name+" event="+response.Name);
                    fsm.Fsm.Event(response);break;
                }
            }
        }
    }
}
