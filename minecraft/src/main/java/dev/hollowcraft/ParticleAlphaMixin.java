package dev.hollowcraft.mixin;

import com.mojang.blaze3d.systems.RenderSystem;
import net.minecraft.client.particle.*;
import net.minecraft.client.render.BufferBuilder;
import net.minecraft.client.texture.TextureManager;
import org.lwjgl.opengl.GL11;
import org.spongepowered.asm.mixin.Mixin;
import org.spongepowered.asm.mixin.injection.*;

@Mixin(ParticleManager.class)
public abstract class ParticleAlphaMixin {
    @Redirect(method="renderParticles",at=@At(value="INVOKE",target="Lnet/minecraft/client/particle/ParticleTextureSheet;begin(Lnet/minecraft/client/render/BufferBuilder;Lnet/minecraft/client/texture/TextureManager;)V"))
    private void hollowcraft$transparentTarget(ParticleTextureSheet sheet,BufferBuilder buffer,TextureManager textures) {
        sheet.begin(buffer,textures);
        if(dev.hollowcraft.NativeParticleBridge.rendering) {
            RenderSystem.enableBlend();
            RenderSystem.blendFuncSeparate(GL11.GL_SRC_ALPHA,GL11.GL_ONE_MINUS_SRC_ALPHA,GL11.GL_ONE,GL11.GL_ONE_MINUS_SRC_ALPHA);
        }
    }
}
