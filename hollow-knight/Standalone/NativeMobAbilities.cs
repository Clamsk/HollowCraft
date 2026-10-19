using System;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace HollowCraft.Standalone
{
    internal sealed partial class NativeGame
    {
        private bool MobBuff(JObject mob,string id)=>Number(mob["nativeEffects"]?[id] as JObject??new JObject(),"ticks")>0;
        private bool TickMobAbility(JObject mob) {
            string kind=(string)mob["kind"];double px=Number(mob,"x"),py=Number(mob,"y"),distance=Math.Abs(px-x);bool inWater=FluidAt(px,py+.4,"water");
            if(kind=="minecraft:enderman"&&inWater&&tick%20==0){HitCreature(mob,1,true,false);MobTeleport(mob);}
            bool aquatic=NativeMobCatalog.Find(kind)?.Motion=="swim";
            if(inWater&&!aquatic&&kind!="minecraft:drowned"&&kind!="minecraft:turtle"&&kind!="minecraft:guardian"&&kind!="minecraft:elder_guardian"){int air=(int?)mob["air"]??300;mob["air"]=air-1;if(air<=-20){mob["air"]=0;HitCreature(mob,2,true,false);}}else mob["air"]=300;
            if(kind=="minecraft:chicken"&&tick>=Number(mob,"eggAt",tick+1)){DropAt(NativeStack.New("egg"),px,py+.3);mob["eggAt"]=tick+6000+random.Next(6001);}else if(kind=="minecraft:chicken"&&mob["eggAt"]==null)mob["eggAt"]=tick+6000+random.Next(6001);
            if(kind=="minecraft:sheep"&&(bool?)mob["sheared"]==true&&tick%200==0&&(string)BlockAt((int)Math.Floor(px),(int)Math.Floor(py-.1))?["id"]=="minecraft:grass_block"){mob["sheared"]=false;var grass=BlockAt((int)Math.Floor(px),(int)Math.Floor(py-.1));grass["id"]="minecraft:dirt";}
            if(kind=="minecraft:pufferfish"&&distance<1.2&&!Creative&&!Spectator&&tick>=Number(mob,"nextPuff")){mob["nextPuff"]=tick+20;Damage(2,"mob",px,py,(int)mob["entity"]);Effect("poison",120,0);}
            if(kind=="minecraft:witch"&&Number(mob,"health")<Number(mob,"maxHealth")&&tick%80==0)mob["health"]=Math.Min(Number(mob,"maxHealth"),Number(mob,"health")+4);
            if(kind=="minecraft:dolphin"&&inWater&&InFluid("water")&&distance<8&&Math.Abs(py-y)<5)Effect("dolphins_grace",100,0);
            if(kind=="minecraft:warden"&&distance<16&&tick%100==0&&!Creative&&!Spectator)Effect("darkness",120,0);
            if(kind=="minecraft:elder_guardian"&&distance<16&&tick%1200==0&&!Creative&&!Spectator)Effect("mining_fatigue",6000,2);
            if(kind=="minecraft:shulker"||kind=="minecraft:warden") {
                if(!Creative&&!Spectator&&health>0&&tick>=Number(mob,"nextAbility")&&distance<(kind=="minecraft:warden"?15:16)&&distance>2&&Math.Abs(py-y)<10&&ClearSight(px,py+1,x,y+1)){mob["nextAbility"]=tick+(kind=="minecraft:warden"?80:50);MobRanged(mob,kind=="minecraft:warden"?"beam":"bullet");}
                if(kind=="minecraft:shulker")return true;
            }
            if(kind=="minecraft:iron_golem"||kind=="minecraft:snow_golem"||kind=="minecraft:wolf"&&(bool?)mob["tamed"]==true&&(bool?)mob["sitting"]!=true) {
                JObject target=Room.Creatures.Where(m=>m!=mob&&Number(m,"health")>0&&NativeMobCatalog.Find((string)m["kind"])?.Disposition=="hostile"&&Math.Abs(Number(m,"x")-px)<10&&Math.Abs(Number(m,"y")-py)<5).OrderBy(m=>Math.Abs(Number(m,"x")-px)).FirstOrDefault();
                bool native=false;if(target==null){target=enemies.Values.Where(e=>Number(e,"health")>0&&(string)e["kind"]!="npc"&&(string)e["kind"]!="resource"&&Math.Abs(Number(e,"x")-px)<8&&Math.Abs(Number(e,"y")-py)<4).OrderBy(e=>Math.Abs(Number(e,"x")-px)).FirstOrDefault();native=target!=null;}
                if(target!=null){double tx=Number(target,"x"),ty=Number(target,"y"),dx=Math.Abs(tx-px);MoveCreature(mob,kind=="minecraft:snow_golem"?0:Navigate(mob,tx,ty,.15),Number(mob,"vy")-.08);if(tick>=Number(mob,"allyAttack")&&ClearSight(px,py+.8,tx,ty+.8)){if(kind=="minecraft:snow_golem"){if(dx<10){mob["allyAttack"]=tick+20;MobRanged(mob,"snowball",tx,ty+.7);}}else if(dx<1.7){mob["allyAttack"]=tick+20;mob["attacking"]=8;double damage=kind=="minecraft:iron_golem"?7+random.Next(15):4;if(native)HitEnemy(target,damage,"mob",tx<px?180:0);else HitCreature(target,damage,true,false);}}return true;}
            }
            return false;
        }
        private bool MobTeleport(JObject mob) {
            for(int tries=0;tries<12;tries++){double tx=Number(mob,"x")+random.Next(-8,9),ty=Number(mob,"y")+random.Next(-2,3);for(int down=0;down<6;down++,ty--)if(BodyClear(tx,ty,Number(mob,"width",.6),Number(mob,"height",2.9))&&HasGround(tx,ty,Number(mob,"width",.6))&&!FluidAt(tx,ty+.4,"water")&&!FluidAt(tx,ty+.4,"lava")){mob["x"]=tx;mob["y"]=ty;mob["path"]=null;Sound("minecraft:entity.enderman.teleport",.8,1,tx,ty);return true;}}return false;
        }
        private void SlimeChildren(JObject mob) {
            string kind=(string)mob["kind"];int size=(int?)mob["size"]??1;if((kind!="minecraft:slime"&&kind!="minecraft:magma_cube")||size<=1)return;
            int next=size/2,count=random.Next(2,5);for(int i=0;i<count&&Room.Creatures.Count<128;i++){var child=(JObject)mob.DeepClone();child["entity"]=++serial;child["uuid"]=Guid.NewGuid().ToString("N");child["size"]=next;child["width"]=child["height"]=.52*next;child["health"]=child["maxHealth"]=next*next;child["x"]=Number(mob,"x")+(i-1)*.4;child["y"]=Number(mob,"y")+.1;child.Remove("vehicle");child.Remove("nativeEffects");child.Remove("path");Room.Creatures.Add(child);}
        }
    }
}
