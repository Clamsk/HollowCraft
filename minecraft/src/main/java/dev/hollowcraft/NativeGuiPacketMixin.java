package dev.hollowcraft.mixin;
import net.minecraft.client.network.ClientPlayNetworkHandler;
import net.minecraft.network.packet.Packet;
import org.spongepowered.asm.mixin.Mixin;
import org.spongepowered.asm.mixin.injection.*;
import org.spongepowered.asm.mixin.injection.callback.CallbackInfo;
@Mixin(ClientPlayNetworkHandler.class)
public abstract class NativeGuiPacketMixin {
    @Inject(method="sendPacket",at=@At("HEAD"),cancellable=true)
    private void hollowcraft$identity(Packet<?> packet,CallbackInfo callback) {if(dev.hollowcraft.NativeGuiBridge.capture(packet))callback.cancel();}
}
