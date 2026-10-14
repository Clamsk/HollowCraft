package dev.hollowcraft.mixin;

import net.minecraft.server.network.*;
import net.minecraft.util.math.BlockPos;
import org.spongepowered.asm.mixin.*;
import org.spongepowered.asm.mixin.injection.*;
import org.spongepowered.asm.mixin.injection.callback.CallbackInfoReturnable;

@Mixin(ServerPlayerInteractionManager.class)
public abstract class BedrockMiningMixin {
    @Shadow @Final protected ServerPlayerEntity player;
    @Inject(method="tryBreakBlock",at=@At("HEAD"),cancellable=true)
    private void hollowcraft$unbreakable(BlockPos pos,CallbackInfoReturnable<Boolean> callback) {
        if(dev.hollowcraft.BedrockRules.protectedBlock(player,pos))callback.setReturnValue(false);
    }
}
