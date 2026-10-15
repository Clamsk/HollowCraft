package dev.hollowcraft.mixin;
import net.minecraft.entity.ai.pathing.PathNodeMaker;
import net.minecraft.entity.mob.MobEntity;
import net.minecraft.world.chunk.ChunkCache;
import org.spongepowered.asm.mixin.*;
import org.spongepowered.asm.mixin.injection.*;
import org.spongepowered.asm.mixin.injection.callback.CallbackInfo;
@Mixin(PathNodeMaker.class)
public abstract class CreaturePathMixin {
    @Shadow protected int entityBlockZSize;
    @Inject(method="init",at=@At("RETURN"))
    private void hollowcraft$flatPath(ChunkCache world,MobEntity entity,CallbackInfo callback) {
        if(dev.hollowcraft.CreatureBridge.plane(entity))entityBlockZSize=1;
    }
}
