package dev.hollowcraft;

import java.util.*;
import java.util.function.Consumer;
import java.util.function.BiConsumer;
import java.io.IOException;
import java.nio.file.Files;
import net.minecraft.client.MinecraftClient;
import net.minecraft.item.*;
import net.minecraft.text.Text;

/** Uses the vanilla private-profile hotbar.nbt; only the client thread touches HotbarStorage. */
final class CreativeHotbars {
    private static volatile ItemStack[][] published;
    private static MinecraftClient initialized;
    static void tick(MinecraftClient client) {
        if(initialized==client)return;
        initialized=client;publish(client);
    }
    static void publish(MinecraftClient client) {
        var storage=client.getCreativeHotbarStorage();var result=new ItemStack[9][9];
        for(int row=0;row<9;row++)for(int column=0;column<9;column++)result[row][column]=storage.getSavedHotbar(row).get(column).copy();
        published=result;
    }
    static List<ItemStack> entries() {
        var rows=published;var result=new ArrayList<ItemStack>(81);
        for(int row=0;row<9;row++) {
            boolean empty=rows==null||Arrays.stream(rows[row]).allMatch(ItemStack::isEmpty);
            for(int column=0;column<9;column++) {
                ItemStack stack=empty?ItemStack.EMPTY:rows[row][column].copy();
                if(empty&&column==row) {
                    stack=new ItemStack(Items.PAPER);stack.getOrCreateSubNbt("CustomCreativeLock");
                    stack.setCustomName(Text.translatable("inventory.hotbarInfo","C",Integer.toString(row+1)));
                }
                result.add(stack);
            }
        }
        return result;
    }
    static void save(int index,ItemStack[] items,Consumer<String> done) {
        var client=MinecraftClient.getInstance();
        client.execute(()-> {
            try {
                if(!Files.exists(client.runDirectory.toPath().resolve(".hollowcraft-profile"))) {done.accept("Hotbar saving requires the isolated HollowCraft profile");return;}
                var storage=client.getCreativeHotbarStorage();var entry=storage.getSavedHotbar(index);
                for(int i=0;i<9;i++)entry.set(i,items[i].copy());
                storage.save();
                // Vanilla logs write failures internally; do not announce success for a failed save.
                var persisted=net.minecraft.nbt.NbtIo.read(new java.io.File(client.runDirectory,"hotbar.nbt"));
                if(persisted==null||!persisted.getList(Integer.toString(index),net.minecraft.nbt.NbtElement.COMPOUND_TYPE).equals(entry.toNbtList()))throw new IOException("Hotbar file does not match the saved preset");
                publish(client);done.accept("");
            } catch(Exception error) {org.slf4j.LoggerFactory.getLogger("HollowCraft").error("Creative hotbar save failed",error);done.accept("Could not save hotbar");}
        });
    }
    static void load(int index,BiConsumer<ItemStack[],String> done) {
        var client=MinecraftClient.getInstance();
        client.execute(()-> {
            try {
                var entry=client.getCreativeHotbarStorage().getSavedHotbar(index);var result=new ItemStack[9];
                for(int i=0;i<9;i++)result[i]=entry.get(i).copy();
                done.accept(result,"");
            } catch(Exception error) {org.slf4j.LoggerFactory.getLogger("HollowCraft").error("Creative hotbar load failed",error);done.accept(null,"Could not load hotbar");}
        });
    }
}
