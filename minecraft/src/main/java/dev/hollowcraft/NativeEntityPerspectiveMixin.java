package dev.hollowcraft.mixin;
import net.minecraft.client.option.*;
import org.spongepowered.asm.mixin.Mixin;
import org.spongepowered.asm.mixin.injection.*;
import org.spongepowered.asm.mixin.injection.callback.CallbackInfoReturnable;
@Mixin(GameOptions.class)
public abstract class NativeEntityPerspectiveMixin {
    @Inject(method="getPerspective",at=@At("RETURN"),cancellable=true)
    private void hollowcraft$thirdPerson(CallbackInfoReturnable<Perspective> callback) {if(dev.hollowcraft.NativeEntityBridge.rendering)callback.setReturnValue(Perspective.THIRD_PERSON_BACK);}
}
