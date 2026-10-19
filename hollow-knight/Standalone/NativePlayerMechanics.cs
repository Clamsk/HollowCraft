using System;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace HollowCraft.Standalone
{
    internal sealed partial class NativeGame
    {
        private int EffectLevel(string id)=>effects.TryGetValue(id,out var effect)?effect.Item2+1:0;
        private void TickAdditionalPlayer() {
            if(effects.ContainsKey("saturation")){int level=EffectLevel("saturation");food=Math.Min(20,food+level);saturation=Math.Min(food,saturation+2*level);}
            if(effects.ContainsKey("hunger"))exhaustion+=.005*EffectLevel("hunger");
            if(grounded&&tick%5==0&&NativeData.Enchantment(Inventory.Armor[3],"frost_walker")>0&&!InFluid("water")) {
                int radius=Math.Min(16,2+NativeData.Enchantment(Inventory.Armor[3],"frost_walker"));int by=(int)Math.Floor(y-.1);
                for(int bx=(int)Math.Floor(x)-radius;bx<=(int)Math.Floor(x)+radius;bx++){var cell=BlockAt(bx,by);if((string)cell?["id"]!="minecraft:water"||!LiquidSource(cell)||BlockAt(bx,by+1)!=null)continue;cell["id"]="minecraft:ice";cell["frostUntil"]=tick+60+random.Next(61);}
            }
            if(tick%5==0)foreach(var cell in Room.Blocks.Values.Where(c=>(int?)c["frostUntil"]<=tick).ToArray()){cell["id"]="minecraft:water";cell["source"]=true;cell.Remove("frostUntil");}
        }
        private void MovementStats(JObject stats) {
            stats["depthStrider"]=Math.Min(3,NativeData.Enchantment(Inventory.Armor[3],"depth_strider"));
            var under=BlockAt((int)Math.Floor(x),(int)Math.Floor(y-.1));string floor=(string)under?["id"]??"minecraft:stone";
            stats["slipperiness"]=floor.Contains("ice")?.98:floor=="minecraft:slime_block"?.8:.6;
            stats["velocityMultiplier"]=floor=="minecraft:soul_sand"||floor=="minecraft:honey_block"?.4:1;
            int soul=NativeData.Enchantment(Inventory.Armor[3],"soul_speed");if(soul>0&&(floor=="minecraft:soul_sand"||floor=="minecraft:soul_soil")){stats["speed"]=(double)stats["speed"]+.0405+soul*.0105;stats["velocityMultiplier"]=1;}
            var current=FluidCurrent(x,y+.4);stats["currentX"]=current[0];stats["currentY"]=current[1];
            stats["waterHeight"]=FluidDepth("water");stats["lavaHeight"]=FluidDepth("lava");
        }
        private void Thorns(int enemyId,string kind) {
            if(enemyId==0||kind=="thorns")return;var armor=Inventory.Armor.Where(s=>NativeData.Enchantment(s,"thorns")>0).ToArray();
            foreach(var piece in armor)if(random.NextDouble()<Math.Min(1,.15*NativeData.Enchantment(piece,"thorns"))){double damage=random.Next(1,5);if(kind=="mob"){var mob=Room.Creatures.FirstOrDefault(m=>(int)m["entity"]==enemyId);if(mob!=null)HitCreature(mob,damage,true);}else if(enemies.TryGetValue(enemyId,out var enemy))HitEnemy(enemy,damage,"thorns",0);Inventory.Wear(piece,2,Creative);break;}
        }
    }
}
