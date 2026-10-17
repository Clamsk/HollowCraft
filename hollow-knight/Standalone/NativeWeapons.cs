using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace HollowCraft.Standalone
{
    internal sealed partial class NativeGame
    {
        private sealed class Flight
        {
            internal int Id,Style,Steps;internal double Dx,Dy,Spin,Rate,Age;internal readonly Dictionary<int,int> NextHit=new Dictionary<int,int>();
        }
        private readonly List<Flight> flights=new List<Flight>();
        private readonly Dictionary<int,JObject> projectiles=new Dictionary<int,JObject>(),primed=new Dictionary<int,JObject>();
        private readonly Dictionary<string,JObject> impacts=new Dictionary<string,JObject>();
        private readonly Random random=new Random();
        private bool zenithHeld;private double zenithX,zenithY;private int zenithInput=-100,zenithClock,zenithBurst;
        private void ItemUse(JObject packet) {
            if(health<=0||Spectator)return;
            bool release=(bool?)packet["release"]==true,cancel=(bool?)packet["cancel"]==true;
            if(cancel){usingItem=false;return;}
            if(release){if(usingItem)ReleaseWeapon(packet);usingItem=false;return;}
            if((bool?)packet["start"]!=true)return;
            usingOffhand=false;var hand=Inventory.Hand;
            if(!CanUse(hand)){usingOffhand=true;hand=Inventory.Offhand;}
            if(hand.Empty)return;
            if(hand.Definition.Food>0){if(food<20||hand.Definition.AlwaysEdible||hand.Id.EndsWith("golden_apple",StringComparison.Ordinal)){usingItem=true;usingTicks=0;}return;}
            if(hand.Id=="minecraft:crossbow"&&(bool?)hand.Data["Charged"]==true){var ammo=NativeStack.Read(hand.Data["HollowCraftLoadedArrow"]);ammo.Data["HollowCraftPiercing"]=NativeData.Enchantment(hand,"piercing");double aimX=Number(packet,"aimX",1),aimY=Number(packet,"aimY");bool multiple=NativeData.Enchantment(hand,"multishot")>0;foreach(double angle in multiple?new[]{-.174532925,0,.174532925}:new[]{0.0}){var shot=ammo.Copy();if(angle!=0)shot.Data["HollowCraftNoPickup"]=true;SpawnProjectile("minecraft:arrow",aimX*Math.Cos(angle)-aimY*Math.Sin(angle),aimX*Math.Sin(angle)+aimY*Math.Cos(angle),3.15,7,shot);}hand.Data.Remove("Charged");hand.Data.Remove("HollowCraftLoadedArrow");Inventory.Wear(hand,multiple?3:1,Creative);return;}
            if(new[]{"minecraft:shield","minecraft:bow","minecraft:crossbow","minecraft:trident","minecraft:potion","minecraft:milk_bucket"}.Contains(hand.Id)){if(!usingItem){usingItem=true;usingTicks=0;}return;}
            int bx=(int)Number(packet,"x"),by=(int)Number(packet,"y");if(!Reach(bx,by,Creative?5:4.5))return;
            if(PlaceCreature(hand,bx,by))return;
            if(hand.Id=="minecraft:flint_and_steel"){if(Room.Blocks.TryGetValue(NativeRoom.Key(bx,by),out var tnt)&&(string)tnt["id"]=="minecraft:tnt"){Room.Blocks.Remove(NativeRoom.Key(bx,by));Prime(bx+.5,by);Inventory.Wear(hand,1,Creative);}return;}
            if(hand.Id=="minecraft:water_bucket"||hand.Id=="minecraft:lava_bucket") {string key=NativeRoom.Key(bx,by);if(!Room.Blocks.ContainsKey(key)&&!TerrainFull(bx,by)){Room.Blocks[key]=NativeRoom.Cell(bx,by,hand.Id=="minecraft:water_bucket"?"water":"lava");if(!Creative)hand.Id="minecraft:bucket";}return;}
            if(hand.Id=="minecraft:bucket"){string key=NativeRoom.Key(bx,by);if(Room.Blocks.TryGetValue(key,out var fluid)&&(bool?)fluid["source"]==true&&((string)fluid["id"]=="minecraft:water"||(string)fluid["id"]=="minecraft:lava")){string bucket=(string)fluid["id"]=="minecraft:water"?"water_bucket":"lava_bucket";Room.Blocks.Remove(key);if(!Creative){hand.Count--;hand.Normalize();var filled=NativeStack.New(bucket);if(hand.Empty){hand.Id=filled.Id;hand.Count=1;}else if(!Inventory.Insert(filled))DropAt(filled,x,y+.5);}}return;}
            if(new[]{"minecraft:ender_pearl","minecraft:snowball","minecraft:egg","minecraft:splash_potion","minecraft:lingering_potion"}.Contains(hand.Id)) {
                SpawnProjectile(hand.Id,Number(packet,"aimX",1),Number(packet,"aimY"),hand.Id=="minecraft:ender_pearl"?1.5:1.1,0,hand.Copy(1));if(!Creative){hand.Count--;hand.Normalize();}return;
            }
            if(hand.Id=="minecraft:firework_rocket"){Effect("rocket",30,0);if(!Creative){hand.Count--;hand.Normalize();}return;}
            if(hand.Definition.Equipment.Length>0) {int slot=Array.IndexOf(new[]{"helmet","chestplate","leggings","boots"},hand.Definition.Equipment);var old=Inventory.Armor[slot];Inventory.Armor[slot]=hand.Copy(1);hand.Count--;hand.Normalize();Inventory.Insert(old);if(!old.Empty)DropAt(old,x,y+.5);return;}
            Notice("Native item behavior pending: "+NativeCatalog.Label(hand.Id));
        }
        private static bool CanUse(NativeStack item)=>!item.Empty&&(item.Definition.Food>0||!string.IsNullOrEmpty(item.Definition.Equipment)||item.Id.EndsWith("_spawn_egg",StringComparison.Ordinal)||item.Id.EndsWith("_boat",StringComparison.Ordinal)||item.Id.EndsWith("_raft",StringComparison.Ordinal)||new[]{"minecraft:shield","minecraft:bow","minecraft:crossbow","minecraft:trident","minecraft:potion","minecraft:splash_potion","minecraft:lingering_potion","minecraft:bucket","minecraft:water_bucket","minecraft:lava_bucket","minecraft:milk_bucket","minecraft:ender_pearl","minecraft:snowball","minecraft:egg","minecraft:firework_rocket","minecraft:flint_and_steel"}.Contains(item.Id));
        private void ReleaseWeapon(JObject packet) {
            var hand=Active;if(hand.Empty)return;
            if(hand.Id=="minecraft:crossbow") {
                if(usingTicks<Math.Max(1,25-5*NativeData.Enchantment(hand,"quick_charge")))return;var arrow=TakeArrow(false);if(arrow==null)return;hand.Data["Charged"]=true;hand.Data["HollowCraftLoadedArrow"]=arrow.Json();
            }else if(hand.Id=="minecraft:bow") {
                double charge=Math.Min(1,((usingTicks/20.0)*(usingTicks/20.0)+usingTicks/10.0)/3);if(charge<.1)return;var arrow=TakeArrow(NativeData.Enchantment(hand,"infinity")>0);if(arrow==null)return;
                int power=NativeData.Enchantment(hand,"power");SpawnProjectile("minecraft:arrow",Number(packet,"aimX",1),Number(packet,"aimY"),3*charge,Math.Ceiling((2+(power>0?.5*power+.5:0))*3*charge),arrow);Inventory.Wear(hand,1,Creative);
            }else if(hand.Id=="minecraft:trident"&&usingTicks>=10) {var thrown=hand.Copy(1);SpawnProjectile("minecraft:trident",Number(packet,"aimX",1),Number(packet,"aimY"),2.5,Number(hand.Data,"attack",8),thrown);if(!Creative){hand.Count--;hand.Normalize();}}
        }
        private NativeStack TakeArrow(bool infinity) {
            var ammo=new[]{Inventory.Offhand}.Concat(Inventory.Slots).FirstOrDefault(s=>!s.Empty&&(s.Id=="minecraft:arrow"||s.Id=="minecraft:tipped_arrow"||s.Id=="minecraft:spectral_arrow"));
            if(ammo==null){if(!Creative)return null;var free=NativeStack.New("arrow");free.Data["HollowCraftNoPickup"]=true;return free;}var result=ammo.Copy(1);if(!Creative&&!(infinity&&ammo.Id=="minecraft:arrow")){ammo.Count--;ammo.Normalize();}else result.Data["HollowCraftNoPickup"]=true;return result;
        }
        private void SpawnProjectile(string kind,double dx,double dy,double speed,double damage,NativeStack stack) {
            if(kind=="minecraft:arrow")Sound("minecraft:entity.arrow.shoot",.8,.9+random.NextDouble()*.2);
            double length=Math.Sqrt(dx*dx+dy*dy);if(length<.001){dx=1;dy=0;length=1;}
            int id=++serial;projectiles[id]=new JObject{["entity"]=id,["uuid"]=Guid.NewGuid().ToString("N"),["kind"]=kind,["item"]=kind=="minecraft:arrow"?"minecraft:arrow":kind,["x"]=x,["y"]=y+1.4,["vx"]=dx/length*speed,["vy"]=dy/length*speed,["nextVx"]=dx/length*speed,["nextVy"]=dy/length*speed,["age"]=0,["damage"]=damage,["stepRequired"]=true,["grounded"]=false,["noClip"]=false,["stack"]=stack?.Json()};
        }
        private JArray ProjectileJson()=>new JArray(projectiles.Values.Select(value=>value.DeepClone()));
        private void ProjectileSteps(JObject packet) {
            foreach(JObject step in packet["steps"] as JArray??new JArray()) {
                int id=(int?)step["entity"]??-1;if(!projectiles.TryGetValue(id,out var value)||(string)value["uuid"]!=(string)step["uuid"]||(int?)value["age"]!=(int?)step["age"]||(bool?)value["grounded"]==true)continue;
                double px=Number(value,"x")+Number(value,"vx"),py=Number(value,"y")+Number(value,"vy");
                // The shared Unity sweep acknowledges only a clear segment. A sampled
                // native terrain cell may contain a thin ledge, not a full cube.
                value["x"]=px;value["y"]=py;value["vx"]=Number(value,"vx")*.99;value["vy"]=Number(value,"vy")*.99-(((string)value["kind"])=="minecraft:ender_pearl"?.03:.05);value["nextVx"]=value["vx"];value["nextVy"]=value["vy"];value["age"]=(int)value["age"]+1;
                if((int)value["age"]>1200||py< -64)projectiles.Remove(id);
            }
        }
        private void ProjectileHit(JObject packet) {
            int id=(int?)packet["entity"]??-1;string identity=(string)packet["uuid"],key=id+":"+identity+":"+(int?)packet["request"];
            if(impacts.TryGetValue(key,out var previous)){Emit((JObject)previous.DeepClone());return;}
            if(!projectiles.TryGetValue(id,out var value)||(string)value["uuid"]!=identity)return;
            if((bool?)value["returning"]==true)return;
            Observe(packet);string kind=(string)value["kind"];int target=(int?)packet["target"]??0;bool propPass=(bool?)packet["passThrough"]==true,pass=propPass,removed=false;
            int piercing=(int?)value["stack"]?["nbt"]?["HollowCraftPiercing"]??0;if(kind=="minecraft:arrow"&&piercing>0&&target!=0){var hitIds=value["hitIds"] as JArray??new JArray();if(hitIds.Values<int>().Contains(target))return;hitIds.Add(target);value["hitIds"]=hitIds;pass|=hitIds.Count<=piercing;}
            if(packet["creature"] is JObject creature&&(kind=="minecraft:arrow"||kind=="minecraft:trident"))HitCreature(creature,Math.Max(1,Number(value,"damage")),true);
            double angle=Math.Atan2(Number(value,"vy"),Number(value,"vx"))*180/Math.PI;
            if(target!=0&&enemies.TryGetValue(target,out var enemy)&&(kind=="minecraft:trident"||kind=="minecraft:arrow")){HitEnemy(enemy,propPass?999:Math.Max(1,Number(value,"damage")),kind=="minecraft:trident"?"trident":"arrow",angle);if(kind=="minecraft:arrow"&&value["stack"] is JObject carried){var ammo=NativeStack.Read(carried);if(ammo.Id=="minecraft:tipped_arrow")ApplyPotionEnemy(enemy,ammo,.125);}}
            if(kind=="minecraft:ender_pearl") {x=Number(packet,"landingX",Number(packet,"x"));y=Number(packet,"landingY",Number(packet,"y"));teleport++;hurtTicks=0;Damage(3,"pearl",x,y);Emit(new JObject{["type"]="teleport",["x"]=x,["y"]=y,["normalX"]=Number(packet,"normalX"),["normalY"]=Number(packet,"normalY",1),["teleport"]=teleport});removed=true;}
            else if(kind.Contains("potion")){var potion=NativeStack.Read(value["stack"]);double px=Number(packet,"x"),py=Number(packet,"y");foreach(var enemy2 in enemies.Values){double distance=Math.Sqrt(Math.Pow(Number(enemy2,"x")-px,2)+Math.Pow(Number(enemy2,"y")-py,2));if(distance<4)ApplyPotionEnemy(enemy2,potion,(int)enemy2["id"]==target?1:1-distance/4);}double playerDistance=Math.Sqrt((x-px)*(x-px)+(y-py)*(y-py));if(playerDistance<4)ApplyPotion(potion,1-playerDistance/4);removed=true;}
            else if(kind=="minecraft:trident"){var stack=NativeStack.Read(value["stack"]);Inventory.Wear(stack,1,Creative);value["stack"]=stack.Json();value["x"]=Number(packet,"x");value["y"]=Number(packet,"y");value["vx"]=value["vy"]=0;value["stepRequired"]=false;bool loyalty=NativeData.Enchantment(stack,"loyalty")>0;value["returning"]=loyalty;value["noClip"]=loyalty;value["grounded"]=!loyalty;}
            else if(kind=="minecraft:snowball"||kind=="minecraft:egg")removed=true;
            else if(!pass){value["x"]=Number(packet,"x");value["y"]=Number(packet,"y");value["vx"]=value["vy"]=0;value["grounded"]=true;value["stepRequired"]=false;}
            if(removed)projectiles.Remove(id);
            var reply=new JObject{["type"]="nativeImpact",["entity"]=id,["uuid"]=identity,["request"]=packet["request"],["accepted"]=true,["removed"]=removed,["grounded"]=(bool?)value["grounded"]??false};impacts[key]=reply;while(impacts.Count>128)impacts.Remove(impacts.Keys.First());Emit((JObject)reply.DeepClone());
        }
        private void ReturnLoyaltyTridents(){foreach(var pair in projectiles.ToArray())if((string)pair.Value["kind"]=="minecraft:trident"){var item=NativeStack.Read(pair.Value["stack"]);if(NativeData.Enchantment(item,"loyalty")<=0)continue;if(!Creative&&!Inventory.Insert(item))DropAt(item,x,y+.5);projectiles.Remove(pair.Key);}}
        private void Prime(double px,double py,int fuse=80) {int id=++serial;primed[id]=new JObject{["id"]=id,["uuid"]=Guid.NewGuid().ToString("N"),["x"]=px,["y"]=py,["age"]=0,["fuse"]=fuse,["vx"]=0,["vy"]=.2,["width"]=.98,["height"]=.98,["stepRequired"]=true,["nextX"]=0,["nextY"]=.16};}
        private JArray PrimedJson()=>new JArray(primed.Values.Select(value=>value.DeepClone()));
        private void TntSteps(JObject packet) {
            foreach(JObject step in packet["steps"] as JArray??new JArray()) {
                int id=(int?)step["entity"]??-1;if(!primed.TryGetValue(id,out var value)||(string)value["uuid"]!=(string)step["uuid"]||(int?)value["age"]!=(int?)step["age"])continue;
                double vx=Number(value,"nextX"),vy=Number(value,"nextY"),dx=Number(step,"dx"),dy=Number(step,"dy");
                // dx/dy already came from the shared swept Unity collision solver.
                value["x"]=Number(value,"x")+dx;value["y"]=Number(value,"y")+dy+Number(step,"lift");
                bool grounded=vy<0&&Math.Abs(vy-dy)>.001;value["vx"]=vx*.98;value["vy"]=(grounded?-vy*.5:vy)*.98;value["nextX"]=Number(value,"vx")*(grounded?.7:1);value["nextY"]=Number(value,"vy")-.04;value["age"]=(int)value["age"]+1;
            }
        }
        private bool BedrockShield(double ax,double ay,double bx,double by) {int steps=Math.Max(1,(int)Math.Ceiling(Math.Max(Math.Abs(bx-ax),Math.Abs(by-ay))*4));for(int i=1;i<steps;i++){string key=NativeRoom.Key((int)Math.Floor(ax+(bx-ax)*i/steps),(int)Math.Floor(ay+(by-ay)*i/steps));if(Room.Blocks.TryGetValue(key,out var cell)&&(string)cell["id"]=="minecraft:bedrock")return true;}return false;}
        private void Explode(double px,double py) {
            Sound("minecraft:entity.generic.explode",.8,.8+random.NextDouble()*.2,px,py);
            foreach(var cell in Room.Blocks.Values.ToArray()) {double dx=(int)cell["x"]+.5-px,dy=(int)cell["y"]+.5-py;if(dx*dx+dy*dy>16||(string)cell["id"]=="minecraft:bedrock"||BedrockShield(px,py,(int)cell["x"]+.5,(int)cell["y"]+.5))continue;Room.Blocks.Remove(NativeRoom.Key((int)cell["x"],(int)cell["y"]));if((string)cell["id"]=="minecraft:tnt")Prime((int)cell["x"]+.5,(int)cell["y"],10+random.Next(20));}
            foreach(var enemy in enemies.Values){double dx=Number(enemy,"x")-px,dy=Number(enemy,"y")-py,d=Math.Sqrt(dx*dx+dy*dy)/8;if(d>1||BedrockShield(px,py,Number(enemy,"x"),Number(enemy,"y")))continue;double impact=1-d;HitEnemy(enemy,Math.Floor((impact*impact+impact)/2*56+1),"explosion",Math.Atan2(dy,dx)*180/Math.PI);}
            double distance=Math.Sqrt((x-px)*(x-px)+(y-py)*(y-py))/8;if(distance<1&&!BedrockShield(px,py,x,y))Damage(Math.Floor(((1-distance)*(1-distance)+(1-distance))/2*56+1),"explosion",px,py);
            Emit(new JObject{["type"]="explosion",["x"]=px,["y"]=py});
        }
        private void ZenithInput(JObject p) {if(Inventory.Hand.Id!="hollowcraft_bridge:zenith"||health<=0){zenithHeld=false;return;}zenithHeld=(bool?)p["held"]==true;if(!zenithHeld)return;zenithX=Number(p,"x",x+1);zenithY=Number(p,"y",y+1);zenithInput=tick;if(zenithBurst==0)zenithBurst=60;}
        private static Tuple<double,double> SwordPoint(double px,double py,Flight f,double age) {
            const double pixel=.0375;double p=Math.Max(0,Math.Min(1,age/60)),length=Math.Sqrt(f.Dx*f.Dx+f.Dy*f.Dy),lerp=Math.Max(0,Math.Min(1,1-length*2/(900*pixel))),minimum=Math.Min(60*pixel,Math.Max(.6,length));
            double angle=Math.PI+Math.Sign(f.Dx)*p*Math.PI*2,radius=Math.Max(minimum,length+40*pixel*Math.Max(0,Math.Min(1,(p-.5)*2))),sx=Math.Cos(angle)*radius,sy=Math.Sin(angle)*f.Spin*(2-lerp),rotation=Math.Atan2(f.Dy,f.Dx),tail=1-Math.Max(0,Math.Min(1,(p-.5)*2));
            return Tuple.Create(px+f.Dx+sx*Math.Cos(rotation)-sy*Math.Sin(rotation)+tail*(-Math.Sign(f.Dx)*radius*.1),py+1+f.Dy+sx*Math.Sin(rotation)+sy*Math.Cos(rotation)+tail*(-f.Spin*.3*pixel));
        }
        private void TickWeapons() {
            foreach(var pair in projectiles.ToArray()){var shot=pair.Value;if((bool?)shot["grounded"]==true){shot["age"]=(int)shot["age"]+1;if((int)shot["age"]>1200){projectiles.Remove(pair.Key);continue;}}if((bool?)shot["returning"]==true){double dx=x-Number(shot,"x"),dy=y+1.2-Number(shot,"y"),distance=Math.Sqrt(dx*dx+dy*dy),speed=.5+.15*NativeData.Enchantment(NativeStack.Read(shot["stack"]),"loyalty");if(distance<.7){var returned=NativeStack.Read(shot["stack"]);if(!Creative&&!Inventory.Insert(returned))DropAt(returned,x,y+.5);projectiles.Remove(pair.Key);}else{double step=Math.Min(distance,speed);shot["x"]=Number(shot,"x")+dx/distance*step;shot["y"]=Number(shot,"y")+dy/distance*step;shot["vx"]=dx/distance*step;shot["vy"]=dy/distance*step;shot["age"]=(int)shot["age"]+1;}}
                else if((bool?)shot["grounded"]==true&&shot["stack"] is JObject data&&(string)shot["kind"]!="minecraft:ender_pearl"&&Math.Pow(Number(shot,"x")-x,2)+Math.Pow(Number(shot,"y")-y-.8,2)<1.5){var returned=NativeStack.Read(data);if((bool?)returned.Data["HollowCraftNoPickup"]==true)continue;returned.Data.Remove("HollowCraftPiercing");if(Inventory.Insert(returned))projectiles.Remove(pair.Key);}}
            foreach(var pair in primed.ToArray()){var value=pair.Value;value["fuse"]=(int)value["fuse"]-1;if((int)value["fuse"]<=0){primed.Remove(pair.Key);Explode(Number(value,"x"),Number(value,"y"));}}
            if(tick-zenithInput>6||Inventory.Hand.Id!="hollowcraft_bridge:zenith"||health<=0)zenithHeld=false;
            for(int sub=0;sub<6;sub++) {
                if((zenithHeld||zenithBurst>0)&&zenithClock%20==0&&flights.Count<24){double dx=zenithX-x,dy=zenithY-y-1;if(dx*dx+dy*dy<.01)dx=.12;double length=Math.Sqrt(dx*dx+dy*dy);if(length>48){dx*=48/length;dy*=48/length;}flights.Add(new Flight{Id=++serial,Dx=dx,Dy=dy,Spin=(random.Next(2)==0?1:-1)*(.65+random.NextDouble()*.35),Style=zenithClock%60==0?0:1+random.Next(10),Rate=.7+1.3*Math.Max(0,Math.Min(1,1-length*2/(900*.0375)))});}
                if(zenithHeld||zenithBurst>0){zenithClock++;if(zenithBurst>0)zenithBurst--;if(zenithHeld&&zenithClock%60==0)zenithBurst=60;}
                foreach(var flight in flights.ToArray()) {
                    var a=SwordPoint(x,y,flight,flight.Age);flight.Age+=flight.Rate;if(flight.Age>=120){flights.Remove(flight);continue;}if(flight.Age>60)continue;var b=SwordPoint(x,y,flight,flight.Age);
                    foreach(var enemy in enemies.Values) {
                        int id=(int)enemy["id"];if(flight.NextHit.TryGetValue(id,out int until)&&flight.Steps<until)continue;
                        double minX=Number(enemy,"x")-Number(enemy,"width",.6)/2-.55,maxX=Number(enemy,"x")+Number(enemy,"width",.6)/2+.55,minY=Number(enemy,"y")-.55,maxY=Number(enemy,"y")+Number(enemy,"height",1.8)+.55;
                        if(!SegmentBox(a.Item1,a.Item2,b.Item1,b.Item2,minX,minY,maxX,maxY))continue;
                        flight.NextHit[id]=flight.Steps+7;HitEnemy(enemy,190*(random.NextDouble()<.14?2:1),"player",Math.Atan2(b.Item2-a.Item2,b.Item1-a.Item1)*180/Math.PI);
                    }flight.Steps++;
                }
            }
            Emit(new JObject{["type"]="zenith",["tick"]=tick,["swords"]=new JArray(flights.Select(f=>new JObject{["id"]=f.Id,["dx"]=f.Dx,["dy"]=f.Dy,["spin"]=f.Spin,["rate"]=f.Rate,["age"]=f.Age,["style"]=f.Style}))});
        }
        private static bool SegmentBox(double ax,double ay,double bx,double by,double minX,double minY,double maxX,double maxY) {
            double low=0,high=1;double[] p={bx-ax,by-ay},start={ax,ay},min={minX,minY},max={maxX,maxY};
            for(int axis=0;axis<2;axis++){if(Math.Abs(p[axis])<.000001){if(start[axis]<min[axis]||start[axis]>max[axis])return false;}else {double one=(min[axis]-start[axis])/p[axis],two=(max[axis]-start[axis])/p[axis];low=Math.Max(low,Math.Min(one,two));high=Math.Min(high,Math.Max(one,two));if(high<low)return false;}}return true;
        }
    }
}
