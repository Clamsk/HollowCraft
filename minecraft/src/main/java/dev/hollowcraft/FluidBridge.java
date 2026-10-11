package dev.hollowcraft;
import net.minecraft.block.*;
import net.minecraft.item.*;
import net.minecraft.server.network.ServerPlayerEntity;
import net.minecraft.server.world.ServerWorld;
import net.minecraft.util.Hand;
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
    static void bucket(ServerPlayerEntity player,ItemStack stack,Hand hand,BlockPos target) {
        if(target==null) return;
        var world=player.getServerWorld();var state=world.getBlockState(target);
        if(stack.isOf(Items.BUCKET)) {
            if(state.getBlock() instanceof FluidDrainable drainable) {
                var filled=drainable.tryDrainFluid(world,target,state);
                if(!filled.isEmpty()) player.setStackInHand(hand,ItemUsage.exchangeStack(stack,player,filled));
            }
        } else if(stack.getItem() instanceof BucketItem bucket) {
            if(bucket.placeFluid(player,world,target,new BlockHitResult(Vec3d.ofCenter(target),Direction.UP,target,false))) {
                bucket.onEmptied(player,world,stack,target);player.setStackInHand(hand,BucketItem.getEmptiedStack(stack,player));
            }
        }
    }
}
