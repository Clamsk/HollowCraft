using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Newtonsoft.Json.Linq;

namespace HollowCraft.Standalone
{
    internal sealed class NativeInventory
    {
        internal readonly NativeStack[] Slots=Empty(36),Armor=Empty(4),Craft=Empty(4);
        internal NativeStack Offhand=new NativeStack(),Carried=new NativeStack();
        internal int Selected;
        internal NativeStack Hand=>Slots[Selected];
        internal static NativeStack[] Empty(int count)=>Enumerable.Range(0,count).Select(_=>new NativeStack()).ToArray();
        internal static string Equipment(int index)=>new[]{"helmet","chestplate","leggings","boots"}[index];
        internal static bool Fit(NativeStack a,NativeStack b)=>a.Empty||a.Matches(b)&&a.Count<a.Definition.Maximum;
        internal int Capacity(NativeStack item,IEnumerable<int> range=null)=>(range??Enumerable.Range(0,36)).Sum(i=>Slots[i].Empty?item.Definition.Maximum:Slots[i].Matches(item)?item.Definition.Maximum-Slots[i].Count:0);
        internal bool Insert(NativeStack item,IEnumerable<int> range=null) {
            if(item.Empty)return true;int[] targets=(range??Enumerable.Range(0,36)).Distinct().ToArray();
            foreach(bool empties in new[]{false,true})foreach(int i in targets) {
                var at=Slots[i];if(at.Empty!=empties||!Fit(at,item))continue;
                int moved=Math.Min(item.Count,item.Definition.Maximum-(at.Empty?0:at.Count));
                if(at.Empty)Slots[i]=item.Copy(moved);else at.Count+=moved;
                item.Count-=moved;item.Normalize();if(item.Empty)return true;
            }
            return false;
        }
        internal bool Consume(string id,int count=1) {
            id=NativeCatalog.Id(id);if(Slots.Where(s=>s.Id==id).Sum(s=>s.Count)<count)return false;
            foreach(var item in Slots.Where(s=>s.Id==id)) {int used=Math.Min(count,item.Count);item.Count-=used;count-=used;item.Normalize();if(count==0)break;}
            return true;
        }
        internal void Starter() {
            Array.Copy(Empty(36),Slots,36);Array.Copy(Empty(4),Armor,4);Array.Copy(Empty(4),Craft,4);Carried=new NativeStack();Offhand=new NativeStack();Selected=0;
            string[] hotbar={"iron_sword","bow","golden_apple","cobblestone","water_bucket","lava_bucket","tnt","flint_and_steel","ender_pearl"};
            for(int i=0;i<9;i++)Slots[i]=NativeStack.New(hotbar[i],NativeCatalog.Get(hotbar[i]).Maximum==1?1:64);
            Slots[8].Count=16;
            foreach(var pair in new[]{Tuple.Create("arrow",64),Tuple.Create("cooked_beef",64),Tuple.Create("cobblestone",64),Tuple.Create("cobblestone",64),Tuple.Create("oak_log",64),Tuple.Create("shield",1),Tuple.Create("iron_axe",1),Tuple.Create("iron_pickaxe",1),Tuple.Create("torch",64),Tuple.Create("iron_ingot",64),Tuple.Create("bedrock",64),Tuple.Create("totem_of_undying",1),Tuple.Create("totem_of_undying",1),Tuple.Create("oak_boat",1)})Insert(NativeStack.New(pair.Item1,pair.Item2));
            for(int i=0;i<4;i++)Armor[i]=NativeStack.New("iron_"+Equipment(i));
        }
        internal JObject Save()=>new JObject{["slots"]=new JArray(Slots.Select(s=>s.Json())),["armor"]=new JArray(Armor.Select(s=>s.Json())),["offhand"]=Offhand.Json(),["carried"]=Carried.Json(),["craft"]=new JArray(Craft.Select(s=>s.Json())),["selected"]=Selected};
        internal void Load(JObject data) {
            LoadArray(data["slots"],Slots);LoadArray(data["armor"],Armor);LoadArray(data["craft"],Craft);
            Offhand=NativeStack.Read(data["offhand"]);Carried=NativeStack.Read(data["carried"]);Selected=Math.Max(0,Math.Min(8,(int?)data["selected"]??0));
        }
        private static void LoadArray(JToken token,NativeStack[] target) {if(!(token is JArray array)||array.Count!=target.Length)throw new InvalidOperationException("Invalid native inventory length");for(int i=0;i<target.Length;i++)target[i]=NativeStack.Read(array[i]);}
        private readonly Random random=new Random();
        internal void Wear(NativeStack item,int amount,bool creative) {if(creative||item.Empty||item.Definition.Durability<=0)return;int unbreaking=NativeData.Enchantment(item,"unbreaking"),wear=0;for(int i=0;i<amount;i++)if(unbreaking==0||item.Definition.Armor>0&&random.NextDouble()<.6||random.Next(unbreaking+1)==0)wear++;item.Damage+=wear;if(item.Damage>=item.Definition.Durability){item.Count--;item.Damage=0;item.Normalize();}}
    }
    internal sealed class NativeContainer
    {
        internal string Id;internal NativeStack[] Slots;internal int Burn,BurnMax,Cook,Brew,BrewFuel;internal int Sync;internal string CookingItem="",BrewKey="";
        internal NativeContainer(string id,int count) {Id=id;Slots=NativeInventory.Empty(count);}
        internal JObject Save()=>new JObject{["id"]=Id,["slots"]=new JArray(Slots.Select(s=>s.Json())),["burn"]=Burn,["burnMax"]=BurnMax,["cook"]=Cook,["cookingItem"]=CookingItem,["brew"]=Brew,["brewFuel"]=BrewFuel,["brewKey"]=BrewKey};
        internal static NativeContainer Read(JObject data) {
            var slots=(JArray)data["slots"];if(slots==null||slots.Count>54)throw new InvalidOperationException("Invalid native container");
            var box=new NativeContainer((string)data["id"],slots.Count){Burn=Math.Max(0,(int?)data["burn"]??0),BurnMax=Math.Max(0,(int?)data["burnMax"]??0),Cook=Math.Max(0,(int?)data["cook"]??0),CookingItem=(string)data["cookingItem"]??"",Brew=Math.Max(0,Math.Min(400,(int?)data["brew"]??0)),BrewFuel=Math.Max(0,Math.Min(20,(int?)data["brewFuel"]??0)),BrewKey=(string)data["brewKey"]??""};
            for(int i=0;i<slots.Count;i++)box.Slots[i]=NativeStack.Read(slots[i]);return box;
        }
    }
    internal sealed class NativeRecipe
    {
        internal string[] Pattern;internal string Output;internal int Count=1;internal Dictionary<char,string[]> Keys;internal string[][] Loose;
        internal NativeStack Match(NativeStack[] grid,int width) {
            if(Loose!=null) {
                var remaining=grid.Where(s=>!s.Empty).Select(s=>s.Id).ToList();if(remaining.Count!=Loose.Length)return null;
                return LooseMatch(remaining,0,new bool[remaining.Count])?NativeStack.New(Output,Count):null;
            }
            int minX=width,minY=width,maxX=-1,maxY=-1;
            for(int i=0;i<grid.Length;i++)if(!grid[i].Empty){minX=Math.Min(minX,i%width);minY=Math.Min(minY,i/width);maxX=Math.Max(maxX,i%width);maxY=Math.Max(maxY,i/width);}
            if(maxX<0||maxY-minY+1!=Pattern.Length||maxX-minX+1!=Pattern[0].Length)return null;
            foreach(bool mirror in new[]{false,true}) {
                bool valid=true;
                for(int y=0;y<Pattern.Length;y++)for(int x=0;x<Pattern[0].Length;x++) {
                    char key=Pattern[y][mirror?Pattern[0].Length-1-x:x];var stack=grid[(minY+y)*width+minX+x];
                    if(key==' '? !stack.Empty:stack.Empty||!Keys[key].Any(value=>Ingredient(value,stack.Id)))valid=false;
                }
                if(valid)return NativeStack.New(Output,Count);
            }
            return null;
        }
        private bool LooseMatch(List<string> remaining,int at,bool[] used) {if(at==Loose.Length)return true;for(int i=0;i<remaining.Count;i++)if(!used[i]&&Loose[at].Any(key=>Ingredient(key,remaining[i]))){used[i]=true;if(LooseMatch(remaining,at+1,used))return true;used[i]=false;}return false;}
        private static bool Ingredient(string ingredient,string id)=>NativeData.Ingredient(ingredient,id);
    }
    internal static class NativeRecipes
    {
        internal static readonly List<NativeRecipe> All=new List<NativeRecipe>();
        private static void Shape(string output,int count,string pattern,params string[] keys) {var map=new Dictionary<char,string[]>();foreach(string pair in keys)map[pair[0]]=pair.Substring(2).Split('|');All.Add(new NativeRecipe{Output=output,Count=count,Pattern=pattern.Split('/'),Keys=map});}
        static NativeRecipes() {
            foreach(string wood in new[]{"oak","spruce","birch","jungle","acacia","dark_oak","mangrove","cherry","bamboo","crimson","warped"}) {
                Shape(wood+"_planks",4,"L","L="+wood+"_log");Shape(wood+"_boat",1,"P P/PPP","P="+wood+"_planks");Shape(wood+"_slab",6,"PPP","P="+wood+"_planks");Shape(wood+"_stairs",4,"P  /PP /PPP","P="+wood+"_planks");
            }
            Shape("stick",4,"P/P","P=#planks");Shape("crafting_table",1,"PP/PP","P=#planks");Shape("chest",1,"PPP/P P/PPP","P=#planks");Shape("furnace",1,"CCC/C C/CCC","C=cobblestone");
            Shape("torch",4,"C/S","C=coal|charcoal","S=stick");Shape("redstone_torch",1,"R/S","R=redstone","S=stick");Shape("bucket",1,"I I/ I ","I=iron_ingot");
            Shape("bow",1," ST/S T/ ST","S=stick","T=string");Shape("arrow",4,"F/S/A","F=flint","S=stick","A=feather");Shape("shield",1,"PIP/PPP/ P ","P=#planks","I=iron_ingot");
            Shape("golden_apple",1,"GGG/GAG/GGG","G=gold_ingot","A=apple");Shape("bread",1,"WWW","W=wheat");Shape("tnt",1,"GSG/SGS/GSG","G=gunpowder","S=sand|red_sand");
            foreach(var material in new[]{Tuple.Create("wooden","#planks"),Tuple.Create("stone","cobblestone"),Tuple.Create("iron","iron_ingot"),Tuple.Create("golden","gold_ingot"),Tuple.Create("diamond","diamond")}) {
                string p=material.Item1,m="M="+material.Item2;
                Shape(p+"_sword",1,"M/M/S",m,"S=stick");Shape(p+"_pickaxe",1,"MMM/ S / S ",m,"S=stick");Shape(p+"_axe",1,"MM/MS/ S",m,"S=stick");Shape(p+"_shovel",1,"M/S/S",m,"S=stick");Shape(p+"_hoe",1,"MM/ S/ S",m,"S=stick");
            }
            foreach(var material in new[]{Tuple.Create("iron","iron_ingot"),Tuple.Create("golden","gold_ingot"),Tuple.Create("diamond","diamond")}) {
                string m="M="+material.Item2,p=material.Item1;
                Shape(p+"_helmet",1,"MMM/M M",m);Shape(p+"_chestplate",1,"M M/MMM/MMM",m);Shape(p+"_leggings",1,"MMM/M M/M M",m);Shape(p+"_boots",1,"M M/M M",m);
            }
            All.Add(new NativeRecipe{Output="flint_and_steel",Loose=new[]{new[]{"flint"},new[]{"iron_ingot"}}});
        }
        internal sealed class Cooking {internal string Output;internal int Ticks=200;internal double Experience;}
        private static readonly Dictionary<string,Cooking> cooking=new Dictionary<string,Cooking>();
        internal static int ImportedCrafting,ImportedCooking;
        private static string[] Ingredients(JToken token) {if(token is JArray choices)return choices.SelectMany(Ingredients).Distinct().ToArray();if(token?["item"]!=null)return new[]{NativeCatalog.Id((string)token["item"])};if(token?["tag"]!=null)return new[]{"#"+NativeCatalog.Id((string)token["tag"])};return new string[0];}
        internal static void Load(string root) {
            string directory=Path.Combine(root,"vanilla","recipes");if(!Directory.Exists(directory))return;
            var recipes=new List<NativeRecipe>();cooking.Clear();ImportedCooking=0;
            foreach(string file in Directory.GetFiles(directory,"*.json",SearchOption.AllDirectories).OrderBy(p=>p,StringComparer.Ordinal)) {
                var data=JObject.Parse(File.ReadAllText(file));string type=((string)data["type"]??"").Split(':').Last();var result=data["result"];string output=result?.Type==JTokenType.String?(string)result:(string)result?["item"];if(output==null||!NativeCatalog.Items.ContainsKey(NativeCatalog.Id(output)))continue;
                if(type=="crafting_shaped") {var keys=new Dictionary<char,string[]>();foreach(var key in (JObject)data["key"])keys[key.Key[0]]=Ingredients(key.Value);var pattern=((JArray)data["pattern"]).Select(p=>(string)p).ToArray();recipes.Add(new NativeRecipe{Output=output,Count=(int?)result?["count"]??1,Pattern=pattern,Keys=keys});}
                else if(type=="crafting_shapeless")recipes.Add(new NativeRecipe{Output=output,Count=(int?)result?["count"]??1,Loose=((JArray)data["ingredients"]).Select(Ingredients).ToArray()});
                else if(type=="smelting"||type=="blasting"||type=="smoking"||type=="campfire_cooking") {var ingredients=Ingredients(data["ingredient"]);var cook=new Cooking{Output=NativeCatalog.Id(output),Ticks=(int?)data["cookingtime"]??200,Experience=(double?)data["experience"]??0};foreach(string item in NativeCatalog.Items.Keys)if(ingredients.Any(i=>NativeData.Ingredient(i,item)))cooking[type+":"+item]=cook;ImportedCooking++;}
            }
            if(recipes.Count>0){All.Clear();All.AddRange(recipes);}ImportedCrafting=recipes.Count;
        }
        internal static Cooking Cook(string id,string station="minecraft:furnace") {
            string type=station=="minecraft:blast_furnace"?"blasting":station=="minecraft:smoker"?"smoking":station.Contains("campfire")?"campfire_cooking":"smelting";
            if(cooking.Count>0)return cooking.TryGetValue(type+":"+id,out var value)?value:null;
            string output=Smelt(id);return output==null?null:new Cooking{Output=NativeCatalog.Id(output),Ticks=station=="minecraft:furnace"?200:100};
        }
        internal static NativeStack Result(NativeStack[] grid,int width) {if(grid.All(item=>item.Empty))return new NativeStack();foreach(var recipe in All){var result=recipe.Match(grid,width);if(result!=null)return result;}return new NativeStack();}
        internal static string Smelt(string id) {switch(id){case "minecraft:raw_iron":case "minecraft:iron_ore":return "iron_ingot";case "minecraft:raw_gold":case "minecraft:gold_ore":return "gold_ingot";case "minecraft:raw_copper":return "copper_ingot";case "minecraft:cobblestone":return "stone";case "minecraft:sand":case "minecraft:red_sand":return "glass";default:return id.EndsWith("_log",StringComparison.Ordinal)?"charcoal":null;}}
        internal static int Fuel(NativeStack item)=>NativeCatalog.RegistryLoaded?item.Definition.Fuel:item.Id=="minecraft:coal"||item.Id=="minecraft:charcoal"?1600:item.Id=="minecraft:lava_bucket"?20000:item.Id.EndsWith("_log",StringComparison.Ordinal)||item.Id.EndsWith("_planks",StringComparison.Ordinal)?300:item.Id=="minecraft:stick"?100:0;
    }
}
