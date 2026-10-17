import com.google.gson.*;
import java.nio.file.*;
import java.nio.charset.StandardCharsets;
import java.util.*;
import net.minecraft.SharedConstants;
import net.minecraft.Bootstrap;
import net.minecraft.registry.Registries;
import net.minecraft.item.*;
import net.minecraft.entity.*;
import net.minecraft.entity.attribute.*;
import net.minecraft.util.math.BlockPos;
import net.minecraft.world.EmptyBlockView;

/** Build-time numerical metadata only. Creates no client, server, world or window. */
public final class NativeRegistryExporter {
    public static void main(String[] args) throws Exception {
        SharedConstants.createGameVersion();Bootstrap.initialize();
        JsonObject result=new JsonObject();result.addProperty("schema",1);result.addProperty("minecraft","1.20.1");
        result.addProperty("source","Numerical registry facts from a local vanilla 1.20.1 bootstrap; no game binaries or textures included.");
        JsonArray items=new JsonArray(),entities=new JsonArray(),potions=new JsonArray(),enchantments=new JsonArray();
        var fuels=net.minecraft.block.entity.AbstractFurnaceBlockEntity.createFuelTimeMap();
        for(Item item:Registries.ITEM) {
            JsonObject value=new JsonObject();value.addProperty("id",Registries.ITEM.getId(item).toString());value.addProperty("maxCount",item.getMaxCount());value.addProperty("durability",item.getMaxDamage());
            if(item.getRecipeRemainder()!=null)value.addProperty("remainder",Registries.ITEM.getId(item.getRecipeRemainder()).toString());
            if(fuels.containsKey(item))value.addProperty("fuel",fuels.get(item));
            var modifiers=item.getAttributeModifiers(EquipmentSlot.MAINHAND);double attack=1,speed=4;
            for(var modifier:modifiers.get(EntityAttributes.GENERIC_ATTACK_DAMAGE))attack+=modifier.getValue();for(var modifier:modifiers.get(EntityAttributes.GENERIC_ATTACK_SPEED))speed+=modifier.getValue();value.addProperty("attack",attack);value.addProperty("speed",speed);
            if(item instanceof ToolItem tool){value.addProperty("harvest",tool.getMaterial().getMiningLevel());value.addProperty("miningSpeed",tool.getMaterial().getMiningSpeedMultiplier());}
            if(item.getFoodComponent()!=null){var food=item.getFoodComponent();value.addProperty("food",food.getHunger());value.addProperty("saturation",food.getHunger()*food.getSaturationModifier()*2);value.addProperty("alwaysEdible",food.isAlwaysEdible());value.addProperty("fastFood",food.isSnack());JsonArray foodEffects=new JsonArray();for(var pair:food.getStatusEffects()){var effect=pair.getFirst();JsonObject e=new JsonObject();e.addProperty("id",Registries.STATUS_EFFECT.getId(effect.getEffectType()).toString());e.addProperty("ticks",effect.getDuration());e.addProperty("amplifier",effect.getAmplifier());e.addProperty("chance",pair.getSecond());foodEffects.add(e);}value.add("foodEffects",foodEffects);}
            if(item instanceof BlockItem blockItem){var state=blockItem.getBlock().getDefaultState();value.addProperty("block",true);value.addProperty("hardness",state.getHardness(EmptyBlockView.INSTANCE,BlockPos.ORIGIN));value.addProperty("blastResistance",blockItem.getBlock().getBlastResistance());JsonArray boxes=new JsonArray();try{for(var box:state.getCollisionShape(EmptyBlockView.INSTANCE,BlockPos.ORIGIN).getBoundingBoxes()){JsonArray b=new JsonArray();b.add(box.minX);b.add(box.minY);b.add(box.minZ);b.add(box.maxX);b.add(box.maxY);b.add(box.maxZ);boxes.add(b);}}catch(RuntimeException ignored){}value.add("collision",boxes);}
            if(item instanceof ArmorItem armor){value.addProperty("armor",armor.getProtection());value.addProperty("toughness",armor.getToughness());value.addProperty("equipment",armor.getSlotType().getName());}
            if(item instanceof BlockItem placed){var state=placed.getBlock().getDefaultState();value.addProperty("requiresTool",state.isToolRequired());value.addProperty("tool",state.isIn(net.minecraft.registry.tag.BlockTags.PICKAXE_MINEABLE)?"pickaxe":state.isIn(net.minecraft.registry.tag.BlockTags.AXE_MINEABLE)?"axe":state.isIn(net.minecraft.registry.tag.BlockTags.SHOVEL_MINEABLE)?"shovel":state.isIn(net.minecraft.registry.tag.BlockTags.HOE_MINEABLE)?"hoe":"");value.addProperty("harvest",state.isIn(net.minecraft.registry.tag.BlockTags.NEEDS_DIAMOND_TOOL)?3:state.isIn(net.minecraft.registry.tag.BlockTags.NEEDS_IRON_TOOL)?2:state.isIn(net.minecraft.registry.tag.BlockTags.NEEDS_STONE_TOOL)?1:0);}
            if(item instanceof SpawnEggItem egg)value.addProperty("entity",Registries.ENTITY_TYPE.getId(egg.getEntityType(null)).toString());
            items.add(value);
        }
        for(EntityType<?> entity:Registries.ENTITY_TYPE){JsonObject value=new JsonObject();value.addProperty("id",Registries.ENTITY_TYPE.getId(entity).toString());value.addProperty("width",entity.getWidth());value.addProperty("height",entity.getHeight());try{var attributes=DefaultAttributeRegistry.get((EntityType<? extends LivingEntity>)entity);if(attributes!=null){value.addProperty("health",attributes.getBaseValue(EntityAttributes.GENERIC_MAX_HEALTH));if(attributes.has(EntityAttributes.GENERIC_MOVEMENT_SPEED))value.addProperty("speed",attributes.getBaseValue(EntityAttributes.GENERIC_MOVEMENT_SPEED));if(attributes.has(EntityAttributes.GENERIC_ATTACK_DAMAGE))value.addProperty("attack",attributes.getBaseValue(EntityAttributes.GENERIC_ATTACK_DAMAGE));}}catch(RuntimeException ignored){}entities.add(value);}
        for(var potion:Registries.POTION){JsonObject value=new JsonObject();value.addProperty("id",Registries.POTION.getId(potion).toString());JsonArray effects=new JsonArray();for(var effect:potion.getEffects()){JsonObject e=new JsonObject();e.addProperty("id",Registries.STATUS_EFFECT.getId(effect.getEffectType()).toString());e.addProperty("ticks",effect.getDuration());e.addProperty("amplifier",effect.getAmplifier());e.addProperty("instant",effect.getEffectType().isInstant());effects.add(e);}value.add("effects",effects);potions.add(value);}
        for(var enchantment:Registries.ENCHANTMENT){JsonObject value=new JsonObject();value.addProperty("id",Registries.ENCHANTMENT.getId(enchantment).toString());value.addProperty("maxLevel",enchantment.getMaxLevel());value.addProperty("treasure",enchantment.isTreasure());value.addProperty("curse",enchantment.isCursed());JsonArray compatible=new JsonArray();for(var other:Registries.ENCHANTMENT)if(enchantment.canCombine(other))compatible.add(Registries.ENCHANTMENT.getId(other).toString());value.add("compatible",compatible);enchantments.add(value);}
        net.minecraft.recipe.BrewingRecipeRegistry.registerDefaults();JsonArray brewing=new JsonArray();
        for(var bottle:new Item[]{Items.POTION,Items.SPLASH_POTION,Items.LINGERING_POTION})for(var potion:Registries.POTION)for(var ingredient:Registries.ITEM){var in=new ItemStack(bottle);net.minecraft.potion.PotionUtil.setPotion(in,potion);var reagent=new ItemStack(ingredient);if(!net.minecraft.recipe.BrewingRecipeRegistry.hasRecipe(in,reagent))continue;var out=net.minecraft.recipe.BrewingRecipeRegistry.craft(reagent,in);JsonObject recipe=new JsonObject();recipe.addProperty("item",Registries.ITEM.getId(bottle).toString());recipe.addProperty("potion",Registries.POTION.getId(potion).toString());recipe.addProperty("ingredient",Registries.ITEM.getId(ingredient).toString());recipe.addProperty("outputItem",Registries.ITEM.getId(out.getItem()).toString());recipe.addProperty("outputPotion",Registries.POTION.getId(net.minecraft.potion.PotionUtil.getPotion(out)).toString());brewing.add(recipe);}
        result.add("items",items);result.add("entities",entities);result.add("potions",potions);result.add("enchantments",enchantments);result.add("brewing",brewing);
        Path output=Path.of(args[0]);Files.createDirectories(output.getParent());Files.writeString(output,new Gson().toJson(result),StandardCharsets.UTF_8);
        System.out.println("Native registry: "+items.size()+" items, "+entities.size()+" entity records, "+potions.size()+" potions, "+enchantments.size()+" enchantments. No game/world/window created.");
    }
}
