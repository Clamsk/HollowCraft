package dev.hollowcraft;

import com.google.gson.JsonObject;
import com.mojang.blaze3d.systems.*;
import io.netty.buffer.Unpooled;
import java.nio.*;
import java.nio.file.*;
import java.util.Set;
import java.util.concurrent.atomic.AtomicLong;
import net.minecraft.client.MinecraftClient;
import net.minecraft.client.gl.SimpleFramebuffer;
import net.minecraft.client.gui.DrawContext;
import net.minecraft.client.gui.screen.Screen;
import net.minecraft.client.gui.screen.ingame.*;
import net.minecraft.client.gui.widget.TextFieldWidget;
import net.minecraft.network.PacketByteBuf;
import net.minecraft.network.packet.Packet;
import net.minecraft.network.packet.c2s.play.*;
import net.minecraft.network.listener.ServerPlayPacketListener;
import net.minecraft.server.network.ServerPlayerEntity;
import org.joml.Matrix4f;
import org.lwjgl.opengl.*;
import org.slf4j.LoggerFactory;

/** Actual vanilla screens; native clicks retain campaign and handler identity through packet dispatch. */
public final class NativeGuiBridge {
    public record Session(long id,String scene,String campaign,int syncId,boolean inventory,boolean creative,int request,int revision) { }
    private static final AtomicLong ids=new AtomicLong(System.currentTimeMillis()*1000);
    private static volatile Session current;
    private static Session bound;
    private static Screen screen;
    private static final ThreadLocal<Scope> scope=new ThreadLocal<>();
    private static SimpleFramebuffer framebuffer;
    private static ByteBuffer pixels;
    private static byte[] transfer;
    private static int width=480,height=270,sequence;
    private static long lastRender,lastWarning,retryAt;
    private static final Set<Class<?>> packets=Set.of(ClickSlotC2SPacket.class,ButtonClickC2SPacket.class,CraftRequestC2SPacket.class,
        CreativeInventoryActionC2SPacket.class,RenameItemC2SPacket.class,CloseHandledScreenC2SPacket.class,
        RecipeBookDataC2SPacket.class,RecipeCategoryOptionsC2SPacket.class,UpdateSelectedSlotC2SPacket.class,PickFromInventoryC2SPacket.class);
    public static Session session() {return current;}
    static void open(ServerPlayerEntity player,String scene,String campaign,boolean inventory,int request) {
        player.currentScreenHandler.syncState();
        current=new Session(ids.incrementAndGet(),scene,campaign,player.currentScreenHandler.syncId,inventory,player.isCreative(),request,player.currentScreenHandler.getRevision());
    }
    static void close() {
        var old=current;current=null;
        if(old!=null)MinecraftClient.getInstance().execute(()-> {
            if(bound==old) {if(MinecraftClient.getInstance().currentScreen==screen)MinecraftClient.getInstance().setScreen(null);screen=null;bound=null;}
        });
    }
    static JsonObject description() {
        var value=new JsonObject();var session=current;if(session==null)return value;
        value.addProperty("session",session.id);value.addProperty("syncId",session.syncId);value.addProperty("inventory",session.inventory);value.addProperty("creative",session.creative);value.addProperty("request",session.request);value.addProperty("revision",session.revision);return value;
    }
    private static boolean prepare(MinecraftClient client,Session expected) {
        if(expected==null||current!=expected||client.world==null||client.player==null||client.interactionManager==null)return false;
        if(bound!=expected) {
            if(expected.inventory) {
                if(client.player.currentScreenHandler.syncId!=expected.syncId||client.interactionManager.hasCreativeInventory()!=expected.creative)return false;
                if(((client.player.playerScreenHandler.getRevision()-expected.revision)&32767)>=16384)return false;
                try(var ignored=new Scope(expected,false,modifiers(),held("guiC"),held("guiX"))) {
                    screen=expected.creative?new CreativeInventoryScreen(client.player,client.world.getEnabledFeatures(),client.options.getOperatorItemsTab().getValue()):new InventoryScreen(client.player);
                    client.setScreen(screen);bound=expected;
                }
            } else {
                if(!(client.currentScreen instanceof HandledScreen<?> handled)||handled.getScreenHandler().syncId!=expected.syncId)return false;
                if(((handled.getScreenHandler().getRevision()-expected.revision)&32767)>=16384)return false;
                screen=client.currentScreen;bound=expected;
            }
        }
        if(client.currentScreen!=screen) {HollowCraftBridge.guiClientClosed(expected);return false;}
        var input=PlayerRenderState.controls;
        if(input==null||!expected.scene.equals(text(input,"scene"))||!expected.campaign.equals(text(input,"campaign")))return false;
        int w=Math.max(320,Math.min(1024,(int)number(input,"guiWidth",480))),h=Math.max(240,Math.min(576,(int)number(input,"guiHeight",270)));
        width=w;height=h;
        if(screen.width!=w||screen.height!=h)try(var ignored=new Scope(expected,false,modifiers(),held("guiC"),held("guiX"))) {screen.init(client,w,h);}
        return current==expected;
    }
    private static boolean ready(MinecraftClient client,Session expected) {
        if(System.nanoTime()<retryAt)return false;
        try {return prepare(client,expected);}catch(Exception error) {retryAt=System.nanoTime()+1_000_000_000L;warn(error);return false;}
    }
    public static boolean owns(Screen candidate) {return current!=null&&bound==current&&candidate==screen;}
    static void input(JsonObject command) {
        var expected=current;
        if(expected==null||!command.has("gui")||command.get("gui").getAsLong()!=expected.id)return;
        var copy=command.deepCopy();MinecraftClient.getInstance().execute(()-> {
            var client=MinecraftClient.getInstance();
            if(!ready(client,expected)||integer(copy,"w",0)!=width||integer(copy,"h",0)!=height)return;
            double x=number(copy,"x",0),y=number(copy,"y",0),dx=number(copy,"dx",0),dy=number(copy,"dy",0);
            if(!Double.isFinite(x)||!Double.isFinite(y)||!Double.isFinite(dx)||!Double.isFinite(dy)||x<-width||x>width*2||y<-height||y>height*2)return;
            int button=integer(copy,"button",0),mods=integer(copy,"mods",0)&15;
            try(var ignored=new Scope(expected,false,mods,flag(copy,"heldC"),flag(copy,"heldX"))) {
                switch(text(copy,"action")) {
                    case "down" -> {if(button>=0&&button<=2)screen.mouseClicked(x,y,button);}
                    case "up" -> {if(button>=0&&button<=2)screen.mouseReleased(x,y,button);}
                    case "drag" -> {if(button>=0&&button<=2)screen.mouseDragged(x,y,button,dx,dy);}
                    case "scroll" -> {double amount=number(copy,"amount",0);if(Double.isFinite(amount))screen.mouseScrolled(x,y,Math.max(-20,Math.min(20,amount)));}
                    case "key" -> {
                        int key=integer(copy,"key",-1);if(key>=0&&key<=348)screen.keyPressed(key,0,mods);
                        if(expected.creative&&key>=49&&key<=57&&flag(copy,"heldC"))CreativeHotbars.publish(client);
                        String chars=text(copy,"character");if(client.currentScreen==screen&&chars.length()<=8)for(char ch:chars.toCharArray())if(ch>=32&&ch!=127)screen.charTyped(ch,mods);
                    }
                    case "keyUp" -> {int key=integer(copy,"key",-1);if(key>=0&&key<=348)screen.keyReleased(key,0,mods);}
                }
            } catch(Exception error) {warn(error);}
        });
    }
    public static boolean capture(Packet<?> packet) {
        var active=scope.get();if(active==null)return false;
        if(!packets.contains(packet.getClass())) {warn(new IllegalArgumentException("Unsupported remote GUI packet "+packet.getClass().getSimpleName()));return true;}
        var buffer=new PacketByteBuf(Unpooled.buffer(256,2*1024*1024));
        try {
            packet.write(buffer);
            @SuppressWarnings("unchecked") var copy=(Packet<ServerPlayPacketListener>)packet.getClass().getConstructor(PacketByteBuf.class).newInstance(buffer);
            HollowCraftBridge.guiPacket(active.session,copy);
        } catch(Exception error) {warn(error);}finally {buffer.release();}
        return true;
    }
    public static boolean dimensions() {return scope.get()!=null;}
    public static boolean rendering() {var active=scope.get();return active!=null&&active.rendering;}
    public static int logicalWidth() {return width;}
    public static int logicalHeight() {return height;}
    public static Boolean keyState(int key) {
        var active=scope.get();if(active==null)return null;
        return switch(key) {case 340,344 -> (active.mods&1)!=0;case 341,345 -> (active.mods&2)!=0;case 342,346 -> (active.mods&4)!=0;case 343,347 -> (active.mods&8)!=0;case 67 -> active.heldC;case 88 -> active.heldX;default -> null;};
    }
    public static Boolean activator(net.minecraft.client.option.KeyBinding key) {
        var active=scope.get();if(active==null)return null;var options=MinecraftClient.getInstance().options;
        if(key==options.saveToolbarActivatorKey)return active.heldC;if(key==options.loadToolbarActivatorKey)return active.heldX;return null;
    }
    private static final class Scope implements AutoCloseable {
        final Session session;final boolean rendering,heldC,heldX;final int mods;final Scope previous;
        Scope(Session session,boolean rendering,int mods,boolean heldC,boolean heldX) {this.session=session;this.rendering=rendering;this.mods=mods;this.heldC=heldC;this.heldX=heldX;previous=scope.get();scope.set(this);}
        public void close() {if(previous==null)scope.remove();else scope.set(previous);}
    }
    public static void render(MinecraftClient client,float delta) {
        if(!RenderSystem.isOnRenderThread())return;var expected=current;if(!ready(client,expected))return;
        long now=System.nanoTime();if(now<retryAt||now-lastRender<50_000_000L)return;lastRender=now;
        String root=System.getProperty("hollowcraft.icons","");if(root.isEmpty())return;
        int px=width*2,py=height*2;var input=PlayerRenderState.controls;
        float mx=number(input,"guiMouseX",.5f)*width,my=number(input,"guiMouseY",.5f)*height;
        if(!Float.isFinite(mx)||!Float.isFinite(my))return;
        try(var guard=new OffscreenRenderState(client);var ignored=new Scope(expected,true,modifiers(),held("guiC"),held("guiX"))) {
            if(framebuffer==null||framebuffer.textureWidth!=px||framebuffer.textureHeight!=py) {
                if(framebuffer!=null)framebuffer.delete();framebuffer=new SimpleFramebuffer(px,py,true,MinecraftClient.IS_SYSTEM_MAC);
                pixels=ByteBuffer.allocateDirect(px*py*4);transfer=new byte[48+px*py*4+4];
            }
            framebuffer.setClearColor(0,0,0,0);RenderSystem.disableScissor();RenderSystem.depthMask(true);framebuffer.clear(MinecraftClient.IS_SYSTEM_MAC);framebuffer.beginWrite(true);
            var view=RenderSystem.getModelViewStack();view.loadIdentity();view.translate(0,0,-11000);RenderSystem.applyModelViewMatrix();
            RenderSystem.setProjectionMatrix(new Matrix4f().setOrtho(0,width,height,0,1000,21000),VertexSorter.BY_Z);RenderSystem.setShaderColor(1,1,1,1);
            var draw=new DrawContext(client,client.getBufferBuilders().getEntityVertexConsumers());screen.renderWithTooltip(draw,(int)mx,(int)my,delta);draw.draw();
            pixels.clear();GL11.glReadPixels(0,0,px,py,GL11.GL_RGBA,GL11.GL_UNSIGNED_BYTE,pixels);pixels.get(transfer,48,px*py*4);
            for(int at=48;at<48+px*py*4;at+=4) {int alpha=transfer[at+3]&255;if(alpha==0)transfer[at]=transfer[at+1]=transfer[at+2]=0;else if(alpha<255)for(int c=0;c<3;c++)transfer[at+c]=(byte)Math.min(255,((transfer[at+c]&255)*255+alpha/2)/alpha);}
            var header=ByteBuffer.wrap(transfer).order(ByteOrder.LITTLE_ENDIAN);
            header.putInt(0x48434755).putInt(++sequence).putInt(px).putInt(py).putLong(expected.id).putInt(expected.syncId).putInt((expected.scene+"|"+expected.campaign).hashCode());
            header.putInt(width).putInt(height).putInt(screen.getFocused() instanceof TextFieldWidget field&&field.isFocused()?1:0).putInt(1);header.putInt(48+px*py*4,sequence);
            Path directory=Path.of(root).getParent().resolve("gui_frames");Files.createDirectories(directory);Files.write(directory.resolve("frame-"+(sequence%3)+".bin"),transfer);
        } catch(Exception error) {
            try {
                client.getBufferBuilders().getEntityVertexConsumers().draw();
                var buffer=net.minecraft.client.render.Tessellator.getInstance().getBuffer();
                if(buffer.isBuilding()) {var built=buffer.endNullable();if(built!=null)built.release();buffer.clear();}
            } catch(Exception ignored) { }
            retryAt=now+1_000_000_000L;warn(error);
        }
    }
    public static void closeRenderer() {if(framebuffer!=null)framebuffer.delete();framebuffer=null;pixels=null;transfer=null;}
    private static int modifiers() {return integer(PlayerRenderState.controls,"guiMods",0)&15;}
    private static boolean held(String key) {return flag(PlayerRenderState.controls,key);}
    private static float number(JsonObject input,String key,float fallback) {return input!=null&&input.has(key)?input.get(key).getAsFloat():fallback;}
    private static int integer(JsonObject input,String key,int fallback) {return input!=null&&input.has(key)?input.get(key).getAsInt():fallback;}
    private static String text(JsonObject input,String key) {return input!=null&&input.has(key)?input.get(key).getAsString():"";}
    private static boolean flag(JsonObject input,String key) {return input!=null&&input.has(key)&&input.get(key).getAsBoolean();}
    private static void warn(Exception error) {long now=System.nanoTime();if(now-lastWarning>5_000_000_000L) {lastWarning=now;LoggerFactory.getLogger("HollowCraft").warn("Native GUI forwarding unavailable",error);}}
}
