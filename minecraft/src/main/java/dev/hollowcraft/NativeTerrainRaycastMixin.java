package dev.hollowcraft.mixin;

import net.minecraft.block.*;
import net.minecraft.util.math.BlockPos;
import net.minecraft.util.shape.*;
import net.minecraft.world.*;
import org.spongepowered.asm.mixin.*;
import org.spongepowered.asm.mixin.injection.*;
import org.spongepowered.asm.mixin.injection.callback.CallbackInfoReturnable;

/** Older bridge worlds can retain full barrier proxies outside the current terrain sample. */
@Mixin(RaycastContext.class)
public abstract class NativeTerrainRaycastMixin {
    @Shadow @Final private ShapeContext entityPosition;
    @Inject(method="getBlockShape",at=@At("HEAD"),cancellable=true)
    private void hollowcraft$legacyTerrain(BlockState state,BlockView world,BlockPos pos,CallbackInfoReturnable<VoxelShape> callback) {
        if(dev.hollowcraft.HollowCraftBridge.delegateLegacyTerrain(state,pos,entityPosition))callback.setReturnValue(VoxelShapes.empty());
    }
}
