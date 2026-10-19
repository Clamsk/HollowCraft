using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace HollowCraft.Standalone
{
    internal sealed class NativeSettings
    {
        internal bool KeepInventory=true,AutomaticWater=true,ApplyExisting;
        internal int WaterReturnTicks=40;
        internal string Revision="",Error="";
        internal JObject Loadout;
        internal static NativeSettings Read(string path) {
            var result=new NativeSettings();if(string.IsNullOrEmpty(path)||!File.Exists(path))return result;
            try {var data=JObject.Parse(File.ReadAllText(path));if((int?)data["schema"]!=1)throw new InvalidDataException("Unknown launcher settings schema");result.KeepInventory=(bool?)data["keepInventory"]??true;result.AutomaticWater=(bool?)data["automaticWater"]??true;result.WaterReturnTicks=Math.Max(20,Math.Min(120,(int?)data["waterReturnTicks"]??40));result.ApplyExisting=(bool?)data["applyLoadoutToExisting"]??false;result.Revision=(string)data["loadoutRevision"]??"";
                if(data["loadout"] is JObject kit){if(!(kit["slots"] is JArray slots)||slots.Count!=36||!(kit["armor"] is JArray armor)||armor.Count!=4)throw new InvalidDataException("Invalid launcher equipment layout");foreach(var value in slots.Concat(armor).Concat(new[]{kit["offhand"]}))NativeStack.Read(value);for(int i=0;i<4;i++){var piece=NativeStack.Read(armor[i]);if(!piece.Empty&&piece.Definition.Equipment!=NativeInventory.Equipment(i))throw new InvalidDataException("Item does not fit armor slot");}result.Loadout=(JObject)kit.DeepClone();}
            }catch(Exception error){return new NativeSettings{Error=error.Message};}return result;
        }
        internal void Starter(NativeInventory inventory) {
            inventory.Starter();if(Loadout==null)return;
            for(int i=0;i<36;i++)inventory.Slots[i]=NativeStack.Read(Loadout["slots"][i]);for(int i=0;i<4;i++)inventory.Armor[i]=NativeStack.Read(Loadout["armor"][i]);inventory.Offhand=NativeStack.Read(Loadout["offhand"]);inventory.Selected=0;
        }
    }
    internal sealed partial class NativeGame
    {
        private NativeSettings settings;private string appliedLoadout="";private bool deathDropped;
        private void DropDeathInventory() {
            if(deathDropped||settings.KeepInventory)return;deathDropped=true;CloseContainer();
            foreach(var stack in Inventory.Slots.Concat(Inventory.Armor).Concat(new[]{Inventory.Offhand,Inventory.Carried}).Concat(Inventory.Craft)){
                if(stack.Empty)continue;if(NativeData.Enchantment(stack,"vanishing_curse")==0){DropAt(stack.Copy(),x+(random.NextDouble()-.5)*.7,y+.5+random.NextDouble()*.4);Room.Drops.Last()["ready"]=tick+60;}stack.Count=0;stack.Normalize();
            }
            usingItem=false;mining=null;Save();
        }
    }
}
