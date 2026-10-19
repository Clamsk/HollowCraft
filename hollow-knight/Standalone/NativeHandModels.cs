using System;

namespace HollowCraft.Standalone
{
    internal static class NativeHandModels
    {
        internal static bool Trident(string id)=>id=="minecraft:trident"||id=="minecraft:trident_throwing";
        internal static string Render(string id,bool active,int ticks,bool charged=false) {
            if(id=="minecraft:trident")return active?"minecraft:trident_throwing":id;
            if(id=="minecraft:bow")return active?"minecraft:bow_pulling_"+Math.Max(0,Math.Min(2,ticks/7)):id;
            if(id=="minecraft:crossbow"){if(charged)return "minecraft:crossbow_arrow";if(active)return "minecraft:crossbow_pulling_"+Math.Max(0,Math.Min(2,ticks/8));}return id;
        }
        internal static float SpearPitch(float aim)=>180+aim;
    }
}
