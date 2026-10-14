package dev.hollowcraft.mixin;

import net.minecraft.entity.Entity;
import net.minecraft.util.math.Vec3d;
import net.minecraft.world.explosion.Explosion;
import org.spongepowered.asm.mixin.Mixin;
import org.spongepowered.asm.mixin.injection.*;
import org.spongepowered.asm.mixin.injection.callback.CallbackInfoReturnable;

@Mixin(Explosion.class)
public abstract class BedrockExplosionMixin {
    @Inject(method="getExposure",at=@At("HEAD"),cancellable=true)
    private static void hollowcraft$bedrockExposure(Vec3d origin,Entity target,CallbackInfoReturnable<Float> callback) {
        Float exposure=dev.hollowcraft.BedrockRules.exposure(origin,target);
        if(exposure!=null)callback.setReturnValue(exposure);
    }
}
