using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;

namespace HollowCraft.Standalone
{
    internal sealed class NativeMobDefinition
    {
        internal string Id,Texture,Motion,Disposition,Ranged;internal JObject Model;internal JArray Layers;
    }
    internal static class NativeMobCatalog
    {
        internal static readonly Dictionary<string,NativeMobDefinition> Types=new Dictionary<string,NativeMobDefinition>();
        internal static readonly Dictionary<string,NativeMobDefinition> Boats=new Dictionary<string,NativeMobDefinition>();
        private static readonly HashSet<string> fallback=new HashSet<string>{"minecraft:zombie","minecraft:skeleton","minecraft:creeper","minecraft:spider","minecraft:pig","minecraft:cow","minecraft:sheep","minecraft:chicken","minecraft:villager"};
        internal static bool Supported(string kind)=>Types.ContainsKey(kind)||fallback.Contains(kind);
        internal static NativeMobDefinition Find(string kind)=>Types.TryGetValue(kind,out var value)||Boats.TryGetValue(kind,out value)?value:null;
        internal static void Load(string root) {
            string file=Path.Combine(root,"native-creature-models.json");if(!File.Exists(file))return;
            var source=JObject.Parse(File.ReadAllText(file));if((int?)source["schema"]!=1)throw new InvalidDataException("Unsupported creature model descriptions");
            Types.Clear();foreach(JObject row in source["types"] as JArray??new JArray()) {
                string id=(string)row["id"];Types[id]=new NativeMobDefinition{Id=id,Texture=(string)row["texture"],Motion=(string)row["motion"],Disposition=(string)row["disposition"],Ranged=(string)row["ranged"],Model=(JObject)row["model"],Layers=row["layers"] as JArray??new JArray()};
            }
            Boats.Clear();foreach(JObject row in source["boats"] as JArray??new JArray()){string id=(string)row["id"];Boats[id]=new NativeMobDefinition{Id=id,Texture=(string)row["texture"],Motion="boat",Disposition="passive",Model=(JObject)row["model"],Layers=new JArray()};}
        }
    }
}
