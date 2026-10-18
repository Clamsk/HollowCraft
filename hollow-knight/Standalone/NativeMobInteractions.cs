using System;
using System.Linq;
using Newtonsoft.Json.Linq;
namespace HollowCraft.Standalone
{
    internal sealed partial class NativeGame
    {
        private static bool BreedFood(string kind,string item) {
            if(kind=="minecraft:cow"||kind=="minecraft:mooshroom"||kind=="minecraft:sheep"||kind=="minecraft:goat")return item=="minecraft:wheat";
            if(kind=="minecraft:pig")return new[]{"minecraft:carrot","minecraft:potato","minecraft:beetroot"}.Contains(item);
            if(kind=="minecraft:chicken")return item.EndsWith("_seeds",StringComparison.Ordinal);
            if(kind=="minecraft:rabbit")return new[]{"minecraft:carrot","minecraft:golden_carrot","minecraft:dandelion"}.Contains(item);
            if(kind=="minecraft:fox")return item=="minecraft:sweet_berries"||item=="minecraft:glow_berries";
            if(kind=="minecraft:horse"||kind=="minecraft:donkey"||kind=="minecraft:mule")return item=="minecraft:golden_carrot"||item=="minecraft:golden_apple";
            if(kind=="minecraft:cat"||kind=="minecraft:ocelot")return item=="minecraft:cod"||item=="minecraft:salmon";
            if(kind=="minecraft:wolf")return new[]{"minecraft:beef","minecraft:cooked_beef","minecraft:porkchop","minecraft:cooked_porkchop","minecraft:chicken","minecraft:cooked_chicken","minecraft:mutton","minecraft:cooked_mutton","minecraft:rabbit","minecraft:cooked_rabbit","minecraft:rotten_flesh"}.Contains(item);
            return false;
        }
        private bool TryInteractCreature(JObject packet) {
            double tx=Number(packet,"x")+.5,ty=Number(packet,"y")+.5;
            var mob=Room.Creatures.FirstOrDefault(v=>(bool?)v["boat"]!=true&&Number(v,"health")>0&&Math.Abs(Number(v,"x")-tx)<Number(v,"width",.6)/2+.5&&ty>=Number(v,"y")-.5&&ty<=Number(v,"y")+Number(v,"height",1.8)+.5);
            if(mob==null||Math.Abs(Number(mob,"x")-x)>4.5||Math.Abs(Number(mob,"y")-y)>4.5)return false;
            var hand=!Inventory.Hand.Empty?Inventory.Hand:Inventory.Offhand;string kind=(string)mob["kind"];
            if(hand.Id=="minecraft:bucket"&&(kind=="minecraft:cow"||kind=="minecraft:mooshroom"||kind=="minecraft:goat")) {
                if(!Creative){hand.Count--;hand.Normalize();var milk=NativeStack.New("milk_bucket");if(hand.Empty){hand.Id=milk.Id;hand.Count=1;}else if(!Inventory.Insert(milk))DropAt(milk,x,y+.5);}else {var milk=NativeStack.New("milk_bucket");Inventory.Insert(milk);}Sound("minecraft:entity.cow.milk",.8);return true;
            }
            if(hand.Id=="minecraft:shears"&&kind=="minecraft:sheep"&&(bool?)mob["sheared"]!=true) {mob["sheared"]=true;DropAt(NativeStack.New("white_wool",random.Next(1,4)),Number(mob,"x"),Number(mob,"y")+.5);Inventory.Wear(hand,1,Creative);Sound("minecraft:entity.sheep.shear",.8);return true;}
            if(hand.Id=="minecraft:bone"&&kind=="minecraft:wolf") {if(random.Next(3)==0){mob["tamed"]=true;mob["angryUntil"]=0;mob["health"]=mob["maxHealth"]=20;Notice("Wolf tamed");}if(!Creative){hand.Count--;hand.Normalize();}return true;}
            if(BreedFood(kind,hand.Id)) {
                if(Number(mob,"health")<Number(mob,"maxHealth",20))mob["health"]=Math.Min(Number(mob,"maxHealth",20),Number(mob,"health")+4);
                else if((bool?)mob["baby"]==true)mob["growTicks"]=Math.Max(0,Number(mob,"growTicks")*.9);
                else if(Number(mob,"breedCooldown")<=0)mob["loveTicks"]=600;
                if(!Creative){hand.Count--;hand.Normalize();}return true;
            }
            if(hand.Id=="minecraft:name_tag"&&hand.Data["display"]?["Name"]!=null) {mob["name"]=hand.Data["display"]["Name"].DeepClone();if(!Creative){hand.Count--;hand.Normalize();}return true;}
            return false;
        }
        private void TickMobLifecycle(JObject mob) {
            foreach(string field in new[]{"loveTicks","breedCooldown","growTicks"})if(Number(mob,field)>0)mob[field]=Number(mob,field)-1;
            if((bool?)mob["baby"]==true&&Number(mob,"growTicks")<=0){mob["baby"]=false;mob["width"]=Number(mob,"width")*2;mob["height"]=Number(mob,"height")*2;}
            if(Number(mob,"loveTicks")<=0||Room.Creatures.Count>=128)return;
            var mate=Room.Creatures.FirstOrDefault(other=>other!=mob&&(string)other["kind"]==(string)mob["kind"]&&Number(other,"loveTicks")>0&&Math.Abs(Number(other,"x")-Number(mob,"x"))<3&&Math.Abs(Number(other,"y")-Number(mob,"y"))<2);
            if(mate==null)return;mob["loveTicks"]=mate["loveTicks"]=0;mob["breedCooldown"]=mate["breedCooldown"]=6000;
            var child=(JObject)mob.DeepClone();child["entity"]=++serial;child["uuid"]=Guid.NewGuid().ToString("N");child["baby"]=true;child["growTicks"]=24000;child["breedCooldown"]=0;child["health"]=child["maxHealth"]?.DeepClone()??new JValue(10);child["width"]=Number(child,"width")*.5;child["height"]=Number(child,"height")*.5;child["x"]=Number(child,"x")+.4;child.Remove("vehicle");Room.Creatures.Add(child);AddExperience(random.Next(1,8));
        }
    }
}
