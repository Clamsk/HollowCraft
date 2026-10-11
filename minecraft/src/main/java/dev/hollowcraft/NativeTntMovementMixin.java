package dev.hollowcraft.mixin;

import net.minecraft.entity.*;
import net.minecraft.util.math.Vec3d;
import org.spongepowered.asm.mixin.Mixin;
import org.spongepowered.asm.mixin.injection.*;
import org.spongepowered.asm.mixin.injection.callback.CallbackInfo;

@Mixin(Entity.class)
public abstract class NativeTntMovementMixin {
    @ModifyVariable(method="move",at=@At("HEAD"),argsOnly=true)
    private Vec3d hollowcraft$tntHull(Vec3d movement) {
        return (Object)this instanceof TntEntity tnt?dev.hollowcraft.NativeTntSteps.limit(tnt,movement):movement;
    }
    @Inject(method="move",at=@At("RETURN"))
    private void hollowcraft$tntContact(MovementType type,Vec3d movement,CallbackInfo callback) {
        if((Object)this instanceof TntEntity tnt)dev.hollowcraft.NativeTntSteps.finish(tnt);
    }
}
