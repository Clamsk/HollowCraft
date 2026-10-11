package dev.hollowcraft.mixin;

import net.minecraft.client.sound.*;
import org.spongepowered.asm.mixin.Mixin;
import org.spongepowered.asm.mixin.injection.*;
import org.spongepowered.asm.mixin.injection.callback.CallbackInfo;

@Mixin(SoundSystem.class)
public abstract class NativeSoundMixin {
    @Inject(method="play(Lnet/minecraft/client/sound/SoundInstance;)V",at=@At(value="INVOKE",target="Lnet/minecraft/client/sound/SoundInstance;getSoundSet(Lnet/minecraft/client/sound/SoundManager;)Lnet/minecraft/client/sound/WeightedSoundSet;",shift=At.Shift.AFTER),cancellable=true)
    private void hollowcraft$selectedSound(SoundInstance sound,CallbackInfo callback) {
        if(dev.hollowcraft.NativeAudio.capture(sound))callback.cancel();
    }
    @Inject(method="reloadSounds",at=@At("HEAD"))
    private void hollowcraft$reload(CallbackInfo callback) {dev.hollowcraft.NativeAudio.reload();}
    @Inject(method="stopAll",at=@At("HEAD"))
    private void hollowcraft$stop(CallbackInfo callback) {dev.hollowcraft.HollowCraftBridge.audioStop();}
}
