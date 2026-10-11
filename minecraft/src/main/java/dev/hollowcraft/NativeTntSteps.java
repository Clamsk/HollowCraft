package dev.hollowcraft;

import java.util.*;
import net.minecraft.block.Blocks;
import net.minecraft.entity.TntEntity;
import net.minecraft.util.math.Vec3d;

/** A native hull sweep approves one vanilla TNT tick; inventory, fuse and explosion stay in MC. */
public final class NativeTntSteps {
    private record Step(int age,double x,double y,double lift) { }
    private record Move(Step step,Vec3d requested) { }
    private static final Map<UUID,Step> permits=new LinkedHashMap<>() {
        @Override protected boolean removeEldestEntry(Map.Entry<UUID,Step> entry) {return size()>2048;}
    };
    private static final Map<UUID,Move> active=new HashMap<>();
    public static boolean required(TntEntity tnt) {return !tnt.isRemoved()&&HollowCraftBridge.nativeTnt(tnt);}
    static Vec3d next(TntEntity tnt) {return tnt.getVelocity().multiply(1,1,0).add(0,tnt.hasNoGravity()?0:-.04,0);}
    public static boolean tick(TntEntity tnt) {
        Step step=permits.remove(tnt.getUuid());active.remove(tnt.getUuid());
        if(!required(tnt))return true;
        if(step==null||step.age!=tnt.age)return false;
        tnt.setPosition(tnt.getX(),tnt.getY(),.5);tnt.setVelocity(tnt.getVelocity().multiply(1,1,0));
        active.put(tnt.getUuid(),new Move(step,null));return true;
    }
    static void allow(TntEntity tnt,int age,double x,double y,double lift) {
        if(!required(tnt)||age!=tnt.age||!Double.isFinite(x)||!Double.isFinite(y)||!Double.isFinite(lift)||lift<0||lift>1)return;
        Vec3d requested=next(tnt);
        if(!within(x,requested.x)||!within(y,requested.y))return;
        permits.put(tnt.getUuid(),new Step(age,x,y,lift));
    }
    private static boolean within(double value,double requested) {return Math.abs(value)<=Math.abs(requested)+.0001&&value*requested>=-.000001;}
    public static Vec3d limit(TntEntity tnt,Vec3d requested) {
        if(tnt.getWorld().isClient)return requested;
        Move move=active.get(tnt.getUuid());if(move==null)return requested;
        Step step=move.step;
        active.put(tnt.getUuid(),new Move(step,requested));
        if(step.lift>0)tnt.setPosition(tnt.getX(),tnt.getY()+step.lift,.5);
        // An explosion or another entity can alter velocity after approval. Never let
        // an older sweep approve a longer displacement in that case.
        return new Vec3d(within(step.x,requested.x)?step.x:0,within(step.y,requested.y)?step.y:0,0);
    }
    public static void finish(TntEntity tnt) {
        if(tnt.getWorld().isClient)return;
        Move move=active.remove(tnt.getUuid());if(move==null||move.requested==null)return;
        boolean x=Math.abs(move.step.x-move.requested.x)>.00001,y=Math.abs(move.step.y-move.requested.y)>.00001;
        if(x) {tnt.horizontalCollision=true;tnt.setVelocity(tnt.getVelocity().multiply(0,1,0));}
        if(y) {
            tnt.verticalCollision=true;
            if(move.requested.y<0) {tnt.groundCollision=true;tnt.setOnGround(true);}
            // Same landing callback as a vanilla solid floor, before TNT's own drag.
            Blocks.STONE.onEntityLand(tnt.getWorld(),tnt);
        }
    }
    static void clear() {permits.clear();active.clear();}
}
