using System;
using Newtonsoft.Json.Linq;

namespace HollowCraft.Standalone
{
    internal sealed partial class NativeGame
    {
        // Shared frontend contract: right-click always sends interact, not place
        // or itemUse.start. Preserve target, placement cell and hand priority.
        private void Interact(JObject packet) {
            if(Creative||!Spectator&&health>0) {
                if(usingItem)return;
                bool sneak=(bool?)lastVisual?["crouching"]==true;
                if(!sneak&&TryInteractCreature(packet)){RejectPredictedPlacement(packet);return;}
                if(!sneak&&TryInteractBlock(packet)){RejectPredictedPlacement(packet);return;}
                var item=Inventory.Hand.Definition.Block&&!Inventory.Hand.Empty?Inventory.Hand:!CanUse(Inventory.Hand)?Inventory.Offhand:new NativeStack();
                if(!item.Empty&&item.Definition.Block) {
                    var placement=(JObject)packet.DeepClone();placement["x"]=packet["placeX"]??packet["x"];placement["y"]=packet["placeY"]??packet["y"];
                    if((bool?)packet["canPlace"]!=false)Place(placement,false);else RejectPredictedPlacement(packet);return;
                }
                var use=(JObject)packet.DeepClone();use["start"]=true;use["release"]=false;ItemUse(use);
            }
            RejectPredictedPlacement(packet);
        }
        private void RejectPredictedPlacement(JObject packet)=>Emit(new JObject{["type"]="placeResult",["x"]=Number(packet,"placeX",Number(packet,"x")),["y"]=Number(packet,"placeY",Number(packet,"y")),["accepted"]=false});
    }
}
