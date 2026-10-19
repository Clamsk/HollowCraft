using System;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace HollowCraft.Standalone
{
    internal sealed partial class NativeGame
    {
        private bool DeployLandingWater() {
            int bx=(int)Math.Floor(x),by=(int)Math.Floor(y+.03);
            for(int dy=0;dy<=1;dy++) {
                int ty=by+dy;string key=NativeRoom.Key(bx,ty);var cell=BlockAt(bx,ty);
                if(WaterCell(cell))return true;
                if(cell!=null&&((string)cell["id"]).EndsWith("_slab",StringComparison.Ordinal)&&(bool?)cell["double"]!=true){cell["waterlogged"]=true;cell["autoWaterlogged"]=true;cell["autoReturnAt"]=tick+settings.WaterReturnTicks;return true;}
                if(cell!=null||TerrainFull(bx,ty))continue;
                var water=NativeRoom.Cell(bx,ty,"water");water["autoReturnAt"]=tick+settings.WaterReturnTicks;Room.Blocks[key]=water;landingWater=water;collectWater=tick+settings.WaterReturnTicks;return true;
            }return false;
        }
        private void CollectLandingWater() {
            bool collected=false;
            foreach(var cell in Room.Blocks.Values.Where(c=>c["autoReturnAt"]!=null&&(int)c["autoReturnAt"]<=tick).ToArray()) {
                if((bool?)cell["autoWaterlogged"]==true){cell["waterlogged"]=false;cell.Remove("autoWaterlogged");cell.Remove("autoReturnAt");collected=true;}
                else if((string)cell["id"]=="minecraft:water"){Room.Blocks.Remove(NativeRoom.Key((int)cell["x"],(int)cell["y"]));collected=true;}
                else cell.Remove("autoReturnAt");
            }
            if(collected){landingWater=null;Emit(new JObject{["type"]="waterCollected",["recovered"]=true,["hand"]=0});}
        }
    }
}
