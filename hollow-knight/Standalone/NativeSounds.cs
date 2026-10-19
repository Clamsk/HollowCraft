using System;
using Newtonsoft.Json.Linq;

namespace HollowCraft.Standalone
{
    internal sealed partial class NativeGame
    {
        private bool soundsWarmed;private double stepDistance;
        private void ConsumptionSound() {
            bool food=Active.Definition.Food>0;bool drink=Active.Id=="minecraft:potion"||Active.Id=="minecraft:milk_bucket"||Active.Id=="minecraft:honey_bottle";
            if((!food&&!drink)||usingTicks<8||usingTicks%4!=0)return;
            bool drinking=drink||Active.Id=="minecraft:honey_bottle";
            Sound(drinking?"minecraft:entity.generic.drink":"minecraft:entity.generic.eat",drinking?.5:.5+random.NextDouble()*.5,.8+random.NextDouble()*.4);
        }
        private void Sound(string id,double volume=1,double pitch=1,double? px=null,double? py=null) {
            if(!(NativeData.Sounds[id] is JArray choices)||choices.Count==0)return;string key=(string)choices[random.Next(choices.Count)];
            Emit(new JObject{["type"]="mcSound",["key"]=key,["play"]=true,["volume"]=volume,["pitch"]=pitch,["x"]=px??x,["y"]=py??y+1,["relative"]=px==null,["linear"]=true,["distance"]=16});
        }
        private void WarmSounds() {if(soundsWarmed)return;soundsWarmed=true;foreach(string id in new[]{"minecraft:entity.player.hurt","minecraft:entity.arrow.shoot","minecraft:block.stone.place","bettercombat:sword_slash","minecraft:entity.generic.eat","minecraft:entity.generic.drink","minecraft:entity.player.burp"})if(NativeData.Sounds[id] is JArray sounds)foreach(string key in sounds)Emit(new JObject{["type"]="mcSound",["key"]=key,["play"]=false});}
        private static string SurfaceSound(string id,string action){string material=id.Contains("glass")?"glass":id.Contains("sand")?"sand":id.Contains("dirt")||id.Contains("gravel")?"gravel":id.Contains("grass")||id.Contains("leaves")?"grass":NativeCatalog.Get(id).Tool=="axe"?"wood":id.Contains("iron")||id.Contains("gold")?"metal":"stone";return "minecraft:block."+material+"."+action;}
    }
}
