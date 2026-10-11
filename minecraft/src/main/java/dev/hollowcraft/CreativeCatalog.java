package dev.hollowcraft;

import com.google.gson.*;
import java.util.*;
import net.minecraft.item.*;
import net.minecraft.registry.Registries;
import net.minecraft.server.network.ServerPlayerEntity;

/** Vanilla creative groups and their exact stacks, including enchantment and potion NBT. */
final class CreativeCatalog {
    private final List<ItemStack> stacks=new ArrayList<>();
    JsonObject build(ServerPlayerEntity player,String scene) {
        stacks.clear();JsonArray items=new JsonArray(),groups=new JsonArray();Map<String,Integer> indices=new LinkedHashMap<>();
        ItemGroups.updateDisplayContext(player.getWorld().getEnabledFeatures(),false,player.getServer().getRegistryManager());
        for(ItemGroup group:ItemGroups.getGroupsToDisplay()) {
            if(group.getType()==ItemGroup.Type.HOTBAR)continue;
            JsonObject tab=new JsonObject();tab.addProperty("id",Registries.ITEM_GROUP.getId(group).toString());
            tab.addProperty("name",group.getDisplayName().getString());tab.addProperty("type",group.getType().name());
            tab.addProperty("column",group.getColumn());tab.addProperty("row",group.getRow().name());
            tab.add("icon",PlayerBridge.item(group.getIcon()));JsonArray entries=new JsonArray();
            var contents=group.getType()==ItemGroup.Type.SEARCH?group.getSearchTabStacks():group.getDisplayStacks();
            for(ItemStack stack:contents) {
                String key=Registries.ITEM.getId(stack.getItem())+"|"+String.valueOf(stack.getNbt());
                Integer index=indices.get(key);
                if(index==null) {
                    index=stacks.size();indices.put(key,index);stacks.add(stack.copy());
                    JsonObject entry=PlayerBridge.item(stack);entry.addProperty("catalogIndex",index);items.add(entry);
                }
                entries.add(index);
            }
            tab.add("entries",entries);groups.add(tab);
        }
        JsonObject result=new JsonObject();result.addProperty("type","catalog");result.addProperty("scene",scene);result.add("items",items);result.add("groups",groups);return result;
    }
    ItemStack stack(int index,int count) {
        if(index<0||index>=stacks.size())return ItemStack.EMPTY;
        ItemStack stack=stacks.get(index).copy();stack.setCount(Math.max(1,Math.min(count,stack.getMaxCount())));return stack;
    }
}
