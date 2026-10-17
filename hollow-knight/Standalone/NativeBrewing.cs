using System;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace HollowCraft.Standalone
{
    internal sealed partial class NativeGame
    {
        private void TickBrew(NativeContainer box) {
            var reagent=box.Slots[3];bool can=Enumerable.Range(0,3).Any(i=>NativeData.Brew(box.Slots[i],reagent)!=null);
            string key=reagent.Empty?"":reagent.Id;
            if(box.Brew>0){if(!can||box.BrewKey!=key){box.Brew=0;return;}if(--box.Brew>0)return;
                for(int i=0;i<3;i++){var result=NativeData.Brew(box.Slots[i],reagent);if(result==null)continue;box.Slots[i].Id=(string)result["outputItem"];box.Slots[i].Data["Potion"]=(string)result["outputPotion"];}
                string remainder=reagent.Definition.Remainder;reagent.Count--;reagent.Normalize();if(remainder!=null){var left=NativeStack.New(remainder);if(reagent.Empty)box.Slots[3]=left;else DropAt(left,x,y+.5);}return;
            }
            if(box.BrewFuel==0&&box.Slots[4].Id=="minecraft:blaze_powder"&&!box.Slots[4].Empty){box.Slots[4].Count--;box.Slots[4].Normalize();box.BrewFuel=20;}
            if(can&&box.BrewFuel>0){box.BrewFuel--;box.Brew=400;box.BrewKey=key;}
        }
        private void ApplyPotion(NativeStack item,double factor=1) {
            string id=(string)item.Data["Potion"]??"minecraft:water";if(!NativeData.Potions.TryGetValue(id,out var potion))return;
            foreach(JObject effect in potion["effects"] as JArray??new JArray()) {
                string kind=((string)effect["id"]).Split(':').Last();int level=(int?)effect["amplifier"]??0;
                if(kind=="instant_health")health=Math.Min(20,health+(4<<level)*factor);
                else if(kind=="instant_damage")Damage((6<<level)*factor,"magic",x,y);
                else if(kind=="saturation"){food=Math.Min(20,food+(level+1)*factor);saturation=Math.Min(food,saturation+(level+1)*2*factor);}
                else {int ticks=(int)Math.Round(((int?)effect["ticks"]??0)*factor);if(ticks>0)Effect(kind,ticks,level);}
            }
        }
        private void ApplyPotionEnemy(JObject enemy,NativeStack item,double factor) {
            if((string)enemy["kind"]=="npc"||Number(enemy,"health")<=0)return;string id=(string)item.Data["Potion"]??"minecraft:water";if(!NativeData.Potions.TryGetValue(id,out var potion))return;
            if(!(enemy["nativeEffects"] is JObject active))enemy["nativeEffects"]=active=new JObject();
            foreach(JObject effect in potion["effects"] as JArray??new JArray()) {string kind=((string)effect["id"]).Split(':').Last();int level=(int?)effect["amplifier"]??0;
                if(kind=="instant_damage")HitEnemy(enemy,(6<<level)*factor,"magic",0);
                else if(kind=="instant_health")enemy["health"]=Math.Min(Number(enemy,"maxHealth",Number(enemy,"health")),Number(enemy,"health")+(4<<level)*factor);
                else {int duration=(int)Math.Round(((int?)effect["ticks"]??0)*factor);var old=active[kind];if(duration>0&&(old==null||level>((int?)old["level"]??0)||level==(int?)old["level"]&&duration>((int?)old["ticks"]??0)))active[kind]=new JObject{["ticks"]=duration,["level"]=level};}
            }
        }
        private void TickEnemyEffects() {
            foreach(var enemy in enemies.Values){double movement=1;var active=enemy["nativeEffects"] as JObject;if(active==null)continue;
                foreach(var entry in active.Properties().ToArray()){int remaining=(int)entry.Value["ticks"]-1,level=(int)entry.Value["level"];if(remaining<=0){entry.Remove();continue;}entry.Value["ticks"]=remaining;
                    if(entry.Name=="slowness")movement*=Math.Max(0,1-.15*(level+1));else if(entry.Name=="speed")movement*=1+.2*(level+1);
                    else if(entry.Name=="poison"&&tick%Math.Max(1,25>>Math.Min(8,level))==0&&Number(enemy,"health")>1)HitEnemy(enemy,Math.Min(1,Number(enemy,"health")-1),"magic",0);
                    else if(entry.Name=="wither"&&tick%Math.Max(1,40>>Math.Min(8,level))==0)HitEnemy(enemy,1,"magic",0);
                    else if(entry.Name=="regeneration"&&tick%Math.Max(1,50>>Math.Min(8,level))==0&&Number(enemy,"health")>0)enemy["health"]=Math.Min(Number(enemy,"maxHealth",20),Number(enemy,"health")+1);
                }enemy["moveFactor"]=movement;
            }
        }
    }
}
