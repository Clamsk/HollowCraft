using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace HollowCraft
{
    // Combat driver controls only the MC player; the real encounter AI, health and quest FSM remain intact.
    internal sealed class BossSmoke
    {
        private readonly HollowCraftRuntime runtime;private readonly HeroController hero;private readonly Action<bool,string> report;
        private int stage;private float since,nextAttack,nextLog;private HealthManager boss;private bool injured;
        private void Send(object command)=>runtime.SmokeSend(command);
        private void Next(int value) {stage=value;since=Time.unscaledTime;}
        internal BossSmoke(HollowCraftRuntime value,HeroController player,Action<bool,string> result) {runtime=value;hero=player;report=result;Send(new {type="mode",scene=runtime.Room,mode="creative"});Send(new {type="creativeItem",scene=runtime.Room,id="minecraft:diamond_sword",slot=8});runtime.Select(8);Next(0);}
        internal void Tick()
        {
            if(Time.unscaledTime-since>(stage<2?40:180)) {report(false,"Actual False Knight encounter stage "+stage+" injured="+injured+" scene="+runtime.Room+" boss="+boss?.hp+" quest="+PlayerData.instance.falseKnightDefeated);return;}
            if(stage==0&&runtime.Creative&&runtime.HeldItem=="minecraft:diamond_sword") {GameManager.instance.BeginSceneTransition(new GameManager.SceneLoadInfo{SceneName="Crossroads_10",EntryGateName="left1",Visualization=GameManager.SceneLoadVisualizations.Default,PreventCameraFadeOut=true,AlwaysUnloadUnusedAssets=true});Next(1);}
            else if(stage==1&&runtime.Room=="Crossroads_10"&&hero.CanInput()) {
                var enemies=UnityEngine.Object.FindObjectsByType<HealthManager>(FindObjectsInactive.Include,FindObjectsSortMode.None);
                File.WriteAllText(Path.Combine(HollowCraftBootstrap.SaveRoot(),"false-knight-enemies.json"),new JArray(enemies.Select(e=>new JObject{["name"]=e.name,["hp"]=e.hp,["x"]=e.transform.position.x,["y"]=e.transform.position.y,["active"]=e.gameObject.activeInHierarchy})).ToString());
                boss=enemies.FirstOrDefault(e=>e.name.IndexOf("False Knight",StringComparison.OrdinalIgnoreCase)>=0);if(boss==null) throw new Exception("Actual Crossroads_10 False Knight was not found");
                Vector3 point=boss.transform.position;var ground=Physics2D.RaycastAll(point,Vector2.down,100,LayerMask.GetMask("Terrain")).FirstOrDefault(h=>h.collider!=null&&!h.collider.isTrigger);
                hero.transform.position=new Vector3(point.x-3,ground.collider!=null?ground.point.y+1:point.y-6,0);hero.GetComponent<Rigidbody2D>().linearVelocity=Vector2.zero;Next(2);
            } else if(stage==2) {
                if(PlayerData.instance.falseKnightDefeated||PlayerData.instance.killedFalseKnight) {Send(new {type="checkpoint",scene=runtime.Room});Debug.Log("[HollowCraft SMOKE] PASS real False Knight AI/phase/death FSM sets campaign quest flag using MC weapon attacks");ScreenCapture.CaptureScreenshot(Path.Combine(HollowCraftBootstrap.SaveRoot(),"hollowcraft-false-knight.png"));report(true,"Actual False Knight encounter completed with MC diamond sword and native AI/phase/death/quest flow; creative player used only as integration fixture");return;}
                var available=UnityEngine.Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None).Where(e=>!e.isDead&&e.hp>0&&!e.IsInvincible&&Vector2.Distance(e.transform.position,boss.transform.position)<20&&(e.name.IndexOf("False Knight",StringComparison.OrdinalIgnoreCase)>=0||e.transform.IsChildOf(boss.transform))).ToArray();
                var target=available.FirstOrDefault(e=>e.GetComponent<Collider2D>()?.enabled==true);
                if(Time.unscaledTime>=nextLog) {nextLog=Time.unscaledTime+5;Debug.Log("[HollowCraft BOSS] hero="+hero.transform.position+" boss="+boss.hp+" invincible="+boss.IsInvincible+" target="+target?.name+" hp="+target?.hp+" input="+hero.CanInput());}
                if(target==null||!hero.CanInput()) return;
                if(Time.unscaledTime>=nextAttack&&!runtime.Combat.Swinging) {
                    nextAttack=Time.unscaledTime+.25f;var collider=target.GetComponent<Collider2D>()??target.GetComponentInChildren<Collider2D>();if(collider==null) return;
                    Vector3 point=collider.bounds.center;hero.transform.position=new Vector3(point.x-collider.bounds.extents.x-1,collider.bounds.min.y+.9f,0);hero.GetComponent<Rigidbody2D>().linearVelocity=Vector2.zero;
                    runtime.Combat.Request(runtime.Room,hero,point);injured|=target.hp<300;
                }
            }
        }
    }
}
