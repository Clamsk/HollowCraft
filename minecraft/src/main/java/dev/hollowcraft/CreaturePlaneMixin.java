package dev.hollowcraft.mixin;
import net.minecraft.entity.Entity;
import net.minecraft.util.math.Box;
import org.spongepowered.asm.mixin.Mixin;
import org.spongepowered.asm.mixin.injection.*;
import org.spongepowered.asm.mixin.injection.callback.CallbackInfoReturnable;
@Mixin(Entity.class)
public abstract class CreaturePlaneMixin {
    @Inject(method="calculateBoundingBox",at=@At("RETURN"),cancellable=true)
    private void hollowcraft$flatBody(CallbackInfoReturnable<Box> callback) {
        if(dev.hollowcraft.CreatureBridge.plane((Entity)(Object)this)) {
            Box box=callback.getReturnValue();callback.setReturnValue(new Box(box.minX,box.minY,.05,box.maxX,box.maxY,.95));
        }
    }
}
