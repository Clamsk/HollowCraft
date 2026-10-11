package dev.hollowcraft;

import java.util.*;
import net.minecraft.entity.projectile.*;

/** One native sweep permits one vanilla tick. All access is on the server thread. */
final class NativeProjectileSteps {
    private static final Map<UUID,Integer> permits=new LinkedHashMap<>() {
        @Override protected boolean removeEldestEntry(Map.Entry<UUID,Integer> entry) {return size()>2048;}
    };
    static boolean required(ProjectileEntity projectile) {
        return HollowCraftBridge.projectileStepsEnabled()&&NativeTerrainBlock.delegated(projectile)
            &&!projectile.isRemoved()&&!projectile.getCommandTags().contains("hollowcraft_native_anchor")
            &&!(projectile instanceof PersistentProjectileEntity arrow&&(arrow.isNoClip()||((dev.hollowcraft.mixin.ArrowAccess)arrow).hollowcraft$inGround()));
    }
    static boolean tick(ProjectileEntity projectile) {
        Integer age=permits.remove(projectile.getUuid());
        return !required(projectile)||age!=null&&age==projectile.age;
    }
    static void allow(ProjectileEntity projectile,int age) {
        if(required(projectile)&&age==projectile.age)permits.put(projectile.getUuid(),age);
    }
    static void revoke(ProjectileEntity projectile) {permits.remove(projectile.getUuid());}
    static void clear() {permits.clear();}
}
