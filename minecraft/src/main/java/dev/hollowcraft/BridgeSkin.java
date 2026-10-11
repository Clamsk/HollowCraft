package dev.hollowcraft;

import java.io.IOException;
import net.minecraft.client.MinecraftClient;
import net.minecraft.client.texture.NativeImage;
import net.minecraft.client.texture.NativeImageBackedTexture;
import net.minecraft.util.Identifier;

/** Derive HIM from the installed vanilla skin, so the eyes share its exact UV and head bone. */
public final class BridgeSkin {
    private static final Identifier STEVE=new Identifier("minecraft","textures/entity/player/wide/steve.png");
    private static Identifier him;
    public static Identifier texture(boolean creative) {
        if(!creative)return STEVE;
        if(him==null) {
            var client=MinecraftClient.getInstance();
            try(var stream=client.getResourceManager().getResource(STEVE).orElseThrow().getInputStream()) {
                NativeImage image=NativeImage.read(stream);
                // Vanilla Steve's front face is (8,8)-(16,16); the two eyes occupy row 12.
                for(int x:new int[]{9,10,13,14})image.setColor(x,12,0xffffffff);
                him=client.getTextureManager().registerDynamicTexture("hollowcraft_him",new NativeImageBackedTexture(image));
            } catch(IOException error) {throw new IllegalStateException("Cannot load the local Steve skin",error);}
        }
        return him;
    }
    public static boolean creative() {var state=PlayerRenderState.latest;return state!=null&&state.creative;}
}
