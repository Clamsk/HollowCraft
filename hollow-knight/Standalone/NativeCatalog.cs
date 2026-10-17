using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace HollowCraft.Standalone
{
    internal sealed class NativeItem
    {
        internal string Id,Tool="",Equipment="",Group="items",Remainder,Entity;
        internal int Maximum=64,Durability,Armor,Food,Harvest;
        internal double Attack=1,Speed=4,Hardness=.8,Saturation,MiningSpeed=1;
        internal bool Block,Solid=true,AlwaysEdible,FastFood,RequiresTool;internal int Fuel;internal double Toughness;internal JArray Collision,FoodEffects;
    }
    internal static class NativeCatalog
    {
        internal static readonly Dictionary<string,NativeItem> Items=new Dictionary<string,NativeItem>(StringComparer.Ordinal);
        internal static bool RegistryLoaded;
        private static readonly HashSet<string> vanillaItems=new HashSet<string>();
        internal static bool CreatureSupported(string kind)=>new[]{"minecraft:zombie","minecraft:skeleton","minecraft:creeper","minecraft:spider","minecraft:pig","minecraft:cow","minecraft:sheep","minecraft:chicken","minecraft:villager"}.Contains(kind);
        internal static bool Visible(string id)=>(!RegistryLoaded||vanillaItems.Contains(id)||!id.StartsWith("minecraft:",StringComparison.Ordinal))&&(Get(id).Entity==null||CreatureSupported(Get(id).Entity));
        internal static string Id(string id)=>string.IsNullOrEmpty(id)?"minecraft:air":id.Contains(":")?id:"minecraft:"+id;
        internal static NativeItem Get(string id)=>Items.TryGetValue(Id(id),out var value)?value:Items["minecraft:air"];
        private static NativeItem Add(string id,int max=64) {var value=new NativeItem{Id=Id(id),Maximum=max};Items[value.Id]=value;return value;}
        static NativeCatalog() {
            Add("air").Solid=false;
            foreach(string liquid in new[]{"water","lava"}){var fluid=Add(liquid);fluid.Block=true;fluid.Solid=false;fluid.Group="fluids";}
            foreach(string id in new[]{"stone","cobblestone","cobbled_deepslate","deepslate","granite","diorite","andesite","dirt","grass_block","sand","red_sand","gravel","glass","obsidian","crying_obsidian","bedrock","netherrack","end_stone","bricks","stone_bricks","coal_ore","iron_ore","gold_ore","diamond_ore","redstone_ore","coal_block","iron_block","gold_block","diamond_block","crafting_table","furnace","blast_furnace","smoker","chest","barrel","ender_chest","hopper","dispenser","dropper","anvil","enchanting_table","brewing_stand","tnt","bookshelf","hay_block","slime_block","honey_block","sponge","wet_sponge","redstone_block","redstone_lamp","target","piston","sticky_piston","observer","note_block","jukebox"}) {
                var item=Add(id);item.Block=true;item.Group="blocks";
                item.Hardness=id=="bedrock"?-1:id.Contains("obsidian")?50:id.Contains("deepslate")?3:id=="stone"||id=="cobblestone"?1.5:id.Contains("_ore")?3:id=="dirt"||id=="grass_block"||id.Contains("sand")||id=="gravel"?.5:id=="glass"?.3:2;
                item.Tool=id=="dirt"||id=="grass_block"||id.Contains("sand")||id=="gravel"?"shovel":id.Contains("chest")||id=="crafting_table"||id=="barrel"||id=="bookshelf"?"axe":"pickaxe";
                item.Harvest=id.Contains("obsidian")?3:id.Contains("diamond_ore")||id=="gold_ore"||id=="redstone_ore"?2:id=="iron_ore"?1:0;
            }
            foreach(string wood in new[]{"oak","spruce","birch","jungle","acacia","dark_oak","mangrove","cherry","bamboo","crimson","warped"})foreach(string suffix in new[]{"log","planks","leaves","slab","stairs","fence","fence_gate","door","trapdoor","button","pressure_plate","boat","chest_boat"}) {
                var value=Add(wood+"_"+suffix,suffix.Contains("boat")?1:64);value.Block=!suffix.Contains("boat");value.Group=value.Block?"blocks":"transport";value.Tool=suffix=="leaves"?"shears":"axe";value.Hardness=suffix=="leaves"?.2:2;value.Solid=!suffix.Contains("button")&&!suffix.Contains("pressure");
            }
            foreach(string wool in new[]{"white","orange","magenta","light_blue","yellow","lime","pink","gray","light_gray","cyan","purple","blue","brown","green","red","black"}) {var value=Add(wool+"_wool");value.Block=true;value.Group="blocks";value.Tool="shears";value.Hardness=.8;}
            string[] materials={"wooden","stone","iron","golden","diamond","netherite"};int[] durability={59,131,250,32,1561,2031};double[] bonus={0,1,2,0,3,4},mining={2,4,6,12,8,9};
            for(int i=0;i<materials.Length;i++)foreach(string tool in new[]{"sword","axe","pickaxe","shovel","hoe"}) {
                var value=Add(materials[i]+"_"+tool,1);value.Tool=tool;value.Durability=durability[i];value.Harvest=i==3?0:i==4?3:i==5?4:i;value.MiningSpeed=mining[i];value.Group="equipment";
                value.Attack=tool=="sword"?4+bonus[i]:tool=="axe"?(i==0||i==3?7:i==5?10:9):tool=="pickaxe"?2+bonus[i]:tool=="shovel"?2.5+bonus[i]:1;
                value.Speed=tool=="sword"?1.6:tool=="axe"?(i==0||i==1?.8:1):tool=="pickaxe"?1.2:tool=="shovel"?1:i==0||i==3?1:i==1?2:i==2?3:4;
            }
            string[] armor={"leather","golden","chainmail","iron","diamond","netherite"};int[][] points={new[]{1,3,2,1},new[]{2,5,3,1},new[]{2,5,4,1},new[]{2,6,5,2},new[]{3,8,6,3},new[]{3,8,6,3}};int[] factors={5,7,15,15,33,37};
            for(int i=0;i<armor.Length;i++)for(int slot=0;slot<4;slot++) {string part=new[]{"helmet","chestplate","leggings","boots"}[slot];var value=Add(armor[i]+"_"+part,1);value.Equipment=part;value.Armor=points[i][slot];value.Durability=new[]{11,16,15,13}[slot]*factors[i];value.Group="equipment";}
            foreach(var pair in new[]{Tuple.Create("cooked_beef",8,12.8),Tuple.Create("cooked_porkchop",8,12.8),Tuple.Create("cooked_chicken",6,7.2),Tuple.Create("bread",5,6.0),Tuple.Create("apple",4,2.4),Tuple.Create("golden_apple",4,9.6),Tuple.Create("enchanted_golden_apple",4,9.6),Tuple.Create("carrot",3,3.6),Tuple.Create("golden_carrot",6,14.4),Tuple.Create("baked_potato",5,6.0),Tuple.Create("rotten_flesh",4,.8)}) {var value=Add(pair.Item1);value.Food=pair.Item2;value.Saturation=pair.Item3;value.Group="food";}
            foreach(string id in new[]{"arrow","spectral_arrow","tipped_arrow","iron_ingot","gold_ingot","diamond","netherite_ingot","coal","charcoal","raw_iron","raw_gold","raw_copper","iron_nugget","gold_nugget","copper_ingot","stick","string","feather","flint","gunpowder","wheat","wheat_seeds","redstone","redstone_torch","torch","soul_torch","repeater","comparator","lever","bucket","water_bucket","lava_bucket","milk_bucket","ender_pearl","snowball","egg","firework_rocket","book","lapis_lazuli","blaze_powder","nether_wart","glass_bottle","potion","splash_potion","lingering_potion","totem_of_undying","shield","bow","crossbow","trident","elytra","flint_and_steel","shears","hollowcraft_bridge:zenith","hollowcraft_bridge:stardust_wings"})Add(id);
            foreach(string id in new[]{"shield","bow","crossbow","trident","elytra","flint_and_steel","shears","totem_of_undying","water_bucket","lava_bucket","milk_bucket","potion","splash_potion","lingering_potion","hollowcraft_bridge:zenith","hollowcraft_bridge:stardust_wings"})Get(id).Maximum=1;
            Get("bucket").Maximum=Get("ender_pearl").Maximum=Get("snowball").Maximum=Get("egg").Maximum=16;
            foreach(string mob in new[]{"zombie","skeleton","creeper","spider","pig","cow","sheep","chicken","villager"})Add(mob+"_spawn_egg").Group="creatures";
            Get("bow").Durability=384;Get("crossbow").Durability=465;Get("shield").Durability=336;Get("trident").Durability=250;Get("trident").Attack=9;Get("trident").Speed=1.1;Get("elytra").Durability=432;Get("elytra").Equipment=Get("hollowcraft_bridge:stardust_wings").Equipment="chestplate";Get("hollowcraft_bridge:zenith").Attack=190;Get("hollowcraft_bridge:zenith").Speed=2;Get("flint_and_steel").Durability=64;Get("shears").Durability=238;
            foreach(string id in new[]{"torch","redstone_torch","soul_torch","redstone","lever","repeater","comparator"}) {Get(id).Block=true;Get(id).Solid=false;Get(id).Group="redstone";Get(id).Hardness=0;}
        }
        internal static void Import(JObject data) {
            foreach(JObject entry in (JArray)data["items"]) {
                string id=(string)entry["id"];vanillaItems.Add(id);if(!Items.TryGetValue(id,out var item))item=Add(id);
                item.Maximum=(int)entry["maxCount"];item.Durability=(int)entry["durability"];item.Food=(int?)entry["food"]??0;item.Saturation=(double?)entry["saturation"]??0;item.AlwaysEdible=(bool?)entry["alwaysEdible"]??false;item.FastFood=(bool?)entry["fastFood"]??false;
                item.Remainder=(string)entry["remainder"];item.Entity=(string)entry["entity"];item.Fuel=(int?)entry["fuel"]??0;item.Armor=(int?)entry["armor"]??0;item.Toughness=(double?)entry["toughness"]??0;
                item.FoodEffects=entry["foodEffects"] as JArray;
                if(entry["tool"]!=null)item.Tool=(string)entry["tool"];item.RequiresTool=(bool?)entry["requiresTool"]??false;
                item.Attack=(double?)entry["attack"]??item.Attack;item.Speed=(double?)entry["speed"]??item.Speed;item.Harvest=(int?)entry["harvest"]??item.Harvest;item.MiningSpeed=(double?)entry["miningSpeed"]??item.MiningSpeed;
                string slot=(string)entry["equipment"];if(slot!=null)item.Equipment=slot=="head"?"helmet":slot=="chest"?"chestplate":slot=="legs"?"leggings":"boots";
                if((bool?)entry["block"]==true){item.Block=true;item.Hardness=(double?)entry["hardness"]??item.Hardness;item.Collision=entry["collision"] as JArray;item.Solid=item.Collision==null||item.Collision.Count>0;item.Group="blocks";}
                else if(item.Food>0)item.Group="food";else if(item.Entity!=null)item.Group="creatures";else if(item.Durability>0)item.Group="equipment";
            }
            RegistryLoaded=true;
        }
        internal static string Label(string id)=>Get(id).Id.Split(':').Last().Replace('_',' ');
    }
    internal sealed class NativeStack
    {
        internal string Id="minecraft:air";internal int Count,Damage;internal JObject Data=new JObject();
        internal bool Empty=>Count<=0||Id=="minecraft:air";
        internal NativeItem Definition=>NativeCatalog.Get(Id);
        internal NativeStack Copy(int count=-1)=>new NativeStack{Id=Id,Count=count<0?Count:count,Damage=Damage,Data=(JObject)Data.DeepClone()};
        internal bool Matches(NativeStack other)=>!Empty&&!other.Empty&&Id==other.Id&&Damage==other.Damage&&JToken.DeepEquals(Data,other.Data);
        internal void Normalize() {if(Count<=0){Id="minecraft:air";Count=Damage=0;Data=new JObject();}}
        internal JObject Json()=>new JObject{["id"]=Empty?"minecraft:air":Id,["count"]=Empty?0:Count,["name"]=(Data["Potion"]!=null?((string)Data["Potion"]).Split(':').Last().Replace('_',' ')+" ":"")+NativeCatalog.Label(Id),["render"]=Id,["block"]=Definition.Block,["predictSolid"]=Definition.Block&&Definition.Solid,["maxCount"]=Definition.Maximum,["damage"]=Damage,["maxDamage"]=Definition.Durability,["food"]=Definition.Food>0,["nbt"]=Data.DeepClone()};
        internal static NativeStack Read(JToken token) {
            var value=new NativeStack{Id=NativeCatalog.Id((string)token?["id"]),Count=Math.Max(0,(int?)token?["count"]??0),Damage=Math.Max(0,(int?)token?["damage"]??0),Data=token?["nbt"] is JObject data?(JObject)data.DeepClone():new JObject()};
            if(!NativeCatalog.Items.ContainsKey(value.Id))throw new InvalidOperationException("Unknown native item in save: "+value.Id);
            value.Count=Math.Min(value.Count,value.Definition.Maximum);value.Normalize();return value;
        }
        internal static NativeStack New(string id,int count=1)=>new NativeStack{Id=NativeCatalog.Id(id),Count=count};
    }
}
