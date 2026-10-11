package dev.hollowcraft.mixin;
import net.minecraft.entity.projectile.ProjectileEntity;
import net.minecraft.util.hit.HitResult;
import org.spongepowered.asm.mixin.Mixin;
import org.spongepowered.asm.mixin.injection.*;
import org.spongepowered.asm.mixin.injection.callback.CallbackInfo;
@Mixin(ProjectileEntity.class)
public abstract class ProjectileCollisionMixin {
    @Inject(method="onCollision",at=@At("HEAD"))
    private void hollowcraft$impact(HitResult hit,CallbackInfo callback) {
        var entity=(ProjectileEntity)(Object)this;
        boolean land=entity instanceof net.minecraft.entity.projectile.PersistentProjectileEntity&&hit.getType()==HitResult.Type.BLOCK;
        dev.hollowcraft.HollowCraftBridge.projectileEvent(entity,land?"land":"impact",hit.getPos());
    }
}
