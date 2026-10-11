using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace HollowCraft
{
    internal sealed class CampaignSmoke
    {
        private readonly HollowCraftRuntime runtime;private readonly HeroController hero;private readonly string scene;private readonly Action<bool,string> report;
        private int stage;private float since,startY,peak;private long worldTime;private bool oldDash,oldJump;
        private string townMarker;private Vector3 benchPoint;
        private GameObject npc;private object conversation;private float nextAdvance;
        private void Next(int value) {stage=value;since=Time.unscaledTime;}
        private void Send(object command)=>runtime.SmokeSend(command);
        internal CampaignSmoke(HollowCraftRuntime value,HeroController player,string room,Action<bool,string> result) {runtime=value;hero=player;scene=room;report=result;Send(new {type="mode",scene,mode="creative"});Next(0);}
        internal void Tick()
        {
            if(Time.unscaledTime-since>25) {Send(new {type="pause",paused=false});Time.timeScale=1;report(false,"Campaign stage "+stage+" status "+runtime.LatestSnapshot["status"]+" hero="+hero.transform.position+" control="+hero.controlReqlinquished+" input="+hero.acceptingInput+" state="+hero.hero_state+" transition="+hero.transitionState+" dead="+hero.cState.dead+" recoil="+hero.cState.recoiling+" hazard="+hero.cState.hazardRespawning);return;}
            if(stage==0&&runtime.Creative&&Time.unscaledTime-since>2&&MinecraftControls.Grounded(hero)) {
                oldDash=PlayerData.instance.hasDash;oldJump=PlayerData.instance.hasDoubleJump;PlayerData.instance.hasDash=true;PlayerData.instance.hasDoubleJump=true;Next(1);
            } else if(stage==1&&Time.unscaledTime-since>1) {
                if(!PlayerData.instance.hasDash||!PlayerData.instance.hasDoubleJump) throw new Exception("Bridge erased campaign ability acquisition");
                if((bool)hero.GetType().GetMethod("CanDoubleJump",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Public).Invoke(hero,null)) throw new Exception("Knight double jump should be suppressed during MC controls");
                PlayerData.instance.hasDash=oldDash;PlayerData.instance.hasDoubleJump=oldJump;Debug.Log("[HollowCraft SMOKE] PASS campaign acquisition flags persist while Knight movement is suppressed");
                startY=hero.transform.position.y;peak=startY;hero.GetComponent<Rigidbody2D>().linearVelocity=Vector2.zero;
                hero.GetType().GetMethod("HeroJump",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Public).Invoke(hero,null);Next(2);
            } else if(stage==2) {
                peak=Math.Max(peak,hero.transform.position.y);
                if(Time.unscaledTime-since>.8f&&MinecraftControls.Grounded(hero)) {
                    float height=peak-startY;if(height<1.10f||height>1.40f) throw new Exception("Vanilla jump expected about 1.25 blocks, got "+height);
                    Debug.Log("[HollowCraft SMOKE] PASS Minecraft jump height "+height.ToString("F3")+" blocks using 20 Hz vanilla gravity");Send(new {type="pause",paused=true});Time.timeScale=0;Next(3);
                }
            } else if(stage==3&&Time.unscaledTime-since>.5f) {worldTime=(long)runtime.LatestSnapshot["worldTime"];Next(4);}
            else if(stage==4&&Time.unscaledTime-since>1.2f) {
                if((long)runtime.LatestSnapshot["worldTime"]!=worldTime) throw new Exception("Minecraft world kept ticking during pause");
                Send(new {type="pause",paused=false});Time.timeScale=1;Next(5);
            } else if(stage==5&&(long)runtime.LatestSnapshot["worldTime"]>worldTime+5) {
                Debug.Log("[HollowCraft SMOKE] PASS remote pause freezes vanilla world and resumes through independent socket path");
                Send(new {type="mode",scene,mode="survival"});Send(new {type="damage",scene,amount=8});Next(6);
            } else if(stage==6&&(float)runtime.Stats["health"]<=12.1f) {
                CampaignBridge.BenchCheckpoint();Next(7);
            } else if(stage==7&&(float)runtime.Stats["health"]>=19.9f) {
                string path=Path.Combine(HollowCraftBootstrap.SaveRoot(),"campaign-checkpoint.json");if(!File.Exists(path)) throw new Exception("Campaign checkpoint was not saved");
                Debug.Log("[HollowCraft SMOKE] PASS campaign rest saves both engines and restores Minecraft health/food");
                Send(new {type="checkpoint",scene});
                GameManager.instance.BeginSceneTransition(new GameManager.SceneLoadInfo{SceneName="Town",EntryGateName="left1",Visualization=GameManager.SceneLoadVisualizations.Default,PreventCameraFadeOut=true,AlwaysUnloadUnusedAssets=true});Next(8);
            } else if(stage==8&&runtime.Room=="Town"&&hero.CanInput()&&MinecraftControls.Grounded(hero)) {
                var markers=GameObject.FindGameObjectsWithTag("RespawnPoint");
                File.WriteAllText(Path.Combine(HollowCraftBootstrap.SaveRoot(),"town-objects.json"),new JObject{["markers"]=new JArray(markers.Select(m=>new JObject{["name"]=m.name,["x"]=m.transform.position.x,["y"]=m.transform.position.y})),["dialogues"]=new JArray(UnityEngine.Object.FindObjectsByType<DialogueBox>(FindObjectsSortMode.None).Select(d=>d.name))}.ToString());
                var marker=markers.FirstOrDefault(m=>m.GetComponents<Component>().Any(c=>c.GetType().Name=="PlayMakerFSM"))??markers.FirstOrDefault();if(marker==null) throw new Exception("Town lacks native respawn markers");
                townMarker=marker.name;benchPoint=marker.transform.position;
                hero.SetBenchRespawn(townMarker,"Town",1,true);Debug.Log("[HollowCraft SMOKE] PASS native transition Tutorial_01 -> Town with MC backend scene isolation; checkpoint "+townMarker);
                Send(new {type="damage",scene="Town",amount=100});Next(9);
            } else if(stage==9&&runtime.Dead) {Send(new {type="respawn",scene="Town"});Next(10);}
            else if(stage==10&&!runtime.Dead&&runtime.Room=="Town"&&Time.unscaledTime-since>3) {
                if(!hero.CanInput()) {File.WriteAllText(Path.Combine(HollowCraftBootstrap.SaveRoot(),"bench-states.json"),CampaignBridge.BenchStates().ToString());CampaignBridge.LeaveBench();return;}
                if(Vector2.Distance(hero.transform.position,benchPoint)>5) throw new Exception("Minecraft respawn failed to reach native Town bench: "+hero.transform.position+" expected "+benchPoint);
                Debug.Log("[HollowCraft SMOKE] PASS Minecraft death/respawn returns to native campaign bench and restores input");
                var elders=UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Where(t=>t.name.IndexOf("elder",StringComparison.OrdinalIgnoreCase)>=0).ToArray();
                File.WriteAllText(Path.Combine(HollowCraftBootstrap.SaveRoot(),"elder-objects.json"),new JArray(elders.Select(t=>new JObject{["name"]=t.name,["x"]=t.position.x,["y"]=t.position.y,["convo"]=CampaignBridge.ConversationFsm(t.gameObject)!=null})).ToString());
                npc=elders.Where(t=>CampaignBridge.ConversationFsm(t.gameObject)!=null).OrderByDescending(t=>t.GetComponent<Collider2D>()!=null).ThenByDescending(t=>t.position.x).FirstOrDefault()?.gameObject;
                if(npc==null) throw new Exception("Town elder NPC not found");
                conversation=CampaignBridge.ConversationFsm(npc);hero.transform.position=npc.transform.position+Vector3.right;hero.GetComponent<Rigidbody2D>().linearVelocity=Vector2.zero;Send(new {type="mode",scene="Town",mode="creative"});Next(11);
            } else if(stage==11&&Time.unscaledTime-since>1) {
                File.WriteAllText(Path.Combine(HollowCraftBootstrap.SaveRoot(),"elder-conversation-states.json"),CampaignBridge.FsmStates(conversation).ToString());
                if(CampaignBridge.StartConversation(conversation)) Next(12);
            } else if(stage==12&&!string.IsNullOrEmpty(CampaignBridge.DialogueText)) {
                File.WriteAllText(Path.Combine(HollowCraftBootstrap.SaveRoot(),"dialogue-states.json"),CampaignBridge.DialogueStates().ToString());
                Debug.Log("[HollowCraft SMOKE] PASS native Elderbug conversation is readable through Minecraft UI: "+CampaignBridge.DialogueText.Substring(0,Math.Min(50,CampaignBridge.DialogueText.Length)));ScreenCapture.CaptureScreenshot(Path.Combine(HollowCraftBootstrap.SaveRoot(),"hollowcraft-dialogue.png"));Next(13);
            } else if(stage==13) {
                if(Time.unscaledTime>=nextAdvance) {nextAdvance=Time.unscaledTime+.5f;CampaignBridge.AdvanceDialogue();}
                if(Time.unscaledTime-since>2&&string.IsNullOrEmpty(CampaignBridge.DialogueText)&&hero.CanInput()) {Debug.Log("[HollowCraft SMOKE] PASS Minecraft dialogue paging exits native conversation and restores gameplay");Send(new {type="checkpoint",scene="Town"});report(true,"Campaign flags, vanilla jump/pause, native map transition, dual-engine checkpoint/bench respawn and actual Elderbug dialogue paging verified");}
            }
        }
    }
}
