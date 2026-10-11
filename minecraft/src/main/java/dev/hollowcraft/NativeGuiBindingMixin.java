package dev.hollowcraft.mixin;
import net.minecraft.client.option.KeyBinding;
import org.spongepowered.asm.mixin.Mixin;
import org.spongepowered.asm.mixin.injection.*;
import org.spongepowered.asm.mixin.injection.callback.CallbackInfoReturnable;
@Mixin(KeyBinding.class)
public abstract class NativeGuiBindingMixin {
    @Inject(method="isPressed",at=@At("RETURN"),cancellable=true)
    private void hollowcraft$hotbar(CallbackInfoReturnable<Boolean> callback) {Boolean value=dev.hollowcraft.NativeGuiBridge.activator((KeyBinding)(Object)this);if(value!=null)callback.setReturnValue(value);}
}
