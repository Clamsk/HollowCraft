package dev.hollowcraft;

import net.minecraft.client.MinecraftClient;
import net.minecraft.client.model.*;
import net.minecraft.client.network.AbstractClientPlayerEntity;
import net.minecraft.client.render.*;
import net.minecraft.client.render.entity.feature.FeatureRenderer;
import net.minecraft.client.render.entity.feature.FeatureRendererContext;
import net.minecraft.client.render.entity.model.PlayerEntityModel;
import net.minecraft.client.texture.NativeImageBackedTexture;
import net.minecraft.client.util.math.MatrixStack;
import net.minecraft.util.Identifier;

/** Eyes are attached to the same head bone and participate in MC's real depth test. */
public final class BridgeEyesFeature extends FeatureRenderer<AbstractClientPlayerEntity,PlayerEntityModel<AbstractClientPlayerEntity>> {
    private final ModelPart eyes;
    private static Identifier white;
    public BridgeEyesFeature(FeatureRendererContext<AbstractClientPlayerEntity,PlayerEntityModel<AbstractClientPlayerEntity>> context) {
        super(context);var data=new ModelData();
        data.getRoot().addChild("eyes",ModelPartBuilder.create().uv(0,0)
            .cuboid(-3,-4.5f,-4.04f,2,1,.04f).cuboid(1,-4.5f,-4.04f,2,1,.04f),ModelTransform.NONE);
        eyes=TexturedModelData.of(data,1,1).createModel().getChild("eyes");
    }
    @Override public void render(MatrixStack matrices,VertexConsumerProvider consumers,int light,AbstractClientPlayerEntity entity,float limb,float amount,float delta,float age,float yaw,float pitch) {
        boolean avatar=PlayerVisualBridge.rendering&&PlayerVisualBridge.isAvatar(entity);
        boolean inventory=NativeGuiBridge.rendering()&&entity==MinecraftClient.getInstance().player;
        if((!avatar&&!inventory)||!entity.isCreative())return;
        if(white==null) {
            var texture=new NativeImageBackedTexture(1,1,false);texture.getImage().setColor(0,0,-1);texture.upload();
            white=MinecraftClient.getInstance().getTextureManager().registerDynamicTexture("hollowcraft_eyes",texture);
        }
        matrices.push();getContextModel().head.rotate(matrices);
        eyes.render(matrices,consumers.getBuffer(RenderLayer.getEntityCutoutNoCull(white)),LightmapTextureManager.MAX_LIGHT_COORDINATE,OverlayTexture.DEFAULT_UV);
        matrices.pop();
    }
}
