package dev.hollowcraft.mixin;
import net.minecraft.client.network.ClientPlayerEntity;
import org.spongepowered.asm.mixin.Mixin;
import org.spongepowered.asm.mixin.injection.At;
import org.spongepowered.asm.mixin.injection.Inject;
import org.spongepowered.asm.mixin.injection.callback.CallbackInfo;
@Mixin(ClientPlayerEntity.class)
public abstract class RemoteMovementMixin {
    @Inject(method="sendMovementPackets",at=@At("HEAD"),cancellable=true)
    private void hollowcraft$keepRemotePose(CallbackInfo callback) {
        if(dev.hollowcraft.HollowCraftBridge.remoteActive()) callback.cancel();
    }
    @Inject(method="sendAbilitiesUpdate",at=@At("HEAD"),cancellable=true)
    private void hollowcraft$keepRemoteFlight(CallbackInfo callback) {
        if(dev.hollowcraft.HollowCraftBridge.remoteActive())callback.cancel();
    }
}
