using Newtonsoft.Json.Linq;
namespace HollowCraft
{
    internal static class GameplayPackets
    {
        internal static JObject Interact(string scene,int x,int y,int placeX,int placeY,bool canPlace,double playerX,double playerY,double aimX,double aimY)=>new JObject{["type"]="interact",["scene"]=scene,["x"]=x,["y"]=y,["placeX"]=placeX,["placeY"]=placeY,["canPlace"]=canPlace,["playerX"]=playerX,["playerY"]=playerY,["aimX"]=aimX,["aimY"]=aimY};
        internal static JObject Use(string scene,int x,int y,bool start,bool release,double aimX,double aimY)=>new JObject{["type"]="itemUse",["scene"]=scene,["start"]=start,["release"]=release,["x"]=x,["y"]=y,["aimX"]=aimX,["aimY"]=aimY};
    }
}
