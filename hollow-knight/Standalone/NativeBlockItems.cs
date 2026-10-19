using System;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace HollowCraft.Standalone
{
    internal sealed partial class NativeGame
    {
        private NativeStack[] enderSlots=NativeInventory.Empty(27);
        private void RestorePlacedItem(NativeStack item,JObject cell) {
            int bx=(int)cell["x"],by=(int)cell["y"];string key=NativeRoom.Key(bx,by);
            if(item.Data["display"]?["Name"]!=null)cell["name"]=item.Data["display"]["Name"].DeepClone();
            if(!item.Id.EndsWith("shulker_box",StringComparison.Ordinal))return;
            var box=StorageAt(bx,by);foreach(JObject entry in item.Data["BlockEntityTag"]?["Items"] as JArray??new JArray()) {
                int slot=(int?)entry["Slot"]??-1;string id=NativeCatalog.Id((string)entry["id"]);if(slot<0||slot>=27||!NativeCatalog.Items.ContainsKey(id)||id.EndsWith("shulker_box",StringComparison.Ordinal))continue;
                box.Slots[slot]=NativeStack.Read(new JObject{["id"]=id,["count"]=(int?)entry["Count"]??0,["damage"]=(int?)entry["tag"]?["Damage"]??0,["nbt"]=entry["tag"]?.DeepClone()??new JObject()});
            }
        }
        private NativeStack PackedBlock(JObject cell,NativeContainer box) {
            var item=NativeStack.New((string)cell["id"]);if(cell["name"]!=null)item.Data["display"]=new JObject{["Name"]=cell["name"].DeepClone()};
            if(box!=null&&item.Id.EndsWith("shulker_box",StringComparison.Ordinal)){var list=new JArray();for(int i=0;i<box.Slots.Length;i++){var stack=box.Slots[i];if(stack.Empty)continue;var tag=(JObject)stack.Data.DeepClone();if(stack.Damage>0)tag["Damage"]=stack.Damage;list.Add(new JObject{["Slot"]=i,["id"]=stack.Id,["Count"]=stack.Count,["tag"]=tag});}item.Data["BlockEntityTag"]=new JObject{["Items"]=list};}return item;
        }
        private void DropMinedBlock(JObject cell,NativeContainer box,bool harvest) {
            double px=(int)cell["x"]+.5,py=(int)cell["y"]+.5;string id=(string)cell["id"];
            if(id.EndsWith("shulker_box",StringComparison.Ordinal)){if(!Creative||box?.Slots.Any(s=>!s.Empty)==true)DropAt(PackedBlock(cell,box),px,py);return;}
            if(box!=null&&id!="minecraft:ender_chest")foreach(var stack in box.Slots)DropAt(stack,px,py);
            if(Creative||!harvest||cell["pistonHead"]!=null)return;
            var drops=NativeLoot.RollBlock(cell,Inventory.Hand,random);if(drops!=null){foreach(var stack in drops)DropAt(stack,px,py);return;}
            string drop=NativeData.Enchantment(Inventory.Hand,"silk_touch")>0?id:id=="minecraft:stone"?"minecraft:cobblestone":id=="minecraft:iron_ore"?"minecraft:raw_iron":id=="minecraft:gold_ore"?"minecraft:raw_gold":id=="minecraft:diamond_ore"?"minecraft:diamond":id=="minecraft:coal_ore"?"minecraft:coal":id;
            if((bool?)cell["crop"]==true)drop=id=="minecraft:carrots"?"minecraft:carrot":id=="minecraft:potatoes"?"minecraft:potato":id=="minecraft:beetroots"?"minecraft:beetroot_seeds":"minecraft:wheat_seeds";DropAt(NativeStack.New(drop),px,py);
        }
        private bool ExtraItemUse(NativeStack item,JObject packet,int bx,int by) {
            string id=item.Id;var block=BlockAt(bx,by);
            if(id=="minecraft:water_bucket"&&block!=null&&((string)block["id"]).EndsWith("_slab",StringComparison.Ordinal)&&(bool?)block["double"]!=true){block["waterlogged"]=true;if(!Creative)ExchangeUsedItem(item,NativeStack.New("bucket"));return true;}
            if(id=="minecraft:bucket"&&(bool?)block?["waterlogged"]==true){block["waterlogged"]=false;ExchangeUsedItem(item,NativeStack.New("water_bucket"));return true;}
            if(id=="minecraft:water_bucket"&&(string)block?["id"]=="minecraft:lava"){block["id"]=LiquidSource(block)?"minecraft:obsidian":"minecraft:cobblestone";if(!Creative)ExchangeUsedItem(item,NativeStack.New("bucket"));return true;}
            string crop=id=="minecraft:wheat_seeds"?"wheat":id=="minecraft:beetroot_seeds"?"beetroots":id=="minecraft:carrot"?"carrots":id=="minecraft:potato"?"potatoes":null;
            if(crop!=null&&(string)block?["id"]=="minecraft:farmland"&&BlockAt(bx,by+1)==null){var planted=NativeRoom.Cell(bx,by+1,crop);planted["crop"]=true;planted["age"]=0;Room.Blocks[NativeRoom.Key(bx,by+1)]=planted;if(!Creative){item.Count--;item.Normalize();}return true;}
            if(id=="minecraft:glass_bottle"&&WaterCell(block)){var potion=NativeStack.New("potion");potion.Data["Potion"]="minecraft:water";ExchangeUsedItem(item,potion);return true;}
            if(id=="minecraft:bone_meal"){
                if(block==null)return true;string plant=(string)block["id"];int limit=plant=="minecraft:beetroots"||plant=="minecraft:nether_wart"?3:plant=="minecraft:cocoa"?2:7;
                if(new[]{"minecraft:wheat","minecraft:carrots","minecraft:potatoes","minecraft:beetroots","minecraft:cocoa","minecraft:sweet_berry_bush"}.Contains(plant)){block["age"]=Math.Min(limit,((int?)block["age"]??0)+random.Next(2,6));if(!Creative){item.Count--;item.Normalize();}}return true;
            }
            if(item.Definition.Tool=="hoe"&&block!=null&&new[]{"minecraft:dirt","minecraft:grass_block","minecraft:dirt_path","minecraft:coarse_dirt","minecraft:rooted_dirt"}.Contains((string)block["id"])&&BlockAt(bx,by+1)==null){block["id"]=(string)block["id"]=="minecraft:coarse_dirt"?"minecraft:dirt":"minecraft:farmland";block["moisture"]=0;Inventory.Wear(item,1,Creative);return true;}
            if(item.Definition.Tool=="axe"&&block!=null){string wood=(string)block["id"];if(!wood.Contains("stripped_")&&(wood.EndsWith("_log",StringComparison.Ordinal)||wood.EndsWith("_wood",StringComparison.Ordinal)||wood.EndsWith("_stem",StringComparison.Ordinal)||wood.EndsWith("_hyphae",StringComparison.Ordinal))){string stripped="minecraft:stripped_"+wood.Substring(10);if(NativeCatalog.Items.ContainsKey(stripped)){block["id"]=stripped;Inventory.Wear(item,1,Creative);return true;}}}
            if(id=="minecraft:experience_bottle"){AddExperience(random.Next(3,12));if(!Creative){item.Count--;item.Normalize();}return true;}
            return false;
        }
        private void ExchangeUsedItem(NativeStack item,NativeStack result) {
            if(Creative){if(!Inventory.Slots.Any(s=>s.Matches(result)))Inventory.Insert(result);return;}
            item.Count--;item.Normalize();if(item.Empty){item.Id=result.Id;item.Count=result.Count;item.Damage=result.Damage;item.Data=(JObject)result.Data.DeepClone();}else if(!Inventory.Insert(result))DropAt(result,x,y+.5);
        }
        private void TickPlants() {
            if(tick%20!=0)return;
            foreach(var cell in Room.Blocks.Values.ToArray()) {
                string id=(string)cell["id"];int bx=(int)cell["x"],by=(int)cell["y"];
                if(id=="minecraft:farmland"){bool water=false;for(int dx=-4;dx<=4;dx++)for(int dy=0;dy<=1;dy++)water|=WaterCell(BlockAt(bx+dx,by+dy));int moisture=(int?)cell["moisture"]??0;cell["moisture"]=water?7:Math.Max(0,moisture-1);if(!water&&moisture==0&&BlockAt(bx,by+1)==null)cell["id"]="minecraft:dirt";}
                if(new[]{"minecraft:wheat","minecraft:carrots","minecraft:potatoes","minecraft:beetroots"}.Contains(id)&&random.Next(25)==0)cell["age"]=Math.Min(id=="minecraft:beetroots"?3:7,((int?)cell["age"]??0)+1);
            }
        }
    }
}
