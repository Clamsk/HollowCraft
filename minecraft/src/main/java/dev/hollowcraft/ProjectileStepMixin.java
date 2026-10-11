package dev.hollowcraft.mixin;

import net.minecraft.entity.Entity;
import net.minecraft.entity.projectile.ProjectileEntity;
import net.minecraft.server.world.ServerWorld;
import org.spongepowered.asm.mixin.Mixin;
import org.spongepowered.asm.mixin.injection.*;
import org.spongepowered.asm.mixin.injection.callback.CallbackInfo;

/** Cancel before ServerWorld increments age or invokes subclass physics. */
@Mixin(ServerWorld.class)
public abstract class ProjectileStepMixin {
    @Inject(method="tickEntity",at=@At("HEAD"),cancellable=true)
    private void hollowcraft$nativeStep(Entity entity,CallbackInfo callback) {
        if(entity instanceof ProjectileEntity projectile&&!dev.hollowcraft.HollowCraftBridge.tickNativeProjectile(projectile))callback.cancel();
        if(entity instanceof net.minecraft.entity.TntEntity tnt&&!dev.hollowcraft.NativeTntSteps.tick(tnt))callback.cancel();
    }
}
