package dev.hollowcraft;

import com.google.gson.*;
import com.mojang.blaze3d.systems.RenderSystem;
import java.nio.file.*;
import java.util.*;
import net.minecraft.client.MinecraftClient;
import net.minecraft.client.texture.NativeImage;
import net.minecraft.client.render.model.BakedQuad;
import net.minecraft.client.render.model.json.ModelTransformationMode;
import net.minecraft.client.util.math.MatrixStack;
import net.minecraft.item.ItemStack;
import net.minecraft.registry.Registries;
import net.minecraft.util.math.Direction;
import org.joml.Vector4f;
import org.lwjgl.opengl.GL11;

/** Export the vanilla baked mesh, including generated item edge geometry and hand transforms. */
final class ItemSceneModels {
    private static final Set<String> atlases=new HashSet<>();
    static void export(MinecraftClient client,ItemStack stack,Path root) throws Exception {
        export(client,stack,root,PlayerBridge.renderKey(stack),null);
    }
    static void export(MinecraftClient client,ItemStack stack,Path root,String key,String modelName) throws Exception {
        exportHand(client,stack,root,key,modelName,false);
        exportHand(client,stack,root,key+"_left_hand",modelName,true);
    }
    private static void exportHand(MinecraftClient client,ItemStack stack,Path root,String key,String modelName,boolean left) throws Exception {
        var id=new net.minecraft.util.Identifier(key);
        Path file=root.resolve(id.getNamespace()).resolve(id.getPath()+".json");
        if(Files.exists(file)) {
            if(!stack.isOf(net.minecraft.item.Items.SHIELD)&&!stack.isOf(net.minecraft.item.Items.TRIDENT))return;
            if(JsonParser.parseString(Files.readString(file)).getAsJsonObject().has("builtinMatrix"))return;
        }
        var model=client.getItemRenderer().getModel(stack,client.world,client.player,0);
        if(modelName==null&&stack.isOf(net.minecraft.item.Items.TRIDENT))modelName="trident_in_hand";
        if(modelName!=null) model=client.getBakedModelManager().getModel(net.minecraft.client.util.ModelIdentifier.ofVanilla(modelName,"inventory"));
        JsonObject data=new JsonObject();data.addProperty("id",id.toString());data.addProperty("builtin",model.isBuiltin());
        JsonArray quads=new JsonArray();
        MatrixStack matrices=new MatrixStack();
        // HeldItemFeatureRenderer: arm pivot, X -90, Y 180, grip offset, then baked model.
        matrices.multiply(net.minecraft.util.math.RotationAxis.POSITIVE_X.rotationDegrees(-90));
        matrices.multiply(net.minecraft.util.math.RotationAxis.POSITIVE_Y.rotationDegrees(180));
        matrices.translate((left?-1:1)/16f,.125f,-.625f);
        model.getTransformation().getTransformation(left?ModelTransformationMode.THIRD_PERSON_LEFT_HAND:ModelTransformationMode.THIRD_PERSON_RIGHT_HAND).apply(left,matrices);
        var matrix=matrices.peek().getPositionMatrix();
        if(model.isBuiltin()) {
            var builtin=new org.joml.Matrix4f(matrix).translate(-.5f,-.5f,-.5f).scale(1,-1,-1);
            float[] values=new float[16];builtin.get(values);JsonArray transform=new JsonArray();for(float value:values)transform.add(value);data.add("builtinMatrix",transform);
        }
        List<Direction> sides=new ArrayList<>(Arrays.asList(Direction.values()));sides.add(null);
        for(Direction side:sides) for(BakedQuad quad:model.getQuads(null,side,net.minecraft.util.math.random.Random.create(42))) {
            var sprite=quad.getSprite();String atlas=sprite.getAtlasId().toString().replace(':','_').replace('/','_');
            if(atlases.add(atlas)) {
                Files.createDirectories(root);client.getTextureManager().bindTexture(sprite.getAtlasId());
                int width=GL11.glGetTexLevelParameteri(GL11.GL_TEXTURE_2D,0,GL11.GL_TEXTURE_WIDTH);
                int height=GL11.glGetTexLevelParameteri(GL11.GL_TEXTURE_2D,0,GL11.GL_TEXTURE_HEIGHT);
                try(var image=new NativeImage(width,height,false)) {image.loadFromTextureImage(0,false);image.writeTo(root.resolve(atlas+".png"));}
            }
            JsonObject face=new JsonObject();face.addProperty("atlas",atlas);JsonArray vertices=new JsonArray();
            int[] raw=quad.getVertexData();int stride=raw.length/4;
            Direction normal=quad.getFace();float shade=normal==Direction.UP?1f:normal==Direction.DOWN?.5f:normal.getAxis()==Direction.Axis.Z?.8f:.6f;
            int tint=quad.hasColor()?((dev.hollowcraft.mixin.ItemRendererAccess)client.getItemRenderer()).hollowcraft$getColors().getColor(stack,quad.getColorIndex()):0xffffff;
            for(int n=0;n<4;n++) {
                int offset=n*stride;
                var p=new Vector4f(Float.intBitsToFloat(raw[offset])-.5f,Float.intBitsToFloat(raw[offset+1])-.5f,Float.intBitsToFloat(raw[offset+2])-.5f,1).mul(matrix);
                // MC model-space X/Y point left/down; the Steve mesh uses right/up.
                JsonArray v=new JsonArray();v.add(-p.x);v.add(-p.y);v.add(p.z);
                v.add(Float.intBitsToFloat(raw[offset+4]));v.add(1-Float.intBitsToFloat(raw[offset+5]));
                v.add(((tint>>16)&255)/255f*shade);v.add(((tint>>8)&255)/255f*shade);v.add((tint&255)/255f*shade);
                vertices.add(v);
            }
            face.add("vertices",vertices);quads.add(face);
        }
        data.add("quads",quads);Files.createDirectories(file.getParent());Files.writeString(file,new Gson().toJson(data));
    }
}
