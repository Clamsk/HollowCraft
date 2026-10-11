package dev.hollowcraft;

import com.google.gson.*;
import net.minecraft.entity.projectile.*;
import net.minecraft.entity.FlyingItemEntity;
import net.minecraft.item.*;
import net.minecraft.registry.Registries;
import net.minecraft.server.network.ServerPlayerEntity;
import net.minecraft.util.Hand;
import net.minecraft.util.ActionResult;
import net.minecraft.util.math.*;
import java.util.*;

/** Use vanilla item actions and entities; damage, enchantments and potion effects remain server-owned. */
final class ItemUseBridge {
    private static final Map<UUID,Vec3d> anchors=new HashMap<>();
    static void anchor(PersistentProjectileEntity arrow,Vec3d point) {
        anchors.put(arrow.getUuid(),point);arrow.addCommandTag("hollowcraft_native_anchor");
        arrow.setVelocity(Vec3d.ZERO);arrow.setNoGravity(true);((dev.hollowcraft.mixin.ArrowAccess)arrow).hollowcraft$inGround(true);
    }
    static void clearAnchors() {anchors.clear();}
    static void use(ServerPlayerEntity player,JsonObject command,BlockPos target) {
        if(command.has("cancel")&&command.get("cancel").getAsBoolean()) {player.clearActiveItem();return;}
        if(!aim(player,command))return;
        if(command.has("release")&&command.get("release").getAsBoolean()) {player.stopUsingItem();return;}
        if(!command.has("start")||!command.get("start").getAsBoolean()||player.isUsingItem()||!player.isAlive()) return;
        for(var hand:Hand.values()) {
            var result=air(player,hand,target);
            if(result.isAccepted()) {if(result.shouldSwingHand())player.swingHand(hand,true);return;}
        }
    }
    static boolean aim(ServerPlayerEntity player,JsonObject command) {
        double dx=command.has("aimX")?command.get("aimX").getAsDouble():1;
        double dy=command.has("aimY")?command.get("aimY").getAsDouble():0;
        if(!Double.isFinite(dx)||!Double.isFinite(dy)) return false;
        player.setYaw(dx>=0?-90:90);player.setPitch((float)-Math.toDegrees(Math.atan2(dy,Math.abs(dx))));player.setHeadYaw(player.getYaw());
        return true;
    }
    static ActionResult air(ServerPlayerEntity player,Hand hand,BlockPos target) {
        var stack=player.getStackInHand(hand);
        if(stack.isEmpty()||!stack.isItemEnabled(player.getWorld().getEnabledFeatures())||player.getItemCooldownManager().isCoolingDown(stack.getItem()))return ActionResult.PASS;
        if(stack.getItem() instanceof BucketItem)return FluidBridge.bucket(player,stack,hand,target);
        return player.interactionManager.interactItem(player,player.getServerWorld(),stack,hand);
    }
    private static Box region(int offset,int x,int y) {return new Box(offset+x-28,64+y-24,-4,offset+x+28,64+y+24,4);}
    static void tick(ServerPlayerEntity player,int offset,int x,int y) {
        anchors.entrySet().removeIf(entry->player.getServerWorld().getEntity(entry.getKey())==null);
        for(var projectile:player.getServerWorld().getEntitiesByClass(ProjectileEntity.class,region(offset,x,y),e->true)) {
            if(projectile instanceof PersistentProjectileEntity arrow&&arrow.getCommandTags().contains("hollowcraft_native_anchor")) {
                if(arrow instanceof TridentEntity&&arrow.isNoClip()) {anchors.remove(arrow.getUuid());arrow.removeScoreboardTag("hollowcraft_native_anchor");arrow.setNoGravity(false);}
                else {var point=anchors.computeIfAbsent(arrow.getUuid(),id->arrow.getPos());arrow.setPosition(point);arrow.setVelocity(Vec3d.ZERO);arrow.setNoGravity(true);((dev.hollowcraft.mixin.ArrowAccess)arrow).hollowcraft$inGround(true);}
            }
            projectile.setPosition(projectile.getX(),projectile.getY(),.5);
            projectile.setVelocity(projectile.getVelocity().multiply(1,1,0));
            // Keep arrows pickable, including ammunition fired by the native scene's player.
            if(projectile instanceof PersistentProjectileEntity arrow&&arrow.getBoundingBox().intersects(player.getBoundingBox().expand(.25,.5,.25))) arrow.onPlayerCollision(player);
        }
        for(var block:player.getServerWorld().getEntitiesByClass(net.minecraft.entity.FallingBlockEntity.class,region(offset,x,y),e->true)) {block.setPosition(block.getX(),block.getY(),.5);block.setVelocity(block.getVelocity().multiply(1,1,0));}
    }
    static JsonArray snapshot(ServerPlayerEntity player,int offset,int x,int y) {
        JsonArray result=new JsonArray();
        for(var projectile:player.getServerWorld().getEntitiesByClass(ProjectileEntity.class,region(offset,x,y),e->true)) {
            result.add(projectile(projectile,offset));
        }
        for(var block:player.getServerWorld().getEntitiesByClass(net.minecraft.entity.FallingBlockEntity.class,region(offset,x,y),e->true)) {
            JsonObject value=new JsonObject();value.addProperty("entity",block.getId());value.addProperty("kind","minecraft:falling_block");value.addProperty("item",Registries.ITEM.getId(block.getBlockState().getBlock().asItem()).toString());value.addProperty("x",block.getX()-offset);value.addProperty("y",block.getY()-64+.5);value.addProperty("vx",block.getVelocity().x);value.addProperty("vy",block.getVelocity().y);result.add(value);
        }
        return result;
    }
    static JsonObject projectile(ProjectileEntity projectile,int offset) {
        JsonObject value=new JsonObject();value.addProperty("entity",projectile.getId());value.addProperty("kind",Registries.ENTITY_TYPE.getId(projectile.getType()).toString());
        value.addProperty("uuid",projectile.getUuidAsString());
        value.addProperty("age",projectile.age);value.addProperty("stepRequired",NativeProjectileSteps.required(projectile));
        value.addProperty("x",projectile.getX()-offset);value.addProperty("y",projectile.getY()-64);value.addProperty("vx",projectile.getVelocity().x);value.addProperty("vy",projectile.getVelocity().y);
        boolean grounded=projectile instanceof PersistentProjectileEntity arrow&&((dev.hollowcraft.mixin.ArrowAccess)arrow).hollowcraft$inGround();
        value.addProperty("grounded",grounded);
        if(projectile instanceof FireworkRocketEntity firework) {
            var motion=firework.getVelocity();var access=(dev.hollowcraft.mixin.FireworkAccess)firework;
            if(!access.hollowcraft$attached()&&!firework.wasShotAtAngle())motion=motion.multiply(firework.horizontalCollision?1:1.15,1,firework.horizontalCollision?1:1.15).add(0,.04,0);
            value.addProperty("nextVx",motion.x);value.addProperty("nextVy",motion.y);value.addProperty("attached",access.hollowcraft$attached());
        }
        value.addProperty("nativeCollision",NativeTerrainBlock.delegated(projectile));
        value.addProperty("noClip",projectile instanceof PersistentProjectileEntity arrow&&arrow.isNoClip());
        String item=projectile instanceof FlyingItemEntity flying?PlayerBridge.renderKey(flying.getStack()):projectile instanceof TridentEntity?"minecraft:trident":"minecraft:arrow";
        value.addProperty("item",item);return value;
    }
}
