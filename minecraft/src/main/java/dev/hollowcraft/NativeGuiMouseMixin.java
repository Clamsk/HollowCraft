package dev.hollowcraft.mixin;
import net.minecraft.client.Mouse;
import net.minecraft.client.util.InputUtil;
import org.lwjgl.glfw.GLFW;
import org.spongepowered.asm.mixin.Mixin;
import org.spongepowered.asm.mixin.injection.*;
@Mixin(Mouse.class)
public abstract class NativeGuiMouseMixin {
    @Redirect(method={"lockCursor","unlockCursor"},at=@At(value="INVOKE",target="Lnet/minecraft/client/util/InputUtil;setCursorParameters(JIDD)V"))
    private void hollowcraft$noBackendWarp(long window,int mode,double x,double y) {
        if(dev.hollowcraft.HollowCraftBridge.remoteActive())GLFW.glfwSetInputMode(window,GLFW.GLFW_CURSOR,GLFW.GLFW_CURSOR_NORMAL);
        else InputUtil.setCursorParameters(window,mode,x,y);
    }
}
