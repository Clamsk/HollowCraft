package dev.hollowcraft.mixin;

import net.minecraft.entity.LivingEntity;
import net.minecraft.entity.damage.*;
import org.spongepowered.asm.mixin.Mixin;
import org.spongepowered.asm.mixin.injection.*;
import org.spongepowered.asm.mixin.injection.callback.CallbackInfoReturnable;

@Mixin(LivingEntity.class)
public abstract class RemoteDamageMixin {
    @Inject(method="damage",at=@At("HEAD"),cancellable=true)
    private void hollowcraft$nativeCollisionAuthority(DamageSource source,float amount,CallbackInfoReturnable<Boolean> callback) {
        if(dev.hollowcraft.BedrockRules.blocksExplosion((LivingEntity)(Object)this,source)) {callback.setReturnValue(false);return;}
        // The native XY collider already prevents wall penetration. MC's projected door/wall
        // volume has a different Z thickness and must not invent suffocation at the doorway.
        if(dev.hollowcraft.HollowCraftBridge.isRemotePlayer((LivingEntity)(Object)this)&&source.isOf(DamageTypes.IN_WALL)) callback.setReturnValue(false);
    }
    @Inject(method="damage",at=@At("RETURN"))
    private void hollowcraft$hurtFeedback(DamageSource source,float amount,CallbackInfoReturnable<Boolean> callback) {
        if(callback.getReturnValue()) dev.hollowcraft.HollowCraftBridge.playerHurt((LivingEntity)(Object)this,source);
    }
    @Inject(method="tryUseTotem",at=@At("RETURN"))
    private void hollowcraft$totemFeedback(DamageSource source,CallbackInfoReturnable<Boolean> callback) {
        if(callback.getReturnValue())dev.hollowcraft.HollowCraftBridge.playerTotem((LivingEntity)(Object)this);
    }
}
