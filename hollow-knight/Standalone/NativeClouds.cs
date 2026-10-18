using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
namespace HollowCraft.Standalone
{
    internal sealed partial class NativeGame
    {
        private void SpawnCloud(NativeStack potion,double px,double py) {
            if(Room.Clouds.Count>=32)return;string id=(string)potion.Data["Potion"]??"minecraft:water";
            Room.Clouds.Add(new JObject{["entity"]=++serial,["x"]=px,["y"]=py,["radius"]=3,["age"]=0,["duration"]=600,["stack"]=potion.Json(),["potion"]=id,["color"]=NativeData.Potions.TryGetValue(id,out var metadata)?metadata["color"]?.DeepClone()??new JValue(0x9966cc):new JValue(0x9966cc),["cooldowns"]=new JObject()});
        }
        private void TickClouds() {
            foreach(var cloud in Room.Clouds.ToArray()) {
                int age=(int)cloud["age"]+1;cloud["age"]=age;if(age<10)continue;
                cloud["duration"]=(int)cloud["duration"]-1;cloud["radius"]=Number(cloud,"radius")-.005;
                if(Number(cloud,"radius")<.5||(int)cloud["duration"]<=0){Room.Clouds.Remove(cloud);continue;}
                var cooldowns=cloud["cooldowns"] as JObject;if(cooldowns==null){cooldowns=new JObject();cloud["cooldowns"]=cooldowns;}
                foreach(var previous in cooldowns.Properties().ToArray()){int value=(int)previous.Value-1;if(value<=0)previous.Remove();else previous.Value=value;}
                if(age%5!=0)continue;
                double px=Number(cloud,"x"),py=Number(cloud,"y"),radius=Number(cloud,"radius");var potion=NativeStack.Read(cloud["stack"]);
                // Vanilla clouds apply a quarter-duration effect and half instant
                // strength, with a 20-tick per-target cooldown and radius on use.
                bool player=health>0&&!Spectator&&!cooldowns.ContainsKey("player")&&Math.Pow(x-px,2)+Math.Pow(y-py,2)<radius*radius;
                if(player){ApplyPotion(potion,.25,.5);cooldowns["player"]=20;cloud["radius"]=Number(cloud,"radius")-.5;}
                foreach(var target in enemies.Values.Concat(Room.Creatures).ToArray()) {
                    double currentRadius=Number(cloud,"radius");if(currentRadius<.5)break;if(Number(target,"health")<=0||(string)target["kind"]=="npc"||(string)target["kind"]=="resource")continue;
                    string key=(target["entity"]!=null?"mob:":"native:")+((int?)target["entity"]??(int?)target["id"]??0);
                    if(cooldowns.ContainsKey(key)||Math.Pow(Number(target,"x")-px,2)+Math.Pow(Number(target,"y")-py,2)>=currentRadius*currentRadius)continue;
                    ApplyPotionEnemy(target,potion,.25,.5);cooldowns[key]=20;cloud["radius"]=Number(cloud,"radius")-.5;
                }
            }
        }
    }
}
