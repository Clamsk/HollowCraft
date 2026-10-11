using System;
using System.Linq;
using HutongGames.PlayMaker;
using UnityEngine;
namespace HollowCraft {
    internal static class NativeChoice {
        private static PlayMakerFSM controller;
        internal static bool Visible=>controller!=null&&controller.gameObject.activeInHierarchy&&controller.ActiveStateName=="Ready for Input";
        internal static void Tick() {if(controller==null) {var text=GameObject.Find("Text YN");if(text!=null) controller=text.GetComponents<PlayMakerFSM>().FirstOrDefault(f=>f.FsmName=="Dialogue Page Control");}}
        internal static bool Select(PlayMakerFSM target,PlayMakerFSM selector,string stateName,bool yes) {
            var state=target.FsmStates.FirstOrDefault(s=>s.Name==stateName);if(state==null||selector==null) return false;
            foreach(var action in state.Actions) if(action.GetType().Name=="IntSwitch") {
                var values=action.GetType().GetField("compareTo")?.GetValue(action) as FsmInt[];var events=action.GetType().GetField("sendEvent")?.GetValue(action) as FsmEvent[];if(values==null||events==null) continue;
                for(int i=0;i<Math.Min(values.Length,events.Length);i++) if(events[i]?.Name==(yes?"YES":"NO")) {selector.FsmVariables.FindFsmInt("Current Item").Value=values[i].Value;target.SendEvent("UI SELECTION MADE");return true;}
            }return false;
        }
        internal static bool Decide(bool yes) {
            if(!Visible) return false;var list=controller.FsmVariables.GameObjectVariables.Where(v=>v.Value!=null).SelectMany(v=>v.Value.GetComponentsInChildren<PlayMakerFSM>(true)).FirstOrDefault(f=>f.FsmName=="ui_list"&&f.gameObject.activeInHierarchy);return Select(controller,list,"End Conversation",yes);
        }
    }
}
