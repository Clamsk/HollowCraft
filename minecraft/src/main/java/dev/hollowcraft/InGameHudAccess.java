package dev.hollowcraft.mixin;

import net.minecraft.client.gui.DrawContext;
import net.minecraft.client.gui.hud.InGameHud;
import org.spongepowered.asm.mixin.Mixin;
import org.spongepowered.asm.mixin.gen.*;

@Mixin(InGameHud.class)
public interface InGameHudAccess {
    @Accessor("scaledWidth") int hollowcraft$width();
    @Accessor("scaledHeight") int hollowcraft$height();
    @Accessor("scaledWidth") void hollowcraft$width(int value);
    @Accessor("scaledHeight") void hollowcraft$height(int value);
    @Invoker("renderHotbar") void hollowcraft$hotbar(float delta,DrawContext draw);
    @Invoker("renderStatusBars") void hollowcraft$status(DrawContext draw);
    @Invoker("renderMountHealth") void hollowcraft$mount(DrawContext draw);
    @Invoker("renderStatusEffectOverlay") void hollowcraft$effects(DrawContext draw);
}
