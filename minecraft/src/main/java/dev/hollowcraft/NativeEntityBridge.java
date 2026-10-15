package dev.hollowcraft;

import com.mojang.blaze3d.systems.*;
import java.nio.*;
import java.nio.file.*;
import java.util.*;
import net.minecraft.client.MinecraftClient;
import net.minecraft.client.gl.SimpleFramebuffer;
import net.minecraft.client.render.Camera;
import net.minecraft.client.util.math.MatrixStack;
import net.minecraft.entity.*;
import net.minecraft.entity.projectile.ProjectileEntity;
import net.minecraft.server.network.ServerPlayerEntity;
import net.minecraft.util.math.*;
import org.joml.Matrix4f;
import org.joml.Quaternionf;
import org.lwjgl.opengl.GL11;
import org.slf4j.LoggerFactory;

/** Real vanilla entity renderers; immutable server poses avoid client projectile prediction drift. */
public final class NativeEntityBridge {
    private static final int WIDTH=768,HEIGHT=512,HEADER=48,MAX=256;
    private static final long SESSION=System.currentTimeMillis();
    private record Pose(int id,UUID uuid,double x,double y,double z,double px,double py,double pz,float yaw,float pitch,float pyaw,float ppitch,int age,int fuse) {
        Pose(Entity e) {this(e.getId(),e.getUuid(),e.getX(),e.getY(),e.getZ(),e.lastRenderX,e.lastRenderY,e.lastRenderZ,e.getYaw(),e.getPitch(),e.prevYaw,e.prevPitch,e.age,e instanceof TntEntity t?t.getFuse():-1);}
    }
    private record State(String scene,String campaign,int offset,List<Pose> entities) { }
    private static volatile State latest;
    private static final FlatCamera camera=new FlatCamera();
    private static SimpleFramebuffer framebuffer;
    private static ByteBuffer pixels;
    private static byte[] transfer;
    private static long lastRender,lastWarning,retryAt;
    private static int sequence;
    private static boolean hadEntities;
    private static String lastScene="";
    public static boolean rendering;
    static void publish(ServerPlayerEntity player,String scene,String campaign,int offset) {
        var list=player.getServerWorld().getEntitiesByClass(Entity.class,new Box(player.getX()-72,player.getY()-52,-4,player.getX()+72,player.getY()+52,4),NativeEntityBridge::supported);
        list.sort(Comparator.comparingDouble(e->e.squaredDistanceTo(player)));
        latest=new State(scene,campaign,offset,list.stream().limit(MAX).map(Pose::new).toList());
    }
    static void clear() {latest=null;}
    private static boolean supported(Entity e) {return !e.isRemoved()&&!(e instanceof NativeProxy)&&(e instanceof ProjectileEntity||e instanceof ItemEntity||e instanceof TntEntity||e instanceof FallingBlockEntity||e instanceof net.minecraft.entity.vehicle.BoatEntity||e instanceof net.minecraft.entity.mob.MobEntity||e instanceof ExperienceOrbEntity);}
    public static void render(MinecraftClient client,float delta) {
        var state=latest;var input=PlayerRenderState.controls;
        if(!RenderSystem.isOnRenderThread()||!HollowCraftBridge.entityRenderRequested()||client.world==null||client.player==null||state==null||input==null
            ||!state.scene.equals(text(input,"scene"))||!state.campaign.equals(text(input,"campaign")))return;
        String root=System.getProperty("hollowcraft.icons","");if(root.isEmpty())return;
        var dispatcher=client.getEntityRenderDispatcher();if(dispatcher.camera==null||dispatcher.getRotation()==null)return;
        long now=System.nanoTime();if(now<retryAt||now-lastRender<50_000_000L)return;
        float x=number(input,"viewX",(float)client.player.getX()-state.offset),y=number(input,"viewY",(float)client.player.getY()-64+1);
        float sx=number(input,"viewWidth",36),sy=number(input,"viewHeight",24);
        if(!Float.isFinite(x)||!Float.isFinite(y)||!Float.isFinite(sx)||!Float.isFinite(sy)||Math.abs(x)>500||y<-100||y>300||sx<2||sx>120||sy<2||sy>80)return;
        String key=state.scene+"|"+state.campaign;
        if(state.entities.isEmpty()&&!hadEntities&&lastScene.equals(key))return;lastRender=now;
        var access=(dev.hollowcraft.mixin.EntityRenderDispatcherAccess)dispatcher;
        var oldWorld=access.hollowcraft$world();var oldCamera=dispatcher.camera;var oldTarget=dispatcher.targetedEntity;var oldRotation=new Quaternionf(dispatcher.getRotation());
        boolean shadows=access.hollowcraft$shadows(),hitboxes=dispatcher.shouldRenderHitboxes();
        float fogStart=RenderSystem.getShaderFogStart(),fogEnd=RenderSystem.getShaderFogEnd();
        var ids=new ArrayList<Integer>(MAX);
        try(var guard=new OffscreenRenderState(client)) {
            rendering=true;
            if(framebuffer==null) {framebuffer=new SimpleFramebuffer(WIDTH,HEIGHT,true,MinecraftClient.IS_SYSTEM_MAC);pixels=ByteBuffer.allocateDirect(WIDTH*HEIGHT*4);transfer=new byte[HEADER+WIDTH*HEIGHT*4+8+MAX*4];}
            framebuffer.setClearColor(0,0,0,0);RenderSystem.disableScissor();RenderSystem.depthMask(true);framebuffer.clear(MinecraftClient.IS_SYSTEM_MAC);framebuffer.beginWrite(true);
            var view=RenderSystem.getModelViewStack();view.loadIdentity();RenderSystem.applyModelViewMatrix();
            RenderSystem.setProjectionMatrix(new Matrix4f().setOrtho(-sx/2,sx/2,-sy/2,sy/2,.1f,128),VertexSorter.BY_DISTANCE);
            RenderSystem.setShaderColor(1,1,1,1);RenderSystem.setShaderFogStart(Float.MAX_VALUE);RenderSystem.setShaderFogEnd(Float.MAX_VALUE);
            RenderSystem.enableDepthTest();RenderSystem.enableBlend();RenderSystem.defaultBlendFunc();
            net.minecraft.client.render.DiffuseLighting.enableForLevel(view.peek().getPositionMatrix());
            camera.update(client.world,client.player,false,false,delta);camera.place(state.offset+x,64+y,16);
            dispatcher.configure(client.world,camera,null);dispatcher.setRenderShadows(false);dispatcher.setRenderHitboxes(false);
            client.gameRenderer.getLightmapTextureManager().enable();
            var vertices=client.getBufferBuilders().getEntityVertexConsumers();var matrices=new MatrixStack();
            float partial=HollowCraftBridge.remotePaused?1:delta;
            for(var pose:state.entities) {
                Entity entity=client.world.getEntityById(pose.id);
                if(entity==null||!entity.getUuid().equals(pose.uuid)||!supported(entity)||Math.abs(pose.x-state.offset-x)>sx/2+3||Math.abs(pose.y-64-y)>sy/2+3)continue;
                try(var placement=new Placement(entity,pose,partial)) {
                    int light=entity instanceof net.minecraft.entity.mob.MobEntity||entity instanceof net.minecraft.entity.vehicle.BoatEntity?net.minecraft.client.render.LightmapTextureManager.MAX_LIGHT_COORDINATE:dispatcher.getLight(entity,partial);
                    dispatcher.render(entity,entity.getX()-state.offset-x,entity.getY()-64-y,entity.getZ()-16,entity.getYaw(),partial,matrices,vertices,light);
                }
                ids.add(pose.id);
            }
            vertices.draw();
            pixels.clear();GL11.glReadPixels(0,0,WIDTH,HEIGHT,GL11.GL_RGBA,GL11.GL_UNSIGNED_BYTE,pixels);pixels.get(transfer,HEADER,WIDTH*HEIGHT*4);
            for(int at=HEADER;at<HEADER+WIDTH*HEIGHT*4;at+=4) {int alpha=transfer[at+3]&255;if(alpha==0)transfer[at]=transfer[at+1]=transfer[at+2]=0;else if(alpha<255)for(int c=0;c<3;c++)transfer[at+c]=(byte)Math.min(255,((transfer[at+c]&255)*255+alpha/2)/alpha);}
            var header=ByteBuffer.wrap(transfer).order(ByteOrder.LITTLE_ENDIAN);
            header.putInt(0x48434554).putInt(++sequence).putInt(WIDTH).putInt(HEIGHT).putLong(SESSION).putInt(key.hashCode()).putFloat(x).putFloat(y).putFloat(sx).putFloat(sy).putInt(2);
            header.position(HEADER+WIDTH*HEIGHT*4);header.putInt(ids.size());for(int id:ids)header.putInt(id);header.putInt(sequence);
            Path directory=Path.of(root).getParent().resolve("entity_frames");Files.createDirectories(directory);
            try(var output=Files.newOutputStream(directory.resolve("frame-"+(sequence%3)+".bin"))) {output.write(transfer,0,header.position());}
            hadEntities=!ids.isEmpty();lastScene=key;
        } catch(Exception error) {
            try {client.getBufferBuilders().getEntityVertexConsumers().draw();var buffer=net.minecraft.client.render.Tessellator.getInstance().getBuffer();if(buffer.isBuilding()) {var built=buffer.endNullable();if(built!=null)built.release();buffer.clear();}}catch(Exception ignored) { }
            retryAt=now+1_000_000_000L;if(now-lastWarning>5_000_000_000L) {lastWarning=now;LoggerFactory.getLogger("HollowCraft").warn("Native entity frame unavailable",error);}
        } finally {
            rendering=false;dispatcher.setWorld(oldWorld);dispatcher.camera=oldCamera;dispatcher.targetedEntity=oldTarget;dispatcher.setRotation(oldRotation);dispatcher.setRenderShadows(shadows);dispatcher.setRenderHitboxes(hitboxes);
            RenderSystem.setShaderFogStart(fogStart);RenderSystem.setShaderFogEnd(fogEnd);client.gameRenderer.getLightmapTextureManager().disable();
        }
    }
    private static final class Placement implements AutoCloseable {
        final Entity e;final Pose previous;
        Placement(Entity e,Pose p,float partial) {
            this.e=e;previous=new Pose(e);
            e.setPosition(MathHelper.lerp(partial,p.px,p.x),MathHelper.lerp(partial,p.py,p.y),MathHelper.lerp(partial,p.pz,p.z));
            e.prevYaw=p.pyaw;e.prevPitch=p.ppitch;e.setYaw(p.yaw);e.setPitch(p.pitch);e.age=p.age;
            if(e instanceof TntEntity t&&p.fuse>=0)t.setFuse(p.fuse);
        }
        public void close() {e.setPosition(previous.x,previous.y,previous.z);e.prevYaw=previous.pyaw;e.prevPitch=previous.ppitch;e.setYaw(previous.yaw);e.setPitch(previous.pitch);e.age=previous.age;if(e instanceof TntEntity t&&previous.fuse>=0)t.setFuse(previous.fuse);}
    }
    public static void close() {clear();if(framebuffer!=null)framebuffer.delete();framebuffer=null;pixels=null;transfer=null;hadEntities=false;lastScene="";}
    private static float number(com.google.gson.JsonObject input,String key,float fallback) {return input.has(key)?input.get(key).getAsFloat():fallback;}
    private static String text(com.google.gson.JsonObject input,String key) {return input.has(key)?input.get(key).getAsString():"";}
    private static final class FlatCamera extends Camera {void place(double x,double y,double z) {setRotation(180,0);setPos(x,y,z);}}
}
