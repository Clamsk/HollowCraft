package dev.hollowcraft.mixin;

import net.minecraft.entity.projectile.TridentEntity;
import net.minecraft.item.ItemStack;
import org.spongepowered.asm.mixin.Mixin;
import org.spongepowered.asm.mixin.gen.Accessor;
import org.spongepowered.asm.mixin.injection.*;

@Mixin(TridentEntity.class)
public interface CreativeTridentMixin {
    @Accessor("tridentStack") ItemStack hollowcraft$stack();
}
