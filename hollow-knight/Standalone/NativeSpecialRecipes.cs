using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace HollowCraft.Standalone
{
    internal static partial class NativeRecipes
    {
        private static readonly string[] dyeNames={"white","orange","magenta","light_blue","yellow","lime","pink","gray","light_gray","cyan","purple","blue","brown","green","red","black"};
        private static readonly int[] dyeRgb={16383998,16351261,13061821,3847130,16701501,8439583,15961002,4673362,10329495,1481884,8991416,3949738,8606770,6192150,11546150,1908001};
        private static int Dye(NativeStack item)=>Array.IndexOf(dyeNames,item.Id.StartsWith("minecraft:",StringComparison.Ordinal)&&item.Id.EndsWith("_dye",StringComparison.Ordinal)?item.Id.Substring(10,item.Id.Length-14):"");
        private static bool IsBanner(NativeStack item)=>item.Id.EndsWith("_banner",StringComparison.Ordinal);
        private static NativeStack From(string id,NativeStack source,int count=1){var copy=source.Copy(count);copy.Id=NativeCatalog.Id(id);return copy;}
        internal static NativeStack Special(NativeStack[] grid,int width) {
            var items=grid.Where(s=>!s.Empty).ToArray();if(items.Length==0)return null;var dyes=items.Where(s=>Dye(s)>=0).ToArray();
            if(items.Length==2&&items[0].Id==items[1].Id&&items.All(s=>s.Count==1)&&items[0].Definition.Durability>0){var a=items[0];var result=NativeStack.New(a.Id);result.Damage=Math.Max(0,a.Damage+items[1].Damage-a.Definition.Durability-(int)(a.Definition.Durability*.05));var curses=items.SelectMany(s=>s.Data["Enchantments"] as JArray??new JArray()).OfType<JObject>().Where(e=>NativeData.Enchantments.TryGetValue(NativeCatalog.Id((string)e["id"]),out var data)&&(bool?)data["curse"]==true).GroupBy(e=>(string)e["id"]).Select(g=>new JObject{["id"]=g.Key,["lvl"]=g.Max(e=>(int?)e["lvl"]??1)});var list=new JArray(curses);if(list.Count>0)result.Data["Enchantments"]=list;return result;}
            if(dyes.Length>0&&items.Length==dyes.Length+1) {
                var basis=items.First(i=>Dye(i)<0);
                if(basis.Id.StartsWith("minecraft:leather_",StringComparison.Ordinal)&&basis.Definition.Armor>0) {
                    var colors=dyes.Select(d=>dyeRgb[Dye(d)]).ToList();if(basis.Data["display"]?["color"]!=null)colors.Add((int)basis.Data["display"]["color"]);double r=colors.Average(c=>(c>>16)&255),g=colors.Average(c=>(c>>8)&255),b=colors.Average(c=>c&255),brightness=colors.Average(c=>Math.Max((c>>16)&255,Math.Max((c>>8)&255,c&255))),max=Math.Max(r,Math.Max(g,b));if(max>0){r*=brightness/max;g*=brightness/max;b*=brightness/max;}var result=basis.Copy(1);var display=result.Data["display"] as JObject??new JObject();display["color"]=((int)r<<16)|((int)g<<8)|(int)b;result.Data["display"]=display;return result;
                }
                if(basis.Id.EndsWith("shulker_box",StringComparison.Ordinal)&&dyes.Length==1)return From(dyeNames[Dye(dyes[0])]+"_shulker_box",basis);
                if(basis.Id=="minecraft:firework_star"&&basis.Data["Explosion"] is JObject){var result=basis.Copy(1);result.Data["Explosion"]["FadeColors"]=new JArray(dyes.Select(d=>dyeRgb[Dye(d)]));return result;}
            }
            if(items.Length==2&&items.Any(i=>i.Id=="minecraft:shield")&&items.Any(IsBanner)){var shield=items.First(i=>i.Id=="minecraft:shield");if(shield.Data["BlockEntityTag"]!=null)return null;var banner=items.First(IsBanner);var result=shield.Copy(1);var tag=banner.Data["BlockEntityTag"] as JObject;result.Data["BlockEntityTag"]=tag?.DeepClone()??new JObject();result.Data["BlockEntityTag"]["Base"]=Array.IndexOf(dyeNames,banner.Id.Substring(10,banner.Id.Length-17));return result;}
            if(items.Length==2&&items.All(IsBanner)&&items[0].Id==items[1].Id){var patterned=items.FirstOrDefault(i=>(i.Data["BlockEntityTag"]?["Patterns"] as JArray)?.Count>0);if(patterned!=null&&items.Count(i=>(i.Data["BlockEntityTag"]?["Patterns"] as JArray)?.Count>0)==1)return patterned.Copy(1);}
            var written=items.Where(i=>i.Id=="minecraft:written_book").ToArray();if(written.Length==1&&items.Length>1&&items.All(i=>i.Id=="minecraft:written_book"||i.Id=="minecraft:writable_book")&&((int?)written[0].Data["generation"]??0)<2){var result=written[0].Copy(items.Length-1);result.Data["generation"]=((int?)written[0].Data["generation"]??0)+1;return result;}
            var maps=items.Where(i=>i.Id=="minecraft:filled_map").ToArray();if(maps.Length==1&&items.Length>1&&items.All(i=>i.Id=="minecraft:filled_map"||i.Id=="minecraft:map"))return maps[0].Copy(items.Length);
            if(maps.Length==1&&items.Length==2&&items.Any(i=>i.Id=="minecraft:glass_pane")){var result=maps[0].Copy(1);result.Data["map_to_lock"]=true;return result;}
            if(width==3&&items.Length==9&&grid[4].Id=="minecraft:lingering_potion"&&Enumerable.Range(0,9).Where(i=>i!=4).All(i=>grid[i].Id=="minecraft:arrow"))return From("tipped_arrow",grid[4],8);
            if(items.Any(i=>i.Id=="minecraft:paper")&&items.Count(i=>i.Id=="minecraft:paper")==1&&items.Count(i=>i.Id=="minecraft:gunpowder")>=1&&items.Count(i=>i.Id=="minecraft:gunpowder")<=3&&items.All(i=>i.Id=="minecraft:paper"||i.Id=="minecraft:gunpowder"||i.Id=="minecraft:firework_star")){var result=NativeStack.New("firework_rocket",3);result.Data["Fireworks"]=new JObject{["Flight"]=items.Count(i=>i.Id=="minecraft:gunpowder"),["Explosions"]=new JArray(items.Where(i=>i.Id=="minecraft:firework_star"&&i.Data["Explosion"]!=null).Select(i=>i.Data["Explosion"].DeepClone()))};return result;}
            if(dyes.Length>0&&items.Count(i=>i.Id=="minecraft:gunpowder")==1) {
                var extras=items.Where(i=>Dye(i)<0&&i.Id!="minecraft:gunpowder").ToArray();var shapes=new[]{"minecraft:fire_charge","minecraft:feather","minecraft:gold_nugget","minecraft:skeleton_skull","minecraft:wither_skeleton_skull","minecraft:creeper_head","minecraft:player_head","minecraft:zombie_head","minecraft:dragon_head","minecraft:piglin_head"};
                if(extras.All(i=>i.Id=="minecraft:diamond"||i.Id=="minecraft:glowstone_dust"||shapes.Contains(i.Id))&&extras.Count(i=>shapes.Contains(i.Id))<=1&&extras.Count(i=>i.Id=="minecraft:diamond")<=1&&extras.Count(i=>i.Id=="minecraft:glowstone_dust")<=1){string shape=extras.FirstOrDefault(i=>shapes.Contains(i.Id))?.Id;int type=shape==null?0:shape=="minecraft:fire_charge"?1:shape=="minecraft:gold_nugget"?2:shape=="minecraft:feather"?4:3;var result=NativeStack.New("firework_star");result.Data["Explosion"]=new JObject{["Type"]=type,["Colors"]=new JArray(dyes.Select(d=>dyeRgb[Dye(d)])),["Trail"]=extras.Any(i=>i.Id=="minecraft:diamond"),["Flicker"]=extras.Any(i=>i.Id=="minecraft:glowstone_dust")};return result;}
            }
            if(items.Length==4&&items.Any(i=>i.Id=="minecraft:bowl")&&items.Any(i=>i.Id=="minecraft:red_mushroom")&&items.Any(i=>i.Id=="minecraft:brown_mushroom")) {
                var flower=items.First(i=>i.Id!="minecraft:bowl"&&i.Id!="minecraft:red_mushroom"&&i.Id!="minecraft:brown_mushroom");string name=flower.Id.Substring(10),effect=null;int ticks=0;
                if(name=="dandelion"||name=="blue_orchid"){effect="saturation";ticks=7;}else if(name=="allium"){effect="fire_resistance";ticks=80;}else if(name=="azure_bluet"){effect="blindness";ticks=160;}else if(name.EndsWith("tulip",StringComparison.Ordinal)){effect="weakness";ticks=180;}else if(name=="oxeye_daisy"){effect="regeneration";ticks=160;}else if(name=="cornflower"){effect="jump_boost";ticks=120;}else if(name=="lily_of_the_valley"){effect="poison";ticks=240;}else if(name=="wither_rose"){effect="wither";ticks=160;}
                if(effect!=null){var result=NativeStack.New("suspicious_stew");result.Data["Effects"]=new JArray(new JObject{["EffectId"]="minecraft:"+effect,["EffectDuration"]=ticks});return result;}
            }
            return null;
        }
        internal static NativeStack[] SpecialRemainders(NativeStack[] grid) {
            var remainders=NativeInventory.Empty(grid.Length);for(int i=0;i<grid.Length;i++){var item=grid[i];if(item.Empty)continue;if(item.Id=="minecraft:written_book"||IsBanner(item)&&(item.Data["BlockEntityTag"]?["Patterns"] as JArray)?.Count>0&&grid.Where(s=>!s.Empty).Count()==2&&grid.Where(s=>!s.Empty).All(IsBanner))remainders[i]=item.Copy(1);else if(item.Definition.Remainder!=null)remainders[i]=NativeStack.New(item.Definition.Remainder);}return remainders;
        }
    }
}
