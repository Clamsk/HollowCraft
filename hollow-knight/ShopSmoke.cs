using System;
using System.IO;
using System.Linq;
using UnityEngine;
namespace HollowCraft {
    internal sealed class ShopSmoke {
        private readonly HollowCraftRuntime runtime;private readonly Action<bool,string> report;private int stage;private float since;
        internal ShopSmoke(HollowCraftRuntime value,Action<bool,string> result) {runtime=value;report=result;Next(0);}
        private void Next(int value) {stage=value;since=Time.unscaledTime;}
        private int Lantern()=>NativeShop.Items.Where(i=>((string)i["name"]).IndexOf("灯笼",StringComparison.Ordinal)>=0).Select(i=>(int)i["index"]).DefaultIfEmpty(-1).First();
        internal void Tick(HeroController hero) {
            if(Time.unscaledTime-since>45) {report(false,"Shop stage "+stage+" room "+runtime.Room+" visible="+NativeShop.Visible+" confirm="+NativeShop.Confirming);return;}
            if(stage==0) {runtime.SmokeSend(new {type="mode",scene=runtime.Room,mode="creative"});GameManager.instance.BeginSceneTransition(new GameManager.SceneLoadInfo{SceneName="Room_shop",EntryGateName="left1",Visualization=GameManager.SceneLoadVisualizations.Default,PreventCameraFadeOut=true,AlwaysUnloadUnusedAssets=true});Next(1);}
            else if(stage==1&&runtime.Room=="Room_shop"&&hero.CanInput()&&Time.unscaledTime-since>3) {
                PlayerData.instance.geo=5000;GameObject.Find("Shop Menu").GetComponents<PlayMakerFSM>().First(f=>f.FsmName=="shop_control").SendEvent("SHOP UP");Next(2);
            } else if(stage==2&&NativeShop.Visible&&!NativeShop.Confirming&&Lantern()>=0&&Time.unscaledTime-since>1) {
                ScreenCapture.CaptureScreenshot(Path.Combine(HollowCraftBootstrap.SaveRoot(),"hollowcraft-shop.png"));if(NativeShop.Buy(Lantern())) Next(3);
            } else if(stage==3&&NativeShop.Confirming&&NativeShop.Cost==1800) {
                ScreenCapture.CaptureScreenshot(Path.Combine(HollowCraftBootstrap.SaveRoot(),"hollowcraft-shop-confirm.png"));if(NativeShop.Decide(false)) Next(4);
            } else if(stage==4&&!NativeShop.Confirming&&Time.unscaledTime-since>.5f) {
                if(PlayerData.instance.hasLantern||PlayerData.instance.geo!=5000) throw new Exception("Cancelled native purchase changed campaign flag or Geo");
                if(NativeShop.Buy(Lantern())) {Debug.Log("[HollowCraft SMOKE] PASS Minecraft shop UI cancellation keeps native stock, Geo and flags");Next(5);}
            } else if(stage==5&&NativeShop.Confirming&&NativeShop.Decide(true)) Next(6);
            else if(stage==6&&PlayerData.instance.hasLantern&&Time.unscaledTime-since>3&&!NativeShop.Confirming) {
                if(PlayerData.instance.geo!=3200) throw new Exception("Native lantern purchase must deduct exactly 1800 Geo");Debug.Log("[HollowCraft SMOKE] PASS actual shop purchase sets hasLantern and deducts 1800 Geo through native confirmation FSM");NativeShop.Close();Next(7);
            } else if(stage==7&&!NativeShop.Visible&&hero.CanInput()&&Time.unscaledTime-since>1) {runtime.SmokeSend(new {type="checkpoint",scene=runtime.Room});report(true,"Actual native stock/localization, Minecraft shop UI, cancel conservation, lantern purchase/1800 Geo/native flag and close/input verified; fixture started shop through native SHOP UP event");}
        }
    }
}
