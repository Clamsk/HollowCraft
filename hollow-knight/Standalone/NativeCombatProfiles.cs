using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;

namespace HollowCraft.Standalone
{
    internal sealed class NativeCombatProfiles
    {
        private readonly string root;private readonly Dictionary<string,JObject> cache=new Dictionary<string,JObject>();
        internal NativeCombatProfiles(string resourceRoot){root=resourceRoot;}
        private JObject Read(string id,int depth=0) {
            if(root==null||depth>16||id.Contains(".."))return null;id=NativeCatalog.Id(id);if(cache.TryGetValue(id,out var previous))return previous;var parts=id.Split(':');if(parts.Length!=2)return null;
            string path=Path.Combine(root,"bettercombat_profiles",parts[0],parts[1]+".json");if(!File.Exists(path)){cache[id]=null;return null;}
            var json=JObject.Parse(File.ReadAllText(path));var result=Read((string)json["parent"]??"",depth+1)?.DeepClone() as JObject??new JObject();foreach(var value in json["attributes"] as JObject??new JObject())result[value.Key]=value.Value.DeepClone();cache[id]=result;return result;
        }
        internal JObject Weapon(NativeStack item) {
            var data=Read(item.Id);string category=item.Empty?"fist":item.Definition.Tool;data=data??Read("bettercombat:"+(item.Id=="minecraft:trident"?"trident":category.Length>0?category:"fist"));
            if(data?["attacks"] is JArray a&&a.Count>0)return (JObject)data.DeepClone();
            var attacks=new JArray();foreach(string clip in new[]{"one_handed_slash_horizontal_right","one_handed_slash_horizontal_left","one_handed_stab"})attacks.Add(new JObject{["damage_multiplier"]=1,["angle"]=120,["upswing"]=.5,["animation"]="bettercombat:"+clip,["hitbox"]="HORIZONTAL_PLANE"});
            return new JObject{["attack_range"]=3,["attacks"]=attacks};
        }
    }
}
