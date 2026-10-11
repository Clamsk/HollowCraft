package dev.hollowcraft.mixin;
import net.minecraft.client.util.Window;
import org.spongepowered.asm.mixin.Mixin;
import org.spongepowered.asm.mixin.injection.*;
import org.spongepowered.asm.mixin.injection.callback.CallbackInfoReturnable;
@Mixin(Window.class)
public abstract class NativeGuiWindowMixin {
    @Inject(method={"getWidth","getFramebufferWidth"},at=@At("RETURN"),cancellable=true)
    private void hollowcraft$width(CallbackInfoReturnable<Integer> callback) {if(dev.hollowcraft.NativeGuiBridge.dimensions())callback.setReturnValue(dev.hollowcraft.NativeGuiBridge.logicalWidth()*2);}
    @Inject(method={"getHeight","getFramebufferHeight"},at=@At("RETURN"),cancellable=true)
    private void hollowcraft$height(CallbackInfoReturnable<Integer> callback) {if(dev.hollowcraft.NativeGuiBridge.dimensions())callback.setReturnValue(dev.hollowcraft.NativeGuiBridge.logicalHeight()*2);}
    @Inject(method="getScaledWidth",at=@At("RETURN"),cancellable=true)
    private void hollowcraft$guiWidth(CallbackInfoReturnable<Integer> callback) {if(dev.hollowcraft.NativeGuiBridge.dimensions())callback.setReturnValue(dev.hollowcraft.NativeGuiBridge.logicalWidth());}
    @Inject(method="getScaledHeight",at=@At("RETURN"),cancellable=true)
    private void hollowcraft$guiHeight(CallbackInfoReturnable<Integer> callback) {if(dev.hollowcraft.NativeGuiBridge.dimensions())callback.setReturnValue(dev.hollowcraft.NativeGuiBridge.logicalHeight());}
    @Inject(method="getScaleFactor",at=@At("RETURN"),cancellable=true)
    private void hollowcraft$scale(CallbackInfoReturnable<Double> callback) {if(dev.hollowcraft.NativeGuiBridge.dimensions())callback.setReturnValue(2d);}
}
