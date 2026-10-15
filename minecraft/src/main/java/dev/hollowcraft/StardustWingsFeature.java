package dev.hollowcraft;

import net.minecraft.client.network.AbstractClientPlayerEntity;
import net.minecraft.client.render.*;
import net.minecraft.client.render.entity.feature.FeatureRenderer;
import net.minecraft.client.render.entity.feature.FeatureRendererContext;
import net.minecraft.client.render.entity.model.PlayerEntityModel;
import net.minecraft.client.util.math.MatrixStack;
import net.minecraft.util.math.RotationAxis;

/** Original four-frame wing sprites, attached to the same torso transform as armor. */
public final class StardustWingsFeature extends FeatureRenderer<AbstractClientPlayerEntity,PlayerEntityModel<AbstractClientPlayerEntity>> {
    public StardustWingsFeature(FeatureRendererContext<AbstractClientPlayerEntity,PlayerEntityModel<AbstractClientPlayerEntity>> context) {super(context);}
    @Override public void render(MatrixStack matrices,VertexConsumerProvider consumers,int light,AbstractClientPlayerEntity player,float limb,float distance,float delta,float age,float yaw,float pitch) {
        if(!TerrariaEasterEgg.wings(player)||player.isInvisible())return;
        boolean flying=PlayerVisualBridge.isAvatar(player)&&PlayerVisualBridge.wingActive();
        float phase=PlayerVisualBridge.isAvatar(player)?PlayerVisualBridge.wingPhase():age/5;
        int frame=flying?1+Math.floorMod((int)phase,3):0;
        var vertices=consumers.getBuffer(RenderLayer.getEntityTranslucent(TerrariaEasterEgg.id("textures/item/stardust_wing_"+frame+".png")));
        matrices.push();getContextModel().body.rotate(matrices);matrices.translate(0,.2,.17);
        for(int side:new int[]{-1,1}) {
            matrices.push();matrices.multiply(RotationAxis.POSITIVE_Y.rotationDegrees(side*(flying?30+25*(float)Math.sin(phase*Math.PI/1.5):65)));
            float x=side*1.6125f,u0=side<0?0:.5f,u1=side<0?.5f:1;
            // Two-sided transparent planes preserve the original pixel silhouette.
            quad(matrices,vertices,Math.min(0,x),Math.max(0,x),u0,u1,false);
            quad(matrices,vertices,Math.min(0,x),Math.max(0,x),u0,u1,true);
            matrices.pop();
        }
        matrices.pop();
    }
    private static void quad(MatrixStack stack,VertexConsumer out,float left,float right,float u0,float u1,boolean reverse) {
        float[][] points={{left,-.6f,u0,0},{right,-.6f,u1,0},{right,1.725f,u1,1},{left,1.725f,u0,1}};
        for(int n=0;n<4;n++){var p=points[reverse?3-n:n];out.vertex(stack.peek().getPositionMatrix(),p[0],p[1],0).color(255,255,255,255).texture(p[2],p[3]).overlay(OverlayTexture.DEFAULT_UV).light(LightmapTextureManager.MAX_LIGHT_COORDINATE).normal(stack.peek().getNormalMatrix(),0,0,reverse?-1:1).next();}
    }
}
