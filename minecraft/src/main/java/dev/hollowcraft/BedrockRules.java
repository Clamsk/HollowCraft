package dev.hollowcraft;

import net.minecraft.block.Blocks;
import net.minecraft.entity.Entity;
import net.minecraft.entity.damage.DamageSource;
import net.minecraft.registry.tag.DamageTypeTags;
import net.minecraft.util.hit.HitResult;
import net.minecraft.util.math.*;
import net.minecraft.world.*;

/** Bedrock is a two-dimensional barrier; a wide proxy must not expose an imaginary Z flank. */
public final class BedrockRules {
    private static boolean remote(Entity entity) {
        return !entity.getWorld().isClient&&(entity instanceof NativeProxy||CreatureBridge.plane(entity)||entity instanceof net.minecraft.entity.LivingEntity living&&HollowCraftBridge.isRemotePlayer(living));
    }
    public static boolean protectedBlock(net.minecraft.server.network.ServerPlayerEntity player,BlockPos pos) {
        return HollowCraftBridge.isRemotePlayer(player)&&!player.isCreative()&&player.getWorld().getBlockState(pos).isOf(Blocks.BEDROCK);
    }
    static boolean occupied(net.minecraft.server.world.ServerWorld world,BlockPos pos) {
        return !world.getEntitiesByClass(NativeProxy.class,new Box(pos),entity->entity.isAlive()&&!entity.resource()).isEmpty();
    }
    public static boolean blocksExplosion(Entity target,DamageSource source) {
        Vec3d origin=source.getPosition();
        if(!remote(target)||!source.isIn(DamageTypeTags.IS_EXPLOSION)||origin==null)return false;
        Box box=target.getBoundingBox();
        int nx=Math.min(32,Math.max(2,(int)Math.ceil((box.maxX-box.minX)*2+1)));
        int ny=Math.min(32,Math.max(2,(int)Math.ceil((box.maxY-box.minY)*2+1)));
        for(int x=0;x<=nx;x++)for(int y=0;y<=ny;y++)
            if(!between(target.getWorld(),origin,sample(box,x,y,nx,ny)))return false;
        return true;
    }
    public static Float exposure(Vec3d origin,Entity target) {
        if(!remote(target))return null;
        Box box=target.getBoundingBox();
        int nx=Math.min(32,Math.max(2,(int)Math.ceil((box.maxX-box.minX)*2+1)));
        int ny=Math.min(32,Math.max(2,(int)Math.ceil((box.maxY-box.minY)*2+1)));
        int clear=0,total=0;boolean bedrock=false;
        Vec3d end=new Vec3d(origin.x,origin.y,.5);
        try(var ignored=NativeTerrainQueries.forceSolid()) {
            for(int x=0;x<=nx;x++)for(int y=0;y<=ny;y++) {
                Vec3d start=sample(box,x,y,nx,ny);total++;
                boolean covered=between(target.getWorld(),end,start);bedrock|=covered;
                if(!covered&&target.getWorld().raycast(new RaycastContext(start,end,RaycastContext.ShapeType.COLLIDER,
                    RaycastContext.FluidHandling.NONE,target)).getType()==HitResult.Type.MISS)clear++;
            }
        }
        // Preserve vanilla's calculation unless bedrock actually intersects a blast ray.
        return bedrock?(float)clear/total:null;
    }
    private static Vec3d sample(Box box,int x,int y,int nx,int ny) {
        return new Vec3d(MathHelper.lerp((double)x/nx,box.minX+.001,box.maxX-.001),
            MathHelper.lerp((double)y/ny,box.minY+.001,box.maxY-.001),.5);
    }
    private static boolean between(World world,Vec3d start,Vec3d end) {
        double dx=end.x-start.x,dy=end.y-start.y;
        if(!Double.isFinite(dx)||!Double.isFinite(dy)||Math.abs(dx)+Math.abs(dy)>2040)return false;
        int x=MathHelper.floor(start.x),y=MathHelper.floor(start.y),ex=MathHelper.floor(end.x),ey=MathHelper.floor(end.y);
        int sx=Double.compare(dx,0),sy=Double.compare(dy,0);
        double stepX=dx==0?Double.POSITIVE_INFINITY:Math.abs(1/dx),stepY=dy==0?Double.POSITIVE_INFINITY:Math.abs(1/dy);
        double tx=dx==0?Double.POSITIVE_INFINITY:(sx>0?x+1-start.x:start.x-x)*stepX;
        double ty=dy==0?Double.POSITIVE_INFINITY:(sy>0?y+1-start.y:start.y-y)*stepY;
        for(int i=0;i<2048;i++) {
            if(world.getBlockState(new BlockPos(x,y,0)).isOf(Blocks.BEDROCK))return true;
            if(x==ex&&y==ey)return false;
            if(tx<ty) {x+=sx;tx+=stepX;}else {y+=sy;ty+=stepY;}
        }
        return false;
    }
}
