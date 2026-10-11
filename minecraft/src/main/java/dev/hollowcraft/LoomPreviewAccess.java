package dev.hollowcraft.mixin;

import net.minecraft.client.gui.DrawContext;
import net.minecraft.client.gui.screen.ingame.LoomScreen;
import net.minecraft.registry.entry.RegistryEntry;
import net.minecraft.block.entity.BannerPattern;
import org.spongepowered.asm.mixin.Mixin;
import org.spongepowered.asm.mixin.gen.Invoker;

@Mixin(LoomScreen.class)
public interface LoomPreviewAccess {
    @Invoker("drawBanner") void hollowcraft$drawBanner(DrawContext context,RegistryEntry<BannerPattern> pattern,int x,int y);
}
