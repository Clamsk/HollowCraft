package dev.hollowcraft;

import net.minecraft.client.model.*;
import net.minecraft.client.network.AbstractClientPlayerEntity;
import net.minecraft.client.render.*;
import net.minecraft.client.render.entity.feature.FeatureRenderer;
import net.minecraft.client.render.entity.feature.FeatureRendererContext;
import net.minecraft.client.render.entity.model.PlayerEntityModel;
import net.minecraft.client.util.math.MatrixStack;
import net.minecraft.util.Identifier;
import net.minecraft.util.math.MathHelper;

/** Authored articulated voxel wings. No billboard or independently positioned sprites. */
public final class StardustWingsFeature extends FeatureRenderer<AbstractClientPlayerEntity,PlayerEntityModel<AbstractClientPlayerEntity>> {
    private static final RenderLayer LAYER=RenderLayer.getEntityCutoutNoCull(new Identifier("minecraft","textures/block/white_concrete.png"));
    private final Rig shell=new Rig(false),core=new Rig(true);
    private float spread,fold=1,beat;
    private long previous;
    public StardustWingsFeature(FeatureRendererContext<AbstractClientPlayerEntity,PlayerEntityModel<AbstractClientPlayerEntity>> context) {super(context);}
    @Override public void render(MatrixStack matrices,VertexConsumerProvider consumers,int light,AbstractClientPlayerEntity player,float limb,float distance,float delta,float age,float yaw,float pitch) {
        if(!TerrariaEasterEgg.wings(player)||player.isInvisible()){previous=0;return;}
        String state=PlayerVisualBridge.isAvatar(player)?PlayerVisualBridge.wingState():player.isOnGround()?"ground":"fall";
        boolean flying=state.equals("fly"),gliding=state.equals("glide"),air=!state.equals("ground")&&!state.equals("none");
        long now=System.nanoTime();float dt=previous==0?1:Math.min(.1f,(now-previous)/1e9f);previous=now;
        float weight=HollowCraftBridge.remotePaused?0:1-(float)Math.exp(-dt*14);
        spread=MathHelper.lerp(weight,spread,flying?1:gliding?.95f:air?.55f:0);
        fold=MathHelper.lerp(weight,fold,flying?.1f:gliding?0:air?.35f:1);
        beat=PlayerVisualBridge.isAvatar(player)?PlayerVisualBridge.wingPhase()*1.6f:age*.5f;
        float flap=flying?(float)Math.sin(beat)*.48f:gliding?(float)Math.sin(age*.12f)*.045f:0;
        // The animation library supplies torso bending in its upper-body layer.
        shell.pose(getContextModel().body,spread,fold,flap);core.pose(getContextModel().body,spread,fold,flap);
        var out=consumers.getBuffer(LAYER);
        shell.mount.render(matrices,out,light,OverlayTexture.DEFAULT_UV,.055f,.27f,.78f,1);
        core.mount.render(matrices,out,LightmapTextureManager.MAX_LIGHT_COORDINATE,OverlayTexture.DEFAULT_UV,.2f,.85f,1,1);
    }
    private static final class Rig {
        final ModelPart mount,left,right,leftFan,rightFan;
        Rig(boolean glow) {
            var data=new ModelData();var root=data.getRoot();var anchor=root.addChild("back",ModelPartBuilder.create(),ModelTransform.NONE);
            for(int sign:new int[]{-1,1}) {
                String side=sign<0?"left":"right";
                var wing=anchor.addChild(side,box(sign,0,-.85f,-.9f,7,1.7f,1.8f,glow),ModelTransform.pivot(sign*2.5f,2.1f,3.4f));
                var fan=wing.addChild("fan",box(sign,0,-.7f,-.7f,5.5f,1.4f,1.4f,glow),ModelTransform.pivot(sign*6.6f,.2f,.2f));
                for(int feather=0;feather<7;feather++) {
                    float length=10.5f-(float)Math.abs(feather-2)*.9f,width=1.8f-feather*.08f;
                    var plume=fan.addChild("plume"+feather,box(sign,0,0,-.6f,length*.55f,width,1.2f,glow),ModelTransform.of(sign*(.4f+feather*.62f),feather*.23f,feather*.09f,0,sign*(feather-3)*.045f,sign*(-.32f+feather*.205f)));
                    var outer=plume.addChild("curve",box(sign,0,0,-.5f,length*.3f,width*.8f,1,glow),ModelTransform.of(sign*length*.52f,.05f,0,0,sign*.08f,sign*.22f));
                    outer.addChild("tip",box(sign,0,0,-.4f,length*.22f,width*.48f,.8f,glow),ModelTransform.of(sign*length*.27f,.04f,0,0,sign*.08f,sign*.3f));
                }
            }
            mount=TexturedModelData.of(data,16,16).createModel().getChild("back");
            left=mount.getChild("left");right=mount.getChild("right");leftFan=left.getChild("fan");rightFan=right.getChild("fan");
            left.xScale=left.yScale=left.zScale=right.xScale=right.yScale=right.zScale=1.45f;
        }
        void pose(ModelPart body,float spread,float fold,float flap) {
            mount.copyTransform(body);
            // Model-space +Z is the back. Mirrored wings extend along opposite X
            // axes, so the left hinge needs POSITIVE yaw to fold toward +Z.
            // Keep the full flight stroke behind the shoulders as well.
            left.yaw=.3f+fold*.65f+flap*.22f;right.yaw=-left.yaw;
            // Fold down along the back rather than into a short shoulder-level fan.
            left.roll=.28f-fold*1.35f;right.roll=-left.roll;
            left.pitch=right.pitch=.1f-fold*.18f;
            leftFan.yaw=fold*.2f+flap*.12f;rightFan.yaw=-leftFan.yaw;
            leftFan.roll=fold*.55f;rightFan.roll=-leftFan.roll;
        }
        private static ModelPartBuilder box(int sign,float x,float y,float z,float width,float height,float depth,boolean glow) {
            float inset=glow?.22f:0;float w=Math.max(.15f,width-inset*2),h=Math.max(.14f,height-inset*2);
            return ModelPartBuilder.create().uv(0,0).cuboid(sign<0?-x-width+inset:x+inset,y+inset,z-(glow?.035f:0),w,h,depth+(glow?.07f:0));
        }
    }
}
