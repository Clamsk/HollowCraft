package dev.hollowcraft;

import com.google.gson.JsonObject;
import net.minecraft.entity.EquipmentSlot;
import net.minecraft.item.ItemStack;
import net.minecraft.server.network.ServerPlayerEntity;
import net.minecraft.util.Hand;

/** Immutable server-to-render-thread copies. Rendering never mutates the real inventory. */
final class PlayerRenderState {
    static volatile PlayerRenderState latest;
    static volatile JsonObject controls;
    final ItemStack[] equipment;
    final boolean creative,using,onFire;
    final Hand activeHand;
    final int useTicks,hurtTicks;
    final String scene,campaign;
    private PlayerRenderState(ServerPlayerEntity player,String scene,String campaign) {
        this.scene=scene;this.campaign=campaign;creative=player.isCreative();using=player.isUsingItem();onFire=player.isOnFire();
        activeHand=player.getActiveHand();useTicks=player.getItemUseTime();hurtTicks=player.hurtTime;
        equipment=new ItemStack[EquipmentSlot.values().length];
        for(var slot:EquipmentSlot.values())equipment[slot.ordinal()]=player.getEquippedStack(slot).copy();
    }
    static void publish(ServerPlayerEntity player,String scene,String campaign) {latest=new PlayerRenderState(player,scene,campaign);}
    static void clear() {latest=null;controls=null;}
}
