package dev.hollowcraft;

import java.util.UUID;
import net.minecraft.enchantment.Enchantments;
import net.minecraft.entity.EquipmentSlot;
import net.minecraft.entity.attribute.EntityAttributeModifier;
import net.minecraft.entity.attribute.EntityAttributes;
import net.minecraft.entity.projectile.TridentEntity;
import net.minecraft.item.*;
import net.minecraft.server.network.ServerPlayerEntity;
import net.minecraft.text.Text;

/** A marked creative reward; ordinary tridents keep all vanilla damage rules. */
public final class CreativeTrident {
    private static final String TAG="HollowCraftCreativeTrident";
    public static boolean special(ItemStack stack) {return stack.isOf(Items.TRIDENT)&&stack.hasNbt()&&stack.getNbt().getBoolean(TAG);}
    public static float thrownDamage(ItemStack stack,float vanilla) {return special(stack)?100:vanilla;}
    static ItemStack grant(ServerPlayerEntity player) {
        for(int i=0;i<player.getInventory().size();i++)if(special(player.getInventory().getStack(i)))return ItemStack.EMPTY;
        for(var entity:player.getServerWorld().iterateEntities())if(entity instanceof TridentEntity trident&&trident.getOwner()==player
            &&special(((dev.hollowcraft.mixin.CreativeTridentMixin)trident).hollowcraft$stack()))return ItemStack.EMPTY;
        var stack=new ItemStack(Items.TRIDENT);
        stack.getOrCreateNbt().putBoolean(TAG,true);
        stack.setCustomName(Text.literal("创造三叉戟"));stack.addEnchantment(Enchantments.LOYALTY,3);
        stack.addAttributeModifier(EntityAttributes.GENERIC_ATTACK_DAMAGE,new EntityAttributeModifier(UUID.fromString("ab50fcae-3175-4379-a2a1-0741e1ecab64"),"HollowCraft creative damage",99,EntityAttributeModifier.Operation.ADDITION),EquipmentSlot.MAINHAND);
        stack.addAttributeModifier(EntityAttributes.GENERIC_ATTACK_SPEED,new EntityAttributeModifier(UUID.fromString("ab50fcae-3175-4379-a2a1-0741e1ecab65"),"HollowCraft trident speed",-2.9,EntityAttributeModifier.Operation.ADDITION),EquipmentSlot.MAINHAND);
        return stack;
    }
}
