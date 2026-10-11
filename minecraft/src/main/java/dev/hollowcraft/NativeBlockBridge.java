package dev.hollowcraft;

import com.mojang.blaze3d.systems.*;
import java.nio.*;
import java.nio.file.*;
import java.util.*;
import net.minecraft.block.*;
import net.minecraft.client.MinecraftClient;
import net.minecraft.client.gl.SimpleFramebuffer;
import net.minecraft.client.render.*;
import net.minecraft.client.util.math.MatrixStack;
import net.minecraft.server.network.ServerPlayerEntity;
import net.minecraft.util.math.*;
import org.joml.Matrix4f;
import org.lwjgl.opengl.GL11;
import org.slf4j.LoggerFactory;

/** Vanilla block models, fluid meshes and block entity renderers in the native camera plane. */
public final class NativeBlockBridge {
    private static final int WIDTH=768,HEIGHT=512,HEADER=48,MAX=525;
    private static final long SESSION=System.currentTimeMillis();
    private record Cell(BlockPos pos,BlockState block,int key) { }
    private record State(String scene,String campaign,int offset,List<Cell> cells) { }
    private static volatile State latest;
    private static final FlatCamera camera=new FlatCamera();
    private static final net.minecraft.util.math.random.Random random=net.minecraft.util.math.random.Random.create(42);
    private static SimpleFramebuffer framebuffer;
    private static ByteBuffer pixels;
    private static byte[] transfer;
    private static long lastRender,lastWarning,retryAt;
    private static int sequence;
    private static boolean hadBlocks;
    private static String lastScene="";
    public static boolean rendering;
    static void publish(ServerPlayerEntity player,String scene,String campaign,int offset,int centerX,int centerY) {
        var cells=new ArrayList<Cell>();
        for(int x=centerX-12;x<=centerX+12;x++)for(int y=centerY-10;y<=centerY+10;y++) {
            var pos=new BlockPos(offset+x,64+y,0);var state=player.getServerWorld().getBlockState(pos);
            if(!state.isAir()&&!NativeTerrainBlock.proxy(state))cells.add(new Cell(pos,state,(x&65535)<<16|(y&65535)));
        }
        latest=new State(scene,campaign,offset,List.copyOf(cells));
    }
    static void clear() {latest=null;}
    public static void render(MinecraftClient client,float delta) {
        var state=latest;var input=PlayerRenderState.controls;
        if(!RenderSystem.isOnRenderThread()||!HollowCraftBridge.blockRenderRequested()||client.world==null||client.player==null||state==null||input==null
            ||!state.scene.equals(text(input,"scene"))||!state.campaign.equals(text(input,"campaign")))return;
        String root=System.getProperty("hollowcraft.icons","");if(root.isEmpty())return;
        long now=System.nanoTime();if(now<retryAt||now-lastRender<50_000_000L)return;
        float x=number(input,"viewX",(float)client.player.getX()-state.offset),y=number(input,"viewY",(float)client.player.getY()-64+1);
        float sx=number(input,"viewWidth",36),sy=number(input,"viewHeight",24);
        if(!Float.isFinite(x)||!Float.isFinite(y)||!Float.isFinite(sx)||!Float.isFinite(sy)||Math.abs(x)>500||y<-100||y>300||sx<2||sx>120||sy<2||sy>80)return;
        String key=state.scene+"|"+state.campaign;
        if(state.cells.isEmpty()&&!hadBlocks&&lastScene.equals(key))return;lastRender=now;
        var dispatcher=client.getBlockEntityRenderDispatcher();var oldWorld=dispatcher.world;var oldCamera=dispatcher.camera;var oldTarget=dispatcher.crosshairTarget;
        float fogStart=RenderSystem.getShaderFogStart(),fogEnd=RenderSystem.getShaderFogEnd();var ids=new ArrayList<Integer>(MAX);
        try(var guard=new OffscreenRenderState(client)) {
            rendering=true;
            if(framebuffer==null) {framebuffer=new SimpleFramebuffer(WIDTH,HEIGHT,true,MinecraftClient.IS_SYSTEM_MAC);pixels=ByteBuffer.allocateDirect(WIDTH*HEIGHT*4);transfer=new byte[HEADER+WIDTH*HEIGHT*4+8+MAX*4];}
            framebuffer.setClearColor(0,0,0,0);RenderSystem.disableScissor();RenderSystem.depthMask(true);framebuffer.clear(MinecraftClient.IS_SYSTEM_MAC);framebuffer.beginWrite(true);
            var view=RenderSystem.getModelViewStack();view.loadIdentity();RenderSystem.applyModelViewMatrix();
            // Keep each cell's Z=.5 centre at its exact native XY position while exposing
            // horizontal surfaces (redstone, rails, carpet) in the side-view game.
            view.multiplyPositionMatrix(new Matrix4f().m20(.12f).m21(.18f).m30(15.5f*.12f).m31(15.5f*.18f));RenderSystem.applyModelViewMatrix();
            RenderSystem.setProjectionMatrix(new Matrix4f().setOrtho(-sx/2,sx/2,-sy/2,sy/2,.1f,128),VertexSorter.BY_DISTANCE);
            RenderSystem.setShaderColor(1,1,1,1);RenderSystem.setShaderFogStart(Float.MAX_VALUE);RenderSystem.setShaderFogEnd(Float.MAX_VALUE);
            RenderSystem.enableDepthTest();RenderSystem.enableBlend();RenderSystem.defaultBlendFunc();DiffuseLighting.enableForLevel(view.peek().getPositionMatrix());
            camera.update(client.world,client.player,false,false,delta);camera.place(state.offset+x,64+y,16);dispatcher.configure(client.world,camera,null);
            client.gameRenderer.getLightmapTextureManager().enable();
            var vertices=client.getBufferBuilders().getEntityVertexConsumers();var matrices=new MatrixStack();var manager=client.getBlockRenderManager();
            for(var cell:state.cells) {
                var pos=cell.pos;var block=cell.block;
                if(Math.abs(pos.getX()-state.offset-x)>sx/2+2||Math.abs(pos.getY()-64-y)>sy/2+2||!client.world.getBlockState(pos).equals(block))continue;
                double rx=pos.getX()-state.offset-x,ry=pos.getY()-64-y,rz=pos.getZ()-16;
                matrices.push();matrices.translate(rx,ry,rz);
                if(block.getRenderType()==BlockRenderType.MODEL)manager.renderBlock(block,pos,client.world,matrices,vertices.getBuffer(RenderLayers.getBlockLayer(block)),false,random);
                if(block.hasBlockEntity()) {
                    var entity=client.world.getBlockEntity(pos);
                    if(entity==null) {matrices.pop();continue;}
                    dispatcher.render(entity,HollowCraftBridge.remotePaused?1:delta,matrices,vertices);
                }
                matrices.pop();
                var fluid=block.getFluidState();
                if(!fluid.isEmpty())manager.renderFluid(pos,client.world,new ShiftedVertex(vertices.getBuffer(RenderLayers.getFluidLayer(fluid)),rx-(pos.getX()&15),ry-(pos.getY()&15),rz-(pos.getZ()&15)),block,fluid);
                ids.add(cell.key);
            }
            vertices.draw();pixels.clear();GL11.glReadPixels(0,0,WIDTH,HEIGHT,GL11.GL_RGBA,GL11.GL_UNSIGNED_BYTE,pixels);pixels.get(transfer,HEADER,WIDTH*HEIGHT*4);
            for(int at=HEADER;at<HEADER+WIDTH*HEIGHT*4;at+=4) {int alpha=transfer[at+3]&255;if(alpha==0)transfer[at]=transfer[at+1]=transfer[at+2]=0;else if(alpha<255)for(int c=0;c<3;c++)transfer[at+c]=(byte)Math.min(255,((transfer[at+c]&255)*255+alpha/2)/alpha);}
            var header=ByteBuffer.wrap(transfer).order(ByteOrder.LITTLE_ENDIAN);
            header.putInt(0x4843424c).putInt(++sequence).putInt(WIDTH).putInt(HEIGHT).putLong(SESSION).putInt(key.hashCode()).putFloat(x).putFloat(y).putFloat(sx).putFloat(sy).putInt(2);
            header.position(HEADER+WIDTH*HEIGHT*4);header.putInt(ids.size());for(int id:ids)header.putInt(id);header.putInt(sequence);
            Path directory=Path.of(root).getParent().resolve("block_frames");Files.createDirectories(directory);
            try(var output=Files.newOutputStream(directory.resolve("frame-"+(sequence%3)+".bin"))) {output.write(transfer,0,header.position());}
            hadBlocks=!ids.isEmpty();lastScene=key;
        } catch(Exception error) {
            try {client.getBufferBuilders().getEntityVertexConsumers().draw();var buffer=Tessellator.getInstance().getBuffer();if(buffer.isBuilding()) {var built=buffer.endNullable();if(built!=null)built.release();buffer.clear();}}catch(Exception ignored) { }
            retryAt=now+1_000_000_000L;if(now-lastWarning>5_000_000_000L) {lastWarning=now;LoggerFactory.getLogger("HollowCraft").warn("Native block frame unavailable",error);}
        } finally {
            rendering=false;dispatcher.configure(oldWorld,oldCamera,oldTarget);RenderSystem.setShaderFogStart(fogStart);RenderSystem.setShaderFogEnd(fogEnd);client.gameRenderer.getLightmapTextureManager().disable();
        }
    }
    /** FluidRenderer writes chunk-local vertices rather than using the caller's MatrixStack. */
    private record ShiftedVertex(VertexConsumer target,double x,double y,double z) implements VertexConsumer {
        public VertexConsumer vertex(double x,double y,double z) {target.vertex(x+this.x,y+this.y,z+this.z);return this;}
        public VertexConsumer color(int r,int g,int b,int a) {target.color(r,g,b,a);return this;}
        public VertexConsumer texture(float u,float v) {target.texture(u,v);return this;}
        public VertexConsumer overlay(int u,int v) {target.overlay(u,v);return this;}
        public VertexConsumer light(int u,int v) {target.light(u,v);return this;}
        public VertexConsumer normal(float x,float y,float z) {target.normal(x,y,z);return this;}
        public void next() {target.next();}
        public void fixedColor(int r,int g,int b,int a) {target.fixedColor(r,g,b,a);}
        public void unfixColor() {target.unfixColor();}
    }
    public static void close() {clear();if(framebuffer!=null)framebuffer.delete();framebuffer=null;pixels=null;transfer=null;hadBlocks=false;lastScene="";}
    private static float number(com.google.gson.JsonObject input,String key,float fallback) {return input.has(key)?input.get(key).getAsFloat():fallback;}
    private static String text(com.google.gson.JsonObject input,String key) {return input.has(key)?input.get(key).getAsString():"";}
    private static final class FlatCamera extends Camera {void place(double x,double y,double z) {setPos(x,y,z);setRotation(180,0);}}
}
