package dev.hollowcraft;

import com.mojang.blaze3d.systems.*;
import java.nio.*;
import java.nio.file.*;
import net.minecraft.client.MinecraftClient;
import net.minecraft.client.gl.SimpleFramebuffer;
import net.minecraft.client.gui.DrawContext;
import org.joml.Matrix4f;
import org.lwjgl.opengl.GL11;
import org.slf4j.LoggerFactory;

/** Vanilla HUD routines and live client state, rendered on the same canvas as native menus. */
public final class NativeHudBridge {
    public static final long SESSION=System.currentTimeMillis()*1000;
    public static boolean rendering;
    private static int width=480,height=270,sequence;
    private static long lastRender,lastWarning,retryAt;
    private static SimpleFramebuffer framebuffer;
    private static ByteBuffer pixels;
    private static byte[] transfer;
    public static int width() {return width;}
    public static int height() {return height;}
    public static void render(MinecraftClient client,float delta) {
        var state=PlayerRenderState.latest;var input=PlayerRenderState.controls;
        if(!RenderSystem.isOnRenderThread()||!HollowCraftBridge.hudRequested()||client.player==null||client.world==null||client.interactionManager==null
            ||state==null||input==null||!state.scene.equals(text(input,"scene"))||!state.campaign.equals(text(input,"campaign"))||state.visit!=integer(input,"visit",-1))return;
        // Character equipment comes from the authoritative server snapshot. Wait for
        // the same selected hand in the real client before exporting its vanilla HUD.
        if(client.player.getInventory().selectedSlot!=state.selected
            ||!net.minecraft.item.ItemStack.areEqual(client.player.getMainHandStack(),state.equipment[net.minecraft.entity.EquipmentSlot.MAINHAND.ordinal()]))return;
        String root=System.getProperty("hollowcraft.icons","");if(root.isEmpty())return;
        long now=System.nanoTime();if(now<retryAt||now-lastRender<50_000_000L)return;lastRender=now;
        width=Math.max(320,Math.min(1024,integer(input,"guiWidth",480)));height=Math.max(240,Math.min(576,integer(input,"guiHeight",270)));
        int px=width*2,py=height*2;
        var hud=client.inGameHud;var access=(dev.hollowcraft.mixin.InGameHudAccess)hud;
        int oldWidth=access.hollowcraft$width(),oldHeight=access.hollowcraft$height();
        try(var guard=new OffscreenRenderState(client)) {
            rendering=true;access.hollowcraft$width(width);access.hollowcraft$height(height);
            if(framebuffer==null||framebuffer.textureWidth!=px||framebuffer.textureHeight!=py) {
                if(framebuffer!=null)framebuffer.delete();framebuffer=new SimpleFramebuffer(px,py,true,MinecraftClient.IS_SYSTEM_MAC);
                pixels=ByteBuffer.allocateDirect(px*py*4);transfer=new byte[48+px*py*4+4];
            }
            framebuffer.setClearColor(0,0,0,0);RenderSystem.disableScissor();RenderSystem.depthMask(true);framebuffer.clear(MinecraftClient.IS_SYSTEM_MAC);framebuffer.beginWrite(true);
            var view=RenderSystem.getModelViewStack();view.loadIdentity();view.translate(0,0,-11000);RenderSystem.applyModelViewMatrix();
            RenderSystem.setProjectionMatrix(new Matrix4f().setOrtho(0,width,height,0,1000,21000),VertexSorter.BY_Z);RenderSystem.setShaderColor(1,1,1,1);
            RenderSystem.enableBlend();RenderSystem.defaultBlendFunc();
            var draw=new DrawContext(client,client.getBufferBuilders().getEntityVertexConsumers());
            access.hollowcraft$hotbar(delta,draw);
            if(client.interactionManager.hasStatusBars()) {access.hollowcraft$status(draw);access.hollowcraft$mount(draw);}
            int barX=width/2-91;var mount=client.player.getJumpingMount();
            if(mount!=null)hud.renderMountJumpBar(mount,draw,barX);else if(client.interactionManager.hasExperienceBar())hud.renderExperienceBar(draw,barX);
            if(!client.player.isSpectator())hud.renderHeldItemTooltip(draw);
            access.hollowcraft$effects(draw);draw.draw();
            // Entity status 35 already starts vanilla's totem animation on this client.
            // It lives in GameRenderer, outside InGameHud, and previously stayed in MC.
            ((dev.hollowcraft.mixin.GameRendererAccess)client.gameRenderer).hollowcraft$floatingItem(width,height,delta);
            pixels.clear();GL11.glReadPixels(0,0,px,py,GL11.GL_RGBA,GL11.GL_UNSIGNED_BYTE,pixels);pixels.get(transfer,48,px*py*4);
            for(int at=48;at<48+px*py*4;at+=4) {int alpha=transfer[at+3]&255;if(alpha==0)transfer[at]=transfer[at+1]=transfer[at+2]=0;else if(alpha<255)for(int c=0;c<3;c++)transfer[at+c]=(byte)Math.min(255,((transfer[at+c]&255)*255+alpha/2)/alpha);}
            var header=ByteBuffer.wrap(transfer).order(ByteOrder.LITTLE_ENDIAN);
            header.putInt(0x48434855).putInt(++sequence).putInt(px).putInt(py).putLong(SESSION).putInt(0).putInt((state.scene+"|"+state.campaign+"|"+state.visit).hashCode());
            header.putInt(width).putInt(height).putInt(0).putInt(1);header.putInt(48+px*py*4,sequence);
            Path directory=Path.of(root).getParent().resolve("hud_frames");Files.createDirectories(directory);Files.write(directory.resolve("frame-"+(sequence%3)+".bin"),transfer);
        } catch(Exception error) {
            try {client.getBufferBuilders().getEntityVertexConsumers().draw();var buffer=net.minecraft.client.render.Tessellator.getInstance().getBuffer();if(buffer.isBuilding()) {var built=buffer.endNullable();if(built!=null)built.release();buffer.clear();}}catch(Exception ignored) { }
            retryAt=now+1_000_000_000L;
            if(now-lastWarning>5_000_000_000L) {lastWarning=now;LoggerFactory.getLogger("HollowCraft").warn("Native vanilla HUD unavailable",error);}
        } finally {rendering=false;access.hollowcraft$width(oldWidth);access.hollowcraft$height(oldHeight);}
    }
    public static void close() {if(framebuffer!=null)framebuffer.delete();framebuffer=null;pixels=null;transfer=null;}
    private static int integer(com.google.gson.JsonObject input,String key,int fallback) {return input.has(key)?input.get(key).getAsInt():fallback;}
    private static String text(com.google.gson.JsonObject input,String key) {return input.has(key)?input.get(key).getAsString():"";}
}
