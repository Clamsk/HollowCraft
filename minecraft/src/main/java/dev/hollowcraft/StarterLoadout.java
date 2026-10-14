package dev.hollowcraft;

import net.minecraft.item.*;
import net.minecraft.server.network.ServerPlayerEntity;

/** Hotbar layout is independent of stack splitting in the backpack. */
final class StarterLoadout {
    static final String LAYOUT_TAG="hollowcraft_starter_hotbar_v2";
    static final String BEDROCK_TAG="hollowcraft_starter_bedrock_v1";
    private static final Item[] HOTBAR={Items.IRON_SWORD,Items.BOW,Items.GOLDEN_APPLE,Items.COBBLESTONE,
        Items.WATER_BUCKET,Items.LAVA_BUCKET,Items.TNT,Items.FLINT_AND_STEEL,Items.ENDER_PEARL};
    private static final int[] COUNTS={1,1,64,64,1,1,64,1,16};
    static void fresh(ServerPlayerEntity player) {
        var inventory=player.getInventory();
        for(int slot=0;slot<HOTBAR.length;slot++)inventory.setStack(slot,new ItemStack(HOTBAR[slot],COUNTS[slot]));
        Item[] reserve={Items.COOKED_BEEF,Items.OAK_LOG,Items.IRON_AXE,Items.IRON_PICKAXE,Items.ARROW,
            Items.ENDER_PEARL,Items.COBBLESTONE,Items.TORCH,Items.IRON_INGOT,Items.TOTEM_OF_UNDYING,Items.BEDROCK};
        int[] counts={64,64,1,1,64,48,128,64,64,2,64};
        int slot=9;
        for(int i=0;i<reserve.length;i++)for(int remaining=counts[i];remaining>0;) {
            int count=Math.min(remaining,reserve[i].getMaxCount());remaining-=count;
            inventory.setStack(slot++,new ItemStack(reserve[i],count));
        }
        inventory.selectedSlot=0;
        player.addCommandTag(LAYOUT_TAG);player.addCommandTag(BEDROCK_TAG);
    }
    static void arrangeExisting(ServerPlayerEntity player) {
        if(player.getCommandTags().contains(LAYOUT_TAG))return;
        var inventory=player.getInventory();
        // Swap existing stacks, retaining counts, durability, enchantments and NBT.
        // Consumed items are not replenished by this layout migration.
        for(int target=0;target<HOTBAR.length;target++) {
            if(inventory.getStack(target).isOf(HOTBAR[target]))continue;
            for(int source=0;source<36;source++)if(inventory.getStack(source).isOf(HOTBAR[target])) {
                ItemStack displaced=inventory.getStack(target),selected=inventory.getStack(source);
                inventory.setStack(target,selected);inventory.setStack(source,displaced);break;
            }
        }
        inventory.selectedSlot=0;inventory.markDirty();player.addCommandTag(LAYOUT_TAG);
    }
}
