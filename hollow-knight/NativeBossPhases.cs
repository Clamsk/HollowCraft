using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HutongGames.PlayMaker;
using UnityEngine;

namespace HollowCraft
{
    // Preserve the NEXT native HP boundary until its real FSM processes TOOK DAMAGE.
    // A large MC hit must not jump through several event-driven boss stages at once.
    internal static class NativeBossPhases
    {
        private static readonly Dictionary<int,Gate> gates=new Dictionary<int,Gate>();
        internal static void Clear()=>gates.Clear();
        internal static int Floor(HealthManager enemy,int maximum) {
            if(enemy==null||enemy.isDead||maximum<100)return 0;
            int id=enemy.GetInstanceID();if(!gates.TryGetValue(id,out var gate))gates[id]=gate=new Gate();
            try{return gate.Floor(enemy,maximum);}catch(Exception error){if(!gate.Warned){gate.Warned=true;Debug.LogWarning("[HollowCraft] Native phase inspection "+enemy.name+": "+error.Message);}return 0;}
        }
        internal static bool Phased(HealthManager enemy)=>enemy!=null&&gates.TryGetValue(enemy.GetInstanceID(),out var gate)&&gate.EventDriven;
        internal static void Damaged(HealthManager enemy,int before) {if(enemy!=null&&enemy.hp<before&&gates.TryGetValue(enemy.GetInstanceID(),out var gate))gate.DamageSerial++;}
        internal static bool Radiance(HealthManager enemy)=>enemy!=null&&enemy.name.IndexOf("radiance",StringComparison.OrdinalIgnoreCase)>=0;
        private static FsmInt Int(object action,string field)=>action.GetType().GetField(field,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic)?.GetValue(action) as FsmInt;
        private sealed class Graph {
            internal PlayMakerFSM Owner;internal Dictionary<string,FsmState> States;
            internal HashSet<string> HpNames;
            internal bool EventDriven;internal int Replayed;internal string Reported;
            internal bool Ready=>States.TryGetValue(Owner.ActiveStateName,out var state)&&(state.Transitions??Array.Empty<FsmTransition>()).Any(t=>t.EventName=="TOOK DAMAGE");
            internal int Next(int maximum) {
                var queue=new Queue<Tuple<string,int>>();var seen=new HashSet<string>();queue.Enqueue(Tuple.Create(Owner.ActiveStateName,0));
                int found=0,depth=int.MaxValue;
                while(queue.Count>0) {
                    var item=queue.Dequeue();if(item.Item2>8||item.Item2>depth||!seen.Add(item.Item1)||!States.TryGetValue(item.Item1,out var state))continue;
                    foreach(var action in state.Actions??Array.Empty<FsmStateAction>()) {
                        if(action==null||!action.Enabled||action.GetType().Name!="IntCompare")continue;
                        var a=Int(action,"integer1");var b=Int(action,"integer2");
                        bool hpA=a!=null&&HpNames.Contains(a.Name),hpB=b!=null&&HpNames.Contains(b.Name);
                        if(hpA==hpB)continue;int threshold=(hpA?b:a)?.Value??0;
                        if(threshold<=1||threshold>=maximum)continue;
                        found=Math.Max(found,threshold-1);depth=item.Item2;
                    }
                    if(item.Item2>=depth)continue;
                    foreach(var transition in state.Transitions??Array.Empty<FsmTransition>())if(!string.IsNullOrEmpty(transition.ToState))queue.Enqueue(Tuple.Create(transition.ToState,item.Item2+1));
                }
                return found;
            }
        }
        private sealed class Gate {
            private readonly List<Graph> graphs=new List<Graph>();private float discover,terminalAt=-1;private bool hadFloor,sawShield,released,nailFinish;
            internal bool Warned;
            internal int DamageSerial;
            internal bool EventDriven=>graphs.Any(graph=>graph.EventDriven);
            internal int Floor(HealthManager enemy,int maximum) {
                if(graphs.Count==0&&Time.unscaledTime>=discover) {
                    discover=Time.unscaledTime+1;
                    foreach(var fsm in enemy.GetComponents<PlayMakerFSM>()) {
                        var states=fsm.Fsm.States;if(states==null)continue;
                        if(Radiance(enemy)&&states.Any(state=>(state.Actions??Array.Empty<FsmStateAction>()).OfType<HutongGames.PlayMaker.Actions.Trigger2dEvent>().Any(action=>action.Enabled&&action.collideTag?.Value=="Nail Attack"&&action.sendEvent?.Name=="FINAL HIT")))nailFinish=true;
                        var names=new HashSet<string>();
                        foreach(var state in states)foreach(var action in state.Actions??Array.Empty<FsmStateAction>())if(action!=null&&action.Enabled&&(action.GetType().Name=="GetHP"||action.GetType().Name=="GetHPEveryFrame")) {var hp=Int(action,"storeValue");if(hp!=null&&!string.IsNullOrEmpty(hp.Name))names.Add(hp.Name);}
                        if(names.Count>0)graphs.Add(new Graph{Owner=fsm,States=states.ToDictionary(s=>s.Name),HpNames=names,
                            EventDriven=states.Any(state=>(state.Transitions??Array.Empty<FsmTransition>()).Any(t=>t.EventName=="TOOK DAMAGE"))});
                    }
                }
                int floor=0;bool checkpoint=false;
                foreach(var graph in graphs)if(graph.Owner!=null&&graph.Owner.enabled) {
                    int next=graph.Next(maximum);
                    // A hit can arrive while a phase controller is waiting for an
                    // animation's RAGE START / ARENA 2 START handshake. Its future
                    // HP check is reachable but it cannot consume TOOK DAMAGE yet.
                    // Hold at CURRENT HP through that wait, not the future threshold.
                    if(next>0&&graph.EventDriven) {
                        if(graph.Ready&&enemy.hp<=next&&DamageSerial>graph.Replayed) {
                            graph.Replayed=DamageSerial;
                            Debug.Log("[HollowCraft] Recheck accepted phase damage actor="+enemy.name+" state="+graph.Owner.ActiveStateName+" HP="+enemy.hp);
                            graph.Owner.SendEvent("TOOK DAMAGE");next=graph.Next(maximum);
                        }
                        if(!graph.Ready)next=Math.Max(next,enemy.hp);
                    }
                    checkpoint|=next>0;floor=Math.Max(floor,next);
                    string report=graph.Owner.ActiveStateName+"|"+next+"|"+graph.Ready;
                    if(graph.EventDriven&&report!=graph.Reported) {
                        graph.Reported=report;
                        Debug.Log("[HollowCraft] Native phase actor="+enemy.name+" fsm="+graph.Owner.FsmName+" state="+graph.Owner.ActiveStateName+" HP="+enemy.hp+" floor="+next+" acceptsDamage="+graph.Ready);
                    }
                }
                // Radiance's ascent ends through a Nail Attack trigger, not zero
                // HP. Preserve its health for that native contact/ending sequence.
                if(!checkpoint&&hadFloor&&nailFinish&&EventDriven)return Math.Max(1,enemy.hp);
                if(floor>0){hadFloor=true;terminalAt=-1;sawShield=released=false;return floor;}
                if(!hadFloor||released)return 0;
                if(terminalAt<0)terminalAt=Time.time;
                bool shield=Enumerable.Range(0,4).All(d=>enemy.IsBlockingByDirection(d,AttackTypes.Nail));
                if(shield)sawShield=true;
                // Give the native ascent/arena controller time to install its shield.
                // Never impose a permanent artificial phase on an unfamiliar boss.
                if(!shield&&(sawShield||Time.time-terminalAt>=1)) {released=true;return 0;}
                return 1;
            }
        }
    }
}
