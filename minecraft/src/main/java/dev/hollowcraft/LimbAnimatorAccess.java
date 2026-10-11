package dev.hollowcraft.mixin;
import net.minecraft.entity.LimbAnimator;
import org.spongepowered.asm.mixin.Mixin;
import org.spongepowered.asm.mixin.gen.Accessor;
@Mixin(LimbAnimator.class)
public interface LimbAnimatorAccess {
    @Accessor("prevSpeed") void hollowcraft$previousSpeed(float value);
    @Accessor("speed") void hollowcraft$speed(float value);
    @Accessor("pos") void hollowcraft$position(float value);
}
