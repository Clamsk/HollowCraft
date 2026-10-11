package dev.hollowcraft;

import com.google.gson.*;
import net.minecraft.entity.EquipmentSlot;
import net.minecraft.item.*;
import net.minecraft.screen.ScreenHandler;
import net.minecraft.screen.slot.SlotActionType;
import net.minecraft.server.network.ServerPlayerEntity;
import net.minecraft.util.Hand;
import net.minecraft.registry.Registries;

final class PlayerBridge {
    static JsonObject item(ItemStack stack) {
        JsonObject value=new JsonObject();value.addProperty("id",Registries.ITEM.getId(stack.getItem()).toString());
        value.addProperty("count",stack.getCount());value.addProperty("damage",stack.getDamage());value.addProperty("maxDamage",stack.getMaxDamage());
        value.addProperty("maxCount",stack.getMaxCount());
        value.addProperty("food",stack.isFood());value.addProperty("block",stack.getItem() instanceof BlockItem);
        boolean cube=false;
        if(stack.getItem() instanceof BlockItem block)try {cube=net.minecraft.block.Block.isShapeFullCube(block.getBlock().getDefaultState().getCollisionShape(net.minecraft.world.EmptyBlockView.INSTANCE,net.minecraft.util.math.BlockPos.ORIGIN));}catch(RuntimeException ignored) { }
        value.addProperty("predictSolid",cube);
        value.addProperty("name",stack.getName().getString());
        value.addProperty("enchanted",stack.hasEnchantments());
        JsonArray tooltip=new JsonArray();for(var entry:net.minecraft.enchantment.EnchantmentHelper.get(stack).entrySet()) tooltip.add(entry.getKey().getName(entry.getValue()).getString());
        for(var effect:net.minecraft.potion.PotionUtil.getPotionEffects(stack)) tooltip.add(effect.getEffectType().getName().getString()+" "+(effect.getAmplifier()+1)+" ("+(effect.getDuration()/20)+"s)");value.add("tooltip",tooltip);
        value.addProperty("render",renderKey(stack));ItemModelIcons.request(stack);
        if(stack.getItem() instanceof PotionItem||stack.isOf(Items.TIPPED_ARROW)) value.addProperty("potion",Registries.POTION.getId(net.minecraft.potion.PotionUtil.getPotion(stack)).toString());
        return value;
    }
    static String renderKey(ItemStack stack) {
        String id=Registries.ITEM.getId(stack.getItem()).toString();
        if(stack.hasNbt()) {
            try {
                var hash=java.security.MessageDigest.getInstance("SHA-256").digest(stack.getNbt().toString().getBytes(java.nio.charset.StandardCharsets.UTF_8));
                return id+"_nbt_"+java.util.HexFormat.of().formatHex(hash,0,8);
            } catch(java.security.NoSuchAlgorithmException impossible) {throw new IllegalStateException(impossible);}
        }
        return id;
    }
    static JsonObject stats(ServerPlayerEntity player) {
        JsonObject value=new JsonObject();value.addProperty("health",player.getHealth());value.addProperty("maxHealth",player.getMaxHealth());
        value.addProperty("food",player.getHungerManager().getFoodLevel());value.addProperty("saturation",player.getHungerManager().getSaturationLevel());
        value.addProperty("mode",player.interactionManager.getGameMode().getName());value.addProperty("hurtTicks",player.hurtTime);
        value.addProperty("armor",player.getArmor());value.addProperty("dead",player.isDead());
        value.addProperty("absorption",player.getAbsorptionAmount());value.addProperty("toughness",player.getAttributeValue(net.minecraft.entity.attribute.EntityAttributes.GENERIC_ARMOR_TOUGHNESS));
        value.addProperty("level",player.experienceLevel);value.addProperty("xp",player.experienceProgress);
        value.add("offhand",item(player.getOffHandStack()));JsonArray armor=new JsonArray();
        value.addProperty("activeHand",player.getActiveHand()==Hand.OFF_HAND?"offhand":"mainhand");
        for(EquipmentSlot slot:new EquipmentSlot[]{EquipmentSlot.HEAD,EquipmentSlot.CHEST,EquipmentSlot.LEGS,EquipmentSlot.FEET}) armor.add(item(player.getEquippedStack(slot)));
        value.add("equipment",armor);
        value.addProperty("using",player.isUsingItem());value.addProperty("useTicks",player.getItemUseTime());
        value.addProperty("blocking",player.isBlocking());
        value.addProperty("useAction",player.isUsingItem()?player.getActiveItem().getUseAction().name():"NONE");
        value.addProperty("speed",player.getAttributeValue(net.minecraft.entity.attribute.EntityAttributes.GENERIC_MOVEMENT_SPEED));
        var motion=(dev.hollowcraft.mixin.LivingEntityAccess)player;
        value.addProperty("jumpVelocity",motion.hollowcraft$jumpVelocity());value.addProperty("velocityMultiplier",motion.hollowcraft$velocityMultiplier());
        value.addProperty("slipperiness",player.getWorld().getBlockState(((dev.hollowcraft.mixin.EntitySurfaceAccess)player).hollowcraft$surfacePos()).getBlock().getSlipperiness());
        value.addProperty("sprinting",player.isSprinting());value.addProperty("depthStrider",net.minecraft.enchantment.EnchantmentHelper.getDepthStrider(player));
        value.addProperty("allowFlying",player.getAbilities().allowFlying);value.addProperty("flying",player.getAbilities().flying);value.addProperty("flySpeed",player.getAbilities().getFlySpeed());
        value.addProperty("waterHeight",player.getFluidHeight(net.minecraft.registry.tag.FluidTags.WATER));value.addProperty("lavaHeight",player.getFluidHeight(net.minecraft.registry.tag.FluidTags.LAVA));value.addProperty("swimHeight",player.getSwimHeight());
        value.addProperty("onFire",player.isOnFire());value.addProperty("air",player.getAir());
        value.addProperty("water",player.isTouchingWater());value.addProperty("lava",player.isInLava());
        value.addProperty("eyeHeight",player.getEyeY()-player.getY());
        value.addProperty("climbing",player.isClimbing());value.addProperty("gliding",player.isFallFlying());
        value.addProperty("rocket",player.getServerWorld().getEntitiesByClass(net.minecraft.entity.projectile.FireworkRocketEntity.class,player.getBoundingBox().expand(5),e->((dev.hollowcraft.mixin.FireworkAccess)e).hollowcraft$getShooter()==player).size()>0);
        JsonArray effects=new JsonArray();for(var effect:player.getStatusEffects()) {
            JsonObject entry=new JsonObject();entry.addProperty("id",Registries.STATUS_EFFECT.getId(effect.getEffectType()).toString());entry.addProperty("ticks",effect.getDuration());entry.addProperty("amplifier",effect.getAmplifier());entry.addProperty("color",effect.getEffectType().getColor());effects.add(entry);
        }
        value.add("effects",effects);return value;
    }
    static void click(ServerPlayerEntity player,JsonObject command) {
        boolean container="container".equals(command.get("handler").getAsString());
        ScreenHandler handler=container ? player.currentScreenHandler : player.playerScreenHandler;
        if(command.has("syncId")&&command.get("syncId").getAsInt()!=handler.syncId)return;
        int index=command.get("slot").getAsInt();
        if((index<0 && index!=-999) || index>=handler.slots.size()) return;
        boolean quick=command.has("quick") && command.get("quick").getAsBoolean();
        int button=command.has("button") ? command.get("button").getAsInt() : 0;
        SlotActionType action=quick?SlotActionType.QUICK_MOVE:SlotActionType.PICKUP;
        if(command.has("action"))try {action=SlotActionType.valueOf(command.get("action").getAsString());}catch(IllegalArgumentException e){return;}
        if(action==SlotActionType.SWAP) {if(button<0||(button>8&&button!=40))return;}
        else if(action==SlotActionType.QUICK_CRAFT) {if(button<0||button>10||((button>>2)==2&&!player.isCreative()))return;}
        else if(action==SlotActionType.CLONE) {if(!player.isCreative())return;button=2;}
        else if(button<0||button>1)return;
        handler.onSlotClick(index,button,action,player);
        handler.sendContentUpdates();
    }
    static void eat(ServerPlayerEntity player) {
        ItemStack food=player.getMainHandStack();Hand hand=Hand.MAIN_HAND;
        if(!food.isFood()) {food=player.getOffHandStack();hand=Hand.OFF_HAND;}
        if(!food.isFood() || !player.canConsume(food.getItem().getFoodComponent().isAlwaysEdible())) return;
        // Compatibility with old clients: start the real action rather than bypassing
        // its duration, effects, animation and interruption rules with finishUsing.
        if(!player.isUsingItem())player.interactionManager.interactItem(player,player.getServerWorld(),food,hand);
    }
    static void offhand(ServerPlayerEntity player) {
        player.clearActiveItem();
        ItemStack first=player.getMainHandStack();player.setStackInHand(Hand.MAIN_HAND,player.getOffHandStack());player.setStackInHand(Hand.OFF_HAND,first);
    }
}
