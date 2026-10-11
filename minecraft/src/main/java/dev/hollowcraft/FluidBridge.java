package dev.hollowcraft;
import net.minecraft.block.*;
import net.minecraft.item.*;
import net.minecraft.server.network.ServerPlayerEntity;
import net.minecraft.server.world.ServerWorld;
import net.minecraft.util.Hand;
import net.minecraft.util.ActionResult;
import net.minecraft.util.hit.BlockHitResult;
import net.minecraft.util.math.*;
import java.util.*;

/** A sealed one-block-deep simulation plane keeps vanilla fluid propagation meaningful in 2D. */
final class FluidBridge {
    private static final Set<BlockPos> sealed=new HashSet<>();
    static void seal(ServerWorld world,BlockPos center) {
        for(int x=center.getX()-12;x<=center.getX()+12;x++) for(int y=center.getY()-10;y<=center.getY()+10;y++) for(int z:new int[]{-1,1}) {
            var p=new BlockPos(x,y,z);
            if(sealed.add(p)&&world.getBlockState(p).isAir()) world.setBlockState(p,Blocks.BARRIER.getDefaultState(),Block.NOTIFY_LISTENERS);
        }
    }
    static void reset() {sealed.clear();}
    static ActionResult bucket(ServerPlayerEntity player,ItemStack stack,Hand hand,BlockPos target) {
        if(target==null) return ActionResult.PASS;
        var world=player.getServerWorld();var state=world.getBlockState(target);
        if(stack.isOf(Items.BUCKET)) {
            if(state.getBlock() instanceof FluidDrainable drainable) {
                var filled=drainable.tryDrainFluid(world,target,state);
                if(!filled.isEmpty()) {
                    player.incrementStat(net.minecraft.stat.Stats.USED.getOrCreateStat(stack.getItem()));
                    drainable.getBucketFillSound().ifPresent(sound->world.playSound(null,target,sound,net.minecraft.sound.SoundCategory.PLAYERS,1,1));
                    world.emitGameEvent(player,net.minecraft.world.event.GameEvent.FLUID_PICKUP,target);
                    player.setStackInHand(hand,ItemUsage.exchangeStack(stack,player,filled));
                    net.minecraft.advancement.criterion.Criteria.FILLED_BUCKET.trigger(player,filled);
                    return ActionResult.SUCCESS;
                }
            }
            return ActionResult.PASS;
        } else if(stack.getItem() instanceof BucketItem bucket) {
            if(bucket.placeFluid(player,world,target,new BlockHitResult(Vec3d.ofCenter(target),Direction.UP,target,false))) {
                bucket.onEmptied(player,world,stack,target);
                net.minecraft.advancement.criterion.Criteria.PLACED_BLOCK.trigger(player,target,stack);
                player.incrementStat(net.minecraft.stat.Stats.USED.getOrCreateStat(stack.getItem()));
                player.setStackInHand(hand,BucketItem.getEmptiedStack(stack,player));
                return ActionResult.SUCCESS;
            }
        }
        return ActionResult.FAIL;
    }
}
