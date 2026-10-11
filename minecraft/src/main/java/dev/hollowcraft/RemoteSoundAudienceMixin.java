package dev.hollowcraft.mixin;

import net.minecraft.entity.player.PlayerEntity;
import net.minecraft.server.world.ServerWorld;
import org.spongepowered.asm.mixin.Mixin;
import org.spongepowered.asm.mixin.injection.*;

@Mixin(ServerWorld.class)
public abstract class RemoteSoundAudienceMixin {
    @ModifyVariable(method={
        "playSound(Lnet/minecraft/entity/player/PlayerEntity;DDDLnet/minecraft/registry/entry/RegistryEntry;Lnet/minecraft/sound/SoundCategory;FFJ)V",
        "playSoundFromEntity(Lnet/minecraft/entity/player/PlayerEntity;Lnet/minecraft/entity/Entity;Lnet/minecraft/registry/entry/RegistryEntry;Lnet/minecraft/sound/SoundCategory;FFJ)V",
        "syncWorldEvent(Lnet/minecraft/entity/player/PlayerEntity;ILnet/minecraft/util/math/BlockPos;I)V"
    },at=@At("HEAD"),argsOnly=true,ordinal=0)
    private PlayerEntity hollowcraft$unpredictedAudience(PlayerEntity excluded) {
        return dev.hollowcraft.RemoteEffects.active()&&dev.hollowcraft.HollowCraftBridge.isRemotePlayer(excluded)?null:excluded;
    }
}
