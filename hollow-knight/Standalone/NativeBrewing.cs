using System;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace HollowCraft.Standalone
{
    internal sealed partial class NativeGame
    {
        private void PotionImpact(JObject projectile,double px,double py,int nativeTarget=0,int creatureTarget=0) {
            var potion=NativeStack.Read(projectile["stack"]);string type=(string)potion.Data["Potion"]??"minecraft:water";
            var effects=NativeData.PotionEffects(potion).ToArray();
            int color=(int?)potion.Data["CustomPotionColor"]??(NativeData.Potions.TryGetValue(type,out var metadata)?(int?)metadata["color"]??3694022:3694022);
            Emit(new JObject{["type"]="potionSplash",["entity"]=projectile["entity"]?.DeepClone(),["x"]=px,["y"]=py,["color"]=color,["instant"]=effects.Any(e=>(string)e["id"]=="minecraft:instant_health"||(string)e["id"]=="minecraft:instant_damage"),["water"]=effects.Length==0,["lingering"]=(string)projectile["kind"]=="minecraft:lingering_potion"});
            Sound("minecraft:entity.splash_potion.break",1,.9+random.NextDouble()*.1,px,py);
            if((string)projectile["kind"]=="minecraft:lingering_potion"){SpawnCloud(potion,px,py);return;}
            foreach(var enemy in enemies.Values.Concat(Room.Creatures).ToArray()) {
                double distance=PotionDistance(enemy,px,py);if(distance>=4)continue;
                bool direct=enemy["entity"]!=null?(int?)enemy["entity"]==creatureTarget:(int?)enemy["id"]==nativeTarget;
                ApplyPotionEnemy(enemy,potion,direct?1:1-distance/4);
                if(effects.Length==0){enemy["fireTicks"]=0;enemy["onFire"]=false;}
            }
            double playerDistance=PotionDistance(new JObject{["x"]=x,["y"]=y,["width"]=.6,["height"]=1.8},px,py);
            if(playerDistance<4){ApplyPotion(potion,1-playerDistance/4);if(effects.Length==0)fireTicks=0;}
        }
        private static double PotionDistance(JObject target,double px,double py) {
            double half=Number(target,"width",.6)/2,bottom=Number(target,"y"),closestX=Math.Max(Number(target,"x")-half,Math.Min(px,Number(target,"x")+half)),closestY=Math.Max(bottom,Math.Min(py,bottom+Number(target,"height",1.8)));
            return Math.Sqrt((closestX-px)*(closestX-px)+(closestY-py)*(closestY-py));
        }
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
        private void ApplyPotion(NativeStack item,double factor=1,double? instantFactor=null) {

            foreach(JObject effect in NativeData.PotionEffects(item)) {
                string kind=((string)effect["id"]).Split(':').Last();int level=(int?)effect["amplifier"]??0;
                if(kind=="instant_health")health=Math.Min(20,health+(4<<level)*(instantFactor??factor));
                else if(kind=="instant_damage")Damage((6<<level)*(instantFactor??factor),"magic",x,y);
                else if(kind=="saturation"){food=Math.Min(20,food+(level+1)*factor);saturation=Math.Min(food,saturation+(level+1)*2*factor);}
                else {int ticks=(int)Math.Round(((int?)effect["ticks"]??0)*factor);if(ticks>0)Effect(kind,ticks,level);}
            }
        }
        private void ApplyPotionEnemy(JObject enemy,NativeStack item,double factor,double? instantFactor=null) {
            if((string)enemy["kind"]=="npc"||Number(enemy,"health")<=0)return;
            string species=(string)enemy["kind"]??"";bool undead=species=="undead"||species.Contains("zombie")||species.Contains("skeleton")||species.Contains("zombified")||species=="minecraft:phantom"||species=="minecraft:zoglin";
            if(!(enemy["nativeEffects"] is JObject active))enemy["nativeEffects"]=active=new JObject();
            foreach(JObject effect in NativeData.PotionEffects(item)) {string kind=((string)effect["id"]).Split(':').Last();int level=(int?)effect["amplifier"]??0;
                if(kind=="instant_damage"&&!undead||kind=="instant_health"&&undead)PotionDamage(enemy,(6<<level)*(instantFactor??factor));
                else if(kind=="instant_health"||kind=="instant_damage")enemy["health"]=Math.Min(Number(enemy,"maxHealth",Number(enemy,"health")),Number(enemy,"health")+(4<<level)*(instantFactor??factor));
                else if((kind=="poison"||kind=="regeneration")&&undead||kind=="poison"&&species.Contains("spider"))continue;
                else {int duration=(int)Math.Round(((int?)effect["ticks"]??0)*factor);var old=active[kind];if(duration>0&&(old==null||level>((int?)old["level"]??0)||level==(int?)old["level"]&&duration>((int?)old["ticks"]??0)))active[kind]=new JObject{["ticks"]=duration,["level"]=level};}
            }
        }
        private void TickEnemyEffects() {
            foreach(var enemy in enemies.Values.Concat(Room.Creatures).ToArray()){double movement=1;var active=enemy["nativeEffects"] as JObject;if(active==null)continue;
                foreach(var entry in active.Properties().ToArray()){int remaining=(int)entry.Value["ticks"]-1,level=(int)entry.Value["level"];if(remaining<=0){entry.Remove();continue;}entry.Value["ticks"]=remaining;
                    if(entry.Name=="slowness")movement*=Math.Max(0,1-.15*(level+1));else if(entry.Name=="speed")movement*=1+.2*(level+1);
                    else if(entry.Name=="poison"&&tick%Math.Max(1,25>>Math.Min(8,level))==0&&Number(enemy,"health")>1)PotionDamage(enemy,Math.Min(1,Number(enemy,"health")-1));
                    else if(entry.Name=="wither"&&tick%Math.Max(1,40>>Math.Min(8,level))==0)PotionDamage(enemy,1);
                    else if(entry.Name=="regeneration"&&tick%Math.Max(1,50>>Math.Min(8,level))==0&&Number(enemy,"health")>0)enemy["health"]=Math.Min(Number(enemy,"maxHealth",20),Number(enemy,"health")+1);
                }enemy["moveFactor"]=movement;
            }
        }
        private void PotionDamage(JObject target,double amount){if(target["entity"]!=null)HitCreature(target,amount,true);else HitEnemy(target,amount,"magic",0);}
    }
}
