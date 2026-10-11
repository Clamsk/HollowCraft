package dev.hollowcraft;

import com.google.gson.*;
import dev.hollowcraft.mixin.LivingEntityAccess;
import java.util.*;
import net.minecraft.entity.*;
import net.minecraft.entity.mob.ZombieEntity;
import net.minecraft.entity.attribute.EntityAttributes;
import net.minecraft.server.network.ServerPlayerEntity;
import net.minecraft.util.math.Vec3d;

/** Vanilla living entities own all combat arithmetic; HK owns movement, attacks and death animation. */
final class EnemyBridge {
    private final Map<Integer,ZombieEntity> enemies=new HashMap<>();
    private final Map<Integer,Vec3d> positions=new HashMap<>();
    private final Map<Integer,Integer> generations=new HashMap<>();
    void clear() {for(var enemy:enemies.values()) enemy.discard();enemies.clear();positions.clear();generations.clear();}
    void observe(ServerPlayerEntity player,JsonArray entries,int offset) {
        Set<Integer> present=new HashSet<>();
        for(var entry:entries) {
            var data=entry.getAsJsonObject();int id=data.get("id").getAsInt();
            present.add(id);
            var point=new Vec3d(offset+data.get("x").getAsDouble(),64+data.get("y").getAsDouble(),.5);
            var entity=enemies.get(id);
            int generation=data.has("generation")?data.get("generation").getAsInt():0;
            if(entity!=null&&generations.getOrDefault(id,0)!=generation) {entity.discard();enemies.remove(id);entity=null;}
            if(entity==null) {
                entity=new NativeProxy(player.getServerWorld(),data);
                entity.setAiDisabled(true);entity.setSilent(true);entity.setNoGravity(true);entity.setPersistent();
                entity.noClip=true;
                entity.addCommandTag("hollowcraft_proxy");
                entity.getAttributeInstance(EntityAttributes.GENERIC_MAX_HEALTH).setBaseValue(data.get("maxHealth").getAsDouble());
                entity.getAttributeInstance(EntityAttributes.GENERIC_ARMOR).setBaseValue(0);
                entity.setHealth(data.has("health")?data.get("health").getAsFloat():entity.getMaxHealth());enemies.put(id,entity);generations.put(id,generation);
                entity.setPosition(point);
                player.getServerWorld().getChunk(entity.getBlockPos());
                player.getServerWorld().spawnEntity(entity);
            }
            ((NativeProxy)entity).size(data);
            double maximum=data.get("maxHealth").getAsDouble();
            entity.setInvulnerable(data.has("invulnerable")&&data.get("invulnerable").getAsBoolean());
            if(Math.abs(entity.getMaxHealth()-maximum)>.001) {float fraction=entity.getHealth()/entity.getMaxHealth();entity.getAttributeInstance(EntityAttributes.GENERIC_MAX_HEALTH).setBaseValue(maximum);entity.setHealth((float)maximum*fraction);}
            positions.put(id,point);entity.setPosition(point);entity.setVelocity(Vec3d.ZERO);
        }
        for(int id:new ArrayList<>(enemies.keySet())) if(!present.contains(id)) {enemies.remove(id).discard();positions.remove(id);generations.remove(id);}
    }
    void tick() {
        for(var entry:enemies.entrySet()) {
            var entity=entry.getValue();entity.setPosition(positions.get(entry.getKey()));entity.setVelocity(Vec3d.ZERO);
        }
    }
    JsonArray snapshot() {
        JsonArray result=new JsonArray();
        for(var entry:enemies.entrySet()) {
            JsonObject value=new JsonObject();value.addProperty("id",entry.getKey());
            value.addProperty("generation",generations.getOrDefault(entry.getKey(),0));
            value.addProperty("health",entry.getValue().getHealth());value.addProperty("maxHealth",entry.getValue().getMaxHealth());result.add(value);
            value.addProperty("x",entry.getValue().getX());value.addProperty("y",entry.getValue().getY());value.addProperty("invulnerable",entry.getValue().isInvulnerable());
            value.addProperty("moveFactor",entry.getValue().getAttributeValue(EntityAttributes.GENERIC_MOVEMENT_SPEED)/.23);value.addProperty("onFire",entry.getValue().isOnFire());
            var source=entry.getValue().getRecentDamageSource();if(source!=null) value.addProperty("damageKind",source.getName());
        }
        return result;
    }
    LivingEntity source(int id) {return enemies.get(id);}
    void confirmDeath(int id,int generation) {var entity=enemies.get(id);if(entity instanceof NativeProxy proxy&&generations.getOrDefault(id,-1)==generation) proxy.confirmDeath();}
    boolean owns(net.minecraft.entity.Entity entity) {return enemies.containsValue(entity);}
    void hit(ServerPlayerEntity player,JsonArray targets,boolean falling,double multiplier) {
        boolean ground=player.isOnGround();float fall=player.fallDistance;
        var attack=player.getAttributeInstance(EntityAttributes.GENERIC_ATTACK_DAMAGE);double base=attack.getBaseValue();
        var clock=(LivingEntityAccess)player;int chargedTicks=clock.hollowcraft$getAttackTicks();
        try {
            player.setOnGround(!falling && ground);player.fallDistance=falling ? 1 : 0;
            org.slf4j.LoggerFactory.getLogger("HollowCraft").info("Combat impact falling={} cooldown={} targets={}",falling,player.getAttackCooldownProgress(.5f),targets);
            attack.setBaseValue(base+player.getAttributeValue(EntityAttributes.GENERIC_ATTACK_DAMAGE)*(multiplier-1));
            // MC's attack method includes cooldown, criticals, enchantments, armor, durability and knockback.
            // Each cleaved target receives the same fully charged swing.
            for(var target:targets) {
                var enemy=enemies.get(target.getAsInt());if(enemy==null || !enemy.isAlive()) continue;
                var eye=player.getEyePos();var bounds=enemy.getBoundingBox();double reach=player.isCreative()?6:3;
                var closest=new Vec3d(Math.max(bounds.minX,Math.min(bounds.maxX,eye.x)),Math.max(bounds.minY,Math.min(bounds.maxY,eye.y)),Math.max(bounds.minZ,Math.min(bounds.maxZ,eye.z)));
                if(eye.squaredDistanceTo(closest)>reach*reach+.001)continue;
                clock.hollowcraft$setAttackTicks(chargedTicks);
                player.attack(enemy);
            }
        } finally {attack.setBaseValue(base);player.setOnGround(ground);player.fallDistance=fall;}
    }
}
