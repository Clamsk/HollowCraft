package dev.hollowcraft;
import com.google.gson.JsonObject;
import net.minecraft.entity.*;
import net.minecraft.entity.mob.ZombieEntity;
import net.minecraft.entity.damage.DamageSource;
import net.minecraft.server.world.ServerWorld;
import net.minecraft.item.*;

/** Native enemies retain their actual collision size and arthropod/ghost classification. */
final class NativeProxy extends ZombieEntity {
    private EntityDimensions dimensions;
    private final String kind,name;
    private DamageSource pendingLoot;
    private boolean pendingPlayer,deathConfirmed,rewarded;
    NativeProxy(ServerWorld world,JsonObject data) {
        super(EntityType.ZOMBIE,world);kind=data.has("kind")?data.get("kind").getAsString():"arthropod";name=data.has("name")?data.get("name").getAsString().toLowerCase():"";
        experiencePoints=kind.equals("resource")?0:data.get("maxHealth").getAsDouble()>=100?25:5;
    }
    void size(JsonObject data) {
        float width=data.has("width")?Math.max(.2f,Math.min(16,data.get("width").getAsFloat())):.6f;
        float height=data.has("height")?Math.max(.2f,Math.min(16,data.get("height").getAsFloat())):1.95f;
        if(dimensions==null||Math.abs(dimensions.width-width)>.01||Math.abs(dimensions.height-height)>.01) {dimensions=EntityDimensions.changing(width,height);calculateDimensions();}
    }
    String bridgeName() {return name;}
    @Override public void setHealth(float health) {
        // Cleave multipliers can leave sub-millipoint float residue. Normalize only
        // native proxies; LivingEntity.damage still runs its original death/loot path.
        super.setHealth(health>0&&health<.0001f?0:health);
    }
    @Override public EntityDimensions getDimensions(EntityPose pose) {return dimensions==null?super.getDimensions(pose):dimensions;}
    @Override protected boolean burnsInDaylight() {return false;}
    @Override protected boolean canConvertInWater() {return false;}
    @Override public EntityGroup getGroup() {return "undead".equals(kind)?EntityGroup.UNDEAD:EntityGroup.ARTHROPOD;}
    @Override protected void dropLoot(DamageSource source,boolean causedByPlayer) {
        pendingLoot=source;pendingPlayer=causedByPlayer;
    }
    @Override protected void dropXp() {}
    void confirmDeath() {
        if(deathConfirmed||rewarded||isAlive()) return;deathConfirmed=true;rewarded=true;
        if(pendingLoot!=null) rewardLoot();
        if(!kind.equals("resource")&&pendingPlayer) net.minecraft.entity.ExperienceOrbEntity.spawn((ServerWorld)getWorld(),getPos(),experiencePoints);
    }
    private void rewardLoot() {
        if(!getWorld().getGameRules().getBoolean(net.minecraft.world.GameRules.DO_MOB_LOOT)) return;
        // Minecraft resources give the survival campaign a renewable crafting progression.
        if(kind.equals("resource")) {
            if(name.contains("grass")||name.contains("plant")) {dropStack(new ItemStack(Items.STRING));dropStack(new ItemStack(Items.WHEAT_SEEDS));}
            else {dropStack(new ItemStack(Items.COBBLESTONE,1+random.nextInt(2)));if(name.contains("wood")||name.contains("crate")||name.contains("sign")) dropStack(new ItemStack(Items.OAK_LOG));if(random.nextInt(4)==0) dropStack(new ItemStack(Items.COAL));if(random.nextInt(6)==0) dropStack(new ItemStack(Items.RAW_IRON));if(random.nextInt(12)==0) dropStack(new ItemStack(Items.REDSTONE));}
        } else {
            dropStack(new ItemStack(Items.STRING));dropStack(new ItemStack(Items.FEATHER));dropStack(new ItemStack(Items.COOKED_CHICKEN));
            if(random.nextInt(3)==0) dropStack(new ItemStack(Items.IRON_NUGGET));
            if(random.nextInt(3)==0) dropStack(new ItemStack(Items.GUNPOWDER));
            if(name.contains("mage")||name.contains("wizard")) {dropStack(new ItemStack(Items.BLAZE_POWDER,2));dropStack(new ItemStack(Items.NETHER_WART));}
            if(getMaxHealth()>=100) {dropStack(new ItemStack(Items.IRON_INGOT,2));dropStack(new ItemStack(Items.GOLDEN_APPLE));}
            if(getMaxHealth()>=200) {dropStack(new ItemStack(Items.DIAMOND,2));dropStack(new ItemStack(Items.ENDER_PEARL));}
            if(name.contains("crystal guardian")||name.contains("beam miner")||name.contains("hornet")&&getMaxHealth()>=600) dropStack(new ItemStack(Items.ELYTRA));
        }
    }
}
