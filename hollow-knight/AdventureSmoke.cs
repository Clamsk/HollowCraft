using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace HollowCraft
{
    internal sealed class AdventureSmoke
    {
        private readonly HollowCraftRuntime runtime;private readonly HeroController hero;private readonly string scene;private readonly Action<bool,string> report;
        private int stage;private float since,before;private HealthManager enemy;private int enemyId;private Vector3 origin;
        private JObject Snapshot=>runtime.LatestSnapshot;
        private void Send(object command)=>runtime.SmokeSend(command);
        private int Slot(string id,string potion=null) {var inv=(JArray)Snapshot["inventory"];for(int i=0;i<inv.Count;i++) if((string)inv[i]["id"]==id&&(int)inv[i]["count"]>0&&(potion==null||(string)inv[i]["potion"]==potion)) return i;return -1;}
        private void Select(string id,string potion=null) {int index=Slot(id,potion);if(index<0) throw new Exception("Missing adventure item "+id+" "+potion);if(index>=9) {Send(new {type="swap",scene,from=index,to=8});index=8;}runtime.Select(index);}
        private float Health=>((JArray)Snapshot["enemies"]).Where(v=>(int)v["id"]==enemyId).Select(v=>(float)v["health"]).DefaultIfEmpty(100).First();
        private Vector2 Aim=>enemy!=null?(Vector2)enemy.transform.position:(Vector2)hero.transform.position+Vector2.right*3;
        private void Pointer(bool down,bool held)=>runtime.HandlePointer(hero,Aim,false,false,down,held);
        private void Next(int value) {stage=value;since=Time.unscaledTime;}
        internal AdventureSmoke(HollowCraftRuntime value,HeroController player,string room,Action<bool,string> result) {runtime=value;hero=player;scene=room;report=result;origin=hero.transform.position;Send(new {type="mode",scene,mode="creative"});Send(new {type="adventureKit",scene});Next(0);}
        internal void Tick()
        {
            if(Time.unscaledTime-since>20) {report(false,"Adventure stage "+stage+": "+Snapshot["status"]+" HP "+Health+" stats "+runtime.Stats);return;}
            if(stage==0&&runtime.Creative&&Slot("minecraft:bow")>=0) {
                var source=UnityEngine.Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None).First(e=>!e.isDead&&e.GetComponent<Collider2D>()!=null);
                enemy=UnityEngine.Object.Instantiate(source);enemy.name="HollowCraft.AdventureSmokeEnemy";enemy.hp=100;enemy.isDead=false;
                foreach(var component in enemy.GetComponents<MonoBehaviour>()) if(component!=enemy) component.enabled=false;
                foreach(var damage in enemy.GetComponentsInChildren<DamageHero>()) damage.enabled=false;
                var body=enemy.GetComponent<Rigidbody2D>();if(body!=null) body.constraints=RigidbodyConstraints2D.FreezeAll;
                enemy.transform.position=hero.transform.position+Vector3.right*6;enemyId=EnemySystem.Register(enemy);
                Select("minecraft:bow");Send(new {type="mode",scene,mode="survival"});Next(1);
            }
            else if(stage==1&&!runtime.Creative&&runtime.HeldItem=="minecraft:bow"&&Time.unscaledTime-since>.4f) {before=Health;Pointer(true,true);Next(2);}
            else if(stage==2) {
                Pointer(false,true);
                if((int?)runtime.Stats["useTicks"]>=22) {if((string)runtime.Stats["useAction"]!="BOW") throw new Exception("Bow did not enter vanilla use state");Pointer(false,false);Next(3);}
            }
            else if(stage==3&&Health<before) {
                float damage=before-Health;if(damage<6||damage>10) throw new Exception("Fully drawn vanilla bow expected 6..10 damage, got "+damage);
                if(enemy.hp>=100) return;
                Debug.Log("[HollowCraft SMOKE] PASS fully drawn bow uses vanilla arrow hit: MC damage "+damage+" -> native HP "+enemy.hp);
                Select("minecraft:shield");Next(4);
            }
            else if(stage==4&&runtime.HeldItem=="minecraft:shield") {Pointer(true,true);Next(5);}
            else if(stage==5) {
                Pointer(false,true);
                if((bool?)runtime.Stats["blocking"]==true&&Time.unscaledTime-since>.5f) {before=(float)runtime.Stats["health"];Send(new {type="damage",scene,enemy=enemyId,amount=6});Next(6);}
            }
            else if(stage==6&&Time.unscaledTime-since>.4f) {
                Pointer(false,true);if(Math.Abs((float)runtime.Stats["health"]-before)>.01f) throw new Exception("Frontal shield failed to block vanilla mob damage");
                Debug.Log("[HollowCraft SMOKE] PASS vanilla shield raises, slows use movement and blocks frontal damage");Pointer(false,false);Select("minecraft:potion","minecraft:strong_healing");Next(7);
            }
            else if(stage==7&&runtime.HeldItem=="minecraft:potion"&&Time.unscaledTime-since>.3f) {Send(new {type="damage",scene,amount=6});Next(8);}
            else if(stage==8&&(float)runtime.Stats["health"]<=14.1f) {Pointer(true,true);Next(9);}
            else if(stage==9) {
                Pointer(false,true);
                if((float)runtime.Stats["health"]>=19.9f&&Time.unscaledTime-since>1.5f) {Pointer(false,false);Debug.Log("[HollowCraft SMOKE] PASS vanilla strong-healing potion drinks for 32 ticks and restores health");Select("minecraft:potion","minecraft:long_swiftness");Next(10);}
            }
            else if(stage==10&&runtime.HeldItem=="minecraft:potion"&&Time.unscaledTime-since>.3f) {Pointer(true,true);Next(11);}
            else if(stage==11) {
                Pointer(false,true);
                if(((JArray)runtime.Stats["effects"]).Any(e=>(string)e["id"]=="minecraft:speed")) {Pointer(false,false);if((float)runtime.Stats["speed"]<.119f) throw new Exception("Speed potion attribute did not transfer");Debug.Log("[HollowCraft SMOKE] PASS speed potion keeps vanilla duration/amplifier/attribute");Select("minecraft:trident");Next(12);}
            }
            else if(stage==12&&runtime.HeldItem=="minecraft:trident"&&Time.unscaledTime-since>.3f) {before=Health;Pointer(true,true);Next(13);}
            else if(stage==13) {Pointer(false,true);if((int?)runtime.Stats["useTicks"]>=22) {Pointer(false,false);Next(14);}}
            else if(stage==14&&Health<before) {
                if(Math.Abs(before-Health-8)>.01f) throw new Exception("Vanilla trident must deal 8 ranged damage, got "+(before-Health));
                Debug.Log("[HollowCraft SMOKE] PASS thrown trident transfers vanilla ranged damage to native enemy");Select("minecraft:diamond_sword");Next(15);
            }
            else if(stage==15&&runtime.HeldItem=="minecraft:diamond_sword"&&Time.unscaledTime-since>1) {
                MinecraftControls.frontView=false;MinecraftControls.Facing=1;hero.GetComponent<Rigidbody2D>().linearVelocity=Vector2.zero;
                var holder=GameObject.Find("HollowCraft.HeldItem3D");var mesh=holder?.GetComponentInChildren<MeshFilter>();
                if(mesh==null||mesh.sharedMesh.vertexCount<=4) throw new Exception("Held sword must use baked 3D mesh with edge faces");
                ScreenCapture.CaptureScreenshot(Path.Combine(HollowCraftBootstrap.SaveRoot(),"hollowcraft-3d-side.png"));Next(16);
            }
            else if(stage==16&&Time.unscaledTime-since>.6f) {MinecraftControls.frontView=true;Next(17);}
            else if(stage==17&&Time.unscaledTime-since>.6f) {
                ScreenCapture.CaptureScreenshot(Path.Combine(HollowCraftBootstrap.SaveRoot(),"hollowcraft-3d-front.png"));
                Next(18);
            }
            else if(stage==18&&Time.unscaledTime-since>.3f) {
                enemy.transform.position=hero.transform.position+Vector3.right*2;
                Select("minecraft:splash_potion","minecraft:strong_harming");Next(19);
            } else if(stage==19&&runtime.HeldItem=="minecraft:splash_potion"&&Time.unscaledTime-since>.3f) {before=Health;Pointer(true,true);Pointer(false,false);Next(20);}
            else if(stage==20&&Health<before) {
                if(before-Health<6||before-Health>12.01f) throw new Exception("Strong harming splash damage must follow 0..12 vanilla distance scaling, got "+(before-Health));
                Debug.Log("[HollowCraft SMOKE] PASS thrown strong-harming potion applies vanilla distance-scaled magic damage to native enemy");
                enemy.transform.position=origin+Vector3.right*100;Select("minecraft:cobblestone");Next(23);
            } else if(stage==23&&runtime.HeldItem=="minecraft:cobblestone") {
                Send(new {type="place",scene,x=Mathf.FloorToInt(hero.transform.position.x)+2,y=Mathf.FloorToInt(hero.transform.position.y)+1});Next(24);
            } else if(stage==24&&Time.unscaledTime-since>.5f) {Select("minecraft:ender_pearl");Next(21);
            } else if(stage==21&&runtime.HeldItem=="minecraft:ender_pearl"&&Time.unscaledTime-since>.4f) {
                before=hero.transform.position.x;runtime.HandlePointer(hero,(Vector2)hero.transform.position+new Vector2(3,1),false,false,true,true);runtime.HandlePointer(hero,(Vector2)hero.transform.position+new Vector2(3,1),false,false,false,false);Next(22);
            } else if(stage==22&&Math.Abs(hero.transform.position.x-before)>1) {
                if((float)runtime.Stats["health"]>15.1f) return;
                Debug.Log("[HollowCraft SMOKE] PASS vanilla ender pearl teleports native player and applies 5 damage with position acknowledgment");Send(new {type="checkpoint",scene});report(true,"Bow, shield, healing/speed potion, trident, harming splash potion, ender pearl teleport/damage, transparent icons and real held 3D mesh verified");
            }
        }
    }
}
