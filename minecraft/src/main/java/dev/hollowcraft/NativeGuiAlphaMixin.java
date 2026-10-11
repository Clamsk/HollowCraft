package dev.hollowcraft.mixin;
import com.mojang.blaze3d.systems.RenderSystem;
import org.lwjgl.opengl.GL11;
import org.spongepowered.asm.mixin.Mixin;
import org.spongepowered.asm.mixin.injection.*;
import org.spongepowered.asm.mixin.injection.callback.CallbackInfo;
@Mixin(value=RenderSystem.class,remap=false)
public abstract class NativeGuiAlphaMixin {
    @Inject(method="defaultBlendFunc",at=@At("TAIL"),remap=false)
    private static void hollowcraft$alpha(CallbackInfo callback) {if(dev.hollowcraft.NativeGuiBridge.rendering()||dev.hollowcraft.NativeHudBridge.rendering||dev.hollowcraft.NativeEntityBridge.rendering)RenderSystem.blendFuncSeparate(GL11.GL_SRC_ALPHA,GL11.GL_ONE_MINUS_SRC_ALPHA,GL11.GL_ONE,GL11.GL_ONE_MINUS_SRC_ALPHA);}
}
