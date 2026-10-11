package dev.hollowcraft.mixin;
import net.minecraft.client.MinecraftClient;
import org.spongepowered.asm.mixin.Mixin;
import org.spongepowered.asm.mixin.injection.At;
import org.spongepowered.asm.mixin.injection.Inject;
import org.spongepowered.asm.mixin.injection.callback.CallbackInfoReturnable;
@Mixin(MinecraftClient.class)
public abstract class RemotePauseMixin {
    @Inject(method="isPaused",at=@At("RETURN"),cancellable=true)
    private void hollowcraft$pauseTogether(CallbackInfoReturnable<Boolean> callback) {
        if(dev.hollowcraft.HollowCraftBridge.remoteActive()) callback.setReturnValue(dev.hollowcraft.HollowCraftBridge.remotePaused);
    }
}
