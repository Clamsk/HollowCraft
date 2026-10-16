package dev.hollowcraft;

import com.google.gson.JsonObject;
import com.mojang.authlib.GameProfile;
import com.mojang.blaze3d.systems.RenderSystem;
import com.mojang.blaze3d.systems.VertexSorter;
import java.lang.reflect.Method;
import java.nio.*;
import java.nio.file.*;
import java.util.UUID;
import java.util.Map;
import java.lang.reflect.Field;
import net.minecraft.client.MinecraftClient;
import net.minecraft.client.gl.SimpleFramebuffer;
import net.minecraft.client.gui.DrawContext;
import net.minecraft.client.gui.screen.ingame.InventoryScreen;
import net.minecraft.client.network.OtherClientPlayerEntity;
import net.minecraft.client.world.ClientWorld;
import net.minecraft.entity.EquipmentSlot;
import net.minecraft.entity.EntityPose;
import net.minecraft.item.ItemStack;
import net.minecraft.util.Identifier;
import org.joml.Matrix4f;
import org.joml.Quaternionf;
import org.lwjgl.opengl.GL11;
import org.slf4j.LoggerFactory;

/** Actual MC player renderer, feature layers and mod animation stack, with transparent depth. */
public final class PlayerVisualBridge {
    private static final int SIZE=256,WING_SIZE=384,SCALE=64;
    private static SimpleFramebuffer framebuffer;
    private static VisualPlayer avatar;
    private static ByteBuffer pixels;
    private static byte[] transfer;
    private static Method playAttack,stopAttack,updateAnimations;
    private static Field animationRegistry;
    private static Object mainHand,twoHands;
    private static int attackSerial=-1,sequence;
    private static int avatarVisit=-1;
    private static int waterSerial=-1,waterPhase=-1;
    private static int itemSwingSerial=-1;
    private static int zenithSerial=-1;
    private static net.minecraft.entity.LimbAnimator walk;
    private static long lastRender,lastWarning;
    public static boolean rendering;
    public static boolean isAvatar(net.minecraft.entity.Entity entity) {return entity==avatar;}
    public static float landing() {return number(PlayerRenderState.controls,"landing",-1);}
    public static boolean riding() {return PlayerRenderState.latest!=null&&PlayerRenderState.latest.riding;}
    public static boolean wingActive() {return flag(PlayerRenderState.controls,"wingActive");}
    public static float wingPhase() {return number(PlayerRenderState.controls,"wingPhase",0);}
    public static String wingState() {return text(PlayerRenderState.controls,"wingState","ground");}

