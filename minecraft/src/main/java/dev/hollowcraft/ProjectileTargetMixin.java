package dev.hollowcraft.mixin;

import net.minecraft.entity.Entity;
import net.minecraft.entity.projectile.ProjectileEntity;
import org.spongepowered.asm.mixin.Mixin;
import org.spongepowered.asm.mixin.injection.At;
import org.spongepowered.asm.mixin.injection.Inject;
import org.spongepowered.asm.mixin.injection.callback.CallbackInfoReturnable;

/** Native colliders select native targets; proxy bounding boxes must not hit through walls. */
@Mixin(ProjectileEntity.class)
public abstract class ProjectileTargetMixin {
    @Inject(method="canHit",at=@At("HEAD"),cancellable=true)
    private void hollowcraft$nativeTargets(Entity target,CallbackInfoReturnable<Boolean> callback) {
        if(dev.hollowcraft.HollowCraftBridge.nativeProjectile((ProjectileEntity)(Object)this)&&target.getCommandTags().contains("hollowcraft_proxy"))callback.setReturnValue(false);
    }
}
