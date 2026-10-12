package dev.hollowcraft.mixin;
import net.minecraft.entity.projectile.thrown.EnderPearlEntity;
import net.minecraft.server.network.ServerPlayerEntity;
import net.minecraft.util.hit.HitResult;
import net.minecraft.entity.Entity;
import net.minecraft.entity.damage.DamageSource;
import org.spongepowered.asm.mixin.Mixin;
import org.spongepowered.asm.mixin.Unique;
import org.spongepowered.asm.mixin.injection.At;
import org.spongepowered.asm.mixin.injection.Inject;
import org.spongepowered.asm.mixin.injection.Redirect;
import org.spongepowered.asm.mixin.injection.callback.CallbackInfo;
@Mixin(EnderPearlEntity.class)
public abstract class PearlTeleportMixin {
    @Unique private boolean hollowcraft$teleported;
    @Inject(method="onCollision",at=@At("HEAD"))
    private void hollowcraft$beginCollision(HitResult hit,CallbackInfo callback) {hollowcraft$teleported=false;}
    @Inject(method="onCollision",at=@At(value="INVOKE",target="Lnet/minecraft/entity/Entity;onLanding()V",ordinal=0,shift=At.Shift.AFTER))
    private void hollowcraft$resetFall(HitResult hit,CallbackInfo callback) {
        var pearl=(EnderPearlEntity)(Object)this;
        if(!pearl.getWorld().isClient&&pearl.getOwner() instanceof ServerPlayerEntity player) {
            hollowcraft$teleported=true;dev.hollowcraft.HollowCraftBridge.pearlLanded(player);
        }
    }
    @Redirect(method="onCollision",at=@At(value="INVOKE",target="Lnet/minecraft/entity/Entity;damage(Lnet/minecraft/entity/damage/DamageSource;F)Z"))
    private boolean hollowcraft$pearlDamage(Entity entity,DamageSource source,float amount) {
        return dev.hollowcraft.HollowCraftBridge.pearlDamage(entity,source,amount);
    }
    @Inject(method="onCollision",at=@At("TAIL"))
    private void hollowcraft$mirrorTeleport(HitResult hit,CallbackInfo callback) {
        var pearl=(EnderPearlEntity)(Object)this;
        if(hollowcraft$teleported&&!pearl.getWorld().isClient&&pearl.getOwner() instanceof ServerPlayerEntity player&&player.isAlive()) {
            org.slf4j.LoggerFactory.getLogger("HollowCraft").info("Pearl collision {} at {} -> owner {} HP {}",hit.getType(),pearl.getPos(),player.getPos(),player.getHealth());
            var normal=hit instanceof net.minecraft.util.hit.BlockHitResult block?net.minecraft.util.math.Vec3d.of(block.getSide().getVector()):pearl.getVelocity().negate().normalize();
            dev.hollowcraft.HollowCraftBridge.vanillaTeleport(player,normal);
        }
    }
}
