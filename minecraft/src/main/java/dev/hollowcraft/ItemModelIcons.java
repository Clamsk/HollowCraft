package dev.hollowcraft;

import com.mojang.blaze3d.systems.RenderSystem;
import java.nio.file.*;
import java.util.*;
import net.minecraft.client.MinecraftClient;
import net.minecraft.client.gl.SimpleFramebuffer;
import net.minecraft.client.gui.DrawContext;
import com.mojang.blaze3d.systems.VertexSorter;
import net.minecraft.client.texture.NativeImage;
import net.minecraft.item.Item;
import net.minecraft.item.ItemStack;
import net.minecraft.registry.Registries;
import org.joml.Matrix4f;

/** Cache actual vanilla baked GUI models, including block lighting and built-in entity renderers. */
final class ItemModelIcons {
    private static Iterator<ItemStack> pending;
    private static SimpleFramebuffer buffer;
    static void tick(MinecraftClient client) {
        String directory=System.getProperty("hollowcraft.icons","");
        if(directory.isEmpty() || client.world==null) return;
        if(pending==null) {
            var stacks=new ArrayList<ItemStack>();for(Item item:Registries.ITEM) stacks.add(new ItemStack(item));
            for(var potion:Registries.POTION) for(Item item:new Item[]{net.minecraft.item.Items.POTION,net.minecraft.item.Items.SPLASH_POTION,net.minecraft.item.Items.LINGERING_POTION,net.minecraft.item.Items.TIPPED_ARROW}) {
                stacks.add(net.minecraft.potion.PotionUtil.setPotion(new ItemStack(item),potion));
            }
            pending=stacks.iterator();
            // Player item variants now come from the real HeldItemFeatureRenderer, including predicates.
        }
        for(int n=0;n<4 && pending.hasNext();n++) {
            ItemStack stack=pending.next();var id=new net.minecraft.util.Identifier(PlayerBridge.renderKey(stack));
            Path file=Path.of(directory,id.getNamespace(),id.getPath()+".png");
            if(Files.exists(file)) {
                try {ItemSceneModels.export(client,stack,Path.of(directory).getParent().resolve("scene_models_v2"));}catch(Exception e){org.slf4j.LoggerFactory.getLogger("HollowCraft").warn("Held model {}: {}",id,e.toString());}
                continue;
            }
            Matrix4f projection=new Matrix4f(RenderSystem.getProjectionMatrix());
            var view=RenderSystem.getModelViewStack();view.push();
            try {
                Files.createDirectories(file.getParent());
                if(buffer==null) buffer=new SimpleFramebuffer(64,64,true,MinecraftClient.IS_SYSTEM_MAC);
                buffer.setClearColor(0,0,0,0);buffer.clear(MinecraftClient.IS_SYSTEM_MAC);buffer.beginWrite(true);
                RenderSystem.setProjectionMatrix(new Matrix4f().setOrtho(0,64,64,0,1000,21000),VertexSorter.BY_Z);
                view.loadIdentity();view.translate(0,0,-11000);RenderSystem.applyModelViewMatrix();
                DrawContext draw=new DrawContext(client,client.getBufferBuilders().getEntityVertexConsumers());
                draw.getMatrices().scale(4,4,4);draw.drawItem(stack,0,0);draw.draw();
                // ScreenshotRecorder forces every alpha to 255; retain the framebuffer's real RGBA.
                RenderSystem.bindTexture(buffer.getColorAttachment());
                try(var png=new NativeImage(64,64,false)) {
                    png.loadFromTextureImage(0,false);png.mirrorVertically();png.writeTo(file);
                }
                ItemSceneModels.export(client,stack,Path.of(directory).getParent().resolve("scene_models_v2"));
            } catch(Exception e) {
                org.slf4j.LoggerFactory.getLogger("HollowCraft").warn("Item model icon {}: {}",id,e.toString());
            } finally {
                view.pop();RenderSystem.applyModelViewMatrix();RenderSystem.setProjectionMatrix(projection,VertexSorter.BY_Z);
                client.getFramebuffer().beginWrite(true);
            }
        }
    }
}
