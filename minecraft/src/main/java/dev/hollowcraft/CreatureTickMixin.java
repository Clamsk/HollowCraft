package dev.hollowcraft.mixin;
import net.minecraft.entity.Entity;
import net.minecraft.server.world.ServerWorld;
import org.spongepowered.asm.mixin.Mixin;
import org.spongepowered.asm.mixin.injection.*;
import org.spongepowered.asm.mixin.injection.callback.CallbackInfo;
@Mixin(ServerWorld.class)
public abstract class CreatureTickMixin {
    @Inject(method="tickEntity",at=@At("HEAD"),cancellable=true)
    private void hollowcraft$sampledRegion(Entity entity,CallbackInfo callback) {
        if(dev.hollowcraft.CreatureBridge.dormant(entity))callback.cancel();
    }
    @Inject(method="tickPassenger",at=@At("HEAD"),cancellable=true)
    private void hollowcraft$sampledPassenger(Entity vehicle,Entity passenger,CallbackInfo callback) {
        if(dev.hollowcraft.CreatureBridge.dormant(vehicle))callback.cancel();
    }
}
