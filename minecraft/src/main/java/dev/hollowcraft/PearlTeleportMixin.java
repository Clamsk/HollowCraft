package dev.hollowcraft.mixin;
import net.minecraft.entity.projectile.thrown.EnderPearlEntity;
import net.minecraft.server.network.ServerPlayerEntity;
import net.minecraft.util.hit.HitResult;
import org.spongepowered.asm.mixin.Mixin;
import org.spongepowered.asm.mixin.injection.At;
import org.spongepowered.asm.mixin.injection.Inject;
import org.spongepowered.asm.mixin.injection.callback.CallbackInfo;
@Mixin(EnderPearlEntity.class)
public abstract class PearlTeleportMixin {
    @Inject(method="onCollision",at=@At("TAIL"))
    private void hollowcraft$mirrorTeleport(HitResult hit,CallbackInfo callback) {
        var pearl=(EnderPearlEntity)(Object)this;
        if(!pearl.getWorld().isClient&&pearl.getOwner() instanceof ServerPlayerEntity player&&player.isAlive()) {
            org.slf4j.LoggerFactory.getLogger("HollowCraft").info("Pearl collision {} at {} -> owner {} HP {}",hit.getType(),pearl.getPos(),player.getPos(),player.getHealth());
            dev.hollowcraft.HollowCraftBridge.vanillaTeleport(player);
        }
    }
}
