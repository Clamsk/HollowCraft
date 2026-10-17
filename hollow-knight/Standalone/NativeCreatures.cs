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
            string kind=item.Id.EndsWith("_spawn_egg",StringComparison.Ordinal)?item.Id.Substring(0,item.Id.Length-10):item.Id;
            bool boat=kind.EndsWith("_boat",StringComparison.Ordinal)||kind.EndsWith("_raft",StringComparison.Ordinal);
            if(!boat&&!item.Id.EndsWith("_spawn_egg",StringComparison.Ordinal))return false;
            if(!Creative&&!boat){Notice("Spawn eggs are creative-only");return true;}
            if(Room.Creatures.Count>=128){Notice("Native room creature limit: 128");return true;}
            double height=boat?.5625:kind=="minecraft:chicken"?.7:kind=="minecraft:spider"?.9:kind=="minecraft:cow"||kind=="minecraft:pig"||kind=="minecraft:sheep"?1.3:1.95,width=boat?1.375:kind=="minecraft:spider"?1.4:.6;
            var value=new JObject{["entity"]=++serial,["uuid"]=Guid.NewGuid().ToString("N"),["kind"]=kind,["boat"]=boat,["x"]=bx+.5,["y"]=by+.01,["width"]=width,["height"]=height,["health"]=boat?6:kind=="minecraft:chicken"?4:kind=="minecraft:pig"||kind=="minecraft:cow"||kind=="minecraft:sheep"?10:20,["vx"]=0,["vy"]=0,["facing"]=1,["nextAttack"]=0};
            Room.Creatures.Add(value);if(!Creative){item.Count--;item.Normalize();}return true;
        }
        private IEnumerable<double[]> CollisionBoxes(double px,double py) {
            for(int bx=(int)Math.Floor(px)-3;bx<=(int)Math.Floor(px)+3;bx++)for(int by=(int)Math.Floor(py)-4;by<=(int)Math.Floor(py)+4;by++){
                string key=NativeRoom.Key(bx,by);if(terrain.TryGetValue(key,out var cells))foreach(JArray cell in cells)yield return cell.Count>=6?new[]{bx+(double)cell[2],by+(double)cell[3],bx+(double)cell[4],by+(double)cell[5]}:new[]{(double)bx,by,bx+1,by+1};
                if(Room.Blocks.TryGetValue(key,out var block)&&NativeCatalog.Get((string)block["id"]).Solid&&(bool?)block["open"]!=true)yield return new[]{(double)bx,by,bx+1,by+(((string)block["id"]).EndsWith("_slab",StringComparison.Ordinal)?.5:1)};
            }
        }
        private void MoveCreature(JObject mob,double dx,double dy) {
            double px=Number(mob,"x"),py=Number(mob,"y"),half=Number(mob,"width",.6)/2,height=Number(mob,"height",1.8),moveY=dy,moveX=dx;
            var boxes=CollisionBoxes(px,py).ToArray();
            foreach(var box in boxes)if(px+half>box[0]+.001&&px-half<box[2]-.001){if(dy<0&&py>=box[3]-.01&&py+moveY<box[3])moveY=Math.Max(moveY,box[3]-py);if(dy>0&&py+height<=box[1]+.01&&py+height+moveY>box[1])moveY=Math.Min(moveY,box[1]-py-height);}
            py+=moveY;
            foreach(var box in boxes)if(py+height>box[1]+.001&&py<box[3]-.001){if(dx>0&&px+half<=box[0]+.01&&px+half+moveX>box[0])moveX=Math.Min(moveX,box[0]-px-half);if(dx<0&&px-half>=box[2]-.01&&px-half+moveX<box[2])moveX=Math.Max(moveX,box[2]-px+half);}
            mob["x"]=px+moveX;mob["y"]=py;mob["grounded"]=dy<0&&Math.Abs(moveY-dy)>.001;mob["vy"]=Math.Abs(moveY-dy)>.001?0:dy*.98;mob["blocked"]=Math.Abs(moveX-dx)>.001;
        }
        private static bool Overlap(JObject a,JObject b)=>Math.Abs(Number(a,"x")-Number(b,"x"))<(Number(a,"width",.6)+Number(b,"width",.6))/2+.1&&Number(a,"y")<Number(b,"y")+Number(b,"height",1.8)+.2&&Number(a,"y")+Number(a,"height",1)>Number(b,"y")-.2;
        private void TickCreatures() {
            foreach(var mob in Room.Creatures.ToArray()) {
                if(Number(mob,"health")<=0){Room.Creatures.Remove(mob);continue;}
                bool boat=(bool?)mob["boat"]==true;string kind=(string)mob["kind"];
                if(Math.Abs(Number(mob,"x")-x)>32||Math.Abs(Number(mob,"y")-y)>24)continue;
                if(mob["vehicle"]!=null){var vehicle=Room.Creatures.FirstOrDefault(v=>(int)v["entity"]==(int)mob["vehicle"]&&(bool?)v["boat"]==true);if(vehicle!=null){mob["x"]=vehicle["x"].DeepClone();mob["y"]=Number(vehicle,"y")+.3;mob["vx"]=mob["vy"]=0;continue;}mob.Remove("vehicle");}
                bool hostile=kind=="minecraft:zombie"||kind=="minecraft:skeleton"||kind=="minecraft:creeper"||kind=="minecraft:spider";
                double speed=boat?0:hostile?Math.Sign(x-Number(mob,"x"))*.11:Math.Sin(((int)mob["entity"])*7+tick*.009)*.035;
                if((bool?)mob["blocked"]==true&&(bool?)mob["grounded"]==true)mob["vy"]=.42;
                MoveCreature(mob,speed,Number(mob,"vy")-.08);if(Math.Abs(speed)>.001)mob["facing"]=speed<0?-1:1;
                if(boat){if(Room.Blocks.TryGetValue(NativeRoom.Key((int)Math.Floor(Number(mob,"x")),(int)Math.Floor(Number(mob,"y"))),out var fluid)&&(string)fluid["id"]=="minecraft:water"){mob["y"]=Math.Floor(Number(mob,"y"))+.8;mob["vy"]=0;}continue;}
                if(hostile&&!Creative&&!Spectator&&health>0&&tick>=Number(mob,"nextAttack")&&Math.Abs(Number(mob,"x")-x)<1.2&&Math.Abs(Number(mob,"y")-y)<1.8){mob["nextAttack"]=tick+20;if(kind=="minecraft:creeper"){mob["health"]=0;Explode(Number(mob,"x"),Number(mob,"y"));}else Damage(3,"mob",Number(mob,"x"),Number(mob,"y"));}
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
        private void HitCreature(JObject target,double amount) {
            if(!Room.Creatures.Any(v=>(int)v["entity"]==(int?)target["entity"]&&(string)v["uuid"]==(string)target["uuid"]))return;
            var mob=Room.Creatures.First(v=>(int)v["entity"]==(int)target["entity"]);if(Math.Abs(Number(mob,"x")-x)>(Creative?7:4)||Math.Abs(Number(mob,"y")-y)>4)return;
            mob["health"]=Math.Max(0,Number(mob,"health")-amount);if(Number(mob,"health")<=0){string kind=(string)mob["kind"];if((bool?)mob["boat"]==true)DropAt(NativeStack.New(kind),Number(mob,"x"),Number(mob,"y"));else DropAt(NativeStack.New(kind=="minecraft:skeleton"?"arrow":kind=="minecraft:zombie"?"rotten_flesh":"cooked_beef"),Number(mob,"x"),Number(mob,"y"));}
        }
    }
}
