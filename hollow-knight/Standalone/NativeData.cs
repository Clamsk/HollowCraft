using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace HollowCraft.Standalone
{
    // Static data only. These files are prepared at install time; no game jar is
    // loaded by the mod and no external process is used during play.
    internal static class NativeData
    {
        internal static readonly Dictionary<string,JObject> Potions=new Dictionary<string,JObject>(),Entities=new Dictionary<string,JObject>(),Enchantments=new Dictionary<string,JObject>();
        internal static readonly Dictionary<string,HashSet<string>> Tags=new Dictionary<string,HashSet<string>>();
        internal static JArray CreativeStacks=new JArray(),CreativeGroups=new JArray();
        internal static JObject Sounds=new JObject();
        private static readonly Dictionary<string,JObject> brewing=new Dictionary<string,JObject>();
        private static readonly HashSet<string> brewIngredients=new HashSet<string>();
        internal static bool BrewIngredient(string id)=>brewIngredients.Contains(id);
        internal static JObject Brew(NativeStack bottle,NativeStack reagent) {if(bottle.Empty||reagent.Empty)return null;return brewing.TryGetValue(bottle.Id+"|"+((string)bottle.Data["Potion"]??"minecraft:water")+"|"+reagent.Id,out var value)?value:null;}
        private static string loaded;
        internal static void Load(string root) {
            if(string.IsNullOrEmpty(root)||loaded==root)return;
            string registry=Path.Combine(root,"native-registry.json");if(!File.Exists(registry))return;
            var data=JObject.Parse(File.ReadAllText(registry));if((string)data["minecraft"]!="1.20.1")throw new InvalidDataException("Unsupported native registry version");
            NativeCatalog.Import(data);
            NativeMobCatalog.Load(root);
            CreativeStacks=data["creativeStacks"] as JArray??new JArray();CreativeGroups=data["creativeGroups"] as JArray??new JArray();
            string audio=Path.Combine(root,"native-audio-v4.json");Sounds=File.Exists(audio)?JObject.Parse(File.ReadAllText(audio))["sounds"] as JObject??new JObject():new JObject();
            brewing.Clear();brewIngredients.Clear();foreach(JObject recipe in data["brewing"] as JArray??new JArray()){brewing[(string)recipe["item"]+"|"+(string)recipe["potion"]+"|"+(string)recipe["ingredient"]]=recipe;brewIngredients.Add((string)recipe["ingredient"]);}
            foreach(var pair in new[]{Tuple.Create("potions",Potions),Tuple.Create("entities",Entities),Tuple.Create("enchantments",Enchantments)}){pair.Item2.Clear();foreach(JObject entry in (JArray)data[pair.Item1])pair.Item2[(string)entry["id"]]=entry;}
            Tags.Clear();string tagRoot=Path.Combine(root,"vanilla","tags","items");var raw=new Dictionary<string,JArray>();
            if(Directory.Exists(tagRoot))foreach(string file in Directory.GetFiles(tagRoot,"*.json",SearchOption.AllDirectories)){string id="minecraft:"+file.Substring(tagRoot.Length+1).Replace('\\','/');raw[id.Substring(0,id.Length-5)]=(JArray)JObject.Parse(File.ReadAllText(file))["values"];}
            foreach(string id in raw.Keys)ResolveTag(id,raw,new HashSet<string>());
            NativeRecipes.Load(root);loaded=root;
            NativeLoot.Load(root);NativeTradeData.Load(root);
        }
        private static HashSet<string> ResolveTag(string id,Dictionary<string,JArray> raw,HashSet<string> visiting) {
            if(Tags.TryGetValue(id,out var cached))return cached;var result=new HashSet<string>();if(!visiting.Add(id))return result;
            if(raw.TryGetValue(id,out var values))foreach(var value in values){string key=value.Type==JTokenType.String?(string)value:(string)value["id"];if(string.IsNullOrEmpty(key))continue;if(key[0]=='#')result.UnionWith(ResolveTag(NativeCatalog.Id(key.Substring(1)),raw,visiting));else result.Add(NativeCatalog.Id(key));}
            visiting.Remove(id);Tags[id]=result;return result;
        }
        internal static bool Ingredient(string ingredient,string id) {
            if(!ingredient.StartsWith("#",StringComparison.Ordinal))return NativeCatalog.Id(ingredient)==id;
            if(Tags.TryGetValue(NativeCatalog.Id(ingredient.Substring(1)),out var values))return values.Contains(id);
            return ingredient=="#planks"?id.EndsWith("_planks",StringComparison.Ordinal):ingredient=="#logs"&&id.EndsWith("_log",StringComparison.Ordinal);
        }
        internal static int Enchantment(NativeStack item,string id) {
            if(item==null||item.Empty)return 0;int result=(int?)item.Data[id]??0;
            foreach(JObject entry in item.Data["Enchantments"] as JArray??new JArray())if(NativeCatalog.Id((string)entry["id"])==NativeCatalog.Id(id))result=Math.Max(result,(int?)entry["lvl"]??0);
            return Math.Max(0,Math.Min(255,result));
        }
        private static readonly string[] effectIds={"","speed","slowness","haste","mining_fatigue","strength","instant_health","instant_damage","jump_boost","nausea","regeneration","resistance","fire_resistance","water_breathing","invisibility","blindness","night_vision","hunger","weakness","poison","wither","health_boost","absorption","saturation","glowing","levitation","luck","unluck","slow_falling","conduit_power","dolphins_grace","bad_omen","hero_of_the_village","darkness"};
        internal static string EffectName(JToken id) {if(id==null)return "";if(id.Type==JTokenType.Integer){int index=(int)id;return index>0&&index<effectIds.Length?effectIds[index]:"";}return ((string)id).Split(':').Last();}
        internal static JArray PotionEffects(NativeStack item) {
            var list=new JArray();if(Potions.TryGetValue((string)item.Data["Potion"]??"minecraft:water",out var potion))foreach(var effect in potion["effects"] as JArray??new JArray())list.Add(effect.DeepClone());
            foreach(JObject custom in item.Data["CustomPotionEffects"] as JArray??new JArray()){string id=EffectName(custom["Id"]??custom["id"]);if(id.Length==0)continue;list.Add(new JObject{["id"]="minecraft:"+id,["amplifier"]=Math.Max(0,Math.Min(8,(int?)custom["Amplifier"]??(int?)custom["amplifier"]??0)),["ticks"]=Math.Max(1,Math.Min(1200000,(int?)custom["Duration"]??(int?)custom["duration"]??1))});}return list;
        }
    }
}
