package dev.hollowcraft.mixin;
import net.minecraft.client.network.ClientPlayerInteractionManager;
import net.minecraft.entity.player.PlayerEntity;
import org.spongepowered.asm.mixin.Mixin;
import org.spongepowered.asm.mixin.injection.At;
import org.spongepowered.asm.mixin.injection.Inject;
import org.spongepowered.asm.mixin.injection.callback.CallbackInfo;
/** The backend window's unpressed mouse must not cancel HK-owned bow/shield/drink actions. */
@Mixin(ClientPlayerInteractionManager.class)
public abstract class RemoteUseMixin {
    @Inject(method="stopUsingItem",at=@At("HEAD"),cancellable=true)
    private void hollowcraft$keepRemoteUse(PlayerEntity player,CallbackInfo callback) {
        if(dev.hollowcraft.HollowCraftBridge.remoteActive()) callback.cancel();
    }
}
