package dev.hollowcraft.mixin;
import net.minecraft.entity.Entity;
import net.minecraft.block.BlockState;
import net.minecraft.util.math.BlockPos;
import org.spongepowered.asm.mixin.Mixin;
import org.spongepowered.asm.mixin.gen.Invoker;
@Mixin(Entity.class)
public interface StepSoundAccess {
    @Invoker("playStepSound") void hollowcraft$stepSound(BlockPos position,BlockState state);
}
