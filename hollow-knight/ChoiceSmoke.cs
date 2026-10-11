using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace HollowCraft {
    internal sealed class ChoiceSmoke {
        private readonly HollowCraftRuntime runtime;private readonly Action<bool,string> report;private int stage,cost;private float since,nextPage;private PlayMakerFSM bell;
        internal ChoiceSmoke(HollowCraftRuntime value,Action<bool,string> result) {runtime=value;report=result;Next(0);}
        private void Next(int value) {stage=value;since=Time.unscaledTime;}
        internal void Tick(HeroController hero) {
            if(Time.unscaledTime-since>40) {report(false,"Choice stage "+stage+" room "+runtime.Room+" visible="+NativeChoice.Visible+" bell="+bell?.ActiveStateName+" text="+CampaignBridge.DialogueText);return;}
            if(stage==0) {PlayerData.instance.openedCrossroads=false;runtime.SmokeSend(new {type="mode",scene=runtime.Room,mode="creative"});GameManager.instance.BeginSceneTransition(new GameManager.SceneLoadInfo{SceneName="Crossroads_47",EntryGateName="door_stagExit",Visualization=GameManager.SceneLoadVisualizations.Default,PreventCameraFadeOut=true,AlwaysUnloadUnusedAssets=true});Next(1);}
            else if(stage==1&&runtime.Room=="Crossroads_47"&&Time.unscaledTime-since>3) {
                PlayerData.instance.geo=5000;
                var fsms=UnityEngine.Object.FindObjectsByType<PlayMakerFSM>(FindObjectsSortMode.None).Where(f=>f.gameObject.scene.name==runtime.Room).ToArray();
                File.WriteAllText(Path.Combine(HollowCraftBootstrap.SaveRoot(),"native-choice-fsms.json"),new JArray(fsms.Select(f=>new JObject{["object"]=f.name,["fsm"]=f.FsmName,["state"]=f.ActiveStateName,["x"]=f.transform.position.x,["y"]=f.transform.position.y,["states"]=CampaignBridge.FsmStates(f),["ints"]=new JObject(f.FsmVariables.IntVariables.Select(v=>new JProperty(v.Name,v.Value))),["strings"]=new JObject(f.FsmVariables.StringVariables.Select(v=>new JProperty(v.Name,v.Value)))})).ToString());
                bell=fsms.First(f=>f.FsmName=="Stag Bell");hero.transform.position=bell.transform.position+Vector3.right*2;hero.GetComponent<Rigidbody2D>().linearVelocity=Vector2.zero;Next(2);
            } else if(stage==2&&Time.unscaledTime-since>.8f&&hero.CanInput()) {bell.SetState("In Range");bell.SendEvent("UP PRESSED");Next(3);}
            else if(stage==3||stage==6) {
                if(NativeChoice.Visible) {
                    if(stage==3) {var yn=GameObject.Find("Text YN").GetComponents<PlayMakerFSM>().First(f=>f.FsmName=="Dialogue Page Control");cost=yn.FsmVariables.FindFsmInt("Toll Cost").Value;if(cost<=0) throw new Exception("Crossroads native toll cost missing");ScreenCapture.CaptureScreenshot(Path.Combine(HollowCraftBootstrap.SaveRoot(),"hollowcraft-toll.png"));if(NativeChoice.Decide(false)) Next(4);}
                    else if(NativeChoice.Decide(true)) Next(7);
                } else if(Time.unscaledTime>=nextPage) {nextPage=Time.unscaledTime+.4f;CampaignBridge.AdvanceDialogue();}
            } else if(stage==4&&hero.CanInput()&&!NativeChoice.Visible&&Time.unscaledTime-since>1) {
                if(PlayerData.instance.geo!=5000||PlayerData.instance.openedCrossroads) throw new Exception("Cancelled native toll changed Geo or station flag");Debug.Log("[HollowCraft SMOKE] PASS native yes/no cancellation preserves Geo and unopened station");bell.SetState("In Range");bell.SendEvent("UP PRESSED");Next(6);
            } else if(stage==7&&PlayerData.instance.openedCrossroads&&hero.CanInput()&&!NativeChoice.Visible&&Time.unscaledTime-since>3) {
                if(PlayerData.instance.geo!=5000-cost) throw new Exception("Native toll did not deduct exact cost "+cost);Debug.Log("[HollowCraft SMOKE] PASS Minecraft yes/no pays "+cost+" Geo, opens Crossroads station and restores native input");runtime.SmokeSend(new {type="checkpoint",scene=runtime.Room});report(true,"Actual Crossroads Stag Bell UP event, dialogue paging, native yes/no cancellation, exact toll payment, station campaign flag and native input verified");
            }
        }
    }
}
