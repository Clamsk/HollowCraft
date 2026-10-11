package dev.hollowcraft.mixin;
import com.mojang.blaze3d.systems.RenderSystem;
import org.joml.Vector3f;
import org.spongepowered.asm.mixin.Mixin;
import org.spongepowered.asm.mixin.gen.Accessor;
@Mixin(value=RenderSystem.class,remap=false)
public interface RenderSystemAccess {
    @Accessor(value="shaderLightDirections",remap=false) static Vector3f[] hollowcraft$lights() {throw new AssertionError();}
    @Accessor(value="shaderTextures",remap=false) static int[] hollowcraft$textures() {throw new AssertionError();}
}
