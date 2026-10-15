package dev.hollowcraft.mixin;

import net.minecraft.client.MinecraftClient;
import net.minecraft.client.network.ClientPlayNetworkHandler;
import net.minecraft.network.packet.Packet;
import net.minecraft.network.packet.c2s.play.*;
import org.spongepowered.asm.mixin.Mixin;
import org.spongepowered.asm.mixin.injection.*;
import org.spongepowered.asm.mixin.injection.callback.CallbackInfo;

@Mixin(ClientPlayNetworkHandler.class)
public abstract class RemoteVehicleControlMixin {
    @Inject(method="sendPacket",at=@At("HEAD"),cancellable=true)
    private void hollowcraft$nativeRider(Packet<?> packet,CallbackInfo callback) {
        if(!(packet instanceof VehicleMoveC2SPacket||packet instanceof PlayerInputC2SPacket||packet instanceof BoatPaddleStateC2SPacket))return;
        var player=MinecraftClient.getInstance().player;
        if(player!=null&&dev.hollowcraft.CreatureBridge.authoritativeBoat(player.getRootVehicle()))callback.cancel();
    }
}
