using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
namespace HollowCraft.Standalone
{
    // Evaluate local vanilla entity loot data; unknown predicates fail closed.
    internal static class NativeLoot
    {
        private static readonly Dictionary<string,JObject> tables=new Dictionary<string,JObject>();
        internal static void Load(string root){tables.Clear();string path=Path.Combine(root,"vanilla","loot_tables");if(!Directory.Exists(path))return;foreach(string file in Directory.GetFiles(path,"*.json",SearchOption.AllDirectories))tables["minecraft:"+file.Substring(path.Length+1).Replace('\\','/').Replace(".json","")]=JObject.Parse(File.ReadAllText(file));}
        private static double Number(JToken token,Random random) {if(token==null)return 0;if(token.Type==JTokenType.Integer||token.Type==JTokenType.Float)return (double)token;string type=((string)token["type"]??"").Split(':').Last();double min=(double?)token["min"]??0,max=(double?)token["max"]??min;if(type=="uniform")return min+random.NextDouble()*(max-min);if(type=="binomial"){int n=(int?)token["n"]??0;double p=(double?)token["p"]??0;return Enumerable.Range(0,Math.Min(64,n)).Count(_=>random.NextDouble()<p);}return (double?)token["value"]??0;}
        private static int Integer(JToken token,Random random){if(token is JObject value&&((string)value["type"]??"").EndsWith(":uniform",StringComparison.Ordinal)){int min=(int)Math.Floor((double?)value["min"]??0),max=(int)Math.Floor((double?)value["max"]??min);return random.Next(min,max+1);}return (int)Math.Floor(Number(token,random));}
        private static bool Nbt(string text,JObject mob){var matches=Regex.Matches(text,@"(?<field>[A-Za-z][A-Za-z0-9_]*)\s*:\s*(?<value>-?\d+)[bs]?");if(matches.Count==0)return false;foreach(Match match in matches){int expected=int.Parse(match.Groups["value"].Value),actual;switch(match.Groups["field"].Value){case "Sheared":actual=(bool?)mob["sheared"]==true?1:0;break;case "Color":actual=(int?)mob["woolColorId"]??0;break;case "Size":actual=((int?)mob["size"]??1)-1;break;case "IsBaby":actual=(bool?)mob["baby"]==true?1:0;break;default:return false;}if(actual!=expected)return false;}return true;}
        private static bool Condition(JObject value,JObject mob,int looting,bool player,Random random) {
            string type=((string)value["condition"]??"").Split(':').Last();
            switch(type) {
                case "killed_by_player":return player;
                case "random_chance":return random.NextDouble()<((double?)value["chance"]??0);
                case "random_chance_with_looting":return random.NextDouble()<((double?)value["chance"]??0)+looting*((double?)value["looting_multiplier"]??0);
                case "inverted":return value["term"] is JObject inverse&&!Condition(inverse,mob,looting,player,random);
                case "alternative":case "any_of":return (value["terms"] as JArray??new JArray()).OfType<JObject>().Any(v=>Condition(v,mob,looting,player,random));
                case "all_of":return (value["terms"] as JArray??new JArray()).OfType<JObject>().All(v=>Condition(v,mob,looting,player,random));
                case "entity_properties":
                    if((string)value["entity"]!="this")return false;var predicate=value["predicate"] as JObject??new JObject();
                    foreach(var flag in predicate["flags"] as JObject??new JObject()){bool actual=flag.Key=="is_on_fire"?((int?)mob["fireTicks"]??0)>0:flag.Key=="is_baby"?(bool?)mob["baby"]==true:false;if(flag.Key!="is_on_fire"&&flag.Key!="is_baby"||actual!=(bool)flag.Value)return false;}
                    if(predicate["nbt"]!=null&&!Nbt((string)predicate["nbt"],mob))return false;
                    return !predicate.Properties().Any(p=>p.Name!="flags"&&p.Name!="nbt");
                case "damage_source_properties":return false;
                case "survives_explosion":return mob["_explosion"]==null||random.NextDouble()<1/(double)mob["_explosion"];
                case "match_tool":
                    var tool=mob["_tool"]==null?new NativeStack():NativeStack.Read(mob["_tool"]);var match=value["predicate"] as JObject??new JObject();
                    if(match["items"] is JArray items&&!items.Values<string>().Contains(tool.Id)||match["tag"]!=null&&!NativeData.Ingredient("#"+(string)match["tag"],tool.Id))return false;
                    foreach(JObject enchant in match["enchantments"] as JArray??new JArray()){int level=NativeData.Enchantment(tool,((string)enchant["enchantment"]).Split(':').Last());var range=enchant["levels"];if(range?.Type==JTokenType.Integer?level!=(int)range:level<((int?)range?["min"]??0)||level>((int?)range?["max"]??int.MaxValue))return false;}return true;
                case "block_state_property":
                    if((string)value["block"]!=(string)mob["_block"]?["id"])return false;foreach(var prop in value["properties"] as JObject??new JObject()){var actual=mob["_block"]?[prop.Key];if(actual==null){if(prop.Key=="half")actual=new JValue("lower");else if(prop.Key=="type")actual=new JValue("bottom");else actual=new JValue("0");}if(prop.Value is JObject bounds){double number; if(!double.TryParse(actual.ToString(),out number)||number<((double?)bounds["min"]??double.MinValue)||number>((double?)bounds["max"]??double.MaxValue))return false;}else if(actual.ToString().ToLowerInvariant()!=prop.Value.ToString().ToLowerInvariant())return false;}return true;
                case "table_bonus":
                    var chances=value["chances"] as JArray;if(chances==null||chances.Count==0)return false;int fortune=mob["_tool"]==null?0:NativeData.Enchantment(NativeStack.Read(mob["_tool"]),((string)value["enchantment"]).Split(':').Last());return random.NextDouble()<(double)chances[Math.Min(fortune,chances.Count-1)];
                default:return false;
            }
        }
        private static bool Conditions(JObject value,JObject mob,int looting,bool player,Random random)=>(value["conditions"] as JArray??new JArray()).OfType<JObject>().All(c=>Condition(c,mob,looting,player,random));
        internal static List<NativeStack> Roll(JObject mob,int looting,bool player,Random random) {
            string kind=(string)mob["kind"]??"",table="minecraft:entities/"+kind.Split(':').Last();
            if(kind=="minecraft:sheep"&&(bool?)mob["sheared"]!=true){string[] colors={"white","orange","magenta","light_blue","yellow","lime","pink","gray","light_gray","cyan","purple","blue","brown","green","red","black"};int color=Math.Max(0,Math.Min(15,(int?)mob["woolColorId"]??0));table+="/"+colors[color];}
            if(!tables.ContainsKey(table))return null;
            var drops=new List<NativeStack>();Table(table,mob,Math.Min(10,looting),player,random,drops,0);return drops;
        }
        internal static List<NativeStack> RollBlock(JObject block,NativeStack tool,Random random,double explosion=0) {
            string table="minecraft:blocks/"+((string)block["id"]).Split(':').Last();if(!tables.ContainsKey(table))return null;var context=new JObject{["_block"]=block.DeepClone(),["_tool"]=tool.Json()};if(explosion>0)context["_explosion"]=explosion;var drops=new List<NativeStack>();Table(table,context,0,true,random,drops,0);return drops;
        }
        private static void Table(string name,JObject mob,int looting,bool player,Random random,List<NativeStack> drops,int depth) {
            if(depth>12||!tables.TryGetValue(name,out var table))return;
            foreach(JObject pool in table["pools"] as JArray??new JArray()) {
                if(!Conditions(pool,mob,looting,player,random))continue;int rolls=pool["rolls"]==null?1:Integer(pool["rolls"],random);
                for(int i=0;i<Math.Min(64,rolls);i++) {
                    var entries=(pool["entries"] as JArray??new JArray()).OfType<JObject>().Where(e=>Conditions(e,mob,looting,player,random)).ToArray();int total=entries.Sum(e=>Math.Max(0,(int?)e["weight"]??1));if(total==0)continue;int choice=random.Next(total);
                    foreach(var entry in entries){choice-=Math.Max(0,(int?)entry["weight"]??1);if(choice>=0)continue;Entry(entry,mob,looting,player,random,drops,depth);break;}
                }
            }
        }
        private static void Entry(JObject entry,JObject mob,int looting,bool player,Random random,List<NativeStack> drops,int depth) {
            string type=((string)entry["type"]??"").Split(':').Last();
            if(type=="loot_table"){Table((string)entry["name"],mob,looting,player,random,drops,depth+1);return;}
            if(type=="alternatives"||type=="group"||type=="sequence"){foreach(JObject child in entry["children"] as JArray??new JArray()){bool pass=Conditions(child,mob,looting,player,random);if(pass)Entry(child,mob,looting,player,random,drops,depth+1);if(type=="alternatives"&&pass||type=="sequence"&&!pass)break;}return;}
            if(type!="item")return;var item=NativeStack.New((string)entry["name"]);if(item.Empty)return;
            foreach(JObject function in entry["functions"] as JArray??new JArray())if(Conditions(function,mob,looting,player,random)) {
                string name=((string)function["function"]??"").Split(':').Last();
                if(name=="set_count") {int count=function["count"]==null?1:Integer(function["count"],random);item.Count=(bool?)function["add"]==true?item.Count+count:count;}
                else if(name=="looting_enchant")item.Count+=(int)Math.Round(Number(function["count"],random)*looting);
                else if(name=="furnace_smelt"){var cooked=NativeRecipes.Cook(item.Id);if(cooked!=null)item.Id=cooked.Output;}
                else if(name=="set_potion")item.Data["Potion"]=(string)function["id"];
                else if(name=="copy_name"&&mob["name"]!=null)item.Data["display"]=new JObject{["Name"]=mob["name"].DeepClone()};
                else if(name=="apply_bonus"&&mob["_tool"]!=null){int level=NativeData.Enchantment(NativeStack.Read(mob["_tool"]),((string)function["enchantment"]).Split(':').Last());string formula=((string)function["formula"]??"").Split(':').Last();if(formula=="ore_drops"){int bonus=Math.Max(0,random.Next(level+2)-1);item.Count*=bonus+1;}else if(formula=="uniform_bonus_count")item.Count+=random.Next(level*((int?)function["parameters"]?["bonusMultiplier"]??1)+1);else if(formula=="binomial_with_bonus_count")for(int n=0;n<level+((int?)function["parameters"]?["extra"]??0);n++)if(random.NextDouble()<((double?)function["parameters"]?["probability"]??0))item.Count++;}
                else if(name=="explosion_decay"&&mob["_explosion"]!=null){int survived=0;for(int n=0;n<item.Count;n++)if(random.NextDouble()<1/(double)mob["_explosion"])survived++;item.Count=survived;}
                else if(name=="copy_state"&&mob["_block"] is JObject block){var state=new JObject();foreach(string property in (function["properties"] as JArray??new JArray()).Values<string>())if(block[property]!=null)state[property]=block[property].ToString();if(state.Count>0)item.Data["BlockStateTag"]=state;}
                else if(name=="limit_count"){var limit=function["limit"];item.Count=limit is JObject?Math.Max((int?)limit["min"]??0,Math.Min((int?)limit["max"]??int.MaxValue,item.Count)):(int?)limit??item.Count;}
            }
            while(item.Count>0&&drops.Count<128){int count=Math.Min(item.Definition.Maximum,item.Count);drops.Add(item.Copy(count));item.Count-=count;}
        }
    }
}
