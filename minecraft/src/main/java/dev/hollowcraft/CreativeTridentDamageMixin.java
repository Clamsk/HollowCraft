package dev.hollowcraft.mixin;

import net.minecraft.entity.projectile.TridentEntity;
import net.minecraft.item.ItemStack;
import org.spongepowered.asm.mixin.Mixin;
import org.spongepowered.asm.mixin.Shadow;
import org.spongepowered.asm.mixin.injection.*;

@Mixin(TridentEntity.class)
public abstract class CreativeTridentDamageMixin {
    @Shadow private ItemStack tridentStack;
    @ModifyConstant(method="onEntityHit",constant=@Constant(floatValue=8f))
    private float hollowcraft$creativeDamage(float vanilla) {return dev.hollowcraft.CreativeTrident.thrownDamage(tridentStack,vanilla);}
}
