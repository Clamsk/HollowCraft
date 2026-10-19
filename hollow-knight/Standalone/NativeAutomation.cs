using System;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace HollowCraft.Standalone
{
    internal sealed partial class NativeGame
    {
        private static int StorageSize(string id)=>id=="minecraft:crafting_table"?9:id.Contains("furnace")||id=="minecraft:smoker"||id.EndsWith("anvil",StringComparison.Ordinal)||id=="minecraft:grindstone"?3:id=="minecraft:enchanting_table"||id=="minecraft:stonecutter"?2:id=="minecraft:smithing_table"?4:id=="minecraft:chest"||id=="minecraft:barrel"||id=="minecraft:ender_chest"||id.EndsWith("shulker_box",StringComparison.Ordinal)?27:id=="minecraft:hopper"||id=="minecraft:brewing_stand"?5:id=="minecraft:dispenser"||id=="minecraft:dropper"?9:0;
        private NativeContainer StorageAt(int bx,int by) {
            string key=NativeRoom.Key(bx,by);if(Room.Containers.TryGetValue(key,out var box)){if(box.Id=="minecraft:ender_chest")box.Slots=enderSlots;return box;}var block=BlockAt(bx,by);if(block==null)return null;string id=(string)block["id"];int count=StorageSize(id);if(count==0)return null;Room.Containers[key]=box=new NativeContainer(id,count);if(id=="minecraft:ender_chest")box.Slots=enderSlots;return box;
        }
        private static bool MachineInsert(NativeContainer box,NativeStack item,int dx,int dy) {
            if(box==null||item.Empty||box.Id=="minecraft:ender_chest"||box.Id=="minecraft:crafting_table"||box.Id=="minecraft:enchanting_table"||box.Id=="minecraft:stonecutter"||box.Id.EndsWith("anvil",StringComparison.Ordinal))return false;
            var order=Enumerable.Range(0,box.Slots.Length);
            if(box.Id.Contains("furnace")||box.Id=="minecraft:smoker")order=dy<0?new[]{0}:new[]{1};
            if(box.Id=="minecraft:brewing_stand")order=dy<0?new[]{3}:item.Id=="minecraft:blaze_powder"?new[]{4}:new[]{0,1,2};
            foreach(int i in order){if((box.Id.Contains("furnace")||box.Id=="minecraft:smoker")&&(i==0?NativeRecipes.Cook(item.Id,box.Id)==null:NativeRecipes.Fuel(item)==0))continue;if(box.Id=="minecraft:brewing_stand"&&(i==3?!NativeData.BrewIngredient(item.Id):i<3?item.Id!="minecraft:glass_bottle"&&item.Id!="minecraft:potion"&&item.Id!="minecraft:splash_potion"&&item.Id!="minecraft:lingering_potion":item.Id!="minecraft:blaze_powder"))continue;var at=box.Slots[i];int limit=box.Id=="minecraft:brewing_stand"&&i<3?1:item.Definition.Maximum;if(!at.Empty&&(!at.Matches(item)||at.Count>=limit))continue;if(at.Empty)box.Slots[i]=item.Copy(1);else at.Count++;item.Count--;item.Normalize();return true;}return false;
        }
        private void TickHoppers() {
            foreach(var hopper in Room.Blocks.Values.Where(c=>(string)c["id"]=="minecraft:hopper").ToArray()) {
                int bx=(int)hopper["x"],by=(int)hopper["y"];if(DirectPower(bx,by)>0)continue;
                var box=StorageAt(bx,by);var d=hopper["dx"]==null?new[]{0,-1}:Facing(hopper);var destination=StorageAt(bx+d[0],by+d[1]);
                foreach(var item in box.Slots)if(!item.Empty&&MachineInsert(destination,item,d[0],d[1]))break;
                var above=StorageAt(bx,by+1);if(above!=null&&above.Id!="minecraft:ender_chest"){
                    var inputs=above.Id.Contains("furnace")||above.Id=="minecraft:smoker"?new[]{2}:Enumerable.Range(0,above.Slots.Length);foreach(int i in inputs)if(!above.Slots[i].Empty&&MachineInsert(box,above.Slots[i],0,-1))break;
                }else foreach(var drop in Room.Drops.ToArray())if(Math.Abs(Number(drop,"x")-bx-.5)<.7&&Number(drop,"y")>=by&&Number(drop,"y")<by+2){var item=NativeStack.Read(drop);if(MachineInsert(box,item,0,-1)){if(item.Empty)Room.Drops.Remove(drop);else drop["count"]=item.Count;break;}}
            }
        }
        private void Dispense(JObject block) {
            int bx=(int)block["x"],by=(int)block["y"];var d=Facing(block);int tx=bx+d[0],ty=by+d[1];var box=StorageAt(bx,by);if(box==null)return;
            int[] occupied=Enumerable.Range(0,box.Slots.Length).Where(i=>!box.Slots[i].Empty).ToArray();if(occupied.Length==0)return;int slot=occupied[random.Next(occupied.Length)];var item=box.Slots[slot];
            if((string)block["id"]=="minecraft:dropper"&&MachineInsert(StorageAt(tx,ty),item,d[0],d[1]))return;
            if((string)block["id"]=="minecraft:dispenser") {
                if(item.Id=="minecraft:water_bucket"||item.Id=="minecraft:lava_bucket") {if(!FluidPassable(tx,ty,item.Id=="minecraft:water_bucket"?"minecraft:water":"minecraft:lava"))return;Room.Blocks[NativeRoom.Key(tx,ty)]=NativeRoom.Cell(tx,ty,item.Id=="minecraft:water_bucket"?"water":"lava");box.Slots[slot]=NativeStack.New("bucket");return;}
                if(item.Id=="minecraft:bucket") {var fluid=BlockAt(tx,ty);if(fluid==null||!Liquid((string)fluid["id"])||!LiquidSource(fluid))return;string filled=(string)fluid["id"]=="minecraft:water"?"water_bucket":"lava_bucket";Room.Blocks.Remove(NativeRoom.Key(tx,ty));item.Count--;item.Normalize();var result=NativeStack.New(filled);if(item.Empty)box.Slots[slot]=result;else {var empty=Array.FindIndex(box.Slots,s=>s.Empty);if(empty>=0)box.Slots[empty]=result;else DropAt(result,tx+.5,ty+.5);}return;}
                if(item.Id=="minecraft:flint_and_steel"){var tnt=BlockAt(tx,ty);if((string)tnt?["id"]=="minecraft:tnt"){Room.Blocks.Remove(NativeRoom.Key(tx,ty));Prime(tx+.5,ty);Inventory.Wear(item,1,false);}return;}
                if(item.Id=="minecraft:tnt"){Prime(tx+.5,ty);item.Count--;item.Normalize();return;}
                if(new[]{"minecraft:arrow","minecraft:tipped_arrow","minecraft:spectral_arrow","minecraft:snowball","minecraft:egg","minecraft:splash_potion","minecraft:lingering_potion"}.Contains(item.Id)) {
                    int id=++serial;bool arrow=item.Id.Contains("arrow");projectiles[id]=new JObject{["entity"]=id,["uuid"]=Guid.NewGuid().ToString("N"),["kind"]=arrow?"minecraft:arrow":item.Id,["item"]=item.Id,["stack"]=item.Copy(1).Json(),["hostile"]=true,["mobKind"]=arrow?"arrow":item.Id.Contains("potion")?"potion":item.Id.Split(':').Last(),["owner"]=0,["x"]=tx+.5,["y"]=ty+.5,["vx"]=d[0]*1.1,["vy"]=d[1]*1.1+.05,["damage"]=arrow?3:0,["age"]=0,["stepRequired"]=true,["grounded"]=false,["noClip"]=false};item.Count--;item.Normalize();return;
                }
            }
            DropAt(item.Copy(1),tx+.5,ty+.5);item.Count--;item.Normalize();
        }
        private void InitializeBlockState(JObject cell,JObject packet) {
            double aimX=Number(packet,"aimX",(int?)lastVisual?["viewYaw"]>0?-1:1),aimY=Number(packet,"aimY");bool vertical=Math.Abs(aimY)>Math.Abs(aimX);
            cell["dx"]=vertical?0:aimX<0?-1:1;cell["dy"]=vertical?aimY<0?-1:1:0;
            if((string)cell["id"]=="minecraft:hopper"){cell["dx"]=0;cell["dy"]=-1;}
        }
    }
}
