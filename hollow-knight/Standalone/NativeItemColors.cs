using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace HollowCraft.Standalone
{
    internal static class NativeItemColors
    {
        private static readonly Dictionary<string,Texture2D> icons=new Dictionary<string,Texture2D>();
        private static Color RGB(int value)=>new Color(((value>>16)&255)/255f,((value>>8)&255)/255f,(value&255)/255f,1);
        internal static Color Tint(JToken item,string texture) {
            string id=(string)item?["id"]??"minecraft:air";var nbt=item?["nbt"];
            if(id.EndsWith("_spawn_egg",StringComparison.Ordinal)){var definition=NativeCatalog.Get(id);return RGB(texture.Contains("overlay")?definition.EggSpots:definition.EggBase);}
            if(id.Contains("potion")||id=="minecraft:tipped_arrow"){
                if(!texture.Contains("overlay")&&!texture.Contains("tipped_arrow_head"))return Color.white;int color=(int?)nbt?["CustomPotionColor"]??(NativeData.Potions.TryGetValue((string)nbt?["Potion"]??"minecraft:water",out var potion)?(int?)potion["color"]??3694022:3694022);return RGB(color);
            }
            if(id.StartsWith("minecraft:leather_",StringComparison.Ordinal)&&!texture.Contains("overlay"))return RGB((int?)nbt?["display"]?["color"]??10511680);return Color.white;
        }
        internal static Texture2D Icon(JToken item) {
            string id=(string)item?["id"];if(string.IsNullOrEmpty(id))return null;bool egg=id.EndsWith("_spawn_egg",StringComparison.Ordinal),potion=id.EndsWith("potion",StringComparison.Ordinal),arrow=id=="minecraft:tipped_arrow",leather=id.StartsWith("minecraft:leather_",StringComparison.Ordinal);if(!egg&&!potion&&!arrow&&!leather)return null;
            string key=id+"|"+item["nbt"];if(icons.TryGetValue(key,out var cached))return cached;string name=id.Substring(id.IndexOf(':')+1);string bottom=egg?"spawn_egg":potion?"potion_overlay":arrow?"tipped_arrow_head":name,top=egg?"spawn_egg_overlay":potion?name:arrow?"tipped_arrow_base":name+"_overlay";
            Texture2D first=NativeAssets.Texture("minecraft:"+bottom),second=NativeAssets.Texture("minecraft:"+top);if(first==null||second==null)return null;int size=first.width;var pixels=new Color[size*size];Color colorA=Tint(item,bottom),colorB=Tint(item,top);
            for(int y=0;y<size;y++)for(int x=0;x<size;x++){Color a=first.GetPixel(x,y)*colorA,b=second.GetPixel(x*second.width/size,y*second.height/size)*colorB;float alpha=b.a+a.a*(1-b.a);pixels[y*size+x]=alpha<=0?Color.clear:new Color((b.r*b.a+a.r*a.a*(1-b.a))/alpha,(b.g*b.a+a.g*a.a*(1-b.a))/alpha,(b.b*b.a+a.b*a.a*(1-b.a))/alpha,alpha);}
            var output=new Texture2D(size,size,TextureFormat.RGBA32,false){filterMode=FilterMode.Point};output.SetPixels(pixels);output.Apply(false,false);icons[key]=output;return output;
        }
    }
}
