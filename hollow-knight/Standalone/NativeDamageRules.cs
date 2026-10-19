namespace HollowCraft.Standalone
{
    internal static class NativeDamageRules
    {
        internal static bool Knockback(string kind) {
            switch(kind) {
                case "lava":case "onFire":case "inFire":case "hotFloor":case "magic":case "poison":case "wither":case "drown":case "fall":case "pearl":case "starve":return false;
                default:return true;
            }
        }
        internal static bool ReplacesFluid(string id)=>id=="minecraft:water"||id=="minecraft:lava";
    }
}
