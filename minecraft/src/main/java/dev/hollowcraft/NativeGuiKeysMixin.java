package dev.hollowcraft.mixin;
import net.minecraft.client.util.InputUtil;
import org.spongepowered.asm.mixin.Mixin;
import org.spongepowered.asm.mixin.injection.*;
import org.spongepowered.asm.mixin.injection.callback.CallbackInfoReturnable;
@Mixin(InputUtil.class)
public abstract class NativeGuiKeysMixin {
    @Inject(method="isKeyPressed",at=@At("RETURN"),cancellable=true)
    private static void hollowcraft$modifiers(long window,int key,CallbackInfoReturnable<Boolean> callback) {Boolean value=dev.hollowcraft.NativeGuiBridge.keyState(key);if(value!=null)callback.setReturnValue(value);}
}
