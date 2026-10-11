package dev.hollowcraft.mixin;
import net.minecraft.client.render.entity.EntityRenderDispatcher;
import net.minecraft.world.World;
import org.spongepowered.asm.mixin.Mixin;
import org.spongepowered.asm.mixin.gen.Accessor;
@Mixin(EntityRenderDispatcher.class)
public interface EntityRenderDispatcherAccess {
    @Accessor("world") World hollowcraft$world();
    @Accessor("renderShadows") boolean hollowcraft$shadows();
}
