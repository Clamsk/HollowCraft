package dev.hollowcraft.mixin;
import net.minecraft.client.render.item.ItemRenderer;
import net.minecraft.client.color.item.ItemColors;
import org.spongepowered.asm.mixin.Mixin;
import org.spongepowered.asm.mixin.gen.Accessor;
@Mixin(ItemRenderer.class)
public interface ItemRendererAccess {
    @Accessor("colors") ItemColors hollowcraft$getColors();
}
