using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
namespace HollowCraft.Standalone
{
    internal sealed partial class NativeGame
    {
        private JObject rider;private int dismountUntil;
        private bool PlaceCreature(NativeStack item,int bx,int by) {
            string kind=item.Definition.Entity??(item.Id.EndsWith("_spawn_egg",StringComparison.Ordinal)?item.Id.Substring(0,item.Id.Length-10):item.Id);
            bool boat=kind.EndsWith("_boat",StringComparison.Ordinal)||kind.EndsWith("_raft",StringComparison.Ordinal);
            if(!boat&&!item.Id.EndsWith("_spawn_egg",StringComparison.Ordinal))return false;
            if(!boat&&!NativeCatalog.CreatureSupported(kind)){Notice("Native creature model/AI pending: "+kind);return true;}
            if(!Creative&&!boat){Notice("Spawn eggs are creative-only");return true;}
            if(Room.Creatures.Count>=128){Notice("Native room creature limit: 128");return true;}
            double height=boat?.5625:kind=="minecraft:chicken"?.7:kind=="minecraft:spider"?.9:kind=="minecraft:cow"||kind=="minecraft:pig"||kind=="minecraft:sheep"?1.3:1.95,width=boat?1.375:kind=="minecraft:spider"?1.4:.6;
            var value=new JObject{["entity"]=++serial,["uuid"]=Guid.NewGuid().ToString("N"),["kind"]=kind,["boat"]=boat,["x"]=bx+.5,["y"]=by+.01,["width"]=width,["height"]=height,["health"]=boat?6:kind=="minecraft:chicken"?4:kind=="minecraft:pig"||kind=="minecraft:cow"||kind=="minecraft:sheep"?10:20,["vx"]=0,["vy"]=0,["facing"]=1,["nextAttack"]=0};
            if(!boat&&NativeData.Entities.TryGetValue(kind,out var definition)){value["width"]=definition["width"].DeepClone();value["height"]=definition["height"].DeepClone();if(definition["health"]!=null)value["health"]=definition["health"].DeepClone();}
            if(kind=="minecraft:slime"||kind=="minecraft:magma_cube"){value["size"]=2;value["width"]=value["height"]=1.04;value["health"]=4;}value["maxHealth"]=value["health"].DeepClone();
            Room.Creatures.Add(value);if(!Creative){item.Count--;item.Normalize();}return true;
        }
        private IEnumerable<double[]> CollisionBoxes(double px,double py) {for(int bx=(int)Math.Floor(px)-3;bx<=(int)Math.Floor(px)+3;bx++)for(int by=(int)Math.Floor(py)-4;by<=(int)Math.Floor(py)+4;by++)foreach(var box in ShapesAt(bx,by))yield return box;}
        private void MoveCreature(JObject mob,double dx,double dy) {
            double px=Number(mob,"x"),py=Number(mob,"y"),half=Number(mob,"width",.6)/2,height=Number(mob,"height",1.8),moveY=dy,moveX=dx;
            var boxes=CollisionBoxes(px,py).ToArray();
            foreach(var box in boxes)if(px+half>box[0]+.001&&px-half<box[2]-.001){if(dy<0&&py>=box[3]-.01&&py+moveY<box[3])moveY=Math.Max(moveY,box[3]-py);if(dy>0&&py+height<=box[1]+.01&&py+height+moveY>box[1])moveY=Math.Min(moveY,box[1]-py-height);}
            py+=moveY;
            foreach(var box in boxes)if(py+height>box[1]+.001&&py<box[3]-.001){if(dx>0&&px+half<=box[0]+.01&&px+half+moveX>box[0])moveX=Math.Min(moveX,box[0]-px-half);if(dx<0&&px-half>=box[2]-.01&&px-half+moveX<box[2])moveX=Math.Max(moveX,box[2]-px+half);}
            mob["x"]=px+moveX;mob["y"]=py;mob["grounded"]=dy<0&&Math.Abs(moveY-dy)>.001;mob["vy"]=Math.Abs(moveY-dy)>.001?0:dy*.98;mob["blocked"]=Math.Abs(moveX-dx)>.001;
            string kind=(string)mob["kind"];bool ignore=NativeMobCatalog.Find(kind)?.Motion=="fly"||(bool?)mob["boat"]==true||new[]{"minecraft:chicken","minecraft:iron_golem","minecraft:snow_golem","minecraft:slime","minecraft:magma_cube"}.Contains(kind);
            if(!ignore&&dy<0)mob["fallDistance"]=Number(mob,"fallDistance")-moveY;
            if((bool?)mob["grounded"]==true){double fall=Number(mob,"fallDistance");mob["fallDistance"]=0;if(!ignore&&fall>3)HitCreature(mob,Math.Ceiling(fall-3),true,false);}
        }
        private static bool Overlap(JObject a,JObject b)=>Math.Abs(Number(a,"x")-Number(b,"x"))<(Number(a,"width",.6)+Number(b,"width",.6))/2+.1&&Number(a,"y")<Number(b,"y")+Number(b,"height",1.8)+.2&&Number(a,"y")+Number(a,"height",1)>Number(b,"y")-.2;
        private void TickCreatures() {
            foreach(var mob in Room.Creatures.ToArray()) {
                if(Number(mob,"health")<=0){Room.Creatures.Remove(mob);continue;}
                TickMobLifecycle(mob);
                if((int?)mob["attacking"]>0)mob["attacking"]=(int)mob["attacking"]-1;
                bool boat=(bool?)mob["boat"]==true;string kind=(string)mob["kind"];
                if(Math.Abs(Number(mob,"x")-x)>32||Math.Abs(Number(mob,"y")-y)>24)continue;
                if(mob["vehicle"]!=null){var vehicle=Room.Creatures.FirstOrDefault(v=>(int)v["entity"]==(int)mob["vehicle"]&&(bool?)v["boat"]==true);if(vehicle!=null){mob["x"]=vehicle["x"].DeepClone();mob["y"]=Number(vehicle,"y")+.3;mob["vx"]=mob["vy"]=0;continue;}mob.Remove("vehicle");}
                if(TickMobAbility(mob))continue;var spec=NativeMobCatalog.Find(kind);bool hostile=spec!=null?spec.Disposition=="hostile"||Number(mob,"angryUntil")>tick:kind=="minecraft:zombie"||kind=="minecraft:skeleton"||kind=="minecraft:creeper"||kind=="minecraft:spider";
                bool target=hostile&&!Creative&&!Spectator&&health>0;double px=Number(mob,"x"),py=Number(mob,"y"),distance=Math.Abs(px-x);
                double attribute=NativeData.Entities.TryGetValue(kind,out var stats)?Number(stats,"speed",.2):.2;
                double speed=boat?0:target?Math.Sign(x-px)*Math.Min(.18,attribute*.5):Math.Sin(((int)mob["entity"])*7+tick*.009)*Math.Min(.045,attribute*.16);
                if(Number(mob,"panicUntil")>tick)speed=Math.Sign(px-x)*.16;
                if(!hostile&&distance<8&&BreedFood(kind,Inventory.Hand.Id))speed=distance>1.5?Math.Sign(x-px)*Math.Min(.12,attribute*.5):0;
                if((bool?)mob["baby"]==true)speed*=1.3;
                speed*=Number(mob,"moveFactor",1);if(target)speed=Navigate(mob,x,y,speed);else if((bool?)mob["tamed"]==true){if((bool?)mob["sitting"]==true)speed=0;else if(distance>3)speed=Navigate(mob,x,y,.13);}
                bool inWater=FluidAt(px,py+.4,"water"),inLava=FluidAt(px,py+.4,"lava");
                bool fireImmune=new[]{"minecraft:blaze","minecraft:ghast","minecraft:magma_cube","minecraft:zombified_piglin","minecraft:strider","minecraft:wither_skeleton"}.Contains(kind);
                if(inWater){mob["fireTicks"]=0;mob["fallDistance"]=0;}
                else if(inLava&&!fireImmune&&!MobBuff(mob,"fire_resistance")){mob["fireTicks"]=300;if(tick%10==0)HitCreature(mob,4,true,false);}
                else if(Number(mob,"fireTicks")>0){mob["fireTicks"]=Number(mob,"fireTicks")-1;if(!fireImmune&&!MobBuff(mob,"fire_resistance")&&tick%20==0)HitCreature(mob,1,true,false);}
                if(Number(mob,"health")<=0)continue;
                if(spec?.Motion=="fly"||(spec?.Motion=="swim"&&inWater)) {
                    double desired=target?y+1-Math.Min(1,Number(mob,"height")/2):Number(mob,"homeY",py)+Math.Sin(tick*.04+(int)mob["entity"])*.7;
                    if(mob["homeY"]==null)mob["homeY"]=py;
                    double vertical=Math.Max(-.15,Math.Min(.15,(desired-py)*.05));MoveCreature(mob,speed,vertical);mob["vy"]=vertical*.8;
                }else {
                    if((bool?)mob["blocked"]==true&&(bool?)mob["grounded"]==true||spec?.Motion=="hop"&&(bool?)mob["grounded"]==true&&tick%12==0)mob["vy"]=.42;
                    if(inWater||kind=="minecraft:strider"&&inLava)mob["vy"]=Math.Min(.12,Number(mob,"vy")+.05);
                    MoveCreature(mob,speed,Number(mob,"vy")-(inWater?.02:.08));
                    if(spec?.Motion=="swim"&&!inWater&&tick%20==0&&kind!="minecraft:axolotl")HitCreature(mob,1,true,false);
                }
                if(Number(mob,"health")<=0)continue;
                if(Math.Abs(speed)>.001)mob["facing"]=speed<0?-1:1;
                if(boat){if(Room.Blocks.TryGetValue(NativeRoom.Key((int)Math.Floor(Number(mob,"x")),(int)Math.Floor(Number(mob,"y"))),out var fluid)&&(string)fluid["id"]=="minecraft:water"){mob["y"]=Math.Floor(Number(mob,"y"))+.8;mob["vy"]=0;}continue;}
                if(kind=="minecraft:creeper") {
                    int fuse=(int?)mob["fuse"]??0;fuse=target&&distance<3?Math.Min(30,fuse+1):Math.Max(0,fuse-1);mob["fuse"]=fuse;
                    if(fuse==1)Sound("minecraft:entity.creeper.primed",.8,1,px,py);
                    if(fuse>=30){mob["health"]=0;Explode(px,py,3,false);}
                }else if(target&&tick>=Number(mob,"nextAttack")&&distance<16&&Math.Abs(py-y)<12) {
                    string ranged=spec?.Ranged;if(!string.IsNullOrEmpty(ranged)&&distance>1.8&&ClearSight(px,py+Number(mob,"height",1.8)*.8,x,y+1)&&!BedrockShield(px,py+.8,x,y+.8)){mob["nextAttack"]=tick+40;mob["attacking"]=8;MobRanged(mob,ranged);}
                    else if(distance<Number(mob,"width",.6)/2+.8&&Math.Abs(py-y)<Math.Max(1.8,Number(mob,"height"))&&ClearSight(px,py+.8,x,y+.8)){mob["nextAttack"]=tick+20;mob["attacking"]=8;Damage(stats!=null?Number(stats,"attack",3):3,"mob",px,py,(int)mob["entity"]);if(kind=="minecraft:wither_skeleton")Effect("wither",200,0);if(kind=="minecraft:cave_spider")Effect("poison",140,0);}
                }
            }
            if(rider!=null){var boat=Room.Creatures.FirstOrDefault(v=>(int)v["entity"]==(int)rider["entity"]);if(boat==null||Number(boat,"health")<=0)rider=null;else {rider["x"]=boat["x"].DeepClone();rider["y"]=Number(boat,"y")+.35;x=Number(rider,"x");y=Number(rider,"y");}}
            foreach(var boat in Room.Creatures.Where(v=>(bool?)v["boat"]==true)) {
                int boatId=(int)boat["entity"],capacity=((string)boat["kind"]).Contains("chest")?1:2,occupied=Room.Creatures.Count(v=>(int?)v["vehicle"]==boatId)+enemies.Values.Count(v=>(int?)v["seat"]?["boat"]==boatId)+(rider!=null&&(int)rider["entity"]==boatId?1:0);
                var player=new JObject{["x"]=x,["y"]=y,["width"]=.6,["height"]=1.8};
                if(rider==null&&tick>=dismountUntil&&health>0&&!Spectator&&occupied<capacity&&Overlap(boat,player)){rider=new JObject{["entity"]=boatId,["uuid"]=boat["uuid"].DeepClone(),["x"]=boat["x"].DeepClone(),["y"]=Number(boat,"y")+.35};occupied++;}
                foreach(var mob in Room.Creatures)if(occupied<capacity&&(bool?)mob["boat"]!=true&&mob["vehicle"]==null&&Overlap(boat,mob)){mob["vehicle"]=boatId;occupied++;}
                foreach(var enemy in enemies.Values) {
                    if((int?)enemy["seat"]?["boat"]==boatId){enemy["seat"]["x"]=boat["x"].DeepClone();enemy["seat"]["y"]=Number(boat,"y")+.3;continue;}
                    if(occupied>=capacity||enemy["seat"]!=null||(string)enemy["kind"]=="resource"||Number(enemy,"health")<=0||!Overlap(boat,enemy))continue;
                    enemy["seat"]=new JObject{["boat"]=boatId,["uuid"]=boat["uuid"].DeepClone(),["x"]=boat["x"].DeepClone(),["y"]=Number(boat,"y")+.3};occupied++;
                }
            }
            foreach(var enemy in enemies.Values)if(enemy["seat"]!=null&&!Room.Creatures.Any(v=>(int)v["entity"]==(int)enemy["seat"]["boat"]))enemy.Remove("seat");
        }
        private void Dismount() {if(rider==null)return;rider=null;dismountUntil=tick+20;}
        private bool HitCreature(JObject target,double amount,bool ranged=false,bool playerOwned=true) {
            if(!Room.Creatures.Any(v=>(int)v["entity"]==(int?)target["entity"]&&(string)v["uuid"]==(string)target["uuid"]))return false;
            var mob=Room.Creatures.First(v=>(int)v["entity"]==(int)target["entity"]);if(Number(mob,"health")<=0||!ranged&&(Math.Abs(Number(mob,"x")-x)>(Creative?7:4)||Math.Abs(Number(mob,"y")-y)>4))return false;
            var spec=NativeMobCatalog.Find((string)mob["kind"]);if(playerOwned)mob["lastPlayerHit"]=tick;if(spec?.Disposition=="neutral"&&playerOwned)mob["angryUntil"]=tick+600;else if(spec?.Disposition=="passive")mob["panicUntil"]=tick+100;
            if(!ranged){string kind=(string)mob["kind"];if(kind=="minecraft:zombie"||kind=="minecraft:skeleton")amount+=2.5*NativeData.Enchantment(Inventory.Hand,"smite");else if(kind=="minecraft:spider")amount+=2.5*NativeData.Enchantment(Inventory.Hand,"bane_of_arthropods");}
            if(!ranged&&playerOwned&&NativeData.Enchantment(Inventory.Hand,"fire_aspect")>0)mob["fireTicks"]=80*NativeData.Enchantment(Inventory.Hand,"fire_aspect");mob["health"]=Math.Max(0,Number(mob,"health")-amount);if(Number(mob,"health")<=0){DropCreatureLoot(mob);SlimeChildren(mob);}return true;
        }
        private bool FluidAt(double px,double py,string fluid)=>Room.Blocks.TryGetValue(NativeRoom.Key((int)Math.Floor(px),(int)Math.Floor(py)),out var cell)&&(string)cell["id"]=="minecraft:"+fluid;
        private void MobRanged(JObject mob,string kind,double? targetX=null,double? targetY=null) {
            double px=Number(mob,"x"),py=Number(mob,"y")+Number(mob,"height",1.8)*.75,dx=(targetX??x)-px,dy=(targetY??y+1)-py,distance=Math.Sqrt(dx*dx+dy*dy);
            px+=(dx<0?-1:1)*(Number(mob,"width",.6)/2+.3);dx=(targetX??x)-px;distance=Math.Sqrt(dx*dx+dy*dy);
            int id=++serial;double speed=kind=="arrow"?.8:kind.Contains("fireball")?.3:.45;
            var value=new JObject{["entity"]=id,["uuid"]=Guid.NewGuid().ToString("N"),["kind"]="minecraft:"+(kind=="arrow"?"arrow":kind=="potion"?"splash_potion":"snowball"),["item"]="minecraft:"+(kind=="arrow"?"arrow":kind=="potion"?"splash_potion":"fire_charge"),["hostile"]=!targetX.HasValue,["mobKind"]=kind,["owner"]=(int)mob["entity"],["x"]=px,["y"]=py,["vx"]=dx/Math.Max(.01,distance)*speed,["vy"]=dy/Math.Max(.01,distance)*speed,["damage"]=kind=="arrow"?4:kind=="beam"?6:5,["age"]=0,["stuck"]=false,["hits"]=new JArray()};
            value["stepRequired"]=true;value["grounded"]=false;value["noClip"]=false;
            if(kind=="potion"){var potion=NativeStack.New("splash_potion");potion.Data["Potion"]="minecraft:poison";value["stack"]=potion.Json();}
            projectiles[id]=value;Sound(kind=="arrow"?"minecraft:entity.skeleton.shoot":"minecraft:entity.blaze.shoot",.6,1,px,py);
        }
        private void DropCreatureLoot(JObject mob) {
            string kind=(string)mob["kind"];double px=Number(mob,"x"),py=Number(mob,"y");if((bool?)mob["boat"]==true){DropAt(NativeStack.New(kind),px,py);return;}
            bool credited=mob["lastPlayerHit"]!=null&&tick-Number(mob,"lastPlayerHit")<=100;var vanilla=NativeLoot.Roll(mob,credited?NativeData.Enchantment(Inventory.Hand,"looting"):0,credited,random);
            if(vanilla!=null){foreach(var item in vanilla)DropAt(item,px,py);if((bool?)mob["baby"]!=true&&mob["lastPlayerHit"]!=null&&tick-Number(mob,"lastPlayerHit")<=100)AddExperience(NativeMobCatalog.Find(kind)?.Disposition=="hostile"?5:random.Next(1,4));return;}
            Action<string,int,int> drop=(id,min,max)=>{int count=random.Next(min,max+1);if(count>0)DropAt(NativeStack.New(id,count),px,py);};
            switch(kind){case "minecraft:zombie":drop("rotten_flesh",0,2);break;case "minecraft:skeleton":drop("bone",0,2);drop("arrow",0,2);break;case "minecraft:creeper":drop("gunpowder",0,2);break;case "minecraft:spider":drop("string",0,2);if(random.Next(3)==0)drop("spider_eye",1,1);break;case "minecraft:pig":drop("porkchop",1,3);break;case "minecraft:cow":drop("beef",1,3);drop("leather",0,2);break;case "minecraft:sheep":drop("mutton",1,2);drop("white_wool",1,1);break;case "minecraft:chicken":drop("chicken",1,1);drop("feather",0,2);break;}
            if(kind!="minecraft:villager")AddExperience(kind=="minecraft:zombie"||kind=="minecraft:skeleton"||kind=="minecraft:creeper"||kind=="minecraft:spider"?5:random.Next(1,4));
        }
    }
}
