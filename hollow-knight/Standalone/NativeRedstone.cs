using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace HollowCraft.Standalone
{
    internal sealed partial class NativeGame
    {
        private static bool Wire(JObject c)=>(string)c?["id"]=="minecraft:redstone";
        private static int[] Facing(JObject c)=>new[]{(int?)c["dx"]??1,(int?)c["dy"]??0};
        private JObject BlockAt(int bx,int by)=>Room.Blocks.TryGetValue(NativeRoom.Key(bx,by),out var c)?c:null;
        private bool Conducts(JObject cell) {if(cell==null)return false;string id=(string)cell["id"];if(id=="minecraft:repeater"||id=="minecraft:comparator"||id=="minecraft:observer"||id.Contains("glass")||id.Contains("leaves")||id.Contains("ice")||id=="minecraft:slime_block"||id=="minecraft:honey_block")return false;var boxes=NativeRoom.Shapes(cell);return boxes.Count==1&&(double)boxes[0][0]<=0&&(double)boxes[0][1]<=0&&(double)boxes[0][2]>=1&&(double)boxes[0][3]>=1;}
        private int Emits(JObject c,int bx,int by) {
            if(c==null)return 0;string id=(string)c["id"];int power=(int?)c["power"]??0;var d=Facing(c);int dx=bx-(int)c["x"],dy=by-(int)c["y"];
            if(id=="minecraft:redstone")return power;
            if(id=="minecraft:repeater"||id=="minecraft:comparator")return dx==d[0]&&dy==d[1]?power:0;
            if(id=="minecraft:observer")return dx==-d[0]&&dy==-d[1]&&(bool?)c["powered"]==true?15:0;
            if(id=="minecraft:redstone_torch"||id=="minecraft:redstone_wall_torch")return (bool?)c["lit"]!=false&&dy!=-1?15:0;
            if(id=="minecraft:redstone_block")return 15;
            if(id=="minecraft:lever"||id.EndsWith("_button",StringComparison.Ordinal)||id.EndsWith("pressure_plate",StringComparison.Ordinal)||id=="minecraft:tripwire_hook")return (bool?)c["powered"]==true?Math.Max(1,power==0?15:power):0;
            return 0;
        }
        private int DirectPower(int bx,int by,bool conduct=true,bool wires=true) {
            int power=0;foreach(var d in Neighbors){var c=BlockAt(bx+d[0],by+d[1]);if(wires||!Wire(c))power=Math.Max(power,Emits(c,bx,by));if(conduct&&Conducts(c)&&!Wire(c))foreach(var n in Neighbors)if(d[0]+n[0]!=0||d[1]+n[1]!=0){var input=BlockAt(bx+d[0]+n[0],by+d[1]+n[1]);if(!Wire(input))power=Math.Max(power,Emits(input,bx+d[0],by+d[1]));}}return power;
        }
        private int AnalogPower(int bx,int by,int towardX,int towardY) {
            var c=BlockAt(bx,by);if(c==null)return 0;
            if(Room.Containers.TryGetValue(NativeRoom.Key(bx,by),out var box)){double full=box.Slots.Sum(s=>s.Empty?0:s.Count/(double)Math.Min(64,s.Definition.Maximum))/Math.Max(1,box.Slots.Length);return full<=0?0:1+(int)Math.Floor(full*14);}
            if((string)c["id"]=="minecraft:composter")return Math.Min(8,(int?)c["level"]??0);
            if((string)c["id"]=="minecraft:cauldron")return (int?)c["level"]??0;
            return Math.Max(Emits(c,towardX,towardY),Conducts(c)?DirectPower(bx,by,false):0);
        }
        private int RelaySide(JObject cell,int bx,int by)=>cell!=null&&((string)cell["id"]=="minecraft:repeater"||(string)cell["id"]=="minecraft:comparator")?Emits(cell,bx,by):0;
        private void ScheduleSignal(JObject cell,int desired,int delay) {
            int current=(int?)cell["power"]??0;
            if(current==desired){cell.Remove("pendingPower");cell.Remove("signalDue");return;}
            if((int?)cell["pendingPower"]!=desired){cell["pendingPower"]=desired;cell["signalDue"]=tick+delay;}
            if((int?)cell["signalDue"]<=tick){cell["power"]=desired;cell["powered"]=desired>0;cell["lit"]=desired>0;cell.Remove("pendingPower");cell.Remove("signalDue");}
        }
        private void RedstoneStep() {
            foreach(var head in Room.Blocks.Values.Where(c=>c["pistonHead"]!=null).ToArray())if(!Room.Blocks.TryGetValue((string)head["pistonHead"],out var owner)||(bool?)owner["extended"]!=true)Room.Blocks.Remove(NativeRoom.Key((int)head["x"],(int)head["y"]));
            var cells=Room.Blocks.Values.ToArray();
            foreach(var c in cells){string id=(string)c["id"];if((int?)c["until"]>0&&(int)c["until"]<=tick){c["powered"]=false;c.Remove("until");}
                if(id.EndsWith("pressure_plate",StringComparison.Ordinal)){int bx=(int)c["x"],by=(int)c["y"],count=Room.Creatures.Count(m=>Number(m,"health")>0&&Math.Abs(Number(m,"x")-bx-.5)<.8&&Number(m,"y")>=by-.1&&Number(m,"y")<by+.5)+enemies.Values.Count(m=>Number(m,"health")>0&&Math.Abs(Number(m,"x")-bx-.5)<1&&Math.Abs(Number(m,"y")-by)<1)+(!Spectator&&Math.Abs(x-bx-.5)<.8&&y>=by-.1&&y<by+.5?1:0);if(!id.Contains("stone")&&!id.Contains("polished"))count+=Room.Drops.Count(m=>Math.Abs(Number(m,"x")-bx-.5)<.5&&Math.Abs(Number(m,"y")-by)<.5);c["powered"]=count>0;c["power"]=id=="minecraft:light_weighted_pressure_plate"?Math.Min(15,count):id=="minecraft:heavy_weighted_pressure_plate"?(int)Math.Ceiling(Math.Min(150,count)/10.0):count>0?15:0;}
                if(id=="minecraft:observer"){var d=Facing(c);var watched=BlockAt((int)c["x"]+d[0],(int)c["y"]+d[1]);string signature=watched==null?"air":(string)watched["id"]+"/"+watched["power"]+"/"+watched["open"]+"/"+watched["lit"]+"/"+watched["level"]+"/"+watched["extended"];if(c["observed"]!=null&&(string)c["observed"]!=signature&&c["pulseAt"]==null&&(bool?)c["powered"]!=true)c["pulseAt"]=tick+2;c["observed"]=signature;if((int?)c["pulseAt"]<=tick){c["powered"]=true;c["until"]=tick+2;c.Remove("pulseAt");}}
            }
            // Recompute dust from actual sources, never from its own old power.
            foreach(var c in cells)if(Wire(c))c["power"]=0;
            var queue=new Queue<JObject>();foreach(var c in cells)if(Wire(c)){int power=DirectPower((int)c["x"],(int)c["y"],true,false);if(power>0){c["power"]=power;queue.Enqueue(c);}}
            while(queue.Count>0){var c=queue.Dequeue();int power=(int)c["power"]-1;if(power<=0)continue;foreach(var d in Neighbors){var next=BlockAt((int)c["x"]+d[0],(int)c["y"]+d[1]);if(!Wire(next)||(int?)next["power"]>=power)continue;next["power"]=power;queue.Enqueue(next);}}
            foreach(var c in cells) {
                string id=(string)c["id"];if(c["pistonHead"]!=null)continue;int bx=(int)c["x"],by=(int)c["y"];var d=Facing(c);int power=DirectPower(bx,by);
                if(id=="minecraft:repeater"||id=="minecraft:comparator") {int input=AnalogPower(bx-d[0],by-d[1],bx,by),side=Math.Max(Emits(BlockAt(bx-d[1],by+d[0]),bx,by),Emits(BlockAt(bx+d[1],by-d[0]),bx,by));if(id=="minecraft:repeater"){var rear=BlockAt(bx-d[0],by-d[1]);input=Emits(rear,bx,by);if(Conducts(rear))input=Math.Max(input,DirectPower(bx-d[0],by-d[1],false));side=Math.Max(RelaySide(BlockAt(bx-d[1],by+d[0]),bx,by),RelaySide(BlockAt(bx+d[1],by-d[0]),bx,by));c["locked"]=side>0;if(side==0)ScheduleSignal(c,input>0?15:0,Math.Max(1,(int?)c["delay"]??1)*2);}else ScheduleSignal(c,(bool?)c["subtract"]==true?Math.Max(0,input-side):input>=side?input:0,2);}
                if(id=="minecraft:redstone_torch"||id=="minecraft:redstone_wall_torch") {int desired=DirectPower(bx,by-1,false)>0?0:15;if(c["power"]==null)c["power"]=(bool?)c["lit"]==false?0:15;ScheduleSignal(c,desired,2);}
                if(id=="minecraft:redstone_lamp") {if(power>0){c["lit"]=true;c.Remove("offAt");}else if((bool?)c["lit"]==true){if(c["offAt"]==null)c["offAt"]=tick+4;if((int?)c["offAt"]<=tick)c["lit"]=false;}}
                if(id.EndsWith("_door",StringComparison.Ordinal)||id.EndsWith("_trapdoor",StringComparison.Ordinal)||id.EndsWith("_fence_gate",StringComparison.Ordinal)){if((bool?)c["redstoneOpen"]==true||power>0)c["open"]=power>0;c["redstoneOpen"]=power>0;}
                if(id=="minecraft:tnt"&&power>0){Room.Blocks.Remove(NativeRoom.Key(bx,by));Prime(bx+.5,by);}
                if(id=="minecraft:piston"||id=="minecraft:sticky_piston")TickPiston(c,power>0);
                if(id=="minecraft:dispenser"||id=="minecraft:dropper"){bool was=(bool?)c["triggered"]==true;if(power>0&&!was)c["dispenseAt"]=tick+4;c["triggered"]=power>0;if((int?)c["dispenseAt"]<=tick){c.Remove("dispenseAt");Dispense(c);}}
            }
            if(tick%8==0)TickHoppers();
        }
        private bool PistonMovable(JObject cell) {if(cell==null)return true;string id=(string)cell["id"];return NativeCatalog.Get(id).Hardness>=0&&!new[]{"minecraft:obsidian","minecraft:crying_obsidian","minecraft:ender_chest","minecraft:enchanting_table","minecraft:reinforced_deepslate","minecraft:spawner"}.Contains(id)&&!Room.Containers.ContainsKey(NativeRoom.Key((int)cell["x"],(int)cell["y"]))&&cell["pistonHead"]==null&&(bool?)cell["extended"]!=true;}
        private void TickPiston(JObject c,bool powered) {
            int bx=(int)c["x"],by=(int)c["y"];var d=Facing(c);bool extended=(bool?)c["extended"]==true;if(extended==powered)return;
            string headKey=NativeRoom.Key(bx+d[0],by+d[1]);
            if(!powered){if(Room.Blocks.TryGetValue(headKey,out var head)&&(string)head["pistonHead"]==NativeRoom.Key(bx,by))Room.Blocks.Remove(headKey);c["extended"]=false;if((string)c["id"]=="minecraft:sticky_piston"){string pullKey=NativeRoom.Key(bx+2*d[0],by+2*d[1]);if(Room.Blocks.TryGetValue(pullKey,out var pulled)&&PistonMovable(pulled)&&NativeCatalog.Get((string)pulled["id"]).Solid){Room.Blocks.Remove(pullKey);pulled["x"]=bx+d[0];pulled["y"]=by+d[1];Room.Blocks[headKey]=pulled;}}return;}
            var line=new List<JObject>();int end=1;for(;end<=13;end++){int px=bx+d[0]*end,py=by+d[1]*end;if(terrain.ContainsKey(NativeRoom.Key(px,py)))return;var next=BlockAt(px,py);if(next==null||Liquid((string)next["id"]))break;if(!PistonMovable(next)||line.Count==12)return;line.Add(next);}
            // Commit only after the entire line has been checked.
            for(int i=line.Count-1;i>=0;i--){var moved=line[i];int px=(int)moved["x"],py=(int)moved["y"];Room.Blocks.Remove(NativeRoom.Key(px,py));moved["x"]=px+d[0];moved["y"]=py+d[1];Room.Blocks[NativeRoom.Key(px+d[0],py+d[1])]=moved;}
            var pistonHead=NativeRoom.Cell(bx+d[0],by+d[1],"piston");pistonHead["pistonHead"]=NativeRoom.Key(bx,by);pistonHead["dx"]=d[0];pistonHead["dy"]=d[1];Room.Blocks[headKey]=pistonHead;c["extended"]=true;
            foreach(var mob in Room.Creatures)if(Math.Abs(Number(mob,"x")-(bx+d[0]+.5))<1&&Number(mob,"y")<by+d[1]+1&&Number(mob,"y")+Number(mob,"height",1)>by+d[1]){mob["x"]=Number(mob,"x")+d[0];mob["y"]=Number(mob,"y")+d[1];}
            if(Math.Abs(x-bx-d[0]-.5)<.8&&y<by+d[1]+1&&y+1.8>by+d[1]){x+=d[0];y+=d[1];teleport++;Emit(new JObject{["type"]="teleport",["x"]=x,["y"]=y,["teleport"]=teleport,["normalX"]=d[0],["normalY"]=d[1]});}
        }
    }
}
