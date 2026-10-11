package dev.hollowcraft.mixin;
import net.minecraft.entity.LivingEntity;
import org.spongepowered.asm.mixin.Mixin;
import org.spongepowered.asm.mixin.gen.Accessor;
import org.spongepowered.asm.mixin.gen.Invoker;
@Mixin(LivingEntity.class)
public interface LivingEntityAccess {
    @Accessor("lastAttackedTicks") int hollowcraft$getAttackTicks();
    @Accessor("lastAttackedTicks") void hollowcraft$setAttackTicks(int ticks);
    @Invoker("getJumpVelocity") float hollowcraft$jumpVelocity();
    @Invoker("getVelocityMultiplier") float hollowcraft$velocityMultiplier();
}
