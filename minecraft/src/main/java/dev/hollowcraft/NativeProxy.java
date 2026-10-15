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
    private int nailBlock,spellBlock,blockSerial;
    private float blockedAngle,damageAngle,candidateAngle;
    Float zenithAngle;
    private float phaseFloor;private boolean applyingDamage;
    private boolean blockedSpell;
    private final int actorId;
    private net.minecraft.util.math.Vec3d previousNative,currentNative;
    NativeProxy(ServerWorld world,JsonObject data) {
        super(EntityType.ZOMBIE,world);kind=data.has("kind")?data.get("kind").getAsString():"arthropod";name=data.has("name")?data.get("name").getAsString().toLowerCase():"";
        actorId=data.has("actor")?data.get("actor").getAsInt():data.get("id").getAsInt();
        experiencePoints=kind.equals("resource")?0:data.get("maxHealth").getAsDouble()>=100?25:5;
    }
    void size(JsonObject data) {
        float width=data.has("width")?Math.max(.2f,Math.min(16,data.get("width").getAsFloat())):.6f;
        float height=data.has("height")?Math.max(.2f,Math.min(16,data.get("height").getAsFloat())):1.95f;
        if(dimensions==null||Math.abs(dimensions.width-width)>.01||Math.abs(dimensions.height-height)>.01) {dimensions=EntityDimensions.changing(width,height);calculateDimensions();}
    }
    void guards(JsonObject data) {
        nailBlock=data.has("nailBlock")?data.get("nailBlock").getAsInt():data.has("invulnerable")&&data.get("invulnerable").getAsBoolean()?15:0;
        spellBlock=data.has("spellBlock")?data.get("spellBlock").getAsInt():nailBlock;
        phaseFloor=data.has("phaseFloor")?Math.max(0,Math.min(getMaxHealth(),data.get("phaseFloor").getAsFloat())):0;
    }
    boolean resource() {return kind.equals("resource");}
    int actorId() {return actorId;}
    void nativePoint(net.minecraft.util.math.Vec3d point) {previousNative=currentNative==null?point:currentNative;currentNative=point;}
    void clearMotion() {previousNative=currentNative;}
    boolean crossed(net.minecraft.util.math.Box box) {
        if(previousNative==null||currentNative==null)return false;
        var half=new net.minecraft.util.math.Vec3d(0,getHeight()/2,0);
        return box.expand(getWidth()/2,getHeight()/2,0).raycast(previousNative.add(half),currentNative.add(half)).isPresent();
    }
    @Override public boolean isAttackable() {return !"npc".equals(kind)&&super.isAttackable();}
    void combatState(JsonObject value) {
        value.addProperty("blockSerial",blockSerial);value.addProperty("blockedAngle",blockedAngle);
        value.addProperty("blockedSpell",blockedSpell);value.addProperty("damageAngle",damageAngle);
    }
    @Override public void checkDespawn() {} // Only the native scene owns this entity's lifetime.
    @Override protected void updatePostDeath() {
        // A special native death can expose a weak point or refill a phase. Keep
        // the dead proxy until HK acknowledges death or replaces its generation.
        if(deathConfirmed)super.updatePostDeath();
    }
    @Override public boolean isInvulnerableTo(DamageSource source) {
        if(kind.equals("npc"))return true;
        if(super.isInvulnerableTo(source))return true;
        var origin=source.getPosition();var direct=source.getSource();
        var direction=direct instanceof net.minecraft.entity.projectile.ProjectileEntity projectile&&projectile.getVelocity().lengthSquared()>.000001
            ?projectile.getVelocity():direct instanceof net.minecraft.entity.player.PlayerEntity player
            ?new net.minecraft.util.math.Vec3d(-Math.sin(Math.toRadians(player.getYaw())),0,0)
            :origin==null?net.minecraft.util.math.Vec3d.ZERO:getBoundingBox().getCenter().subtract(origin);
        candidateAngle=(float)Math.toDegrees(Math.atan2(direction.y,direction.x));
        if(zenithAngle!=null)candidateAngle=zenithAngle;
        // HK's DirectionUtils: right/up/left/down = 0/1/2/3.
        int cardinal=Math.floorMod((int)Math.rint(candidateAngle/90.0),4);
        String name=source.getName();
        boolean spell=switch(name) {
            case "arrow","trident","thrown","magic","indirectMagic","explosion","explosion.player","fireworks","inFire","onFire","lava","hotFloor","wither" -> true;
            default -> false;
        };
        if(((spell?spellBlock:nailBlock)&(1<<cardinal))==0)return false;
        if(!resource()) {blockSerial++;blockedAngle=candidateAngle;blockedSpell=spell;}
        return true;
    }
    @Override public boolean damage(DamageSource source,float amount) {
        if(phaseFloor>0&&getHealth()<=phaseFloor+.0001f)return false;
        boolean accepted;applyingDamage=true;
        try {accepted=super.damage(source,amount);}finally{applyingDamage=false;}
        if(accepted)damageAngle=candidateAngle;
        return accepted;
    }
    String bridgeName() {return name;}
    @Override public void setHealth(float health) {
        if(applyingDamage&&phaseFloor>0)health=Math.max(health,Math.min(getHealth(),phaseFloor));
        // Cleave multipliers can leave sub-millipoint float residue. Normalize only
        // native proxies; LivingEntity.damage still runs its original death/loot path.
        super.setHealth(health>0&&health<.0001f?0:health);
    }
    @Override public EntityDimensions getDimensions(EntityPose pose) {return dimensions==null?super.getDimensions(pose):dimensions;}
    @Override public double getHeightOffset() {
        // Native sprites have no bent humanoid legs. Zombie's -0.45 offset plus
        // the wooden boat's -0.1 offset otherwise buries a crawler under the hull.
        if(getVehicle() instanceof net.minecraft.entity.vehicle.BoatEntity boat)return boat.getHeight()*.75-boat.getMountedHeightOffset();
        return super.getHeightOffset();
    }
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
