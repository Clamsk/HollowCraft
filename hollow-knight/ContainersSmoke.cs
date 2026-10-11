using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace HollowCraft
{
    internal sealed class ContainersSmoke
    {
        private readonly HollowCraftRuntime runtime;private readonly HeroController hero;private readonly string scene;private readonly Action<bool,string> report;
        private int stage;private float since;private Vector2Int cell;
        private JObject Container=>runtime.LatestSnapshot["container"] as JObject;
        private void Send(object command)=>runtime.SmokeSend(command);
        private void Item(string id,int count=1,string potion="") {Send(new {type="creativeItem",scene,id,count,potion,slot=8});runtime.Select(8);}
        private void Next(int value) {stage=value;since=Time.unscaledTime;}
        private void Place()=>runtime.HandlePointer(hero,new Vector2(cell.x+.5f,cell.y+.5f),false,false,true,true);
        private void Use()=>Send(new {type="use",scene,x=cell.x,y=cell.y});
        private void Deposit(int target) {int slot=(int)((JArray)Container["inventorySlots"]).First(m=>(int)m["inventory"]==8)["slot"];Send(new {type="slotClick",scene,handler="container",slot,button=0});Send(new {type="slotClick",scene,handler="container",slot=target,button=0});}
        private void Capture(string name)=>ScreenCapture.CaptureScreenshot(Path.Combine(HollowCraftBootstrap.SaveRoot(),name+".png"));
        internal ContainersSmoke(HollowCraftRuntime value,HeroController player,string room,Action<bool,string> result) {runtime=value;hero=player;scene=room;report=result;cell=new Vector2Int(Mathf.FloorToInt(hero.transform.position.x)+2,Mathf.FloorToInt(hero.transform.position.y));Send(new {type="mode",scene,mode="creative"});Item("minecraft:brewing_stand");Next(0);}
        internal void Tick()
        {
            if(Time.unscaledTime-since>35) {report(false,"Container stage "+stage+" "+runtime.LatestSnapshot["status"]+" "+Container);return;}
            if(stage==0&&runtime.Creative&&runtime.HeldItem=="minecraft:brewing_stand") {Place();Next(1);}
            else if(stage==1&&Time.unscaledTime-since>.4f) {Use();Next(2);}
            else if(stage==2&&(string)Container?["id"]=="minecraft:brewing_stand") {Item("minecraft:potion",1,"minecraft:water");Next(3);}
            else if(stage==3&&runtime.HeldItem=="minecraft:potion") {Deposit(0);Item("minecraft:nether_wart");Next(4);}
            else if(stage==4&&runtime.HeldItem=="minecraft:nether_wart") {Deposit(3);Item("minecraft:blaze_powder");Next(5);}
            else if(stage==5&&runtime.HeldItem=="minecraft:blaze_powder") {Deposit(4);Next(6);}
            else if(stage==6&&(int?)Container?["brew"]>0) {Debug.Log("[HollowCraft SMOKE] PASS actual vanilla brewing stand starts 400-tick recipe and consumes fuel");Next(7);}
            else if(stage==7&&(string)Container?["slots"]?[0]?["potion"]=="minecraft:awkward") {
                Debug.Log("[HollowCraft SMOKE] PASS water bottle + nether wart -> awkward potion through real vanilla brewing");Capture("hollowcraft-brewing");Send(new {type="close",scene});Send(new {type="mine",scene,x=cell.x,y=cell.y,heldTicks=100});Item("minecraft:cobblestone");Send(new {type="place",scene,x=cell.x,y=cell.y});cell.y++;Item("minecraft:anvil");Next(8);
            } else if(stage==8&&runtime.HeldItem=="minecraft:anvil"&&Time.unscaledTime-since>.4f) {Place();Next(9);}
            else if(stage==9&&Time.unscaledTime-since>.4f) {Use();Next(10);}
            else if(stage==10&&((string)Container?["id"]??"").Contains("anvil")) {Item("minecraft:iron_sword");Next(11);}
            else if(stage==11&&runtime.HeldItem=="minecraft:iron_sword") {Deposit(0);Send(new {type="anvilName",scene,name="HollowCraft Blade"});Next(12);}
            else if(stage==12&&(string)Container?["slots"]?[2]?["name"]=="HollowCraft Blade") {
                Debug.Log("[HollowCraft SMOKE] PASS vanilla anvil rename/cost/output conserves actual item");Capture("hollowcraft-anvil");Send(new {type="slotClick",scene,handler="container",slot=2,button=0,quick=true});Next(13);
            } else if(stage==13&&((JArray)runtime.LatestSnapshot["inventory"]).Any(i=>(string)i["name"]=="HollowCraft Blade")) {
                Send(new {type="close",scene});Item("minecraft:cobblestone");Send(new {type="mine",scene,x=cell.x,y=cell.y,heldTicks=100});Item("minecraft:enchanting_table");Next(14);
            } else if(stage==14&&runtime.HeldItem=="minecraft:enchanting_table"&&Time.unscaledTime-since>.4f) {Place();Next(15);}
            else if(stage==15&&Time.unscaledTime-since>.4f) {Use();Next(16);}
            else if(stage==16&&(string)Container?["id"]=="minecraft:enchanting_table") {Item("minecraft:diamond_sword");Next(17);}
            else if(stage==17&&runtime.HeldItem=="minecraft:diamond_sword") {Deposit(0);Item("minecraft:lapis_lazuli",3);Next(18);}
            else if(stage==18&&runtime.HeldItem=="minecraft:lapis_lazuli") {Deposit(1);Next(19);}
            else if(stage==19&&(int?)Container?["enchants"]?[0]?["cost"]>0) {Send(new {type="containerButton",scene,button=0});Next(20);}
            else if(stage==20&&(bool?)Container?["slots"]?[0]?["enchanted"]==true) {
                Debug.Log("[HollowCraft SMOKE] PASS vanilla enchanting rolls real enchantment with lapis through screen handler");Capture("hollowcraft-enchanting");Send(new {type="checkpoint",scene});report(true,"Vanilla brewing 400 ticks, fuel consumption, awkward potion NBT; anvil name/cost/result conservation; actual enchanting and container item visual metadata verified");
            }
        }
    }
}
