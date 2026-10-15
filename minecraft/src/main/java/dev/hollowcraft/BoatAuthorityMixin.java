package dev.hollowcraft.mixin;

import net.minecraft.entity.Entity;
import org.spongepowered.asm.mixin.Mixin;
import org.spongepowered.asm.mixin.injection.*;
import org.spongepowered.asm.mixin.injection.callback.CallbackInfoReturnable;

/** Player passengers must not transfer the bridge boat's physics to the hidden client. */
@Mixin(Entity.class)
public abstract class BoatAuthorityMixin {
    @Inject(method="isLogicalSideForUpdatingMovement",at=@At("RETURN"),cancellable=true)
    private void hollowcraft$serverBoat(CallbackInfoReturnable<Boolean> callback) {
        var entity=(Entity)(Object)this;
        if(dev.hollowcraft.CreatureBridge.authoritativeBoat(entity))callback.setReturnValue(!entity.getWorld().isClient);
    }
}
