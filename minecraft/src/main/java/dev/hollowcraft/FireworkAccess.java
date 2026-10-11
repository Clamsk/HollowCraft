package dev.hollowcraft.mixin;
import net.minecraft.entity.LivingEntity;
import net.minecraft.entity.projectile.FireworkRocketEntity;
import org.spongepowered.asm.mixin.Mixin;
import org.spongepowered.asm.mixin.gen.Accessor;
import org.spongepowered.asm.mixin.gen.Invoker;
@Mixin(FireworkRocketEntity.class)
public interface FireworkAccess {
    @Accessor("shooter") LivingEntity hollowcraft$getShooter();
    @Invoker("wasShotByEntity") boolean hollowcraft$attached();
    @Invoker("hasExplosionEffects") boolean hollowcraft$explosive();
}
