using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace HollowCraft.Standalone
{
    internal sealed partial class NativeGame
    {
        private readonly HashSet<int> dragSlots=new HashSet<int>();private int dragMode=-1;
        private NativeStack[] Grid=>container!=null&&container.Id=="minecraft:crafting_table"?container.Slots:Inventory.Craft;
        private int GridWidth=>container!=null&&container.Id=="minecraft:crafting_table"?3:2;
        private bool Crafting=>container==null||container.Id=="minecraft:crafting_table";
        private int SlotLimit(int index,NativeStack item)=>container?.Id=="minecraft:brewing_stand"&&index>=0&&index<3||container?.Id=="minecraft:enchanting_table"&&index==0?1:item.Definition.Maximum;
        private NativeStack Slot(int index) {
            if(container!=null&&index==WorkOutput)return WorkPreview().Stack;
            if(container!=null) {if(Crafting){if(index==0)return NativeRecipes.Result(Grid,3);if(index>=1&&index<=9)return Grid[index-1];}else if(index>=0&&index<container.Slots.Length)return container.Slots[index];int start=Crafting?10:container.Slots.Length;if(index>=start&&index<start+27)return Inventory.Slots[index-start+9];if(index>=start+27&&index<start+36)return Inventory.Slots[index-start-27];return null;}
            if(index==0)return NativeRecipes.Result(Inventory.Craft,2);if(index>=1&&index<=4)return Inventory.Craft[index-1];if(index>=5&&index<=8)return Inventory.Armor[index-5];if(index>=9&&index<=35)return Inventory.Slots[index];if(index>=36&&index<=44)return Inventory.Slots[index-36];return index==45?Inventory.Offhand:null;
        }
        private bool CanInsert(int index,NativeStack item) {
            if(container!=null&&index==WorkOutput)return false;
            if(container?.Id=="minecraft:enchanting_table"&&index<2)return index==0?item.Empty||item.Definition.Enchantability>0:item.Empty||item.Id=="minecraft:lapis_lazuli";
            if(container?.Id=="minecraft:smithing_table"&&index<3){string field=index==0?"template":index==1?"base":"addition";return item.Empty||NativeRecipes.WorkRecipes.Any(r=>((string)r["type"]).StartsWith("minecraft:smithing_",StringComparison.Ordinal)&&NativeRecipes.Accepts(r[field],item));}
            if(container?.Id=="minecraft:stonecutter"&&index==0)return item.Empty||NativeRecipes.WorkRecipes.Any(r=>(string)r["type"]=="minecraft:stonecutting"&&NativeRecipes.Accepts(r["ingredient"],item));
            if(container?.Id=="minecraft:grindstone"&&index<2)return item.Empty||item.Definition.Durability>0||Enchants(item).Count>0;
            if(Crafting&&index==0)return false;if(container==null&&index>=5&&index<=8)return item.Empty||item.Definition.Equipment==NativeInventory.Equipment(index-5);
            if(container!=null&&(container.Id.Contains("furnace")||container.Id=="minecraft:smoker")){if(index==2)return false;if(index==0)return NativeRecipes.Cook(item.Id,container.Id)!=null;if(index==1)return NativeRecipes.Fuel(item)>0;}
            if(container?.Id=="minecraft:brewing_stand"&&index<5){if(item.Empty)return true;if(index==4)return item.Id=="minecraft:blaze_powder";if(index<3)return item.Id=="minecraft:potion"||item.Id=="minecraft:splash_potion"||item.Id=="minecraft:lingering_potion"||item.Id=="minecraft:glass_bottle";return NativeData.BrewIngredient(item.Id);}
            return Slot(index)!=null;
        }
        private void SetSlot(int index,NativeStack value) {
            if(container!=null) {if(Crafting&&index>=1&&index<=9)Grid[index-1]=value;else if(!Crafting&&index>=0&&index<container.Slots.Length)container.Slots[index]=value;else {int start=Crafting?10:container.Slots.Length;if(index>=start&&index<start+27)Inventory.Slots[index-start+9]=value;else if(index>=start+27&&index<start+36)Inventory.Slots[index-start-27]=value;}return;}
            if(index>=1&&index<=4)Inventory.Craft[index-1]=value;else if(index>=5&&index<=8)Inventory.Armor[index-5]=value;else if(index>=9&&index<=35)Inventory.Slots[index]=value;else if(index>=36&&index<=44)Inventory.Slots[index-36]=value;else if(index==45)Inventory.Offhand=value;
        }
        private void SlotClick(JObject packet) {
            if((int?)packet["syncId"]!=(container?.Sync??0))return;
            bool targetContainer=(string)packet["handler"]=="container";if(targetContainer!=(container!=null))return;
            int index=(int?)packet["slot"]??-999,button=(int?)packet["button"]??0;string action=(string)packet["action"]??"PICKUP";
            var at=Slot(index);if(action=="QUICK_CRAFT") {QuickCraft(index,button);return;}
            if(index==-999){if(action=="PICKUP"||action=="THROW")Drop(Inventory.Carried,button==1?1:Inventory.Carried.Count);return;}if(at==null)return;
            if(Crafting&&index==0) {TakeResult(action=="QUICK_MOVE");return;}
            if(container!=null&&index==WorkOutput){if(action=="PICKUP"||action=="QUICK_MOVE")TakeWork(action=="QUICK_MOVE");return;}
            switch(action) {
                case "PICKUP":
                    if(Inventory.Carried.Empty) {if(at.Empty)return;int take=button==1?(at.Count+1)/2:at.Count;Inventory.Carried=at.Copy(take);at.Count-=take;at.Normalize();}
                    else if(CanInsert(index,Inventory.Carried)) {
                        if(at.Empty||at.Matches(Inventory.Carried)){int count=Math.Max(0,Math.Min(button==1?1:Inventory.Carried.Count,SlotLimit(index,Inventory.Carried)-(at.Empty?0:at.Count)));if(count==0)return;if(at.Empty)SetSlot(index,Inventory.Carried.Copy(count));else at.Count+=count;Inventory.Carried.Count-=count;Inventory.Carried.Normalize();}
                        else if(Inventory.Carried.Count<=SlotLimit(index,Inventory.Carried)){SetSlot(index,Inventory.Carried);Inventory.Carried=at;}
                    }
                    break;
                case "SWAP":
                    if(button!=40&&(button<0||button>8))return;var other=button==40?Inventory.Offhand:Inventory.Slots[button];if(!CanInsert(index,other)||!other.Empty&&other.Count>SlotLimit(index,other))return;SetSlot(index,other);if(button==40)Inventory.Offhand=at;else Inventory.Slots[button]=at;break;
                case "THROW":Drop(at,button==0?1:at.Count);break;
                case "CLONE":if(Creative&&!at.Empty)Inventory.Carried=at.Copy(at.Definition.Maximum);break;
                case "QUICK_MOVE":
                    if(at.Empty)return;
                    if(container==null&&!string.IsNullOrEmpty(at.Definition.Equipment)){int slot=Array.IndexOf(new[]{"helmet","chestplate","leggings","boots"},at.Definition.Equipment);if(slot>=0&&Inventory.Armor[slot].Empty){Inventory.Armor[slot]=at.Copy(1);at.Count--;at.Normalize();break;}}
                    if(container!=null) {
                        int start=Crafting?10:container.Slots.Length;
                        if(index<start)Inventory.Insert(at);
                        else if(!Crafting){var order=Enumerable.Range(0,container.Slots.Length);if(container.Id=="minecraft:brewing_stand"&&at.Id=="minecraft:blaze_powder")order=new[]{4,3,0,1,2};foreach(int i in order){if(at.Empty)break;if(CanInsert(i,at)&&NativeInventory.Fit(container.Slots[i],at)){int count=Math.Max(0,Math.Min(at.Count,SlotLimit(i,at)-container.Slots[i].Count));if(container.Slots[i].Empty)container.Slots[i]=at.Copy(count);else container.Slots[i].Count+=count;at.Count-=count;at.Normalize();}}}
                    }else if(index>=36&&index<=44)Inventory.Insert(at,Enumerable.Range(9,27));else if(index>=9&&index<=35)Inventory.Insert(at,Enumerable.Range(0,9));else Inventory.Insert(at);break;
                case "PICKUP_ALL":
                    if(Inventory.Carried.Empty)break;
                    int last=container==null?45:(Crafting?10:container.Slots.Length)+35;
                    for(int i=1;i<=last&&Inventory.Carried.Count<Inventory.Carried.Definition.Maximum;i++){var item=Slot(i);if(item==null||!item.Matches(Inventory.Carried))continue;int count=Math.Min(item.Count,Inventory.Carried.Definition.Maximum-Inventory.Carried.Count);Inventory.Carried.Count+=count;item.Count-=count;item.Normalize();}break;
            }
        }
        private void QuickCraft(int index,int button) {
            int phase=button&3,kind=button>>2;if(kind<0||kind>2||kind==2&&!Creative)return;
            if(phase==0){dragMode=kind;dragSlots.Clear();return;}if(dragMode!=kind||Inventory.Carried.Empty)return;
            if(phase==1){var item=Slot(index);if(item!=null&&CanInsert(index,Inventory.Carried)&&NativeInventory.Fit(item,Inventory.Carried))dragSlots.Add(index);return;}
            if(phase!=2)return;
            int each=kind==0?Inventory.Carried.Count/Math.Max(1,dragSlots.Count):kind==1?1:Inventory.Carried.Definition.Maximum;
            foreach(int slot in dragSlots){var at=Slot(slot);if(at==null||!CanInsert(slot,Inventory.Carried)||!NativeInventory.Fit(at,Inventory.Carried))continue;int count=Math.Max(0,Math.Min(each,SlotLimit(slot,Inventory.Carried)-at.Count));if(kind!=2)count=Math.Min(count,Inventory.Carried.Count);if(count<=0)continue;if(at.Empty)SetSlot(slot,Inventory.Carried.Copy(count));else at.Count+=count;if(kind!=2){Inventory.Carried.Count-=count;Inventory.Carried.Normalize();}}
            dragSlots.Clear();dragMode=-1;
        }
        private void TakeResult(bool quick) {
            for(int i=0;i<(quick?64:1);i++) {
                var result=NativeRecipes.Result(Grid,GridWidth);if(result.Empty)return;
                if(quick){if(Inventory.Capacity(result)<result.Count)return;Inventory.Insert(result.Copy());}
                else {if(!Inventory.Carried.Empty&&(!Inventory.Carried.Matches(result)||Inventory.Carried.Count+result.Count>result.Definition.Maximum))return;if(Inventory.Carried.Empty)Inventory.Carried=result.Copy();else Inventory.Carried.Count+=result.Count;}
                for(int slot=0;slot<Grid.Length;slot++){var item=Grid[slot];if(item.Empty)continue;string remainder=item.Definition.Remainder;item.Count--;item.Normalize();if(remainder!=null){var left=NativeStack.New(remainder);if(item.Empty)Grid[slot]=left;else if(!Inventory.Insert(left))DropAt(left,x,y+.5);}}
            }
        }
        private void CloseContainer() {
            if(!Inventory.Carried.Empty){Inventory.Insert(Inventory.Carried);if(!Inventory.Carried.Empty)Drop(Inventory.Carried,Inventory.Carried.Count);}
            if(container?.Id=="minecraft:crafting_table")foreach(var item in container.Slots){Inventory.Insert(item);if(!item.Empty)Drop(item,item.Count);}
            foreach(var item in Inventory.Craft){Inventory.Insert(item);if(!item.Empty)Drop(item,item.Count);}
            if(container!=null&&(WorkOutput>=0||container.Id=="minecraft:enchanting_table")){int inputs=WorkOutput>=0?WorkOutput:2;for(int i=0;i<inputs;i++){var item=container.Slots[i];Inventory.Insert(item);if(!item.Empty)Drop(item,item.Count);}container.Choice=-1;container.Rename=null;}
            container=null;containerKey=null;dragMode=-1;dragSlots.Clear();syncId++;
        }
        private bool TryInteractBlock(JObject packet) {
            int bx=(int)Number(packet,"x"),by=(int)Number(packet,"y");string key=NativeRoom.Key(bx,by);if(!Room.Blocks.TryGetValue(key,out var cell)||!Reach(bx,by,Creative?5:4.5))return false;
            string id=(string)cell["id"];
            if(id.EndsWith("_door",StringComparison.Ordinal)||id.EndsWith("_trapdoor",StringComparison.Ordinal)||id.EndsWith("_fence_gate",StringComparison.Ordinal)){cell["open"]=(bool?)cell["open"]!=true;return true;}
            if(id=="minecraft:lever"||id.EndsWith("_button",StringComparison.Ordinal)){cell["powered"]=(bool?)cell["powered"]!=true;cell["until"]=id=="minecraft:lever"?0:tick+20;return true;}
            if(id=="minecraft:tnt"&&Inventory.Hand.Id=="minecraft:flint_and_steel"){Room.Blocks.Remove(key);Prime(bx+.5,by);Inventory.Wear(Inventory.Hand,1,Creative);return true;}
            int count=id=="minecraft:crafting_table"?9:id.Contains("furnace")||id=="minecraft:smoker"||id.EndsWith("anvil",StringComparison.Ordinal)||id=="minecraft:grindstone"?3:id=="minecraft:enchanting_table"||id=="minecraft:stonecutter"?2:id=="minecraft:smithing_table"?4:id=="minecraft:chest"||id=="minecraft:barrel"||id=="minecraft:ender_chest"?27:id=="minecraft:hopper"||id=="minecraft:brewing_stand"?5:id=="minecraft:dispenser"||id=="minecraft:dropper"?9:0;
            if(count==0)return false;
            CloseContainer();if(!Room.Containers.TryGetValue(key,out var box))Room.Containers[key]=box=new NativeContainer(id,count);container=box;containerKey=key;box.Sync=++syncId;return true;
        }
        private JObject ContainerJson() {
            bool craft=container.Id=="minecraft:crafting_table",furnace=container.Id.Contains("furnace")||container.Id=="minecraft:smoker";int count=craft?10:container.Slots.Length;
            var slots=new JArray();
            if(craft){var output=NativeRecipes.Result(container.Slots,3).Json();output["slot"]=0;output["x"]=124;output["y"]=35;slots.Add(output);for(int i=0;i<9;i++){var value=container.Slots[i].Json();value["slot"]=i+1;value["x"]=30+i%3*18;value["y"]=17+i/3*18;slots.Add(value);}}
            else for(int i=0;i<container.Slots.Length;i++){var value=Slot(i).Json();value["slot"]=i;value["x"]=furnace?i==2?116:56:8+i%9*18;value["y"]=furnace?i==0?17:i==1?53:35:18+i/9*18;slots.Add(value);}
            if(container.Id=="minecraft:brewing_stand"){int[] xs={56,79,102,79,17},ys={51,58,51,17,17};for(int i=0;i<5;i++){slots[i]["x"]=xs[i];slots[i]["y"]=ys[i];}}
            var mapping=new JArray();for(int i=0;i<36;i++)mapping.Add(new JObject{["inventory"]=i<27?i+9:i-27,["slot"]=count+i,["x"]=8+(i%9)*18,["y"]=i<27?84+i/9*18:142});
            var json=new JObject{["id"]=container.Id,["syncId"]=container.Sync,["slots"]=slots,["inventorySlots"]=mapping,["brew"]=container.Brew,["brewFuel"]=container.BrewFuel,["cook"]=Math.Min(1,container.Cook/(double)Math.Max(1,NativeRecipes.Cook(container.Slots[0].Id,container.Id)?.Ticks??200)),["fuel"]=container.BurnMax>0?(double)container.Burn/container.BurnMax:0};
            WorkstationInfo(json,slots);return json;
        }
        private List<NativeStack> catalogStacks;
        private List<NativeStack> CatalogStacks() {
            return catalogStacks??(catalogStacks=BuildCatalogStacks());
        }
        private void Catalog() {
            var items=new JArray();int index=0;foreach(var item in CatalogStacks()){var value=item.Json();value["catalogIndex"]=index++;value["groups"]=new JArray(item.Definition.Group);items.Add(value);}
            Emit(new JObject{["type"]="catalog",["revision"]=++catalogRevision,["items"]=items,["groups"]=nativeGroups.DeepClone()});
        }
        private void CreativeItem(JObject p) {
            if(!Creative||(int?)p["catalogRevision"]!=catalogRevision)return;var items=CatalogStacks();int index=(int?)p["catalogIndex"]??-1;if(index<0||index>=items.Count)return;
            var item=items[index].Copy(Math.Max(1,Math.Min(items[index].Definition.Maximum,(int?)p["count"]??64)));string destination=(string)p["destination"]??"cursor";
            if(destination=="inventory")Inventory.Insert(item);else if(destination=="hotbar"||destination=="slot"){int slot=(int?)p["slot"]??Inventory.Selected;if(slot>=0&&slot<9)Inventory.Slots[slot]=item;}else Inventory.Carried=item;
        }
    }
}
