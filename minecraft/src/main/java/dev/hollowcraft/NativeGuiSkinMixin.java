package dev.hollowcraft.mixin;
import net.minecraft.client.MinecraftClient;
import net.minecraft.client.network.AbstractClientPlayerEntity;
import net.minecraft.util.Identifier;
import org.spongepowered.asm.mixin.Mixin;
import org.spongepowered.asm.mixin.injection.*;
import org.spongepowered.asm.mixin.injection.callback.CallbackInfoReturnable;
@Mixin(AbstractClientPlayerEntity.class)
public abstract class NativeGuiSkinMixin {
    @Inject(method="getSkinTexture",at=@At("RETURN"),cancellable=true)
    private void hollowcraft$steve(CallbackInfoReturnable<Identifier> callback) {if(dev.hollowcraft.NativeGuiBridge.rendering()&&(Object)this==MinecraftClient.getInstance().player)callback.setReturnValue(new Identifier("minecraft","textures/entity/player/wide/steve.png"));}
    @Inject(method="getModel",at=@At("RETURN"),cancellable=true)
    private void hollowcraft$wide(CallbackInfoReturnable<String> callback) {if(dev.hollowcraft.NativeGuiBridge.rendering()&&(Object)this==MinecraftClient.getInstance().player)callback.setReturnValue("default");}
}