    static void tick(MinecraftClient client) {
        var state=PlayerRenderState.latest;var input=PlayerRenderState.controls;
        if(client.world==null||state==null||input==null||!state.scene.equals(text(input,"scene",""))||!state.campaign.equals(text(input,"campaign",""))||state.visit!=(int)number(input,"visit",-1))return;
        if(HollowCraftBridge.remotePaused)return;
        try {
            if(avatar==null||avatar.clientWorld!=client.world||avatarVisit!=state.visit) {
                avatar=new VisualPlayer(client.world);attackSerial=-1;
                avatarVisit=state.visit;waterSerial=waterPhase=itemSwingSerial=zenithSerial=-1;
                walk=new net.minecraft.entity.LimbAnimator();
                Class<?> api=Class.forName("net.bettercombat.client.animation.PlayerAttackAnimatable");
                Class<?> hands=Class.forName("net.bettercombat.logic.AnimatedHand");
                playAttack=api.getMethod("playAttackAnimation",String.class,hands,float.class,float.class);
                stopAttack=api.getMethod("stopAttackAnimation",float.class);updateAnimations=api.getMethod("updateAnimationsOnTick");
                animationRegistry=Class.forName("net.bettercombat.client.animation.AnimationRegistry").getField("animations");
                mainHand=hands.getField("MAIN_HAND").get(null);twoHands=hands.getField("TWO_HANDED").get(null);
            }
            avatar.tick();
            if(flag(input,"zenithSwing")&&!state.using) {
                int serial=(int)number(input,"zenithSerial",0);
                if(serial!=zenithSerial){avatar.swingHand(net.minecraft.util.Hand.MAIN_HAND);zenithSerial=serial;}
            }
            avatar.fishHook=client.player==null?null:client.player.fishHook;
            for(var slot:EquipmentSlot.values()) {
                var item=state.equipment[slot.ordinal()];
                if(!ItemStack.areEqual(avatar.getEquippedStack(slot),item))avatar.equipStack(slot,item.copy());
            }
            float water=number(input,"waterLanding",-1);
            if(water>=0&&water<.6f&&!state.using&&!flag(input,"swinging")) {
                int serial=(int)number(input,"waterLandingSerial",0),phase=(int)number(input,"waterLandingPhase",0);
                if(serial!=waterSerial||phase!=waterPhase) {avatar.swingHand(number(input,"waterLandingHand",0)==1?net.minecraft.util.Hand.OFF_HAND:net.minecraft.util.Hand.MAIN_HAND);waterSerial=serial;waterPhase=phase;}
            } else {waterSerial=waterPhase=-1;}
            if(flag(input,"itemSwing")&&!state.using&&!flag(input,"swinging")) {
                int useSerial=(int)number(input,"itemSwingSerial",0);
                if(useSerial!=itemSwingSerial) {avatar.swingHand(number(input,"itemSwingHand",0)==1?net.minecraft.util.Hand.OFF_HAND:net.minecraft.util.Hand.MAIN_HAND);itemSwingSerial=useSerial;}
            } else if(!flag(input,"itemSwing"))itemSwingSerial=-1;
            avatar.creative=state.creative;
            avatar.getAbilities().allowFlying=state.creative;avatar.getAbilities().flying=state.creative&&flag(input,"flying");
            avatar.setSneaking(flag(input,"crouching"));avatar.setSprinting(flag(input,"sprinting"));
            avatar.setPose(flag(input,"gliding")?EntityPose.FALL_FLYING:flag(input,"crouching")?EntityPose.CROUCHING:EntityPose.STANDING);
            avatar.flight(flag(input,"gliding"));
            avatar.setOnGround(flag(input,"grounded"));avatar.hurtTime=state.hurtTicks;avatar.deathTime=0;
            avatar.setFireTicks(state.onFire?10:0);
            float speed=number(input,"speed",0);
            walk.updateLimbs(Math.min(Math.abs(speed)*.2f,1),.4f);
            var limbs=(dev.hollowcraft.mixin.LimbAnimatorAccess)avatar.limbAnimator;
            limbs.hollowcraft$previousSpeed(walk.getSpeed(0));limbs.hollowcraft$speed(walk.getSpeed());limbs.hollowcraft$position(walk.getPos());
            avatar.setVelocity(speed/20,number(input,"vy",0)/20,0);
            avatar.prevX=avatar.getX()-speed/20;avatar.prevZ=avatar.getZ();
            if(state.using) {
                var active=avatar.getStackInHand(state.activeHand);
                avatar.applyUse(active,state.activeHand,state.useTicks);
                if(attackSerial!=-1) {stopAttack.invoke(avatar,1f);attackSerial=-1;}
            } else avatar.endUse();
            int serial=(int)number(input,"attackSerial",0);
            if(!state.using&&flag(input,"swinging")&&serial!=attackSerial) {
                attackSerial=serial;String animation=text(input,"animation","");
                // Better Combat dereferences missing registry entries. Unknown/fallback
                // animations must still render a complete vanilla hand swing.
                if(!animation.isEmpty()&&((Map<?,?>)animationRegistry.get(null)).containsKey(animation))playAttack.invoke(avatar,animation,flag(input,"twoHanded")?twoHands:mainHand,
                    number(input,"duration",.5f)*20,number(input,"upswing",.3f));
                else avatar.swingHand(net.minecraft.util.Hand.MAIN_HAND);
            } else if(!flag(input,"swinging")&&attackSerial!=-1) {stopAttack.invoke(avatar,3f);attackSerial=-1;}
            updateAnimations.invoke(avatar);
            if(System.nanoTime()-lastRender>45_000_000L)render(client,1);
        } catch(Exception e) {warn(e);}
    }
    public static void render(MinecraftClient client,float delta) {
        var state=PlayerRenderState.latest;var input=PlayerRenderState.controls;
        String root=System.getProperty("hollowcraft.icons","");
        if(root.isEmpty()||avatar==null||state==null||input==null||avatarVisit!=state.visit||!state.scene.equals(text(input,"scene",""))||!state.campaign.equals(text(input,"campaign",""))||state.visit!=(int)number(input,"visit",-1))return;
        for(var slot:EquipmentSlot.values())if(!ItemStack.areEqual(avatar.getEquippedStack(slot),state.equipment[slot.ordinal()]))avatar.equipStack(slot,state.equipment[slot.ordinal()].copy());
        long now=System.nanoTime();if(now-lastRender<30_000_000L)return;
        lastRender=now;
        Matrix4f projection=new Matrix4f(RenderSystem.getProjectionMatrix());
        var view=RenderSystem.getModelViewStack();view.push();
        var dispatcher=client.getEntityRenderDispatcher();var cameraRotation=new Quaternionf(dispatcher.getRotation());
        float yaw=180+number(input,"viewYaw",0);
        avatar.bodyYaw=avatar.prevBodyYaw=avatar.headYaw=avatar.prevHeadYaw=yaw;avatar.setYaw(yaw);avatar.prevYaw=yaw;
        avatar.setPitch(number(input,"pitch",0));avatar.prevPitch=avatar.getPitch();
        int size=TerrariaEasterEgg.wings(avatar)?WING_SIZE:SIZE;
        try {
            if(framebuffer==null||framebuffer.textureWidth!=size) {
                if(framebuffer!=null)framebuffer.delete();
                framebuffer=new SimpleFramebuffer(size,size,true,MinecraftClient.IS_SYSTEM_MAC);
                pixels=ByteBuffer.allocateDirect(size*size*4);transfer=new byte[32+size*size*4];
            }
            framebuffer.setClearColor(0,0,0,0);framebuffer.clear(MinecraftClient.IS_SYSTEM_MAC);framebuffer.beginWrite(true);
            RenderSystem.enableDepthTest();
            RenderSystem.setProjectionMatrix(new Matrix4f().setOrtho(0,size,size,0,1000,21000),VertexSorter.BY_Z);
            view.loadIdentity();view.translate(0,0,-11000);RenderSystem.applyModelViewMatrix();
            rendering=true;
            DrawContext draw=new DrawContext(client,client.getBufferBuilders().getEntityVertexConsumers());
            InventoryScreen.drawEntity(draw,size/2,size-48,SCALE,new Quaternionf().rotateZ((float)Math.PI),new Quaternionf(),avatar);
            draw.draw();
            pixels.clear();GL11.glReadPixels(0,0,size,size,GL11.GL_RGBA,GL11.GL_UNSIGNED_BYTE,pixels);
            ByteBuffer header=ByteBuffer.wrap(transfer).order(ByteOrder.LITTLE_ENDIAN);
            header.putInt(0x48435039).putInt(++sequence).putInt(size).putInt(size).putLong(FRAME_SESSION).putInt((state.scene+"|"+state.campaign+"|"+state.visit).hashCode());
            pixels.get(transfer,28,size*size*4);
            header.putInt(28+size*size*4,sequence);
            Path directory=Path.of(root).getParent().resolve("player_frames");Files.createDirectories(directory);
            // Windows/Unity can deny rename even with a delete-shared reader. Rotate
            // three writable files; readers reject incomplete or mixed-sequence frames.
            Files.write(directory.resolve("frame-"+(sequence%3)+".bin"),transfer);
        } catch(Exception e) {warn(e);}
        finally {
            rendering=false;dispatcher.setRotation(cameraRotation);dispatcher.setRenderShadows(true);
            view.pop();RenderSystem.applyModelViewMatrix();RenderSystem.setProjectionMatrix(projection,VertexSorter.BY_Z);
            client.getFramebuffer().beginWrite(true);
        }
    }
    private static final long FRAME_SESSION=System.currentTimeMillis();
    private static void warn(Exception e) {if(System.nanoTime()-lastWarning>5_000_000_000L){lastWarning=System.nanoTime();LoggerFactory.getLogger("HollowCraft").warn("Native player renderer",e);}}
    private static float number(JsonObject data,String key,float fallback) {return data!=null&&data.has(key)?data.get(key).getAsFloat():fallback;}
    private static String text(JsonObject data,String key,String fallback) {return data!=null&&data.has(key)?data.get(key).getAsString():fallback;}
    private static boolean flag(JsonObject data,String key) {return data!=null&&data.has(key)&&data.get(key).getAsBoolean();}
    private static final class VisualPlayer extends OtherClientPlayerEntity {
        boolean creative;
        VisualPlayer(ClientWorld world) {super(world,new GameProfile(UUID.fromString("3d41453d-09f0-4e99-b4a7-38f943878f92"),"Steve"));setNoGravity(true);}
        @Override public Identifier getSkinTexture() {return BridgeSkin.texture(creative);}
        @Override public String getModel() {return "default";}
        @Override public boolean canRenderCapeTexture() {return false;}
        @Override public boolean isCreative() {return creative;}
        @Override public boolean isSpectator() {return false;}
        @Override public void playSound(net.minecraft.sound.SoundEvent sound,float volume,float pitch) { }
        void flight(boolean value) {setFlag(7,value);}
        void applyUse(ItemStack stack,net.minecraft.util.Hand hand,int ticks) {
            // Vanilla setCurrentHand sets these flags only on a server world. This
            // isolated client avatar has no entity tracker, so copy both flags explicitly.
            setLivingFlag(1,true);setLivingFlag(2,hand==net.minecraft.util.Hand.OFF_HAND);
            activeItemStack=stack;itemUseTimeLeft=Math.max(1,stack.getMaxUseTime()-ticks);
        }
        void endUse() {setLivingFlag(1,false);setLivingFlag(2,false);activeItemStack=ItemStack.EMPTY;itemUseTimeLeft=0;}
    }
}
