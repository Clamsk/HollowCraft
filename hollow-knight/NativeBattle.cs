using System.Collections.Generic;
using System.Reflection;
using HutongGames.PlayMaker;
using UnityEngine;

namespace HollowCraft
{
    // Repair only a confirmed dead enemy's own unacknowledged native battle count.
    // Door states, wave spawning and victory events remain owned by the original FSM.
    public static class NativeBattle
    {
        private static readonly FieldInfo battle=typeof(HealthManager).GetField("battleScene",BindingFlags.Instance|BindingFlags.NonPublic);
        private static readonly FieldInfo notified=typeof(HealthManager).GetField("notifiedBattleScene",BindingFlags.Instance|BindingFlags.NonPublic);
        private sealed class Pending {internal HealthManager Enemy;internal GameObject Battle;internal float Ready,Expires;}
        private static readonly List<Pending> pending=new List<Pending>();
        private static readonly Dictionary<GameObject,List<HealthManager>> gruzChildren=new Dictionary<GameObject,List<HealthManager>>();
        private static float nextScan;
        public static void Track(HealthManager enemy) {
            if(HollowCraftRuntime.Instance==null||enemy==null)return;
            var owner=battle?.GetValue(enemy) as GameObject;
            if(owner==null||owner.scene.name!="Crossroads_04")return;
            if(!gruzChildren.TryGetValue(owner,out var children))gruzChildren[owner]=children=new List<HealthManager>();
            if(!children.Exists(value=>ReferenceEquals(value,enemy)))children.Add(enemy);
        }
        public static void Death(HealthManager enemy) {
            if(HollowCraftRuntime.Instance==null||ReferenceEquals(enemy,null)||!enemy.isDead||(bool)(notified?.GetValue(enemy)??true))return;
            var owner=battle?.GetValue(enemy) as GameObject;
            if(owner==null||pending.Exists(p=>ReferenceEquals(p.Enemy,enemy)))return;
            pending.Add(new Pending {Enemy=enemy,Battle=owner,Ready=Time.unscaledTime+.05f,Expires=Time.unscaledTime+2});
        }
        public static void Bound(HealthManager enemy) {Track(enemy);Death(enemy);}
        internal static void Clear() {pending.Clear();gruzChildren.Clear();nextScan=0;}
        internal static void Tick() {
            if(HollowCraftRuntime.Instance.Room=="Crossroads_04") {
                if(Time.unscaledTime>=nextScan) {
                    nextScan=Time.unscaledTime+1;
                    foreach(var enemy in Object.FindObjectsByType<HealthManager>(FindObjectsInactive.Include,FindObjectsSortMode.None))Track(enemy);
                }
                foreach(var group in gruzChildren) {
                    if(group.Key==null||group.Value.Count!=7||group.Value.Exists(enemy=>!enemy.isDead))continue;
                    var control=FSMUtility.LocateFSM(group.Key,"Battle Control");
                    var count=control?.FsmVariables.GetFsmInt("Battle Enemies");
                    // This encounter has exactly seven serialized children. A late
                    // START sets the counter after fast MC kills already decremented it.
                    // Reconcile only this fully dead cohort in its native counting state.
                    if(control!=null&&control.ActiveStateName=="Start"&&count!=null&&count.Value>0) {
                        Debug.Log("[HollowCraft] Gruz brood confirmed dead; repair late battle counter "+count.Value+"->0");
                        count.Value=0;
                    }
                }
            }
            for(int i=pending.Count-1;i>=0;i--) {
                var item=pending[i];
                if(item.Battle==null||!item.Enemy.isDead||(bool)(notified?.GetValue(item.Enemy)??true)||Time.unscaledTime>item.Expires) {pending.RemoveAt(i);continue;}
                if(Time.unscaledTime<item.Ready)continue;
                var control=FSMUtility.LocateFSM(item.Battle,"Battle Control");
                var count=control?.FsmVariables.GetFsmInt("Battle Enemies");
                if(count==null||count.Value<=0)continue;
                int before=count.Value;count.Value=before-1;notified.SetValue(item.Enemy,true);
                Debug.Log("[HollowCraft] Repaired native battle death room="+HollowCraftRuntime.Instance.Room+" owner="+item.Battle.name+" count="+before+"->"+count.Value);
                pending.RemoveAt(i);
            }
        }
    }
}
