import com.google.gson.*;
import java.nio.file.*;
import java.lang.reflect.*;
import java.util.*;
import net.minecraft.SharedConstants;
import net.minecraft.Bootstrap;
import net.minecraft.client.model.ModelPart;
import net.minecraft.client.render.entity.model.EntityModels;
import org.joml.Vector3f;

/** Offline model-description exporter. No client, renderer, world or window is created. */
public final class NativeCreatureModelExporter {
    static Object field(Object value,String name) throws Exception {var f=value.getClass().getDeclaredField(name);f.setAccessible(true);return f.get(value);}
    static JsonArray vec(float... values){var a=new JsonArray();for(float v:values)a.add(v);return a;}
    static JsonObject part(ModelPart model) throws Exception {
        var result=new JsonObject();result.add("pivot",vec(model.pivotX,model.pivotY,model.pivotZ));result.add("rotation",vec(model.pitch,model.yaw,model.roll));
        result.add("scale",vec(model.xScale,model.yScale,model.zScale));var faces=new JsonArray();
        for(Object cube:(List<?>)field(model,"cuboids"))for(Object quad:(Object[])field(cube,"sides"))if(quad!=null){
            var face=new JsonArray();for(Object vertex:(Object[])field(quad,"vertices")){
                Vector3f point=(Vector3f)field(vertex,"pos");face.add(vec(point.x,point.y,point.z,((Number)field(vertex,"u")).floatValue(),((Number)field(vertex,"v")).floatValue()));
            }faces.add(face);
        }
        result.add("faces",faces);var children=new JsonObject();
        for(var child:((Map<String,ModelPart>)field(model,"children")).entrySet())children.add(child.getKey(),part(child.getValue()));
        result.add("children",children);return result;
    }
    public static void main(String[] args)throws Exception {
        SharedConstants.createGameVersion();Bootstrap.initialize();var models=new JsonObject();
        for(var entry:EntityModels.getModels().entrySet())models.add(entry.getKey().toString(),part(entry.getValue().createModel()));
        var result=new JsonObject();result.addProperty("schema",1);result.addProperty("minecraft","1.20.1");result.addProperty("source","Local model part dimensions, pivots and UV facts; no executable game code or textures");result.add("models",models);
        Files.writeString(Path.of(args[0]),new Gson().toJson(result));System.out.println("Exported local model descriptions: "+models.size());
    }
}
