package dev.hollowcraft.mixin;
import dev.hollowcraft.HollowCraftBridge;
import net.minecraft.client.gui.screen.ingame.AnvilScreen;
import org.spongepowered.asm.mixin.Mixin;
import org.spongepowered.asm.mixin.injection.At;
import org.spongepowered.asm.mixin.injection.Inject;
import org.spongepowered.asm.mixin.injection.callback.CallbackInfo;
@Mixin(AnvilScreen.class)
public abstract class RemoteAnvilMixin {
    @Inject(method="onRenamed",at=@At("HEAD"),cancellable=true)
    private void hollowcraft$remoteName(String name,CallbackInfo info) {if(HollowCraftBridge.remoteActive()) info.cancel();}
}
