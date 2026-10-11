package dev.hollowcraft;

import java.io.IOException;
import java.nio.file.*;
import net.minecraft.nbt.*;
import net.minecraft.entity.effect.StatusEffectInstance;
import net.minecraft.server.network.ServerPlayerEntity;
import net.minecraft.util.WorldSavePath;
import net.minecraft.world.GameMode;

/** Campaign banks include vanilla item NBT, armor, offhand, durability, hunger and XP. */
final class CampaignStore {
    private final Path root;
    private String active="";
    boolean introDone;
    CampaignStore(net.minecraft.server.MinecraftServer server) {root=server.getSavePath(WorldSavePath.ROOT).resolve("hollowcraft-campaigns");}
    boolean select(ServerPlayerEntity player,String id,boolean legacy) throws IOException {
        if(!id.matches("[A-Za-z0-9_-]{1,64}"))throw new IOException("Invalid campaign identity");
        if(id.equals(active))return false;
        player.closeHandledScreen();player.clearActiveItem();save(player);Files.createDirectories(root);
        Path file=root.resolve(id+".dat"),migration=root.resolve("migrated");
        boolean first=!Files.exists(migration);
        if(first) {write(player,"legacy-unassigned",true);Files.writeString(migration,"Existing shared inventory preserved in legacy-unassigned.dat");}
        if(legacy&&!Files.exists(root.resolve("legacy-owner"))) {
            if(!Files.exists(file))Files.copy(root.resolve("legacy-unassigned.dat"),file);
            Files.writeString(root.resolve("legacy-owner"),id);
        }
        active=id;
        if(Files.exists(file)) {
            NbtCompound data=NbtIo.readCompressed(file.toFile());
            player.clearActiveItem();player.closeHandledScreen();player.getInventory().clear();
            player.getInventory().readNbt(data.getList("Inventory",NbtElement.COMPOUND_TYPE));
            player.getEnderChestInventory().clear();player.getEnderChestInventory().readNbtList(data.getList("EnderItems",NbtElement.COMPOUND_TYPE));
            player.getHungerManager().readNbt(data);
            player.setHealth(Math.max(0,Math.min(player.getMaxHealth(),data.getFloat("Health"))));
            player.experienceLevel=data.getInt("XpLevel");player.experienceProgress=data.getFloat("XpP");player.totalExperience=data.getInt("XpTotal");
            player.getInventory().selectedSlot=Math.max(0,Math.min(8,data.getInt("SelectedItemSlot")));
            player.clearStatusEffects();
            for(var entry:data.getList("ActiveEffects",NbtElement.COMPOUND_TYPE)) {var effect=StatusEffectInstance.fromNbt((NbtCompound)entry);if(effect!=null)player.addStatusEffect(effect);}
            player.setAbsorptionAmount(Math.max(0,data.getFloat("AbsorptionAmount")));
            player.setFireTicks(Math.max(0,data.getShort("Fire")));player.setAir(Math.max(0,data.getShort("Air")));player.setFrozenTicks(data.getInt("TicksFrozen"));
            player.fallDistance=0;player.hurtTime=0;player.timeUntilRegen=0;
            player.changeGameMode(GameMode.byName(data.getString("BridgeMode"),GameMode.SURVIVAL));
            player.addCommandTag("hollowcraft_starter_iron_v1");introDone=data.getBoolean("IntroDone");
            player.currentScreenHandler.sendContentUpdates();
            return false;
        }
        introDone=legacy;
        if(first&&legacy) {save(player);return false;}
        // A brand-new campaign starts with an empty inventory. The normal starter method
        // then installs exactly the requested iron kit, with no old cursor-stack leakage.
        player.clearActiveItem();player.closeHandledScreen();player.playerScreenHandler.setCursorStack(net.minecraft.item.ItemStack.EMPTY);
        player.getInventory().clear();player.getEnderChestInventory().clear();player.clearStatusEffects();
        player.removeScoreboardTag("hollowcraft_starter_iron_v1");player.removeScoreboardTag("hollowcraft_starter");
        NbtCompound food=new NbtCompound();food.putInt("foodLevel",20);food.putInt("foodTickTimer",0);food.putFloat("foodSaturationLevel",5);food.putFloat("foodExhaustionLevel",0);player.getHungerManager().readNbt(food);
        player.setHealth(player.getMaxHealth());player.setAbsorptionAmount(0);player.fallDistance=0;player.experienceLevel=0;player.experienceProgress=0;player.totalExperience=0;
        player.getInventory().selectedSlot=0;player.extinguish();player.setFrozenTicks(0);player.setAir(300);player.hurtTime=0;player.timeUntilRegen=0;
        player.changeGameMode(GameMode.SURVIVAL);player.currentScreenHandler.sendContentUpdates();return true;
    }
    void save(ServerPlayerEntity player) throws IOException {if(player!=null&&!active.isEmpty())write(player,active,introDone);}
    private void write(ServerPlayerEntity player,String id,boolean landed) throws IOException {
        Files.createDirectories(root);NbtCompound data=player.writeNbt(new NbtCompound());
        data.putString("BridgeMode",player.interactionManager.getGameMode().getName());data.putBoolean("IntroDone",landed);
        Path file=root.resolve(id+".dat"),temp=root.resolve(id+".tmp");NbtIo.writeCompressed(data,temp.toFile());
        Files.move(temp,file,StandardCopyOption.REPLACE_EXISTING,StandardCopyOption.ATOMIC_MOVE);
    }
    String id() {return active;}
    boolean legacyOwner() throws IOException {return Files.exists(root.resolve("legacy-owner"))&&Files.readString(root.resolve("legacy-owner")).equals(active);}
}
