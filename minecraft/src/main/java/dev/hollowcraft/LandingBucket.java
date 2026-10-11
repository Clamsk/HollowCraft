package dev.hollowcraft;

import java.util.UUID;
import net.minecraft.entity.ItemEntity;
import net.minecraft.item.*;
import net.minecraft.server.network.ServerPlayerEntity;
import net.minecraft.util.Hand;
import net.minecraft.util.math.*;

/** Temporary real inventory swap; no copies of the displaced weapon are inserted. */
final class LandingBucket {
    private static final String TAG="HollowCraftLandingBucket";
    final ServerPlayerEntity player;
    final Hand hand;
    private final int selected,source;
    private final ItemStack water,displaced;
    private final String token=UUID.randomUUID().toString();
    private LandingBucket(ServerPlayerEntity player,Hand hand,int source) {
        this.player=player;this.hand=hand;this.source=source;selected=player.getInventory().selectedSlot;
        water=hand==Hand.OFF_HAND?player.getOffHandStack():player.getInventory().getStack(source);
        displaced=player.getMainHandStack();
        if(hand==Hand.MAIN_HAND&&source!=selected) {
            player.getInventory().setStack(source,displaced);player.getInventory().setStack(selected,water);
        }
    }
    static LandingBucket begin(ServerPlayerEntity player) {
        if(!player.isAlive()||!player.getAbilities().allowModifyWorld)return null;
        if(player.getMainHandStack().isOf(Items.WATER_BUCKET))return new LandingBucket(player,Hand.MAIN_HAND,player.getInventory().selectedSlot);
        if(player.getOffHandStack().isOf(Items.WATER_BUCKET))return new LandingBucket(player,Hand.OFF_HAND,40);
        for(int i=0;i<36;i++)if(player.getInventory().getStack(i).isOf(Items.WATER_BUCKET))return new LandingBucket(player,Hand.MAIN_HAND,i);
        return null;
    }
    void empty() {
        var stack=BucketItem.getEmptiedStack(water,player);stack.getOrCreateNbt().putString(TAG,token);
        player.setStackInHand(hand,stack);player.getInventory().markDirty();player.playerScreenHandler.sendContentUpdates();
    }
    void abort() {
        if(hand==Hand.MAIN_HAND&&source!=selected&&player.getInventory().getStack(selected)==water&&player.getInventory().getStack(source)==displaced) {
            player.getInventory().setStack(selected,displaced);player.getInventory().setStack(source,water);
        }
    }
    private boolean marked(ItemStack stack) {return stack.isOf(Items.BUCKET)&&stack.hasNbt()&&token.equals(stack.getNbt().getString(TAG));}
    private ItemStack replacement(ItemStack stack,ItemStack filled) {
        stack.removeSubNbt(TAG);return filled.isEmpty()?stack:filled.copy();
    }
    boolean finish(ItemStack filled,BlockPos cell) {
        int found=-1;ItemStack returned=ItemStack.EMPTY;
        for(int i=0;i<player.getInventory().size();i++) {
            var stack=player.getInventory().getStack(i);if(!marked(stack))continue;
            returned=replacement(stack,filled);player.getInventory().setStack(i,returned);found=i;break;
        }
        boolean recovered=found>=0&&!filled.isEmpty();
        if(found<0)for(var dropped:player.getServerWorld().getEntitiesByClass(ItemEntity.class,new Box(cell).expand(6),entity->marked(entity.getStack()))) {
            dropped.setStack(replacement(dropped.getStack(),filled));recovered=!filled.isEmpty();break;
        }
        if(hand==Hand.MAIN_HAND&&source!=selected&&found==selected&&player.getInventory().getStack(source)==displaced) {
            player.getInventory().setStack(selected,displaced);player.getInventory().setStack(source,returned);
        }
        player.getInventory().markDirty();player.playerScreenHandler.sendContentUpdates();return recovered;
    }
}
