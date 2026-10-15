package dev.hollowcraft;

import com.google.gson.*;
import java.util.*;
import net.minecraft.entity.*;
import net.minecraft.entity.mob.MobEntity;
import net.minecraft.entity.vehicle.*;
import net.minecraft.item.*;
import net.minecraft.registry.Registries;
import net.minecraft.server.network.ServerPlayerEntity;
import net.minecraft.server.world.ServerWorld;
import net.minecraft.util.*;
import net.minecraft.util.math.*;

/** Actual vanilla boats and mobs, constrained to the sampled native XY simulation plane. */
public final class CreatureBridge {
    private static final String TAG="hollowcraft_creature";
    private static final Map<UUID,Entity> entities=new LinkedHashMap<>();
    private static volatile Set<UUID> bridgeBoats=Set.of();
    private static final net.minecraft.server.world.ChunkTicketType<UUID> BOAT_TICKET=net.minecraft.server.world.ChunkTicketType.create("hollowcraft_boat",Comparator.<UUID>naturalOrder(),100);
    private static final Map<UUID,ChunkPos> tickets=new HashMap<>();
    private static ServerPlayerEntity owner;
    private static int offset;
    private static boolean terrainReady;
    private static boolean spawning;
    private static String placementFailure="";
    static String placementFailure() {return placementFailure;}
    static void configure(ServerPlayerEntity player,int base) {
        owner=player;offset=base;entities.clear();bridgeBoats=Set.of();terrainReady=false;
        player.getServerWorld().setTimeOfDay(18000);
        player.getServerWorld().getGameRules().get(net.minecraft.world.GameRules.DO_DAYLIGHT_CYCLE).set(false,player.getServer());
        for(var entity:player.getServerWorld().getEntitiesByClass(Entity.class,new Box(base-401,3,-4,base+401,307,4),e->e instanceof MobEntity||e instanceof BoatEntity))register(entity);
    }
    static void clear() {
        if(owner!=null)for(var entry:tickets.entrySet())owner.getServerWorld().getChunkManager().removeTicket(BOAT_TICKET,entry.getValue(),2,entry.getKey());
        tickets.clear();owner=null;entities.clear();bridgeBoats=Set.of();terrainReady=false;
    }
    static void terrainReady() {terrainReady=true;}
    static void playerChanged(ServerPlayerEntity player) {owner=player;}
    static void register(Entity entity) {
        if(owner==null||entity instanceof NativeProxy||entity.getCommandTags().contains("hollowcraft_proxy")||entity.getWorld()!=owner.getWorld()
            ||!(entity instanceof MobEntity||entity instanceof BoatEntity)||Math.abs(entity.getX()-offset)>400||Math.abs(entity.getZ()-.5)>4)return;
        entity.addCommandTag(TAG);entities.put(entity.getUuid(),entity);
        if(entity instanceof BoatEntity&&!bridgeBoats.contains(entity.getUuid())) {var copy=new HashSet<>(bridgeBoats);copy.add(entity.getUuid());bridgeBoats=Set.copyOf(copy);}
        if(entity instanceof MobEntity mob)mob.setPersistent();
        entity.setPosition(entity.getX(),entity.getY(),.5);
    }
    public static boolean plane(Entity entity) {return entity.getWorld()!=null&&!entity.getWorld().isClient&&HollowCraftBridge.bridgeWorld(entity)
        &&(entity.getCommandTags().contains(TAG)||spawning&&(entity instanceof MobEntity||entity instanceof BoatEntity));}
    public static boolean authoritativeBoat(Entity entity) {return entity instanceof BoatEntity&&HollowCraftBridge.remoteActive()&&bridgeBoats.contains(entity.getUuid());}
    public static boolean dormant(Entity entity) {
        if(!plane(entity))return false;
        return owner==null||!terrainReady||!HollowCraftBridge.remoteActive()||entity.getWorld()!=owner.getWorld()
            ||Math.abs(entity.getX()-owner.getX())>24||Math.abs(entity.getY()-owner.getY())>17;
    }
    static Collection<Entity> active() {return entities.values();}
    private static void prune() {
        if(entities.entrySet().removeIf(entry->entry.getValue().isRemoved())) {
            var copy=new HashSet<UUID>();for(var entity:entities.values())if(entity instanceof BoatEntity)copy.add(entity.getUuid());bridgeBoats=Set.copyOf(copy);
        }
    }
    static void beforeTick() {
        prune();
        for(var entity:entities.values()) {
            if(dormant(entity))continue;
            if(Math.abs(entity.getZ()-.5)>.00001)entity.setPosition(entity.getX(),entity.getY(),.5);
            if(Math.abs(entity.getVelocity().z)>.00001)entity.setVelocity(entity.getVelocity().multiply(1,1,0));
            if(entity instanceof BoatEntity boat) {boat.setVelocity(0,boat.getVelocity().y,0);boat.setYaw(90);}
        }
    }
    static void afterTick(EnemyBridge natives) {
        beforeTick();
        Set<Integer> occupied=new HashSet<>();
        for(var entity:natives.entities())if(entity.hasVehicle())occupied.add(((NativeProxy)entity).actorId());
        for(var entity:entities.values()) {
            if(dormant(entity))continue;
            if(entity instanceof MobEntity mob) {
                float yaw=mob.getVelocity().x<-.001?90:mob.getVelocity().x>.001?-90:mob.getX()>owner.getX()?90:-90;
                mob.setYaw(yaw);mob.bodyYaw=yaw;mob.setHeadYaw(yaw);
            }
            if(!(entity instanceof BoatEntity boat)||boat.isRemoved()||boat.isSubmergedInWater())continue;
            int capacity=boat instanceof ChestBoatEntity?1:2;
            for(var nativeEntity:natives.entities()) {
                if(boat.getPassengerList().size()>=capacity)break;
                var proxy=(NativeProxy)nativeEntity;
                if(!nativeEntity.isAlive()||nativeEntity.hasVehicle()||proxy.resource()||occupied.contains(proxy.actorId()))continue;
                if(overlap(boat.getBoundingBox().expand(.15,.2,.15),nativeEntity.getBoundingBox())||proxy.crossed(boat.getBoundingBox().expand(.15,.2,.15))) {
                    if(nativeEntity.startRiding(boat,true))occupied.add(proxy.actorId());
                }
            }
            for(var mob:entities.values()) {
                if(boat.getPassengerList().size()>=capacity)break;
                if(mob instanceof MobEntity living&&living.isAlive()&&!living.hasVehicle()&&overlap(boat.getBoundingBox().expand(.15,.2,.15),living.getBoundingBox()))living.startRiding(boat,true);
            }
            for(var passenger:boat.getPassengerList())boat.updatePassengerPosition(passenger);
        }
        for(var entity:natives.entities())((NativeProxy)entity).clearMotion();
        Set<UUID> held=new HashSet<>();
        for(var entity:natives.entities())if(entity.isAlive()&&entity.getVehicle() instanceof BoatEntity boat) {
            UUID id=boat.getUuid();ChunkPos pos=new ChunkPos(boat.getBlockPos());held.add(id);
            ChunkPos previous=tickets.put(id,pos);
            if(previous!=null&&!previous.equals(pos))owner.getServerWorld().getChunkManager().removeTicket(BOAT_TICKET,previous,2,id);
            owner.getServerWorld().getChunkManager().addTicket(BOAT_TICKET,pos,2,id);
        }
        for(var id:new ArrayList<>(tickets.keySet()))if(!held.contains(id))owner.getServerWorld().getChunkManager().removeTicket(BOAT_TICKET,tickets.remove(id),2,id);
    }
    private static boolean overlap(Box a,Box b) {return a.minX<b.maxX&&a.maxX>b.minX&&a.minY<b.maxY&&a.maxY>b.minY;}
    static ActionResult place(ServerPlayerEntity player,Hand hand,BlockPos pos) {
        ItemStack stack=player.getStackInHand(hand);var world=player.getServerWorld();
        if(!(stack.getItem() instanceof BoatItem||stack.getItem() instanceof SpawnEggItem))return ActionResult.PASS;
        placementFailure="";prune();
        if(pos==null) {placementFailure="Move the cursor closer (survival 4.5 blocks, creative 5 blocks)";return ActionResult.FAIL;}
        if(player.isSpectator()||!player.getAbilities().allowModifyWorld) {placementFailure="This game mode cannot place entities";return ActionResult.FAIL;}
        if(entities.size()>=160) {placementFailure="Room creature/boat limit reached (160)";return ActionResult.FAIL;}
        Item used=stack.getItem();var shape=world.getBlockState(pos).getCollisionShape(world,pos);
        double floor=shape.isEmpty()?pos.getY():pos.getY()+shape.getMax(Direction.Axis.Y);
        if(stack.getItem() instanceof BoatItem boatItem) {
            var access=(dev.hollowcraft.mixin.BoatItemAccess)boatItem;
            BoatEntity boat=access.hollowcraft$chest()?new ChestBoatEntity(world,pos.getX()+.5,floor+.005,.5):new BoatEntity(world,pos.getX()+.5,floor+.005,.5);
            boat.setVariant(access.hollowcraft$type());boat.setYaw(90);boat.addCommandTag(TAG);
            boat.setPosition(boat.getX(),boat.getY(),.5);
            if(!world.isSpaceEmpty(boat,boat.getBoundingBox())) {placementFailure="The boat needs a clear space 1.4 blocks wide above the clicked surface";return ActionResult.FAIL;}
            if(!world.spawnEntity(boat)) {placementFailure="Minecraft rejected this boat spawn";return ActionResult.FAIL;}register(boat);
        }else if(stack.getItem() instanceof SpawnEggItem egg) {
            // Vanilla's 3D spawn clearance must use the same thin plane as subsequent ticks.
            // Applying the plane only after spawn made large creatures collide with back walls.
            Entity entity;spawning=true;
            try {entity=egg.getEntityType(stack.getNbt()).spawnFromItemStack(world,stack,player,pos,SpawnReason.SPAWN_EGG,false,false);}
            finally {spawning=false;}
            if(entity==null) {placementFailure="Minecraft rejected this spawn egg";return ActionResult.FAIL;}
            register(entity);entity.setPosition(pos.getX()+.5,floor+.005,.5);
        }
        org.slf4j.LoggerFactory.getLogger("HollowCraft").info("Placed creature item={} at={},{} active={}",Registries.ITEM.getId(used),pos.getX()-offset,floor-64,entities.size());
        if(!player.isCreative())stack.decrement(1);
        player.incrementStat(net.minecraft.stat.Stats.USED.getOrCreateStat(used));
        return ActionResult.SUCCESS;
    }
    static ActionResult interact(ServerPlayerEntity player,Hand hand,BlockPos pos) {
        if(pos==null)return ActionResult.PASS;
        Vec3d point=new Vec3d(pos.getX()+.5,pos.getY()+.5,.5);
        Entity nearest=null;double distance=.9;
        for(var entity:entities.values()) {
            if(entity.isRemoved()||dormant(entity))continue;
            Box box=entity.getBoundingBox();
            double dx=point.x-MathHelper.clamp(point.x,box.minX,box.maxX),dy=point.y-MathHelper.clamp(point.y,box.minY,box.maxY);
            double value=dx*dx+dy*dy;
            if(value<distance) {nearest=entity;distance=value;}
        }
        if(nearest==null)return ActionResult.PASS;
        Vec3d eye=player.getEyePos();Box bounds=nearest.getBoundingBox();double reach=player.isCreative()?6:3;
        Vec3d closest=new Vec3d(MathHelper.clamp(eye.x,bounds.minX,bounds.maxX),MathHelper.clamp(eye.y,bounds.minY,bounds.maxY),.5);
        if(eye.squaredDistanceTo(closest)>reach*reach+.001)return ActionResult.PASS;
        if(player.isSneaking()&&!(nearest instanceof ChestBoatEntity))return ActionResult.PASS;
        return player.interact(nearest,hand);
    }
    static JsonArray snapshot() {
        JsonArray result=new JsonArray();
        if(owner==null)return result;
        for(var entity:entities.values()) {
            if(entity.isRemoved()||Math.abs(entity.getX()-owner.getX())>28||Math.abs(entity.getY()-owner.getY())>22)continue;
            JsonObject value=new JsonObject();value.addProperty("entity",entity.getId());value.addProperty("uuid",entity.getUuidAsString());
            value.addProperty("kind",Registries.ENTITY_TYPE.getId(entity.getType()).toString());
            value.addProperty("x",entity.getX()-offset);value.addProperty("y",entity.getY()-64);
            value.addProperty("width",entity.getWidth());value.addProperty("height",entity.getHeight());
            value.addProperty("health",entity instanceof LivingEntity living?living.getHealth():1);
            value.addProperty("boat",entity instanceof BoatEntity);result.add(value);
        }
        return result;
    }
    static Entity target(int id,String uuid) {
        if(owner==null)return null;Entity entity=owner.getServerWorld().getEntityById(id);
        return entity!=null&&plane(entity)&&!entity.isRemoved()&&entity.getUuidAsString().equals(uuid)?entity:null;
    }
    static Entity target(int id) {return owner==null?null:entities.values().stream().filter(e->e.getId()==id&&!e.isRemoved()).findFirst().orElse(null);}
    static void hit(ServerPlayerEntity player,JsonArray targets,boolean falling,double multiplier) {
        var clock=(dev.hollowcraft.mixin.LivingEntityAccess)player;int ticks=clock.hollowcraft$getAttackTicks();
        var attribute=player.getAttributeInstance(net.minecraft.entity.attribute.EntityAttributes.GENERIC_ATTACK_DAMAGE);
        double base=attribute.getBaseValue();boolean ground=player.isOnGround();float fall=player.fallDistance;
        try {
            attribute.setBaseValue(base+player.getAttributeValue(net.minecraft.entity.attribute.EntityAttributes.GENERIC_ATTACK_DAMAGE)*(multiplier-1));
            player.setOnGround(!falling&&ground);player.fallDistance=falling?1:0;
            for(var value:targets) {
                var data=value.getAsJsonObject();Entity entity=target(data.get("entity").getAsInt(),data.get("uuid").getAsString());
                if(entity==null)continue;
                var box=entity.getBoundingBox();var eye=player.getEyePos();
                var nearest=new Vec3d(MathHelper.clamp(eye.x,box.minX,box.maxX),MathHelper.clamp(eye.y,box.minY,box.maxY),.5);
                double reach=player.isCreative()?6:3;if(eye.squaredDistanceTo(nearest)>reach*reach+.001)continue;
                clock.hollowcraft$setAttackTicks(ticks);player.attack(entity);
            }
        }finally {attribute.setBaseValue(base);player.setOnGround(ground);player.fallDistance=fall;}
    }
}
