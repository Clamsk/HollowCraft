package dev.hollowcraft.mixin;
import net.minecraft.entity.ai.control.MoveControl;
import net.minecraft.entity.mob.MobEntity;
import org.spongepowered.asm.mixin.*;
import org.spongepowered.asm.mixin.injection.*;
@Mixin(MoveControl.class)
public abstract class CreatureMoveMixin {
    @Shadow @Final protected MobEntity entity;
    @ModifyVariable(method="moveTo",at=@At("HEAD"),argsOnly=true,ordinal=2)
    private double hollowcraft$flatGoal(double z) {return dev.hollowcraft.CreatureBridge.plane(entity)?.5:z;}
}
