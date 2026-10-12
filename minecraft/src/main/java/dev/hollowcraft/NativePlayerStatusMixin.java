package dev.hollowcraft.mixin;

import net.minecraft.client.MinecraftClient;
import net.minecraft.client.network.ClientPlayNetworkHandler;
import net.minecraft.network.packet.s2c.play.EntityStatusS2CPacket;
import org.spongepowered.asm.mixin.Mixin;
import org.spongepowered.asm.mixin.injection.*;
import org.spongepowered.asm.mixin.injection.callback.CallbackInfo;

@Mixin(ClientPlayNetworkHandler.class)
public abstract class NativePlayerStatusMixin {
    // This call occurs after forceMainThread: never move the entity on the Netty thread.
    @Inject(method="onEntityStatus",at=@At(value="INVOKE",target="Lnet/minecraft/network/packet/s2c/play/EntityStatusS2CPacket;getEntity(Lnet/minecraft/world/World;)Lnet/minecraft/entity/Entity;"))
    private void hollowcraft$effectOrigin(EntityStatusS2CPacket packet,CallbackInfo callback) {
        var client=MinecraftClient.getInstance();int status=packet.getStatus();
        if(client.world!=null&&(status==29||status==30||status==35)&&packet.getEntity(client.world)==client.player)
            dev.hollowcraft.NativePlayerEffects.sync(client);
    }
}
