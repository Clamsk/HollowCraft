using System;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace HollowCraft.Standalone
{
    internal sealed partial class NativeGame
    {
        private bool FluidTouches(JObject actor,string kind) {
            double half=Math.Max(.01,Number(actor,"width",.6)/2),left=Number(actor,"x")-half,right=Number(actor,"x")+half,bottom=Number(actor,"y"),top=bottom+Math.Max(.01,Number(actor,"height",1.8));
            for(int bx=(int)Math.Floor(left+.001);bx<=(int)Math.Floor(right-.001);bx++)for(int by=(int)Math.Floor(bottom+.001);by<=(int)Math.Floor(top-.001);by++)
                if(Room.Blocks.TryGetValue(NativeRoom.Key(bx,by),out var cell)&&LiquidKind(cell)=="minecraft:"+kind&&NativeFluidGeometry.Intersects(Room,terrain,cell,left,bottom,right,top))return true;
            return false;
        }
        private void TickNativeEnvironment() {
            foreach(var enemy in enemies.Values) {
                if(Number(enemy,"health")<=0||(string)enemy["kind"]=="npc"||(string)enemy["kind"]=="resource")continue;
                int fire=Math.Max(0,(int?)enemy["fireTicks"]??0);
                bool resistant=(int?)enemy["nativeEffects"]?["fire_resistance"]?["ticks"]>0;
                if(FluidTouches(enemy,"water"))fire=0;
                else if(FluidTouches(enemy,"lava")) {fire=300;if(!resistant&&tick%10==0)HitEnemy(enemy,4,"lava",0);}
                else if(fire>0) {fire--;if(!resistant&&tick%20==0)HitEnemy(enemy,1,"onFire",0);}
                enemy["fireTicks"]=fire;enemy["onFire"]=fire>0;
            }
        }
    }
}
