using System;
using System.Collections.Generic;
using System.Linq;
using HutongGames.PlayMaker;
using UnityEngine;

namespace HollowCraft
{
    // False Knight / Failed Champion: the separate Head health pool resets on
    // ZERO HP and sends STUN END to the body. Early hits during opening/recovery
    // can spend that pool before the receiver can consume its authored event.
    internal static class NativeKnightStages
    {
        private static readonly Dictionary<int,PlayMakerFSM> controls=new Dictionary<int,PlayMakerFSM>();
        internal static void Clear()=>controls.Clear();
        internal static int Floor(HealthManager enemy) {
            if(enemy==null||enemy.name!="Head")return 0;
            int id=enemy.GetInstanceID();
            if(!controls.TryGetValue(id,out var control)||control==null) {
                foreach(var fsm in enemy.GetComponents<PlayMakerFSM>()) {
                    var body=fsm.FsmVariables?.GameObjectVariables?.FirstOrDefault(value=>value!=null&&value.Name=="Body")?.Value;
                    if(body!=null)control=body.GetComponents<PlayMakerFSM>().FirstOrDefault(f=>f.FsmName=="FalseyControl");
                    if(control!=null)break;
                }
                if(control==null)return 0;controls[id]=control;
            }
            bool ready=control.enabled&&control.gameObject.activeInHierarchy&&(control.Fsm.ActiveState?.Transitions??Array.Empty<FsmTransition>()).Any(t=>t.EventName=="STUN END");
            return ready?0:Math.Max(1,enemy.hp);
        }
    }
}
