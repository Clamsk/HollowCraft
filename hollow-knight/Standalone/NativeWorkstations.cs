using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace HollowCraft.Standalone
{
    internal sealed partial class NativeGame
    {
        private int experienceTotal,enchantSeed=1;private string enchantKey="";private JArray enchantChoices=new JArray();private readonly List<Dictionary<string,int>> enchantPlans=new List<Dictionary<string,int>>();
        private sealed class WorkResult {internal NativeStack Stack=new NativeStack();internal int Cost,Experience;internal int[] Consume;}
        private bool Anvil=>container?.Id.EndsWith("anvil",StringComparison.Ordinal)==true;
        private int WorkOutput=>Anvil||container?.Id=="minecraft:grindstone"?2:container?.Id=="minecraft:stonecutter"?1:container?.Id=="minecraft:smithing_table"?3:-1;
        private static int ExperienceAt(int level)=>(int)(level<=16?level*level+6*level:level<=31?2.5*level*level-40.5*level+360:4.5*level*level-162.5*level+2220);
        private int ExperienceLevel {get{int level=0;while(level<1000&&ExperienceAt(level+1)<=experienceTotal)level++;return level;}}
        private double ExperienceProgress {get{int level=ExperienceLevel;return (experienceTotal-ExperienceAt(level))/(double)(ExperienceAt(level+1)-ExperienceAt(level));}}
        private void AddExperience(int value){experienceTotal=Math.Max(0,Math.Min(4000000,experienceTotal+value));}
        private void SpendLevels(int count){int level=Math.Max(0,ExperienceLevel-count);experienceTotal=ExperienceAt(level)+(int)(ExperienceProgress*(ExperienceAt(level+1)-ExperienceAt(level)));}
        private static Dictionary<string,int> Enchants(NativeStack item) {var result=new Dictionary<string,int>();foreach(JObject value in item.Data[item.Id=="minecraft:enchanted_book"?"StoredEnchantments":"Enchantments"] as JArray??new JArray()){string id=NativeCatalog.Id((string)value["id"]);result[id]=Math.Max(0,(int?)value["lvl"]??0);}return result;}
        private static void SetEnchants(NativeStack item,Dictionary<string,int> values){item.Data[item.Id=="minecraft:enchanted_book"?"StoredEnchantments":"Enchantments"]=new JArray(values.Select(p=>new JObject{["id"]=p.Key,["lvl"]=p.Value}));}
        private static bool Compatible(string left,string right)=>left==right||NativeData.Enchantments.TryGetValue(left,out var data)&&((JArray)data["compatible"]).Values<string>().Contains(right);
        private static bool AcceptEnchantment(NativeStack item,JObject data)=>item.Id=="minecraft:book"||item.Id=="minecraft:enchanted_book"||(data["acceptedItems"] as JArray??new JArray()).Values<string>().Contains(item.Id);
        private IEnumerable<JObject> StoneRecipes()=>NativeRecipes.WorkRecipes.Where(r=>(string)r["type"]=="minecraft:stonecutting"&&NativeRecipes.Accepts(r["ingredient"],container.Slots[0]));
        private WorkResult WorkPreview() {
            var plan=new WorkResult();if(container==null||WorkOutput<0)return plan;var left=container.Slots[0];if(left.Empty)return plan;
            if(container.Id=="minecraft:stonecutter"){var recipes=StoneRecipes().ToArray();if(container.Choice<0||container.Choice>=recipes.Length)return plan;var recipe=recipes[container.Choice];plan.Stack=NativeStack.New((string)recipe["result"],(int?)recipe["count"]??1);plan.Consume=new[]{1};return plan;}
            if(container.Id=="minecraft:smithing_table") {
                var template=left;var basis=container.Slots[1];var addition=container.Slots[2];foreach(var recipe in NativeRecipes.WorkRecipes)if(((string)recipe["type"]).StartsWith("minecraft:smithing_",StringComparison.Ordinal)&&NativeRecipes.Accepts(recipe["template"],template)&&NativeRecipes.Accepts(recipe["base"],basis)&&NativeRecipes.Accepts(recipe["addition"],addition)) {
                    var output=basis.Copy(1);if((string)recipe["type"]=="minecraft:smithing_transform")output.Id=(string)recipe["result"]["item"];
                    else {string pattern=template.Id.Substring(10).Replace("_armor_trim_smithing_template","");string material=addition.Id.Substring(10).Replace("_ingot","").Replace("_shard","");output.Data["Trim"]=new JObject{["material"]="minecraft:"+material,["pattern"]="minecraft:"+pattern};}
                    plan.Stack=output;plan.Consume=new[]{1,1,1};return plan;
                }return plan;
            }
            var right=container.Slots[1];var result=left.Copy();var enchants=Enchants(left);
            if(container.Id=="minecraft:grindstone") {
                if(!right.Empty&&(right.Id!=left.Id||left.Definition.Durability==0))return plan;
                if(!right.Empty)result.Damage=Math.Max(0,left.Damage+right.Damage-left.Definition.Durability-(int)(left.Definition.Durability*.05));
                int power=0;foreach(var pair in enchants.Concat(Enchants(right)))if(NativeData.Enchantments.TryGetValue(pair.Key,out var data)&&(bool?)data["curse"]!=true){var powers=data["powers"] as JArray;if(powers!=null&&pair.Value>0&&pair.Value<=powers.Count)power+=(int)powers[pair.Value-1][0];}
                enchants=enchants.Concat(Enchants(right)).Where(p=>NativeData.Enchantments.TryGetValue(p.Key,out var data)&&(bool?)data["curse"]==true).GroupBy(p=>p.Key).ToDictionary(g=>g.Key,g=>g.Max(p=>p.Value));if(result.Id=="minecraft:enchanted_book"&&enchants.Count==0)result.Id="minecraft:book";result.Data.Remove("RepairCost");result.Data.Remove("Enchantments");result.Data.Remove("StoredEnchantments");if(enchants.Count>0)SetEnchants(result,enchants);plan.Stack=result;plan.Consume=new[]{1,right.Empty?0:1};plan.Experience=power>0?(power+1)/2:0;return plan;
            }
            int cost=0,used=0;bool changed=false,renamed=false;
            if(!right.Empty) {
                if(result.Definition.Durability>0&&(result.Definition.RepairItems as JArray??new JArray()).Values<string>().Contains(right.Id)){int each=Math.Max(1,result.Definition.Durability/4);while(result.Damage>0&&used<right.Count){result.Damage=Math.Max(0,result.Damage-each);used++;cost++;changed=true;}}
                else {
                    if(right.Id!=left.Id&&right.Id!="minecraft:enchanted_book")return plan;
                    if(right.Id==left.Id&&result.Definition.Durability>0){int damage=Math.Max(0,left.Damage+right.Damage-left.Definition.Durability-(int)(left.Definition.Durability*.12));if(damage<result.Damage){result.Damage=damage;cost+=2;changed=true;}}
                    foreach(var pair in Enchants(right))if(NativeData.Enchantments.TryGetValue(pair.Key,out var metadata)) {
                        if(!Creative&&!AcceptEnchantment(left,metadata))continue;bool valid=true;foreach(string old in enchants.Keys)if(!Compatible(old,pair.Key)){cost++;valid=false;}if(!valid)continue;
                        int oldLevel=enchants.TryGetValue(pair.Key,out int oldValue)?oldValue:0,level=Math.Min((int)metadata["maxLevel"],oldLevel==pair.Value?oldLevel+1:Math.Max(oldLevel,pair.Value));int weight=(int?)metadata["weight"]??10,multiplier=weight>=10?1:weight>=5?2:weight>=2?4:8;if(right.Id=="minecraft:enchanted_book")multiplier=Math.Max(1,multiplier/2);cost+=multiplier*level;enchants[pair.Key]=level;changed=true;
                    }used=changed?1:0;
                }
            }
            string oldName=(string)left.Data["display"]?["Name"]??"";
            if(container.Rename!=null){string text=container.Rename.Trim(),name=text.Length==0?"":new JObject{["text"]=text}.ToString(Newtonsoft.Json.Formatting.None);if(name!=oldName){var display=result.Data["display"] as JObject??new JObject();if(name.Length==0)display.Remove("Name");else display["Name"]=name;result.Data["display"]=display;cost++;renamed=true;}}
            if(!changed&&!renamed)return plan;SetEnchants(result,enchants);int previous=(int?)left.Data["RepairCost"]??0,other=(int?)right.Data["RepairCost"]??0;cost+=previous+other;if(renamed&&!changed)cost=Math.Min(39,cost);if(changed)result.Data["RepairCost"]=2*Math.Max(previous,other)+1;plan.Stack=result;plan.Cost=cost;plan.Consume=new[]{left.Count,used};return plan;
        }
        private void TakeWork(bool quick) {for(int i=0;i<(quick?64:1);i++)if(!TakeWorkOnce(quick))break;}
        private bool TakeWorkOnce(bool quick) {
            var plan=WorkPreview();if(plan.Stack.Empty||!Creative&&(plan.Cost>=40||ExperienceLevel<plan.Cost))return false;
            if(quick){if(Inventory.Capacity(plan.Stack)<plan.Stack.Count)return false;Inventory.Insert(plan.Stack.Copy());}else {if(!Inventory.Carried.Empty&&(!Inventory.Carried.Matches(plan.Stack)||Inventory.Carried.Count+plan.Stack.Count>plan.Stack.Definition.Maximum))return false;if(Inventory.Carried.Empty)Inventory.Carried=plan.Stack.Copy();else Inventory.Carried.Count+=plan.Stack.Count;}
            for(int i=0;i<plan.Consume.Length;i++){container.Slots[i].Count-=plan.Consume[i];container.Slots[i].Normalize();}if(!Creative&&plan.Cost>0)SpendLevels(plan.Cost);if(plan.Experience>0)AddExperience(plan.Experience);container.Rename=null;return true;
        }
        private void ContainerButton(JObject packet) {
            if(container==null||(int?)packet["syncId"]!=container.Sync)return;int button=(int?)packet["button"]??-1;
            if(container.Id=="minecraft:stonecutter"){if(button>=0&&button<StoneRecipes().Count())container.Choice=button;return;}
            if(container.Id!="minecraft:enchanting_table"||button<0||button>=3)return;UpdateEnchanting();var choice=enchantChoices[button];int required=(int)choice["cost"],consume=button+1;
            if(required==0||!Creative&&(ExperienceLevel<required||container.Slots[1].Id!="minecraft:lapis_lazuli"||container.Slots[1].Count<consume))return;
            var item=container.Slots[0];if(item.Id=="minecraft:book")item.Id="minecraft:enchanted_book";SetEnchants(item,enchantPlans[button]);if(!Creative){container.Slots[1].Count-=consume;container.Slots[1].Normalize();SpendLevels(consume);}enchantSeed=random.Next(1,int.MaxValue);enchantKey="";
        }
        private void UpdateEnchanting() {
            var item=container.Slots[0];int shelves=Math.Min(15,Room.Blocks.Values.Count(c=>(string)c["id"]=="minecraft:bookshelf"&&Math.Abs((int)c["x"]-x)<5&&Math.Abs((int)c["y"]-y)<5));string key=item.Id+"|"+item.Data+"|"+shelves+"|"+enchantSeed;if(enchantKey==key)return;enchantKey=key;enchantChoices=new JArray();enchantPlans.Clear();var rng=new Random(enchantSeed);
            for(int slot=0;slot<3;slot++){int basis=rng.Next(8)+1+shelves/2+rng.Next(shelves+1),cost=slot==0?Math.Max(1,basis/3):slot==1?basis*2/3+1:Math.Max(basis,shelves*2);if(item.Empty||item.Definition.Enchantability==0||Enchants(item).Count>0||cost<slot+1)cost=0;var plan=cost>0?GenerateEnchantments(item,cost,new Random(unchecked(enchantSeed+slot))):new Dictionary<string,int>();if(plan.Count==0)cost=0;var clue=plan.FirstOrDefault();enchantChoices.Add(new JObject{["cost"]=cost,["name"]=clue.Key==null?"":clue.Key.Split(':').Last().Replace('_',' ')+" "+clue.Value});enchantPlans.Add(plan);}
        }
        private static Dictionary<string,int> GenerateEnchantments(NativeStack item,int level,Random rng) {
            int ability=item.Definition.Enchantability/4,power=level+1+rng.Next(ability+1)+rng.Next(ability+1);power=Math.Max(1,(int)Math.Round(power*(1+(rng.NextDouble()+rng.NextDouble()-1)*.15)));var pool=new Dictionary<string,int>();
            foreach(var entry in NativeData.Enchantments){var data=entry.Value;if((bool?)data["treasure"]==true||!AcceptEnchantment(item,data))continue;var powers=data["powers"] as JArray;if(powers==null)continue;for(int i=powers.Count-1;i>=0;i--)if(power>=(int)powers[i][0]&&power<=(int)powers[i][1]){pool[entry.Key]=i+1;break;}}
            var chosen=new Dictionary<string,int>();while(pool.Count>0){int total=pool.Keys.Sum(id=>(int?)NativeData.Enchantments[id]["weight"]??1),roll=rng.Next(total);string picked=pool.Keys.First();foreach(string id in pool.Keys){roll-=(int?)NativeData.Enchantments[id]["weight"]??1;if(roll<0){picked=id;break;}}chosen[picked]=pool[picked];pool.Remove(picked);foreach(string id in pool.Keys.Where(id=>!Compatible(id,picked)).ToArray())pool.Remove(id);if(rng.Next(50)>power)break;power/=2;}return chosen;
        }
        private void WorkstationInfo(JObject json,JArray slots) {
            int[] xs=null,ys=null;
            if(Anvil){xs=new[]{27,76,134};ys=new[]{47,47,47};json["cost"]=WorkPreview().Cost;json["texture"]="gui_anvil";}
            else if(container.Id=="minecraft:grindstone"){xs=new[]{49,49,129};ys=new[]{19,40,34};}
            else if(container.Id=="minecraft:smithing_table"){xs=new[]{8,26,44,98};ys=new[]{48,48,48,48};json["texture"]="gui_smithing";}
            else if(container.Id=="minecraft:stonecutter"){xs=new[]{20,143};ys=new[]{33,33};json["selector"]="stonecutter";json["selectedChoice"]=container.Choice;int index=0;json["choices"]=new JArray(StoneRecipes().Select(r=>{var item=NativeStack.New((string)r["result"],(int?)r["count"]??1).Json();item["button"]=index++;return item;}));}
            else if(container.Id=="minecraft:enchanting_table"){xs=new[]{15,35};ys=new[]{47,47};json["texture"]="gui_enchanting_table";UpdateEnchanting();json["enchants"]=enchantChoices.DeepClone();}
            if(xs!=null)for(int i=0;i<xs.Length;i++){slots[i]["x"]=xs[i];slots[i]["y"]=ys[i];}
        }
        private void QuickContainerMove(JObject packet) {
            if(container==null)return;int slot=(int?)packet["slot"]??-1;if((string)packet["direction"]=="deposit"){if(slot<0||slot>=36)return;int start=Crafting?10:container.Slots.Length;slot=start+(slot>=9?slot-9:27+slot);}
            SlotClick(new JObject{["handler"]="container",["syncId"]=container.Sync,["slot"]=slot,["action"]="QUICK_MOVE"});
        }
        private void LegacyCraft(JObject packet) {
            if(!(packet["slots"] is JArray values)||values.Count!=9)return;var grid=NativeInventory.Empty(9);int[] used=new int[36];bool table=Room.Blocks.Values.Any(b=>(string)b["id"]=="minecraft:crafting_table"&&Math.Abs(Number(b,"x")-x)<=3&&Math.Abs(Number(b,"y")-y)<=3);
            for(int i=0;i<9;i++){int index=(int)values[i];if(index<0||index>=36)continue;if(!table&&(i%3>=2||i/3>=2))return;if(++used[index]>Inventory.Slots[index].Count)return;grid[i]=Inventory.Slots[index].Copy(1);}
            var result=NativeRecipes.Result(grid,3);if(result.Empty)return;for(int i=0;i<36;i++){Inventory.Slots[i].Count-=used[i];Inventory.Slots[i].Normalize();}if(!Inventory.Insert(result))DropAt(result,x,y+.5);foreach(var ingredient in grid)if(!ingredient.Empty&&ingredient.Definition.Remainder!=null){var left=NativeStack.New(ingredient.Definition.Remainder);if(!Inventory.Insert(left))DropAt(left,x,y+.5);}
        }
    }
}
