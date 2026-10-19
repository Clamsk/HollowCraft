import com.google.gson.*;
import dev.kosmx.playerAnim.core.data.*;
import dev.kosmx.playerAnim.core.data.gson.AnimationJson;
import dev.kosmx.playerAnim.core.util.Vec3f;
import dev.kosmx.playerAnim.api.TransformType;
import dev.kosmx.playerAnim.api.layered.KeyframeAnimationPlayer;
import java.nio.file.*;
import java.util.*;
import org.joml.Matrix4f;
import org.joml.Vector3f;

/** Build-only oracle: executes the user's original PlayerAnimator jar, without Minecraft. */
public class NativeAnimationReference {
    public static void main(String[] args) throws Exception {
        JsonArray samples = new JsonArray(), rig = new JsonArray(), hands=new JsonArray();
        try (var paths = Files.list(Path.of(args[0]))) {
            for (Path path : paths.filter(p -> p.toString().endsWith(".json")).sorted().toList()) {
                var data = new AnimationJson().deserialize(JsonParser.parseString(Files.readString(path)), AnimationJson.getListedTypeToken(), null).get(0);
                for (float tick = 0; tick <= data.stopTick + 1; tick += .25f) {
                    var player = new KeyframeAnimationPlayer(data, (int)tick);
                    player.setupAnim(tick - (int)tick);
                    for (String bone : List.of("body", "torso", "head", "rightArm", "leftArm", "rightLeg", "leftLeg","rightItem","leftItem")) {
                        // A second moving baseline catches disabled-axis and virtual-keyframe errors.
                        for (int baseline = 0; baseline < 2; baseline++) {
                            for (var type : List.of(TransformType.POSITION, TransformType.ROTATION)) {
                                Vec3f initial;
                                if (type == TransformType.POSITION) initial = switch (bone) {
                                    case "rightArm" -> new Vec3f(-5, 2, 0);
                                    case "leftArm" -> new Vec3f(5, 2, 0);
                                    case "rightLeg" -> new Vec3f(-1.9f, 12, .1f);
                                    case "leftLeg" -> new Vec3f(1.9f, 12, .1f);
                                    default -> Vec3f.ZERO;
                                }; else initial = baseline == 0 ? Vec3f.ZERO : new Vec3f(.21f, -.12f, .035f);
                                var value = player.get3DTransform(bone, type, tick - (int)tick, initial);
                                JsonObject sample = new JsonObject();
                                sample.addProperty("clip", path.getFileName().toString()); sample.addProperty("bone", bone);
                                sample.addProperty("tick", tick); sample.addProperty("rotation", type == TransformType.ROTATION);
                                sample.add("baseline", new Gson().toJsonTree(new float[]{initial.getX(), initial.getY(), initial.getZ()}));
                                sample.add("value", new Gson().toJsonTree(new float[]{value.getX(), value.getY(), value.getZ()}));
                                samples.add(sample);
                                if(baseline==0&&type==TransformType.ROTATION&&tick%1==0&&bone.endsWith("Item")) {
                                    var pos=player.get3DTransform(bone,TransformType.POSITION,tick-(int)tick,Vec3f.ZERO);boolean left=bone.equals("leftItem");
                                    var grip=new Matrix4f().rotateX(-(float)Math.PI/2).rotateY((float)Math.PI).translate((left?-1:1)/16f,.125f,-.625f);
                                    var delta=new Matrix4f(grip).translate(pos.getX()/16,pos.getY()/16,pos.getZ()/16).rotateZYX(value.getZ(),value.getY(),value.getX()).mul(new Matrix4f(grip).invert());
                                    var point=delta.transformPosition(new Vector3f(3f/16,5f/16,-2f/16));JsonObject fixture=new JsonObject();fixture.addProperty("clip",path.getFileName().toString());fixture.addProperty("bone",bone);fixture.addProperty("tick",tick);fixture.add("point",new Gson().toJsonTree(new float[]{-point.x,-point.y,point.z}));hands.add(fixture);
                                }
                                if (baseline == 0 && type == TransformType.ROTATION && tick % 1 == 0 && !bone.equals("body")&&!bone.endsWith("Item")) {
                                    Vec3f pos = player.get3DTransform(bone, TransformType.POSITION, tick-(int)tick, switch(bone) {
                                        case "rightArm" -> new Vec3f(-5,2,0); case "leftArm" -> new Vec3f(5,2,0);
                                        case "rightLeg" -> new Vec3f(-1.9f,12,.1f); case "leftLeg" -> new Vec3f(1.9f,12,.1f); default -> Vec3f.ZERO;
                                    });
                                    Vec3f rootPos=player.get3DTransform("body",TransformType.POSITION,tick-(int)tick,Vec3f.ZERO);
                                    Vec3f rootRot=player.get3DTransform("body",TransformType.ROTATION,tick-(int)tick,Vec3f.ZERO);
                                    // PlayerRendererMixin global body, then vanilla model scale,
                                    // then ModelPart translateAndRotate. All are original MC axes.
                                    var matrix=new Matrix4f().translate(rootPos.getX(),rootPos.getY()+.7f,rootPos.getZ())
                                        .rotateZYX(rootRot.getZ(),rootRot.getY(),rootRot.getX()).translate(0,-.7f,0)
                                        .translate(0,1.5f,0).scale(-1,-1,1).translate(pos.getX()/16,pos.getY()/16,pos.getZ()/16)
                                        .rotateZYX(value.getZ(),value.getY(),value.getX());
                                    var point=matrix.transformPosition(new Vector3f(3f/16,5f/16,-2f/16));
                                    JsonObject fixture=new JsonObject();fixture.addProperty("clip",path.getFileName().toString());fixture.addProperty("bone",bone);fixture.addProperty("tick",tick);
                                    fixture.add("point",new Gson().toJsonTree(new float[]{point.x,point.y,point.z}));rig.add(fixture);
                                }
                            }
                        }
                    }
                }
            }
        }
        Files.writeString(Path.of(args[1]), new Gson().toJson(samples));
        Files.writeString(Path.of(args[1]).resolveSibling("rig-samples.json"),new Gson().toJson(rig));
        Files.writeString(Path.of(args[1]).resolveSibling("hand-samples.json"),new Gson().toJson(hands));
        System.out.println("Original PlayerAnimator reference samples: " + samples.size());
    }
}
