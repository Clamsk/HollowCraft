package dev.hollowcraft.mixin;

import net.minecraft.client.network.AbstractClientPlayerEntity;
import net.minecraft.client.render.entity.*;
import net.minecraft.client.render.entity.model.PlayerEntityModel;
import org.spongepowered.asm.mixin.Mixin;
import org.spongepowered.asm.mixin.injection.*;
import org.spongepowered.asm.mixin.injection.callback.CallbackInfo;

@Mixin(PlayerEntityRenderer.class)
public abstract class PlayerRendererMixin extends LivingEntityRenderer<AbstractClientPlayerEntity,PlayerEntityModel<AbstractClientPlayerEntity>> {
    protected PlayerRendererMixin(EntityRendererFactory.Context context,PlayerEntityModel<AbstractClientPlayerEntity> model,float shadow) {super(context,model,shadow);}
    @Inject(method="renderLabelIfPresent(Lnet/minecraft/client/network/AbstractClientPlayerEntity;Lnet/minecraft/text/Text;Lnet/minecraft/client/util/math/MatrixStack;Lnet/minecraft/client/render/VertexConsumerProvider;I)V",at=@At("HEAD"),cancellable=true)
    private void hollowcraft$hideLabel(AbstractClientPlayerEntity player,net.minecraft.text.Text text,net.minecraft.client.util.math.MatrixStack matrices,net.minecraft.client.render.VertexConsumerProvider consumers,int light,CallbackInfo callback) {
        if(dev.hollowcraft.PlayerVisualBridge.isAvatar(player))callback.cancel();
    }
}
