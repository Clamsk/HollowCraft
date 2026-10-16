using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace HollowCraft
{
    // Opt-in recording session; all native/MC identity changes stay in private saves.
    internal sealed class BossRecording : MonoBehaviour
    {
        private static BossRecording instance;
        private readonly Dictionary<string,JObject> bosses=new Dictionary<string,JObject>();
        private string requestPath,statusPath,requestId,session;
        private JObject selection;
        private int level;
        private bool booted,pending,toHub,showHelp=true;
        private float nextRead;
        private string message="Preparing recording session";
        internal static bool Active=>instance!=null&&instance.enabled;
        private void Awake() {
            requestPath=Environment.GetEnvironmentVariable("HOLLOWCRAFT_BOSS_REQUEST");
            if(string.IsNullOrEmpty(requestPath)) {enabled=false;return;}
            instance=this;statusPath=Environment.GetEnvironmentVariable("HOLLOWCRAFT_BOSS_STATUS");
            session=Environment.GetEnvironmentVariable("HOLLOWCRAFT_RECORD_SESSION");
            try {
                var catalog=JObject.Parse(File.ReadAllText(Environment.GetEnvironmentVariable("HOLLOWCRAFT_BOSS_CATALOG")));
                if((int?)catalog["schema"]!=1)throw new InvalidDataException("Unknown boss catalogue schema");
                foreach(JObject item in (JArray)catalog["items"])bosses.Add((string)item["id"],item);
                ReadRequest();
            } catch(Exception error) {Status("error",error.Message);enabled=false;}
        }
        private void ReadRequest() {
            if(!File.Exists(requestPath))return;
            var file=new FileInfo(requestPath);if(file.Length>65536)throw new InvalidDataException("Recording request too large");
            var data=JObject.Parse(File.ReadAllText(requestPath));
            if((int?)data["schema"]!=1||(string)data["session"]!=session)return;
            string id=(string)data["request"];if(string.IsNullOrEmpty(id)||id==requestId)return;
            if((string)data["action"]=="hub") {requestId=id;pending=true;toHub=true;return;}
            if(!bosses.TryGetValue((string)data["boss"]??"",out var item))throw new InvalidDataException("Boss is not in the installed catalogue");
            selection=item;level=Mathf.Clamp((int?)data["level"]??0,0,(bool?)item["godhome"]==true?2:0);
            requestId=id;pending=true;toHub=false;Status("queued","Boss selected: "+(string)item["en"]);
        }
        private void Update() {
            try {
                if(Time.unscaledTime>=nextRead) {nextRead=Time.unscaledTime+.25f;ReadRequest();}
                if(Application.isFocused) {
                    if(Input.GetKeyDown(KeyCode.F8))showHelp=!showHelp;
                    if(Input.GetKeyDown(KeyCode.F5)&&selection!=null) {pending=true;toHub=false;}
                    if(Input.GetKeyDown(KeyCode.F6)) {pending=true;toHub=true;}
                }
                var manager=GameManager.instance;
                if(!booted) {
                    if(manager==null||selection==null||Time.unscaledTime<3)return;
                    booted=true;manager.profileID=4;manager.StartNewGame(false,true);CampaignSession.Landed();
                    Status("loading","Starting a separate Godseeker recording save");return;
                }
                if(!pending||manager==null)return;
                var runtime=HollowCraftRuntime.Instance;var hero=HeroController.instance;
                if(runtime==null||!runtime.InGame||runtime.LatestSnapshot==null||hero==null||manager.IsInSceneTransition)return;
                runtime.PrepareRecordingTransition();
                if(!runtime.Dead&&!hero.CanInput())return;
                string room=manager.GetSceneNameString();
                if(toHub) {
                    pending=false;manager.playerData.bossRushMode=true;
                    Transition("GG_Workshop","left1");Status("hub","Returning to the Hall of Gods");return;
                }
                // Reload through the workshop so Unity/native FSMs also restart when
                // the same boss is selected twice. No old actor/phase is reused.
                if(room!="GG_Workshop"&&(room.StartsWith("GG_")&&BossSceneController.Instance!=null||room=="Room_Final_Boss_Core"||NativeScriptedAttacks.IsArena(room))) {
                    manager.playerData.bossRushMode=true;Transition("GG_Workshop","left1");Status("loading","Resetting the previous arena");return;
                }
                StartFight(manager);pending=false;
            } catch(Exception error) {pending=false;Debug.LogError("[HollowCraft] Boss recording: "+error);Status("error",error.Message);}
        }
        private void StartFight(GameManager manager) {
            bool godhome=(bool?)selection["godhome"]==true;
            string scene=(string)selection["scenes"][level];
            if(!Application.CanStreamedLevelBeLoaded(scene))throw new InvalidDataException("Installed game lacks arena "+scene);
            BossSequenceController.Reset();
            CampaignSession.NewGame(manager);CampaignSession.Landed(); // Fresh vanilla survival kit/health/hunger via scene handshake.
            var data=manager.playerData;data.bossRushMode=godhome;data.dreamReturnScene="GG_Workshop";
            data.bossReturnEntryGate=(string)selection["returnGate"];
            data.bossStatueTargetLevel=level;data.currentBossStatueCompletionKey=(string)selection["state"];
            data.health=data.maxHealth;data.isFirstGame=false;
            if(!godhome) {data.killedHollowKnight=false;data.killedFinalBoss=false;}
            BossSceneController.SetupEvent=godhome?(BossSceneController.SetupEventDelegate)SetupBoss:null;
            Transition(scene,(string)selection["entryGate"]);
            Status("loading","Loading "+(string)selection["en"]+" with the survival starter kit");
        }
        private void SetupBoss(BossSceneController controller) {
            controller.BossLevel=level;controller.DreamReturnEvent="DREAM RETURN";
            controller.OnBossesDead+=()=>Status("victory","Boss defeated — F5 to retry, F6 for the Hall of Gods");
            controller.OnBossSceneComplete+=()=> {
                var runtime=HollowCraftRuntime.Instance;runtime?.PrepareRecordingTransition();
                GameManager.instance.playerData.bossRushMode=true;
                Transition("GG_Workshop","left1");
            };
            Status("ready",(string)selection["en"]+" — survival starter kit");
        }
        private static void Transition(string scene,string gate) {
            GameManager.instance.BeginSceneTransition(new GameManager.SceneLoadInfo {
                SceneName=scene,EntryGateName=gate,Visualization=GameManager.SceneLoadVisualizations.Default,
                AlwaysUnloadUnusedAssets=true,WaitForSceneTransitionCameraFade=false
            });
        }
        internal static bool Respawn() {
            if(!Active||instance.selection==null)return false;
            instance.pending=true;instance.toHub=false;return true;
        }
        private void Status(string state,string value) {
            message=value;
            if(string.IsNullOrEmpty(statusPath))return;
            try {
                var data=new JObject { ["schema"]=1,["session"]=session,["request"]=requestId,["state"]=state,
                    ["message"]=value,["boss"]=(string)selection?["id"],["level"]=level,["utc"]=DateTime.UtcNow.ToString("o") };
                string temp=statusPath+".tmp";File.WriteAllText(temp,data.ToString());
                if(File.Exists(statusPath))File.Replace(temp,statusPath,null);else File.Move(temp,statusPath);
            } catch(IOException error) {Debug.LogWarning("[HollowCraft] Recording status: "+error.Message);}
        }
        private void OnGUI() {
            if(!showHelp)return;
            var style=new GUIStyle(GUI.skin.label) {fontSize=Math.Max(12,Screen.height/64)};style.normal.textColor=Color.white;
            GUI.Box(new Rect(12,12,Math.Min(640,Screen.width-24),64),"");
            GUI.Label(new Rect(24,18,600,24),"Boss Recording  |  F5 Retry  |  F6 Hall of Gods  |  F8 Hide",style);
            GUI.Label(new Rect(24,44,600,24),message,style);
        }
    }
}
