package dev.hollowcraft;

import com.google.common.collect.ImmutableMultimap;
import com.google.common.collect.Multimap;
import com.google.gson.*;
import java.util.*;
import net.minecraft.entity.*;
import net.minecraft.entity.attribute.*;
import net.minecraft.item.*;
import net.minecraft.registry.*;
import net.minecraft.server.network.ServerPlayerEntity;
import net.minecraft.util.*;
import net.minecraft.util.math.*;

/** Opt-in Terraria equipment. The server owns trajectories, hit budgets and inventory. */
public final class TerrariaEasterEgg {
    public static final Item ZENITH=new Item(new Item.Settings().maxCount(1).rarity(Rarity.EPIC).fireproof()) {
        @Override public Multimap<EntityAttribute,EntityAttributeModifier> getAttributeModifiers(EquipmentSlot slot) {
            if(slot!=EquipmentSlot.MAINHAND)return super.getAttributeModifiers(slot);
            return ImmutableMultimap.of(EntityAttributes.GENERIC_ATTACK_DAMAGE,new EntityAttributeModifier(ATTACK_DAMAGE_MODIFIER_ID,"Zenith damage",189,EntityAttributeModifier.Operation.ADDITION),
                EntityAttributes.GENERIC_ATTACK_SPEED,new EntityAttributeModifier(ATTACK_SPEED_MODIFIER_ID,"Zenith speed",-2,EntityAttributeModifier.Operation.ADDITION));
        }
    };
    // ElytraItem gives vanilla chest-slot placement/quick-equip. No durability or armor bonus.
    public static final Item WINGS=new ElytraItem(new Item.Settings().maxCount(1).rarity(Rarity.EPIC).fireproof());
    public static void register() {
        Registry.register(Registries.ITEM,id("zenith"),ZENITH);Registry.register(Registries.ITEM,id("stardust_wings"),WINGS);
    }
    public static Identifier id(String path) {return new Identifier("hollowcraft_bridge",path);}
    public static boolean wings(LivingEntity player) {return player.getEquippedStack(EquipmentSlot.CHEST).isOf(WINGS);}
    static String grant(ServerPlayerEntity player) {
        int free=0;for(int i=0;i<36;i++)if(player.getInventory().getStack(i).isEmpty())free++;
        if(free<2)return "Clamsk: free two inventory slots, then try again / 请先腾出两个背包格";
        player.getInventory().insertStack(new ItemStack(ZENITH));player.getInventory().insertStack(new ItemStack(WINGS));
        player.getInventory().markDirty();player.playerScreenHandler.sendContentUpdates();
        return "Clamsk: Zenith + Stardust Wings / 已获得天顶剑、星尘之翼（胸甲栏）";
    }
    private static final double PIXEL=.0375;
    private final List<Flight> flights=new ArrayList<>();
    private boolean held;private double aimX,aimY;private int inputTick=-100,clock,serial,burst;
    void clear() {flights.clear();held=false;inputTick=-100;clock=burst=0;}
    void input(ServerPlayerEntity player,JsonObject command,int offset) {
        if(!player.isAlive()||!player.getMainHandStack().isOf(ZENITH)||player.hasVehicle()||player.isUsingItem()) {held=false;return;}
        boolean down=command.has("held")&&command.get("held").getAsBoolean();
        if(!down){held=false;return;}
        double x=command.get("x").getAsDouble(),y=command.get("y").getAsDouble();
        if(!Double.isFinite(x)||!Double.isFinite(y))return;
        // Full visible-screen aiming, with a hard protocol limit independent of melee reach.
        var delta=new Vec3d(x-(player.getX()-offset),y-(player.getY()-64+1),0);
        if(delta.lengthSquared()>48*48)delta=delta.normalize().multiply(48);
        aimX=player.getX()+delta.x;aimY=player.getY()+1+delta.y;
        if(!held&&burst==0){clock=0;burst=60;}held=true;inputTick=player.getServer().getTicks();
    }
    void tick(ServerPlayerEntity player) {
        if(!player.isAlive()||player.hasVehicle()) {clear();return;}
        if(!player.getMainHandStack().isOf(ZENITH)||player.isUsingItem()) {held=false;burst=0;}
        if(player.getServer().getTicks()-inputTick>6)held=false;
        // Terraria runs 60 Hz with one extra projectile update; six substeps per MC tick.
        for(int step=0;step<6;step++) {
            if((held||burst>0)&&clock%20==0&&flights.size()<24) {
                double dx=aimX-player.getX(),dy=aimY-player.getY()-1;
                if(dx*dx+dy*dy<.01)dx=.12;
                double spin=(player.getRandom().nextBoolean()?1:-1)*(.65+player.getRandom().nextDouble()*.35);
                int style=clock%60==0?0:1+player.getRandom().nextInt(10);
                flights.add(new Flight(++serial,dx,dy,spin,style));player.swingHand(Hand.MAIN_HAND);
            }
            if(held||burst>0) {clock++;if(burst>0)burst--;if(held&&clock%60==0)burst=60;}
            for(var it=flights.iterator();it.hasNext();) {
                var f=it.next();double old=f.age;f.age+=f.rate;
                if(f.age>=120){it.remove();continue;}
                if(f.age>60)continue;
                Vec3d a=f.point(player,old),b=f.point(player,f.age);
                var box=new Box(a,b).expand(1.5,1.5,1);
                for(var target:player.getServerWorld().getEntitiesByClass(LivingEntity.class,box,e->e!=player&&e.isAlive()&&e.isAttackable())) {
                    if(f.nextHit.getOrDefault(target.getId(),-100)>f.steps)continue;
                    var bounds=target.getBoundingBox().expand(.55,.55,.1);
                    if(!bounds.contains(a)&&!bounds.contains(b)&&bounds.raycast(a,b).isEmpty())continue;
                    f.nextHit.put(target.getId(),f.steps+7); // Terraria: 15 / MaxUpdates = 7 local ticks.
                    float damage=190*(player.getRandom().nextFloat()<.14f?2:1);
                    int regen=target.timeUntilRegen;target.timeUntilRegen=0;
                    float angle=(float)Math.toDegrees(Math.atan2(b.y-a.y,b.x-a.x));
                    if(target instanceof NativeProxy nativeEnemy)nativeEnemy.zenithAngle=angle;
                    boolean hit;
                    try {hit=target.damage(player.getDamageSources().playerAttack(player),damage);}
                    finally {if(target instanceof NativeProxy nativeEnemy)nativeEnemy.zenithAngle=null;target.timeUntilRegen=regen;}
                    if(hit) {target.takeKnockback(.65,player.getX()-target.getX(),0);player.onAttacking(target);}
                }
                f.steps++;
            }
        }
    }
    JsonArray snapshot() {var values=new JsonArray();for(var f:flights) {var v=new JsonObject();v.addProperty("id",f.id);v.addProperty("dx",f.dx);v.addProperty("dy",f.dy);v.addProperty("spin",f.spin);v.addProperty("style",f.style);v.addProperty("age",f.age);v.addProperty("rate",f.rate);values.add(v);}return values;}
    private static final class Flight {
        final int id,style;final double dx,dy,spin,rate;double age;int steps;
        final Map<Integer,Integer> nextHit=new HashMap<>();
        Flight(int id,double dx,double dy,double spin,int style) {this.id=id;this.dx=dx;this.dy=dy;this.spin=spin;this.style=style;rate=.7+1.3*MathHelper.clamp(1-Math.hypot(dx,dy)*2/(900*PIXEL),0,1);}
        Vec3d point(ServerPlayerEntity player,double age) {
            double p=MathHelper.clamp(age/60,0,1),length=Math.hypot(dx,dy),lerp=MathHelper.clamp(1-length*2/(900*PIXEL),0,1);
            // Compress only the minimum loop at body-range aim points, so a small
            // native flyer overlapping Steve cannot sit inside a hollow attack ring.
            double minimum=Math.min(60*PIXEL,Math.max(.6,length));
            double angle=Math.PI+Math.signum(dx)*p*Math.PI*2,radius=Math.max(minimum,length+40*PIXEL*MathHelper.clamp((p-.5)*2,0,1));
            double x=Math.cos(angle)*radius,y=Math.sin(angle)*spin*(2-lerp),rot=Math.atan2(dy,dx);
            return new Vec3d(player.getX()+dx+x*Math.cos(rot)-y*Math.sin(rot)+(1-MathHelper.clamp((p-.5)*2,0,1))*(-Math.signum(dx)*radius*.1),
                player.getY()+1+dy+x*Math.sin(rot)+y*Math.cos(rot)+(1-MathHelper.clamp((p-.5)*2,0,1))*(-spin*.3*PIXEL),.5);
        }
    }
}
