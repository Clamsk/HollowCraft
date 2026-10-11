package dev.hollowcraft.mixin;
import net.minecraft.entity.LivingEntity;
import org.spongepowered.asm.mixin.Mixin;
import org.spongepowered.asm.mixin.gen.Accessor;
@Mixin(LivingEntity.class)
public interface LivingEntityAccess {
    @Accessor("lastAttackedTicks") int hollowcraft$getAttackTicks();
    @Accessor("lastAttackedTicks") void hollowcraft$setAttackTicks(int ticks);
}
