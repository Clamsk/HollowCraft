package dev.hollowcraft.mixin;

import net.minecraft.client.render.GameRenderer;
import org.spongepowered.asm.mixin.Mixin;
import org.spongepowered.asm.mixin.gen.Invoker;

/** Reuse the original activation-item animation on the exported HUD canvas. */
@Mixin(GameRenderer.class)
public interface GameRendererAccess {
    @Invoker("renderFloatingItem") void hollowcraft$floatingItem(int width,int height,float delta);
}
