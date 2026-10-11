using System;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace HollowCraft {
    // Keep currency, stock requirements, purchases and campaign flags inside the native FSM.
    internal static class NativeShop {
        private static PlayMakerFSM control,list,confirm;private static ShopMenuStock stock;
        private static float nextCheck,ready;private static int pending=-1;
        internal static bool Visible;
        internal static bool Confirming=>Visible&&confirm!=null&&confirm.gameObject.activeInHierarchy;
        internal static JArray Items=new JArray();
        internal static int Cost=>confirm?.FsmVariables.FindFsmInt("Cost").Value??0;
        internal static void Tick() {
            if(Time.unscaledTime<nextCheck) return;nextCheck=Time.unscaledTime+.1f;
            if(control==null) {var root=GameObject.Find("Shop Menu");if(root!=null) control=root.GetComponents<PlayMakerFSM>().FirstOrDefault(f=>f.FsmName=="shop_control");}
            Visible=control!=null&&control.gameObject.activeInHierarchy&&control.ActiveStateName=="Open";
            if(!Visible) {pending=-1;Items.Clear();return;}
            stock=control.GetComponentsInChildren<ShopMenuStock>(true).FirstOrDefault(s=>s.GetItemCount()>0);
            if(stock==null) {Visible=false;return;}
            list=stock.GetComponents<PlayMakerFSM>().FirstOrDefault(f=>f.FsmName=="Item List Control");
            confirm=control.GetComponentsInChildren<PlayMakerFSM>(true).FirstOrDefault(f=>f.FsmName=="Confirm Control");
            if(!Confirming) {
                var values=new JArray();for(int i=0;i<stock.GetItemCount();i++) values.Add(new JObject{["index"]=i,["name"]=Translate(stock.GetNameConvo(i)),["description"]=Translate(stock.GetDescConvo(i)),["cost"]=stock.GetCost(i),["canBuy"]=stock.CanBuy(i)});Items=values;
                if(pending>=0&&Time.unscaledTime>=ready&&list!=null&&list.ActiveStateName=="Idle") {pending=-1;list.SendEvent("UI CONFIRM");}
            }
        }
        private static string Translate(string key)=>Regex.Replace(TeamCherry.Localization.Language.Get(key,"UI")??key,"<[^>]+>","");
        internal static bool Buy(int index) {
            if(!Visible||Confirming||pending>=0||list==null||list.ActiveStateName!="Idle"||index<0||index>=stock.GetItemCount()) return false;
            list.FsmVariables.FindFsmInt("Current Item").Value=index;list.SetState("Get Details");pending=index;ready=Time.unscaledTime+.35f;return true;
        }
        internal static bool Decide(bool yes) {
            if(!Confirming||confirm.ActiveStateName!="Listen") return false;var selector=confirm.GetComponents<PlayMakerFSM>().FirstOrDefault(f=>f.FsmName=="ui_list");if(selector==null) return false;
            return NativeChoice.Select(confirm,selector,"Check Selection",yes);
        }
        internal static void Close() {if(Confirming) Decide(false);else if(Visible) control.SendEvent("CLOSE");pending=-1;}
    }
}
