package dev.hollowcraft.mixin;
import net.minecraft.entity.vehicle.BoatEntity;
import net.minecraft.entity.damage.DamageSource;
import org.spongepowered.asm.mixin.Mixin;
import org.spongepowered.asm.mixin.injection.*;
import org.spongepowered.asm.mixin.injection.callback.CallbackInfoReturnable;
@Mixin(BoatEntity.class)
public abstract class BoatDamageMixin {
    @Inject(method="damage",at=@At("HEAD"),cancellable=true)
    private void hollowcraft$bedrockShield(DamageSource source,float amount,CallbackInfoReturnable<Boolean> callback) {
        if(dev.hollowcraft.BedrockRules.blocksExplosion((BoatEntity)(Object)this,source))callback.setReturnValue(false);
    }
}
