package dev.hollowcraft.mixin;

import net.minecraft.client.model.ModelPart;
import net.minecraft.client.render.entity.model.*;
import net.minecraft.entity.LivingEntity;
import org.spongepowered.asm.mixin.Mixin;
import org.spongepowered.asm.mixin.injection.*;
import org.spongepowered.asm.mixin.injection.callback.CallbackInfo;

@Mixin(PlayerEntityModel.class)
public abstract class PlayerPoseMixin extends BipedEntityModel<LivingEntity> {
    public PlayerPoseMixin(ModelPart root) {super(root);}
    @Inject(method="setAngles(Lnet/minecraft/entity/LivingEntity;FFFFF)V",at=@At("TAIL"))
    private void hollowcraft$landing(LivingEntity entity,float limb,float amount,float age,float yaw,float pitch,CallbackInfo callback) {
        float elapsed=dev.hollowcraft.PlayerVisualBridge.landing();
        if(!dev.hollowcraft.PlayerVisualBridge.rendering||!dev.hollowcraft.PlayerVisualBridge.isAvatar(entity)||elapsed<0)return;
        float weight=Math.min(1,elapsed/.08f)*(1-Math.min(1,Math.max(0,(elapsed-.8f)/.45f)));
        body.pitch=net.minecraft.util.math.MathHelper.lerp(weight,body.pitch,.95f);body.pivotY+=7*weight;
        head.pivotY+=7*weight;head.pivotZ-=4*weight;head.pitch=net.minecraft.util.math.MathHelper.lerp(weight,head.pitch,-.15f);
        rightArm.pivotY+=12*weight;rightArm.pivotZ-=4*weight;rightArm.pitch*=1-weight;
        leftArm.pivotY+=6*weight;leftArm.pitch=net.minecraft.util.math.MathHelper.lerp(weight,leftArm.pitch,-1.4f);
        rightLeg.pitch=net.minecraft.util.math.MathHelper.lerp(weight,rightLeg.pitch,-1.2f);
        leftLeg.pitch=net.minecraft.util.math.MathHelper.lerp(weight,leftLeg.pitch,.3f);
        rightLeg.pivotY+=3*weight;leftLeg.pivotY+=3*weight;
        hat.copyTransform(head);
        var model=(PlayerEntityModel<?>)(Object)this;
        model.jacket.copyTransform(body);model.rightSleeve.copyTransform(rightArm);model.leftSleeve.copyTransform(leftArm);
        model.rightPants.copyTransform(rightLeg);model.leftPants.copyTransform(leftLeg);
    }
}
