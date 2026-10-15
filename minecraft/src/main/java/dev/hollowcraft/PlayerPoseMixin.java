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
    @Inject(method="setAngles(Lnet/minecraft/entity/LivingEntity;FFFFF)V",at=@At("HEAD"))
    private void hollowcraft$resetPivots(LivingEntity entity,float limb,float amount,float age,float yaw,float pitch,CallbackInfo callback) {
        if(!dev.hollowcraft.PlayerVisualBridge.rendering||!dev.hollowcraft.PlayerVisualBridge.isAvatar(entity))return;
        // Vanilla does not reset every body pivot each frame. Previous += offsets
        // accumulated in the shared renderer, separating both skin and armor.
        for(var part:new ModelPart[]{head,hat,body,rightArm,leftArm,rightLeg,leftLeg})part.resetTransform();
        riding=dev.hollowcraft.PlayerVisualBridge.riding();
    }
    @Inject(method="setAngles(Lnet/minecraft/entity/LivingEntity;FFFFF)V",at=@At("TAIL"))
    private void hollowcraft$landing(LivingEntity entity,float limb,float amount,float age,float yaw,float pitch,CallbackInfo callback) {
        float elapsed=dev.hollowcraft.PlayerVisualBridge.landing();
        if(!dev.hollowcraft.PlayerVisualBridge.rendering||!dev.hollowcraft.PlayerVisualBridge.isAvatar(entity)||elapsed<0)return;
        float in=smooth(Math.min(1,elapsed/.12f)),out=smooth(Math.max(0,Math.min(1,(elapsed-.65f)/.6f))),weight=in*(1-out);
        body.pitch=net.minecraft.util.math.MathHelper.lerp(weight,body.pitch,1.22f);
        body.yaw=0;body.roll=0;
        rightLeg.pitch=net.minecraft.util.math.MathHelper.lerp(weight,rightLeg.pitch,-1.15f);
        leftLeg.pitch=net.minecraft.util.math.MathHelper.lerp(weight,leftLeg.pitch,1.35f);
        rightLeg.yaw=net.minecraft.util.math.MathHelper.lerp(weight,rightLeg.yaw,-.08f);
        leftLeg.yaw=net.minecraft.util.math.MathHelper.lerp(weight,leftLeg.yaw,.08f);
        // One connected torso: shoulders and hips are the transformed body
        // endpoints. Move the root down until the supporting boot remains at y=24.
        float hipY=12*(float)Math.cos(body.pitch),hipZ=12*(float)Math.sin(body.pitch);
        float rightFoot=12*(float)Math.cos(rightLeg.pitch)+2*Math.abs((float)Math.sin(rightLeg.pitch));
        float leftFoot=12*(float)Math.cos(leftLeg.pitch)+2*Math.abs((float)Math.sin(leftLeg.pitch));
        float drop=24-hipY-Math.max(rightFoot,leftFoot);
        body.setPivot(0,drop,0);head.setPivot(0,drop,0);
        head.pitch=net.minecraft.util.math.MathHelper.lerp(weight,head.pitch,-.12f);
        float shoulderY=drop+2*(float)Math.cos(body.pitch),shoulderZ=2*(float)Math.sin(body.pitch);
        rightArm.setPivot(-5,shoulderY,shoulderZ);leftArm.setPivot(5,shoulderY,shoulderZ);
        rightArm.pitch=net.minecraft.util.math.MathHelper.lerp(weight,rightArm.pitch,.2f);
        leftArm.pitch=net.minecraft.util.math.MathHelper.lerp(weight,leftArm.pitch,-1.25f);
        rightArm.roll=net.minecraft.util.math.MathHelper.lerp(weight,rightArm.roll,0);
        leftArm.roll=net.minecraft.util.math.MathHelper.lerp(weight,leftArm.roll,-.15f);
        rightLeg.setPivot(-1.9f,drop+hipY,hipZ);leftLeg.setPivot(1.9f,drop+hipY,hipZ);
        hat.copyTransform(head);
        var model=(PlayerEntityModel<?>)(Object)this;
        model.jacket.copyTransform(body);model.rightSleeve.copyTransform(rightArm);model.leftSleeve.copyTransform(leftArm);
        model.rightPants.copyTransform(rightLeg);model.leftPants.copyTransform(leftLeg);
    }
    private static float smooth(float value) {return value*value*(3-2*value);}
}
