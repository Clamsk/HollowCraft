using System;
using System.IO;
using System.Reflection;
using System.Collections;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace HollowCraft
{
    internal static class CampaignBridge
    {
        private static readonly FieldInfo textField=typeof(DialogueBox).GetField("textMesh",BindingFlags.Instance|BindingFlags.NonPublic);
        private static readonly FieldInfo hiddenField=typeof(DialogueBox).GetField("hidden",BindingFlags.Instance|BindingFlags.NonPublic);
        private static DialogueBox dialogue;
        private static float nextCheck;
        internal static string DialogueText;
        private static object bench;
        internal static bool Sitting;
        private static bool awaitingRespawn;
        private static float respawnStarted;
        private static object Property(object value,string name)=>value?.GetType().GetProperty(name)?.GetValue(value,null);
        internal static void Tick()
        {
            if(Time.unscaledTime<nextCheck) return;nextCheck=Time.unscaledTime+.1f;DialogueText=null;
            Sitting=false;bench=null;
            var hero=HeroController.instance;
            if(hero!=null&&hero.controlReqlinquished) foreach(var marker in GameObject.FindGameObjectsWithTag("RespawnPoint")) {
                if(Vector2.Distance(marker.transform.position,hero.transform.position)>5) continue;
                bench=marker.GetComponents<Component>().FirstOrDefault(c=>c.GetType().Name=="PlayMakerFSM"&&(string)Property(c,"FsmName")=="Bench Control");
                if(bench!=null) {Sitting=true;break;}
            }
            if(Sitting&&(Input.GetKeyDown(KeyCode.W)||Input.GetKeyDown(KeyCode.Space))) LeaveBench();
            if(awaitingRespawn&&hero!=null&&Time.unscaledTime-respawnStarted>2&&!GameManager.instance.IsInSceneTransition) {
                string state=(string)Property(bench,"ActiveStateName");
                if(hero.CanInput()) awaitingRespawn=false;
                else if(state=="In Range"||state=="Idle"||state=="Idle Pause") {hero.RegainControl();hero.AcceptInput();awaitingRespawn=false;Debug.Log("[HollowCraft] Restored Minecraft input after completed native bench respawn");}
            }
            if(dialogue==null||!dialogue.gameObject.activeInHierarchy||(bool)(hiddenField?.GetValue(dialogue) ?? true)) dialogue=UnityEngine.Object.FindObjectsByType<DialogueBox>(FindObjectsSortMode.None).FirstOrDefault(d=>!(bool)(hiddenField?.GetValue(d) ?? true));
            if(dialogue==null||!dialogue.gameObject.activeInHierarchy||(bool)(hiddenField?.GetValue(dialogue) ?? true)) return;
            object text=textField?.GetValue(dialogue);string value=text?.GetType().GetProperty("text")?.GetValue(text,null) as string;
            if(string.IsNullOrEmpty(value)) return;
            string[] pages=Regex.Split(value,"<page>",RegexOptions.IgnoreCase);int index=Mathf.Clamp(dialogue.currentPage-1,0,pages.Length-1);
            DialogueText=Regex.Replace(pages[index],"<[^>]+>","").Trim();
        }
        internal static void AdvanceDialogue() {
            if(dialogue==null||string.IsNullOrEmpty(DialogueText)) return;
            object fsm=typeof(DialogueBox).GetField("proxyFSM",BindingFlags.Instance|BindingFlags.NonPublic)?.GetValue(dialogue);
            string active=(string)Property(fsm,"ActiveStateName");
            foreach(JObject state in FsmStates(fsm)) if((string)state["name"]==active) foreach(JObject transition in (JArray)state["transitions"]) {
                string name=(string)transition["event"];
                if(name=="FINISHED"||name=="NEXT"||name=="JUMP"||name=="NEXT PAGE") {fsm.GetType().GetMethod("SendEvent",new[]{typeof(string)}).Invoke(fsm,new object[]{name});return;}
            }
            dialogue.ShowNextPage();
        }
        internal static JArray DialogueStates()=>FsmStates(dialogue==null?null:typeof(DialogueBox).GetField("proxyFSM",BindingFlags.Instance|BindingFlags.NonPublic)?.GetValue(dialogue));
        internal static bool LeaveBench()
        {
            if(bench==null) return false;
            string active=(string)Property(bench,"ActiveStateName");var states=(IEnumerable)Property(bench,"FsmStates");
            foreach(object state in states) if((string)Property(state,"Name")==active) foreach(object transition in (IEnumerable)Property(state,"Transitions")) {
                string target=(string)Property(transition,"ToState"),eventName=(string)Property(transition,"EventName");
                if(target!=null&&(target.IndexOf("get up",StringComparison.OrdinalIgnoreCase)>=0||target.IndexOf("get off",StringComparison.OrdinalIgnoreCase)>=0||target.IndexOf("stand",StringComparison.OrdinalIgnoreCase)>=0)) {
                    bench.GetType().GetMethod("SendEvent",new[]{typeof(string)}).Invoke(bench,new object[]{eventName});Debug.Log("[HollowCraft] Leaving campaign bench via "+eventName+" -> "+target);return true;
                }
            }
            return false;
        }
        internal static JArray BenchStates()
        {
            return FsmStates(bench);
        }
        internal static JArray FsmStates(object fsm) {var result=new JArray();if(fsm==null) return result;foreach(object state in (IEnumerable)Property(fsm,"FsmStates")) {var transitions=new JArray();foreach(object transition in (IEnumerable)Property(state,"Transitions")) transitions.Add(new JObject{["event"]=(string)Property(transition,"EventName"),["target"]=(string)Property(transition,"ToState")});result.Add(new JObject{["name"]=(string)Property(state,"Name"),["active"]=(string)Property(fsm,"ActiveStateName"),["transitions"]=transitions});}return result;}
        internal static object ConversationFsm(GameObject npc)=>npc.GetComponentsInChildren<Component>(true).FirstOrDefault(c=>c.GetType().Name=="PlayMakerFSM"&&((string)Property(c,"FsmName")??"").IndexOf("Conversation",StringComparison.OrdinalIgnoreCase)>=0);
        internal static bool StartConversation(object fsm) {
            if(fsm==null) return false;var states=FsmStates(fsm);string active=(string)Property(fsm,"ActiveStateName");
            foreach(JObject state in states) if((string)state["name"]==active) foreach(JObject transition in (JArray)state["transitions"]) {
                string name=(string)transition["event"];if(name!=null&&(name.IndexOf("UP",StringComparison.OrdinalIgnoreCase)>=0||name.IndexOf("CONVO",StringComparison.OrdinalIgnoreCase)>=0)) {fsm.GetType().GetMethod("SendEvent",new[]{typeof(string)}).Invoke(fsm,new object[]{name});return true;}
            }return false;
        }
        internal static void BenchCheckpoint()
        {
            var runtime=HollowCraftRuntime.Instance;var hero=HeroController.instance;if(runtime==null||hero==null) return;
            var record=new JObject{["scene"]=GameManager.instance.GetSceneNameString(),["x"]=hero.transform.position.x,["y"]=hero.transform.position.y,["marker"]=PlayerData.instance.respawnMarkerName,["utc"]=DateTime.UtcNow.ToString("o")};
            File.WriteAllText(Path.Combine(HollowCraftBootstrap.SaveRoot(),"campaign-checkpoint.json"),record.ToString());
            runtime.Send(new {type="rest",scene=runtime.Room});Debug.Log("[HollowCraft] Campaign bench checkpoint saved");
        }
        internal static bool Respawn()
        {
            var data=PlayerData.instance;var manager=GameManager.instance;
            if(data==null||manager==null||string.IsNullOrEmpty(data.respawnScene)) return false;
            awaitingRespawn=true;respawnStarted=Time.unscaledTime;manager.ReadyForRespawn(false);return true;
        }
    }
}
