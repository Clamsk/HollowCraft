package dev.hollowcraft.mixin;

import java.util.Map;
import java.util.Queue;
import net.minecraft.client.particle.*;
import org.spongepowered.asm.mixin.Mixin;
import org.spongepowered.asm.mixin.gen.Accessor;

@Mixin(ParticleManager.class)
public interface ParticleManagerAccess {
    @Accessor("particles") Map<ParticleTextureSheet,Queue<Particle>> hollowcraft$particles();
}
