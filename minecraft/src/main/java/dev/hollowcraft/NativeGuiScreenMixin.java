package dev.hollowcraft.mixin;
import net.minecraft.client.gui.DrawContext;
import net.minecraft.client.gui.screen.Screen;
import org.spongepowered.asm.mixin.Mixin;
import org.spongepowered.asm.mixin.injection.*;
import org.spongepowered.asm.mixin.injection.callback.CallbackInfo;
@Mixin(Screen.class)
public abstract class NativeGuiScreenMixin {
    @Inject(method="renderWithTooltip",at=@At("HEAD"),cancellable=true)
    private void hollowcraft$oneGuiTarget(DrawContext draw,int x,int y,float delta,CallbackInfo callback) {
        if(dev.hollowcraft.NativeGuiBridge.owns((Screen)(Object)this)&&!dev.hollowcraft.NativeGuiBridge.rendering())callback.cancel();
    }
}
