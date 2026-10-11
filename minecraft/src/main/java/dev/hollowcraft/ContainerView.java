package dev.hollowcraft;

import com.google.gson.*;
import net.minecraft.screen.*;
import net.minecraft.server.network.ServerPlayerEntity;
import net.minecraft.item.*;
import net.minecraft.block.entity.BannerPattern;
import net.minecraft.util.math.BlockPos;

/** Vanilla handler choices and background identities for the remote container view. */
final class ContainerView {
    static void append(JsonObject data,ServerPlayerEntity player,BlockPos position) {
        var handler=player.currentScreenHandler;String texture="";
        if(handler instanceof SmithingScreenHandler)texture="smithing";
        else if(handler instanceof StonecutterScreenHandler)texture="stonecutter";
        else if(handler instanceof LoomScreenHandler)texture="loom";
        else if(handler instanceof GrindstoneScreenHandler)texture="grindstone";
        else if(handler instanceof CartographyTableScreenHandler)texture="cartography_table";
        else if(handler instanceof ShulkerBoxScreenHandler)texture="shulker_box";
        else if(handler instanceof Generic3x3ContainerScreenHandler)texture="dispenser";
        else if(handler instanceof HopperScreenHandler)texture="hopper";
        if(!texture.isEmpty())data.addProperty("texture","gui_"+texture);
        var factory=player.getServerWorld().getBlockState(position).createScreenHandlerFactory(player.getServerWorld(),position);
        if(factory!=null)data.addProperty("title",factory.getDisplayName().getString());
        if(handler instanceof StonecutterScreenHandler cutter) {
            var choices=new JsonArray();var recipes=cutter.getAvailableRecipes();
            for(int i=0;i<recipes.size();i++) {
                var choice=PlayerBridge.item(recipes.get(i).getOutput(player.getServerWorld().getRegistryManager()));choice.addProperty("button",i);choices.add(choice);
            }
            data.add("choices",choices);data.addProperty("selectedChoice",cutter.getSelectedRecipe());data.addProperty("selector","stonecutter");
        } else if(handler instanceof LoomScreenHandler loom) {
            var choices=new JsonArray();var patterns=loom.getBannerPatterns();
            for(int i=0;i<patterns.size();i++) {
                var preview=new ItemStack(Items.GRAY_BANNER);preview.getOrCreateNbt().putString("HollowCraftLoomPreview",patterns.get(i).value().getId());
                var choice=PlayerBridge.item(preview);choice.addProperty("button",i);choices.add(choice);
                var key=patterns.get(i).getKey();if(key.isPresent())choice.addProperty("name",net.minecraft.text.Text.translatable("block.minecraft.banner."+key.get().getValue().getPath()+".white").getString());
            }
            data.add("choices",choices);data.addProperty("selectedChoice",loom.getSelectedPattern());data.addProperty("selector","loom");
            var source=net.minecraft.block.entity.BannerBlockEntity.getPatternListNbt(loom.getBannerSlot().getStack());
            data.addProperty("patternFull",source!=null&&source.size()>=6);
        }
    }
}
