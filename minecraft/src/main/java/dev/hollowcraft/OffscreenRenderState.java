package dev.hollowcraft;

import com.mojang.blaze3d.systems.RenderSystem;
import net.minecraft.client.MinecraftClient;
import net.minecraft.client.util.math.MatrixStack;
import org.joml.Matrix4f;
import org.lwjgl.opengl.*;

/** Scope GPU state so a failed screen render cannot leave the backend matrices unbalanced. */
final class OffscreenRenderState implements AutoCloseable {
    private final MinecraftClient client;
    private final MatrixStack view=RenderSystem.getModelViewStack();
    private final int depth=((dev.hollowcraft.mixin.MatrixStackAccess)view).hollowcraft$stack().size();
    private final Matrix4f projection=new Matrix4f(RenderSystem.getProjectionMatrix());
    private final com.mojang.blaze3d.systems.VertexSorter sorting=RenderSystem.getVertexSorting();
    private final net.minecraft.client.gl.ShaderProgram shader=RenderSystem.getShader();
    private final float[] color=RenderSystem.getShaderColor().clone();
    private final boolean depthTest=GL11.glIsEnabled(GL11.GL_DEPTH_TEST),blend=GL11.glIsEnabled(GL11.GL_BLEND),cull=GL11.glIsEnabled(GL11.GL_CULL_FACE),depthWrite=GL11.glGetBoolean(GL11.GL_DEPTH_WRITEMASK),scissor=GL11.glIsEnabled(GL11.GL_SCISSOR_TEST);
    private final int[] rect=new int[4];
    private final int srcRgb=GL11.glGetInteger(GL14.GL_BLEND_SRC_RGB),dstRgb=GL11.glGetInteger(GL14.GL_BLEND_DST_RGB),srcAlpha=GL11.glGetInteger(GL14.GL_BLEND_SRC_ALPHA),dstAlpha=GL11.glGetInteger(GL14.GL_BLEND_DST_ALPHA);
    OffscreenRenderState(MinecraftClient client) {this.client=client;GL11.glGetIntegerv(GL11.GL_SCISSOR_BOX,rect);view.push();}
    public void close() {
        while(((dev.hollowcraft.mixin.MatrixStackAccess)view).hollowcraft$stack().size()>depth)view.pop();
        RenderSystem.applyModelViewMatrix();RenderSystem.setProjectionMatrix(projection,sorting);RenderSystem.setShader(()->shader);RenderSystem.setShaderColor(color[0],color[1],color[2],color[3]);
        RenderSystem.depthMask(depthWrite);if(depthTest)RenderSystem.enableDepthTest();else RenderSystem.disableDepthTest();
        if(blend)RenderSystem.enableBlend();else RenderSystem.disableBlend();if(cull)RenderSystem.enableCull();else RenderSystem.disableCull();RenderSystem.blendFuncSeparate(srcRgb,dstRgb,srcAlpha,dstAlpha);
        if(scissor)RenderSystem.enableScissor(rect[0],rect[1],rect[2],rect[3]);else RenderSystem.disableScissor();client.getFramebuffer().beginWrite(true);
    }
}
