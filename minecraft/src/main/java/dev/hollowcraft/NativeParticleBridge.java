package dev.hollowcraft;

import com.mojang.blaze3d.systems.*;
import java.nio.*;
import java.nio.file.*;
import net.minecraft.client.MinecraftClient;
import net.minecraft.client.gl.SimpleFramebuffer;
import net.minecraft.client.render.Camera;
import net.minecraft.client.util.math.MatrixStack;
import org.joml.Matrix4f;
import org.lwjgl.opengl.*;
import org.slf4j.LoggerFactory;

/** Render vanilla particle textures, motion and lifetime into the native camera's world rectangle. */
public final class NativeParticleBridge {
    private static final int WIDTH=768,HEIGHT=512,HEADER=48;
    private static final long SESSION=System.currentTimeMillis();
    private static SimpleFramebuffer framebuffer;
    private static ByteBuffer pixels;
    private static byte[] transfer;
    private static final FlatCamera camera=new FlatCamera();
    private static long lastRender,lastWarning,retryAt;
    private static int sequence;
    private static boolean hadParticles;
    private static String lastScene="";
    public static boolean rendering;
    public static void close() {if(framebuffer!=null)framebuffer.delete();framebuffer=null;pixels=null;transfer=null;lastScene="";hadParticles=false;}
    public static void render(MinecraftClient client,float delta) {
        var state=PlayerRenderState.latest;var input=PlayerRenderState.controls;
        if(!RenderSystem.isOnRenderThread()||client.world==null||client.player==null||state==null||input==null
            ||!state.scene.equals(text(input,"scene"))||!state.campaign.equals(text(input,"campaign")))return;
        String root=System.getProperty("hollowcraft.icons","");if(root.isEmpty())return;
        long now=System.nanoTime();if(now<retryAt||now-lastRender<50_000_000L)return;
        var queues=((dev.hollowcraft.mixin.ParticleManagerAccess)client.particleManager).hollowcraft$particles();
        boolean any=queues.values().stream().anyMatch(queue->!queue.isEmpty());
        String scene=state.scene+"|"+state.campaign;
        if(!any&&!hadParticles&&lastScene.equals(scene))return;
        float x=number(input,"viewX",(float)state.x),y=number(input,"viewY",(float)state.y+1);
        float spanX=number(input,"viewWidth",36),spanY=number(input,"viewHeight",24);
        if(!Float.isFinite(x)||!Float.isFinite(y)||!Float.isFinite(spanX)||!Float.isFinite(spanY)||Math.abs(x)>500||y<-100||y>300||spanX<2||spanX>120||spanY<2||spanY>80)return;
        lastRender=now;
        var view=RenderSystem.getModelViewStack();int depth=((dev.hollowcraft.mixin.MatrixStackAccess)view).hollowcraft$stack().size();
        var projection=new Matrix4f(RenderSystem.getProjectionMatrix());var sorting=RenderSystem.getVertexSorting();
        var shader=RenderSystem.getShader();float[] color=RenderSystem.getShaderColor().clone();
        float fogStart=RenderSystem.getShaderFogStart(),fogEnd=RenderSystem.getShaderFogEnd();
        boolean depthTest=GL11.glIsEnabled(GL11.GL_DEPTH_TEST),blend=GL11.glIsEnabled(GL11.GL_BLEND),cull=GL11.glIsEnabled(GL11.GL_CULL_FACE),depthWrite=GL11.glGetBoolean(GL11.GL_DEPTH_WRITEMASK);
        int srcRgb=GL11.glGetInteger(GL14.GL_BLEND_SRC_RGB),dstRgb=GL11.glGetInteger(GL14.GL_BLEND_DST_RGB),srcAlpha=GL11.glGetInteger(GL14.GL_BLEND_SRC_ALPHA),dstAlpha=GL11.glGetInteger(GL14.GL_BLEND_DST_ALPHA);
        view.push();
        try {
            if(framebuffer==null) {
                framebuffer=new SimpleFramebuffer(WIDTH,HEIGHT,true,MinecraftClient.IS_SYSTEM_MAC);
                pixels=ByteBuffer.allocateDirect(WIDTH*HEIGHT*4);transfer=new byte[HEADER+WIDTH*HEIGHT*4+4];
            }
            framebuffer.setClearColor(0,0,0,0);RenderSystem.depthMask(true);framebuffer.clear(MinecraftClient.IS_SYSTEM_MAC);framebuffer.beginWrite(true);
            view.loadIdentity();RenderSystem.applyModelViewMatrix();
            RenderSystem.setProjectionMatrix(new Matrix4f().setOrtho(-spanX/2,spanX/2,-spanY/2,spanY/2,.1f,128),VertexSorter.BY_DISTANCE);
            RenderSystem.setShaderColor(1,1,1,1);RenderSystem.setShaderFogStart(Float.MAX_VALUE);RenderSystem.setShaderFogEnd(Float.MAX_VALUE);
            camera.update(client.world,client.player,false,false,delta);camera.place(state.offset+x,64+y,16);
            rendering=true;
            if(any)client.particleManager.renderParticles(new MatrixStack(),client.getBufferBuilders().getEntityVertexConsumers(),client.gameRenderer.getLightmapTextureManager(),camera,HollowCraftBridge.remotePaused?0:delta);
            rendering=false;
            pixels.clear();GL11.glReadPixels(0,0,WIDTH,HEIGHT,GL11.GL_RGBA,GL11.GL_UNSIGNED_BYTE,pixels);pixels.get(transfer,HEADER,WIDTH*HEIGHT*4);
            // The transparent FBO contains associated RGB. Unity's sprite shader expects
            // straight RGB, so undo that association once rather than darkening soft edges.
            for(int at=HEADER;at<HEADER+WIDTH*HEIGHT*4;at+=4) {
                int alpha=transfer[at+3]&255;
                if(alpha==0) {transfer[at]=transfer[at+1]=transfer[at+2]=0;continue;}
                if(alpha<255)for(int channel=0;channel<3;channel++)transfer[at+channel]=(byte)Math.min(255,((transfer[at+channel]&255)*255+alpha/2)/alpha);
            }
            var header=ByteBuffer.wrap(transfer).order(ByteOrder.LITTLE_ENDIAN);
            header.putInt(0x48435054).putInt(++sequence).putInt(WIDTH).putInt(HEIGHT).putLong(SESSION).putInt(scene.hashCode());
            header.putFloat(x).putFloat(y).putFloat(spanX).putFloat(spanY).putInt(1);
            header.putInt(HEADER+WIDTH*HEIGHT*4,sequence);
            Path directory=Path.of(root).getParent().resolve("particle_frames");Files.createDirectories(directory);
            Files.write(directory.resolve("frame-"+(sequence%3)+".bin"),transfer);hadParticles=any;lastScene=scene;
        } catch(Exception error) {
            try {
                var buffer=net.minecraft.client.render.Tessellator.getInstance().getBuffer();
                if(buffer.isBuilding()) {var built=buffer.endNullable();if(built!=null)built.release();}
                buffer.clear();
            } catch(RuntimeException ignored) { }
            retryAt=now+1_000_000_000L;
            if(now-lastWarning>5_000_000_000L) {lastWarning=now;LoggerFactory.getLogger("HollowCraft").warn("Native vanilla particle frame unavailable",error);}
        } finally {
            rendering=false;
            while(((dev.hollowcraft.mixin.MatrixStackAccess)view).hollowcraft$stack().size()>depth)view.pop();
            RenderSystem.applyModelViewMatrix();RenderSystem.setProjectionMatrix(projection,sorting);
            RenderSystem.setShader(()->shader);RenderSystem.setShaderColor(color[0],color[1],color[2],color[3]);RenderSystem.setShaderFogStart(fogStart);RenderSystem.setShaderFogEnd(fogEnd);
            RenderSystem.depthMask(depthWrite);if(depthTest)RenderSystem.enableDepthTest();else RenderSystem.disableDepthTest();
            if(blend)RenderSystem.enableBlend();else RenderSystem.disableBlend();if(cull)RenderSystem.enableCull();else RenderSystem.disableCull();RenderSystem.blendFuncSeparate(srcRgb,dstRgb,srcAlpha,dstAlpha);
            client.gameRenderer.getLightmapTextureManager().disable();client.getFramebuffer().beginWrite(true);
        }
    }
    private static float number(com.google.gson.JsonObject input,String key,float fallback) {return input.has(key)?input.get(key).getAsFloat():fallback;}
    private static String text(com.google.gson.JsonObject input,String key) {return input.has(key)?input.get(key).getAsString():"";}
    private static final class FlatCamera extends Camera {void place(double x,double y,double z) {setRotation(180,0);setPos(x,y,z);}}
}
