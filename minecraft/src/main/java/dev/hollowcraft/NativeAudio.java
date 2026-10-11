package dev.hollowcraft;

import com.google.gson.JsonObject;
import java.nio.file.*;
import java.security.MessageDigest;
import java.util.HexFormat;
import java.util.concurrent.*;
import java.util.concurrent.atomic.AtomicInteger;
import net.minecraft.client.MinecraftClient;
import net.minecraft.client.sound.*;
import net.minecraft.sound.SoundCategory;
import net.minecraft.util.Identifier;
import net.minecraft.util.math.MathHelper;

/** Export the sound already chosen by vanilla, then transfer only after HK decoded it. */
public final class NativeAudio {
    private static final ConcurrentMap<Identifier,CompletableFuture<String>> files=new ConcurrentHashMap<>();
    private static final AtomicInteger exporting=new AtomicInteger();
    private static long lastWarning,rateWindow;
    private static int sent;
    public static void reload() {files.clear();HollowCraftBridge.audioStop();}
    public static boolean capture(SoundInstance instance) {
        var client=MinecraftClient.getInstance();var state=PlayerRenderState.latest;
        if(!client.isOnThread()||state==null||client.world==null||!HollowCraftBridge.audioRequested())return false;
        var sound=instance.getSound();
        if(sound==null||sound==SoundManager.MISSING_SOUND||sound==SoundManager.INTENTIONALLY_EMPTY_SOUND||sound.isStreamed()||instance.isRepeatable()||instance instanceof TickableSoundInstance
            ||instance.getCategory()==SoundCategory.MUSIC||instance.getCategory()==SoundCategory.RECORDS||instance.getCategory()==SoundCategory.AMBIENT)return false;
        double x=instance.getX()-state.offset,y=instance.getY()-64;
        if(!instance.isRelative()&&(Math.abs(x-state.x)>48||Math.abs(y-state.y)>48||Math.abs(instance.getZ())>8))return false;
        String root=System.getProperty("hollowcraft.audio","");if(root.isEmpty())return false;
        try {
            var location=sound.getLocation();var future=files.get(location);
            if(future==null) {
                if(exporting.get()>=8)return false;
                var resource=client.getResourceManager().getResource(location);if(resource.isEmpty())return false;
                if(files.size()>=512)files.clear();exporting.incrementAndGet();
                future=CompletableFuture.supplyAsync(()-> {
                    try(var stream=resource.get().getInputStream()) {
                        byte[] bytes=stream.readNBytes(2*1024*1024+1);
                        if(bytes.length>2*1024*1024)throw new IllegalArgumentException("Audio sample too large");
                        String key=HexFormat.of().formatHex(MessageDigest.getInstance("SHA-256").digest(bytes));
                        Path directory=Path.of(root);Files.createDirectories(directory);Path output=directory.resolve(key+".ogg");
                        if(!Files.exists(output)) {
                            Path temp=Files.createTempFile(directory,"audio-",".tmp");
                            try {Files.write(temp,bytes);Files.move(temp,output,StandardCopyOption.REPLACE_EXISTING,StandardCopyOption.ATOMIC_MOVE);}
                            finally {Files.deleteIfExists(temp);}
                        }
                        var warm=new JsonObject();warm.addProperty("type","mcSound");warm.addProperty("key",key);warm.addProperty("play",false);HollowCraftBridge.audioEvent(warm);
                        return key;
                    } catch(Exception error) {throw new CompletionException(error);}
                    finally {exporting.decrementAndGet();}
                });files.put(location,future);return false;
            }
            if(!future.isDone())return false;String key=future.getNow(null);if(key==null)return false;
            boolean ready=HollowCraftBridge.audioReady(key);
            long now=System.nanoTime();if(now-rateWindow>=1_000_000_000L) {rateWindow=now;sent=0;}
            if(++sent>128)return false;
            var event=new JsonObject();event.addProperty("type","mcSound");event.addProperty("key",key);event.addProperty("play",ready);
            if(ready) {
                float volume=instance.getVolume(),pitch=instance.getPitch();if(!Float.isFinite(volume)||!Float.isFinite(pitch))return false;
                event.addProperty("volume",MathHelper.clamp(volume*client.options.getSoundVolume(instance.getCategory()),0,1)*client.options.getSoundVolume(SoundCategory.MASTER));
                event.addProperty("pitch",MathHelper.clamp(pitch,.5f,2));event.addProperty("x",x);event.addProperty("y",y);
                event.addProperty("relative",instance.isRelative());event.addProperty("linear",instance.getAttenuationType()==SoundInstance.AttenuationType.LINEAR);
                event.addProperty("distance",Math.max(volume,1)*sound.getAttenuation());
            }
            return HollowCraftBridge.audioEvent(event)&&ready;
        } catch(Exception error) {
            long now=System.nanoTime();if(now-lastWarning>5_000_000_000L) {lastWarning=now;org.slf4j.LoggerFactory.getLogger("HollowCraft").warn("Native audio export unavailable; Minecraft playback retained",error);}
            return false;
        }
    }
}
