package dev.hollowcraft;

import com.google.gson.*;
import net.fabricmc.api.ModInitializer;
import net.fabricmc.fabric.api.event.lifecycle.v1.ServerLifecycleEvents;
import net.fabricmc.fabric.api.event.lifecycle.v1.ServerTickEvents;
import net.fabricmc.fabric.api.event.lifecycle.v1.ServerEntityEvents;
import net.minecraft.entity.TntEntity;
import net.minecraft.entity.EquipmentSlot;
import net.minecraft.block.*;
import net.minecraft.item.*;
import net.minecraft.nbt.NbtCompound;
import net.minecraft.recipe.CraftingRecipe;
import net.minecraft.recipe.RecipeType;
import net.minecraft.registry.Registries;
import net.minecraft.inventory.CraftingInventory;
import net.minecraft.screen.ScreenHandler;
import net.minecraft.screen.ScreenHandlerType;
import net.minecraft.screen.slot.Slot;
import net.minecraft.screen.slot.SlotActionType;
import net.minecraft.screen.AbstractFurnaceScreenHandler;
import net.minecraft.entity.player.PlayerEntity;
import net.minecraft.server.MinecraftServer;
import net.minecraft.server.network.ServerPlayerEntity;
import net.minecraft.server.world.ServerWorld;
import net.minecraft.util.*;
import net.minecraft.util.hit.BlockHitResult;
import net.minecraft.util.math.*;
import net.minecraft.util.shape.VoxelShapes;
import net.minecraft.world.GameMode;
import net.minecraft.world.Difficulty;
import net.minecraft.util.WorldSavePath;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

import java.io.*;
import java.net.*;
import java.nio.charset.StandardCharsets;
import java.nio.file.*;
import java.util.*;
import java.util.concurrent.*;

/** Runs vanilla interactions on Minecraft's server thread, never on the socket thread. */
public final class HollowCraftBridge implements ModInitializer {
    private static HollowCraftBridge instance;
    public static boolean remoteActive() {return instance!=null && instance.peer!=null;}
    private volatile java.util.concurrent.atomic.AtomicInteger guiPackets=new java.util.concurrent.atomic.AtomicInteger();
    private static boolean guiBase(NativeGuiBridge.Session gui) {
        var bridge=instance;return bridge!=null&&bridge.running&&bridge.owner!=null&&bridge.peer!=null&&bridge.peer.gui
            &&NativeGuiBridge.session()==gui&&bridge.scene.equals(gui.scene())&&bridge.campaigns.id().equals(gui.campaign());
    }
    public static void guiPacket(NativeGuiBridge.Session gui,net.minecraft.network.packet.Packet<net.minecraft.network.listener.ServerPlayPacketListener> packet) {
        var bridge=instance;if(bridge==null||bridge.server==null)return;
        var budget=bridge.guiPackets;
        if(budget.incrementAndGet()>256) {budget.decrementAndGet();return;}
        var expectedServer=bridge.server;
        expectedServer.execute(()-> {
            try {
                if(bridge.server!=expectedServer||!guiBase(gui)||!bridge.owner.isAlive()||bridge.owner.currentScreenHandler.syncId!=gui.syncId())return;
                if(packet instanceof net.minecraft.network.packet.c2s.play.ClickSlotC2SPacket click&&click.getSyncId()!=gui.syncId()
                    ||packet instanceof net.minecraft.network.packet.c2s.play.ButtonClickC2SPacket button&&button.getSyncId()!=gui.syncId()
                    ||packet instanceof net.minecraft.network.packet.c2s.play.CraftRequestC2SPacket craft&&craft.getSyncId()!=gui.syncId()
                    ||packet instanceof net.minecraft.network.packet.c2s.play.CloseHandledScreenC2SPacket close&&close.getSyncId()!=gui.syncId())return;
                bridge.collectLandingWater();packet.apply(bridge.owner.networkHandler);
            } catch(Exception error) {LOG.warn("Native GUI packet failed",error);}
            finally {budget.decrementAndGet();}
        });
    }
    public static void guiClientClosed(NativeGuiBridge.Session gui) {
        var bridge=instance;if(bridge==null||bridge.server==null)return;var expectedServer=bridge.server;
        expectedServer.execute(()-> {if(bridge.server==expectedServer&&guiBase(gui)) {
            if(bridge.owner.currentScreenHandler.syncId==gui.syncId())bridge.owner.closeHandledScreen();
            bridge.closeGui();bridge.openContainer=null;
        }});
    }
    private void closeGui() {
        var gui=NativeGuiBridge.session();NativeGuiBridge.close();
        if(gui!=null&&peer!=null) {var event=new JsonObject();event.addProperty("type","guiClosed");event.addProperty("scene",gui.scene());event.addProperty("campaign",gui.campaign());event.addProperty("gui",gui.id());event.addProperty("request",gui.request());peer.event(event);}
    }
    private void openGui(boolean inventory,int request) {if(peer!=null&&peer.gui)NativeGuiBridge.open(owner,scene,campaigns.id(),inventory,request);}
    private void creativeCatalog() {
        if(peer==null||!creative)return;
        if(!peer.gui) {peer.event(catalog.build(owner,scene));return;}
        var gui=NativeGuiBridge.session();if(gui==null)return;
        var player=owner;var connection=peer;var localServer=server;
        // ItemGroups is shared with CreativeInventoryScreen; rebuild only on its client thread.
        net.minecraft.client.MinecraftClient.getInstance().execute(()-> {
            if(NativeGuiBridge.session()!=gui||server!=localServer||owner!=player||peer!=connection)return;
            try {
                var result=catalog.build(player,gui.scene());
                localServer.execute(()-> {if(server==localServer&&owner==player&&peer==connection&&guiBase(gui))connection.event(result);});
            } catch(Exception error) {LOG.warn("Creative fallback catalogue unavailable",error);}
        });
    }
    public static boolean isRemotePlayer(net.minecraft.entity.LivingEntity entity) {return instance!=null&&instance.owner==entity&&instance.peer!=null&&!instance.scene.isEmpty();}
    public static void playerHurt(net.minecraft.entity.LivingEntity entity,net.minecraft.entity.damage.DamageSource source) {
        if(!isRemotePlayer(entity)) return;
        JsonObject event=new JsonObject();event.addProperty("type","hurt");event.addProperty("scene",instance.scene);
        String kind=instance.pearlDamageActive?"ender_pearl":source.getName();
        event.addProperty("ticks",entity.hurtTime);event.addProperty("kind",kind);instance.peer.event(event);
        LOG.info("Bridge player hurt kind={} HP={} hurtTicks={}",kind,entity.getHealth(),entity.hurtTime);
    }
    public static volatile boolean remotePaused;
    public static boolean nativeProjectile(net.minecraft.entity.projectile.ProjectileEntity projectile) {return NativeTerrainBlock.delegated(projectile);}
    public static boolean delegateLegacyTerrain(BlockState state,BlockPos pos,net.minecraft.block.ShapeContext context) {
        var bridge=instance;
        return bridge!=null&&!NativeTerrainQueries.solid()&&state.isOf(Blocks.BARRIER)&&pos.getZ()==0
            &&Math.abs(pos.getX()-bridge.slot*1024-bridge.centerX)<=32&&Math.abs(pos.getY()-64-bridge.centerY)<=28
            &&context instanceof net.minecraft.block.EntityShapeContext entity&&entity.getEntity() instanceof net.minecraft.entity.projectile.ProjectileEntity projectile&&nativeProjectile(projectile);
    }
    public static boolean audioRequested() {return instance!=null&&instance.peer!=null&&instance.peer.audio;}
    public static boolean hudRequested() {return instance!=null&&instance.peer!=null&&instance.peer.hud;}
    public static boolean entityRenderRequested() {return instance!=null&&instance.peer!=null&&instance.peer.entityRender;}
    public static boolean blockRenderRequested() {return instance!=null&&instance.peer!=null&&instance.peer.blockRender;}
    public static boolean audioReady(String key) {var peer=instance==null?null:instance.peer;return peer!=null&&peer.audio&&peer.audioFiles.contains(key);}
    public static boolean audioEvent(JsonObject event) {
        var peer=instance==null?null:instance.peer;var state=PlayerRenderState.latest;
        if(peer==null||!peer.audio||state==null)return false;
        event.addProperty("scene",state.scene);event.addProperty("campaign",state.campaign);peer.event(event);return true;
    }
    public static void audioStop() {var event=new JsonObject();event.addProperty("type","mcSoundStop");audioEvent(event);}
    public static boolean projectileStepsEnabled() {return instance!=null&&instance.peer!=null&&instance.peer.projectileSteps;}
    public static boolean tickNativeProjectile(net.minecraft.entity.projectile.ProjectileEntity projectile) {return NativeProjectileSteps.tick(projectile);}
    public static boolean nativeTnt(TntEntity tnt) {
        var bridge=instance;
        return bridge!=null&&bridge.peer!=null&&bridge.peer.tntSteps&&bridge.owner!=null&&!bridge.scene.isEmpty()
            &&tnt.getWorld()==bridge.owner.getServerWorld()&&Math.abs(tnt.getX()-bridge.slot*1024-bridge.centerX)<=32
            &&Math.abs(tnt.getY()-64-bridge.centerY)<=32&&Math.abs(tnt.getZ())<=8;
    }
    public static boolean nativeProjectileVisible(net.minecraft.entity.projectile.ProjectileEntity projectile) {
        var bridge=instance;
        return bridge!=null&&projectile.getWorld().getServer()==bridge.server
            &&Math.abs(projectile.getX()-bridge.slot*1024-bridge.centerX)<=28&&Math.abs(projectile.getY()-64-bridge.centerY)<=24&&Math.abs(projectile.getZ())<=4;
    }
    public static boolean pearlDamage(net.minecraft.entity.Entity entity,net.minecraft.entity.damage.DamageSource source,float vanillaAmount) {
        if(!(entity instanceof ServerPlayerEntity player)||!isRemotePlayer(player))return entity.damage(source,vanillaAmount);
        var bridge=instance;boolean previous=bridge.pearlDamageActive;bridge.pearlDamageActive=true;
        float health=player.getHealth(),absorption=player.getAbsorptionAmount();
        try {return player.damage(source,3f);}
        finally {
            LOG.info("Pearl teleport damage base=3 healthLoss={} absorptionUsed={}",health-player.getHealth(),absorption-player.getAbsorptionAmount());
            bridge.pearlDamageActive=previous;
        }
    }
    public static void pearlLanded(ServerPlayerEntity player) {
        if(!isRemotePlayer(player))return;
        instance.fallen=0;instance.hadPose=false;instance.wasGrounded=false;instance.footstepDistance=0;player.fallDistance=0;
    }
    public static void vanillaTeleport(ServerPlayerEntity player,Vec3d normal) {
        if(instance==null||instance.owner!=player||instance.peer==null||instance.scene.isEmpty()) return;
        instance.collectLandingWater();
        instance.exactX=player.getX()-instance.slot*1024;instance.exactY=player.getY()-64;
        instance.centerX=(int)Math.floor(instance.exactX);instance.centerY=(int)Math.floor(instance.exactY);
        instance.lastPoseX=instance.exactX;instance.lastPoseY=instance.exactY;pearlLanded(player);
        instance.teleportWaiting=instance.server.getTicks()+40;
        JsonObject event=new JsonObject();event.addProperty("type","teleport");event.addProperty("scene",instance.scene);event.addProperty("x",instance.exactX);event.addProperty("y",instance.exactY);
        event.addProperty("normalX",normal.x);event.addProperty("normalY",normal.y);event.addProperty("teleport",++instance.teleportSerial);instance.peer.event(event);
    }
    private static final Logger LOG = LoggerFactory.getLogger("HollowCraft");
    public static void projectileEvent(net.minecraft.entity.projectile.ProjectileEntity entity,String phase,Vec3d point) {
        var bridge=instance;
        if(bridge==null||bridge.owner==null||bridge.peer==null||bridge.scene.isEmpty()||entity.getWorld().isClient||entity.getWorld().getServer()!=bridge.server)return;
        if(Math.abs(entity.getX()-bridge.slot*1024)>400||entity.getY()<4||entity.getY()>304)return;
        JsonObject event=new JsonObject();event.addProperty("type","projectile");event.addProperty("scene",bridge.scene);event.addProperty("phase",phase);
        JsonObject value=ItemUseBridge.projectile(entity,bridge.slot*1024);
        if(phase.equals("land"))value.addProperty("grounded",true);
        if(point!=null){value.addProperty("x",point.x-bridge.slot*1024);value.addProperty("y",point.y-64);}
        event.add("value",value);bridge.peer.event(event);
    }
    private static final Gson GSON = new GsonBuilder().setPrettyPrinting().create();
    private static final int PORT = 31674;
    private static final int MAX_LINE = 65536;
    private final BlockingQueue<JsonObject> commands = new ArrayBlockingQueue<>(128);
    private final Map<String, Integer> scenes = new LinkedHashMap<>();
    private final Set<BlockPos> terrain = new HashSet<>();
    private final Map<Integer,net.minecraft.entity.ItemEntity> trackedDrops=new HashMap<>();
    private final Map<Integer,Integer> dropObserved=new HashMap<>();
    private final Map<String,JsonObject> projectileImpacts=new LinkedHashMap<>() {
        @Override protected boolean removeEldestEntry(Map.Entry<String,JsonObject> entry) {return size()>512;}
    };
    private volatile Peer peer;
    private volatile boolean running;
    private ServerSocket listener;
    private MinecraftServer server;
    private Path sceneFile;
    private CampaignStore campaigns;
    private boolean introLanding;
    private String scene = "";
    private int slot, centerX, centerY, ticks;
    private ServerPlayerEntity owner;
    private String status = "Open Hollow Knight and press F6";
    private boolean creative;
    private double exactX,exactY,lastPoseX,lastPoseY;
    private int sceneVisit;
    private boolean hadPose,wasGrounded;
    private float fallen;
    private boolean pearlDamageActive;
    private double footstepDistance;
    private BlockPos landingWater;
    private BlockState landingWaterBefore;
    private LandingBucket landingBucket;
    private Hand landingHand=Hand.MAIN_HAND;
    private int collectWaterTick;
    private int teleportWaiting,teleportSerial;
    private int nextSwingTick, combo, attackSerial, activeAttack, hitReadyTick;
    private String lastWeapon="";
    private BlockPos openContainer;
    private final EnemyBridge enemies=new EnemyBridge();
    private final CreativeCatalog catalog=new CreativeCatalog();
    private BlockPos miningPos;
    private float miningProgress;
    private int lastMiningTick;
    private int miningStartedTick;
    private double attackMultiplier=1, reachY;
    private JsonObject pendingHit;
    private JsonObject pendingSwing;
    private final Map<String,JsonObject> towerReplies=new LinkedHashMap<>();
    private JsonObject lastAttackEvent;
    private int lastAttackRequest,cancelledAttackRequest;

    @Override public void onInitialize() {
        instance=this;
        NativeTerrainBlock.register();
        ServerLifecycleEvents.SERVER_STARTED.register(this::start);
        ServerLifecycleEvents.SERVER_STOPPING.register(s -> stop());
        ServerTickEvents.START_SERVER_TICK.register(this::beginTick);
        ServerTickEvents.END_SERVER_TICK.register(this::tick);
        ServerEntityEvents.ENTITY_LOAD.register((entity,world)-> {
            if(entity instanceof net.minecraft.entity.projectile.ProjectileEntity projectile)projectileEvent(projectile,"spawn",null);
            if(world.getServer()==server && entity.getCommandTags().contains("hollowcraft_proxy") && !enemies.owns(entity)) entity.discard();
            if(world.getServer()==server&&peer!=null&&!scene.isEmpty()&&entity instanceof net.minecraft.entity.ItemEntity dropped&&Math.abs(dropped.getX()-slot*1024)<=400) {trackedDrops.put(dropped.getId(),dropped);dropped.setNoGravity(true);dropped.setPosition(dropped.getX(),dropped.getY(),.5);}
        });
        ServerEntityEvents.ENTITY_UNLOAD.register((entity,world)-> {
            if(entity instanceof net.minecraft.entity.projectile.ProjectileEntity projectile)projectileEvent(projectile,"impact",null);
            if(entity instanceof TntEntity tnt && tnt.getFuse()<=0 && server==world.getServer() && peer!=null && !scene.isEmpty()) {
                LOG.info("TNT explosion local {}, {}",tnt.getX()-slot*1024,tnt.getY()-64);
                JsonObject event=new JsonObject();event.addProperty("type","explosion");event.addProperty("scene",scene);
                event.addProperty("x",tnt.getX()-slot*1024);event.addProperty("y",tnt.getY()-64);peer.event(event);
            }
        });
    }

    private void start(MinecraftServer value) {
        // Only opt-in worlds. Existing survival worlds must never become bridge sandboxes.
        String folder = value.getSavePath(WorldSavePath.ROOT).toAbsolutePath().normalize().getFileName().toString();
        if (!folder.toLowerCase(Locale.ROOT).startsWith("hollowcraft")) {
            LOG.info("Bridge disabled: world folder must start with HollowCraft"); return;
        }
        server = value;
        guiPackets=new java.util.concurrent.atomic.AtomicInteger();
        campaigns=new CampaignStore(value);
        for(var world:value.getWorlds()) for(var entity:world.iterateEntities()) if(entity.getCommandTags().contains("hollowcraft_proxy")) entity.discard();
        server.setDifficulty(Difficulty.NORMAL,true);
        sceneFile = value.getSavePath(WorldSavePath.ROOT).resolve("hollowcraft-scenes.json");
        scenes.clear();
        try {
            if (Files.exists(sceneFile)) {
                JsonObject data = JsonParser.parseString(Files.readString(sceneFile)).getAsJsonObject();
                for (var entry : data.entrySet()) scenes.put(entry.getKey(), entry.getValue().getAsInt());
            }
            listener = new ServerSocket();
            listener.bind(new InetSocketAddress(InetAddress.getLoopbackAddress(), PORT));
            running = true;
            Thread thread = new Thread(this::accept, "HollowCraft local bridge");
            thread.setDaemon(true); thread.start();
            LOG.info("HollowCraft ready on localhost:{}", PORT);
        } catch (Exception e) { LOG.error("Cannot start bridge", e); stop(); }
    }
    private void accept() {
        while (running) {
            try (Socket socket = listener.accept()) {
                socket.setTcpNoDelay(true); socket.setSoTimeout(15000);
                var reader = new BufferedReader(new InputStreamReader(socket.getInputStream(), StandardCharsets.UTF_8));
                var writer = new BufferedWriter(new OutputStreamWriter(socket.getOutputStream(), StandardCharsets.UTF_8));
                JsonObject hello = JsonParser.parseString(readBounded(reader)).getAsJsonObject();
                if (!"hello".equals(string(hello, "type", "")) || integer(hello, "protocol", 0) != 1) continue;
                Peer current = new Peer(socket, writer,hello.has("nativeProjectileSteps")&&hello.get("nativeProjectileSteps").getAsBoolean(),hello.has("minecraftAudio")&&hello.get("minecraftAudio").getAsBoolean(),hello.has("nativeGui")&&hello.get("nativeGui").getAsBoolean(),hello.has("nativeHud")&&hello.get("nativeHud").getAsBoolean(),hello.has("nativeEntityRender")&&hello.get("nativeEntityRender").getAsBoolean(),hello.has("nativeBlockRender")&&hello.get("nativeBlockRender").getAsBoolean(),hello.has("nativeTntSteps")&&hello.get("nativeTntSteps").getAsBoolean());
                current.send("{\"type\":\"hello\",\"protocol\":1,\"minecraft\":\"1.20.1\",\"saveSession\":true}");
                socket.setSoTimeout(0);
                peer = current;
                while (running && !socket.isClosed()) {
                    String line = readBounded(reader);
                    if (line == null) break;
                    JsonObject command = JsonParser.parseString(line).getAsJsonObject();
                    if("visual".equals(string(command,"type",""))) {PlayerRenderState.controls=command;continue;}
                    if("pause".equals(string(command,"type",""))) {remotePaused=command.has("paused")&&command.get("paused").getAsBoolean();continue;}
                    if (!commands.offer(command)) throw new IOException("Command queue full");
                }
            } catch (Exception e) { if (running) LOG.info("Bridge connection closed: {}", e.toString()); }
            finally { peer = null;remotePaused=false;PlayerRenderState.clear(); commands.clear(); }
        }
    }
    private static String readBounded(Reader reader) throws IOException {
        StringBuilder text = new StringBuilder();
        for (int ch; (ch = reader.read()) != -1;) {
            if (ch == '\n') return text.toString();
            if (ch != '\r') text.append((char) ch);
            if (text.length() > MAX_LINE) throw new IOException("Oversized message");
        }
        return text.isEmpty() ? null : text.toString();
    }
    private void stop() {
        closeGui();
        NativeProjectileSteps.clear();NativeTntSteps.clear();projectileImpacts.clear();ItemUseBridge.clearAnchors();
        collectLandingWater();saveCampaign();
        PlayerRenderState.clear();
        NativeEntityBridge.clear();
        NativeBlockBridge.clear();
        collectLandingWater();
        for(var dropped:trackedDrops.values()) {dropped.setNoGravity(true);dropped.setVelocity(Vec3d.ZERO);}trackedDrops.clear();dropObserved.clear();
        FluidBridge.reset();
        running = false;
        try { if (listener != null) listener.close(); if (peer != null) peer.socket.close(); }
        catch (IOException ignored) { }
        cleanupTerrain();
        enemies.clear();
        if (owner != null) owner.setNoGravity(false);
        owner = null; scene = ""; server = null; peer = null; commands.clear();
    }
    private void drainCommands() throws IOException {
        JsonObject command;for(int budget=0;budget<32&&(command=commands.poll())!=null;budget++)execute(command);
    }
    private void beginTick(MinecraftServer current) {
        if(!running||current!=server||peer==null||owner==null)return;
        try {drainCommands();}catch(Exception error) {LOG.error("Bridge input before world tick failed",error);}
    }
    private void tick(MinecraftServer current) {
        if (!running || current != server) return;
        // This opt-in void world is a backend. Keep its avatar safe while the 2D client reconnects.
        for (ServerPlayerEntity player : current.getPlayerManager().getPlayerList()) {
            player.setNoGravity(true);
            player.fallDistance = 0;
            if (peer == null || scene.isEmpty()) {
                player.teleport(player.getServerWorld(), 8.5, 64, 8.5, 0, 0);
                player.setVelocity(Vec3d.ZERO);
            }
        }
        if (peer == null) {
            NativeGuiBridge.close();
            NativeEntityBridge.clear();
            NativeBlockBridge.clear();
            NativeProjectileSteps.clear();NativeTntSteps.clear();projectileImpacts.clear();
            if (owner != null) {collectLandingWater();saveCampaign();for(var dropped:trackedDrops.values()) {dropped.setNoGravity(true);dropped.setVelocity(Vec3d.ZERO);}cleanupTerrain();enemies.clear(); owner = null; scene = ""; }
            return;
        }
        if (owner == null && !current.getPlayerManager().getPlayerList().isEmpty()) {
            owner = current.getPlayerManager().getPlayerList().get(0);
            owner.setNoGravity(true);
            owner.noClip=true;owner.changeGameMode(GameMode.SURVIVAL);creative=false;
            grantIronStarter();
        }
        if (owner == null) return;
        try {
            drainCommands();
            if(server.getTicks()%100==0)saveCampaign();
            if(landingWater!=null&&server.getTicks()>=collectWaterTick) collectLandingWater();
            if(pendingHit!=null && server.getTicks()>=hitReadyTick) {var ready=pendingHit;pendingHit=null;execute(ready);}
            if(pendingSwing!=null && server.getTicks()>=nextSwingTick) {var ready=pendingSwing;pendingSwing=null;swing(ready);}
            if (!scene.isEmpty()) {
                // Local coordinates occupy a one-block-thick plane, far from normal world spawn.
                Vec3d impulse=owner.getVelocity();
                if(owner.hurtTime>0&&impulse.lengthSquared()>.0001&&peer!=null) {JsonObject event=new JsonObject();event.addProperty("type","impulse");event.addProperty("scene",scene);event.addProperty("x",impulse.x);event.addProperty("y",impulse.y);peer.event(event);}
                owner.teleport(owner.getServerWorld(), slot * 1024 + exactX, 64 + exactY, 0.5, owner.getYaw(), owner.getPitch());
                owner.setVelocity(Vec3d.ZERO);
                owner.setOnGround(wasGrounded);
                PlayerRenderState.publish(owner,scene,campaigns.id(),slot*1024);
                enemies.tick();
                ItemUseBridge.tick(owner,slot*1024,centerX,centerY);
                if(peer!=null) {
                    JsonObject flight=new JsonObject();flight.addProperty("type","projectiles");flight.addProperty("scene",scene);flight.addProperty("tick",server.getTicks());
                    flight.add("projectiles",ItemUseBridge.snapshot(owner,slot*1024,centerX,centerY));peer.event(flight);
                }
                for(var tnt:owner.getServerWorld().getEntitiesByClass(TntEntity.class,new Box(slot*1024+centerX-24,64+centerY-24,-8,slot*1024+centerX+24,64+centerY+24,8),e->true)) {
                    tnt.setPosition(tnt.getX(),tnt.getY(),.5);tnt.setVelocity(tnt.getVelocity().multiply(1,1,0));
                }
                if(peer.tntSteps) {var event=new JsonObject();event.addProperty("type","primed");event.addProperty("scene",scene);event.addProperty("tick",server.getTicks());event.add("primed",primedSnapshot());peer.event(event);}
                if(miningPos!=null) {
                    if(server.getTicks()-lastMiningTick>5) {miningPos=null;miningProgress=0;}
                    else {
                        BlockState state=owner.getServerWorld().getBlockState(miningPos);
                        if(state.isAir() || NativeTerrainBlock.proxy(state)) {miningPos=null;miningProgress=0;}
                        else {miningProgress=creative ? 1 : state.calcBlockBreakingDelta(owner,owner.getServerWorld(),miningPos)*(server.getTicks()-miningStartedTick+1);
                            if(miningProgress>=1) {breakRemoteBlock(miningPos);miningPos=null;miningProgress=0;}
                        }
                    }
                }
                Box dropRegion=new Box(slot*1024+centerX-16,64+centerY-18,-32,slot*1024+centerX+16,64+centerY+18,32);
                trackedDrops.entrySet().removeIf(entry->{if(!entry.getValue().isRemoved()) return false;dropObserved.remove(entry.getKey());return true;});
                for(var dropped:trackedDrops.values()) if(!dropRegion.intersects(dropped.getBoundingBox())) {dropped.setNoGravity(true);dropped.setVelocity(Vec3d.ZERO);}
                for(var dropped:owner.getServerWorld().getEntitiesByClass(net.minecraft.entity.ItemEntity.class,dropRegion,e->true)) {
                    // Terrain is sampled only around the native hero. Resume gravity inside that
                    // window, so a distant corpse cannot fall before its floor is reconstructed.
                    boolean simulated=peer!=null&&Math.abs(dropped.getX()-slot*1024-centerX)<=7&&Math.abs(dropped.getY()-64-centerY)<=5;
                    trackedDrops.put(dropped.getId(),dropped);dropped.setNoGravity(!simulated);dropped.setPosition(dropped.getX(),dropped.getY(),0.5);dropped.setVelocity(simulated?dropped.getVelocity().multiply(1,1,0):Vec3d.ZERO);
                    int first=dropObserved.computeIfAbsent(dropped.getId(),id->server.getTicks());
                    // HK's transform is the body centre; MC's transform is the feet.
                    if(owner.isAlive() && server.getTicks()-first>=40 && dropped.squaredDistanceTo(owner)<4) {dropped.resetPickupDelay();dropped.onPlayerCollision(owner);}
                }
                if(entityRenderRequested())NativeEntityBridge.publish(owner,scene,campaigns.id(),slot*1024);
                if(blockRenderRequested()&&server.getTicks()%2==0)NativeBlockBridge.publish(owner,scene,campaigns.id(),slot*1024,centerX,centerY);
                if (++ticks % 2 == 0) snapshot();
            }
        } catch (Exception e) { status = e.getClass().getSimpleName() + ": " + e.getMessage(); LOG.error("Bridge tick", e); }
    }
    private void respawnOwner() {
        var grants=owner.getCommandTags().stream().filter(tag->tag.startsWith("hollowcraft_starter")).toList();
        owner=server.getPlayerManager().respawnPlayer(owner,false);owner.networkHandler.player=owner;
        owner.setNoGravity(true);owner.noClip=true;for(var tag:grants)owner.addCommandTag(tag);
        owner.addCommandTag("hollowcraft_starter");owner.addCommandTag("hollowcraft_starter_iron_v1");
    }
    private void grantIronStarter() {
        String tag="hollowcraft_starter_iron_v1";
        String pickTag="hollowcraft_starter_iron_pickaxe_v1";
        String ingotTag="hollowcraft_starter_iron_ingots_v1";
        String suppliesTag="hollowcraft_starter_build_food_v1";
        if(owner.getCommandTags().contains(tag)) {
            if(!owner.getCommandTags().contains(pickTag)) {
                boolean present=false;for(int i=0;i<36;i++)if(owner.getInventory().getStack(i).isOf(Items.IRON_PICKAXE))present=true;
                if(!present)give(new ItemStack(Items.IRON_PICKAXE));owner.addCommandTag(pickTag);
            }
            if(!owner.getCommandTags().contains(ingotTag)) {give(new ItemStack(Items.IRON_INGOT,64));owner.addCommandTag(ingotTag);}
            if(!owner.getCommandTags().contains(suppliesTag)) {
                give(new ItemStack(Items.COBBLESTONE,64));give(new ItemStack(Items.COBBLESTONE,64));give(new ItemStack(Items.GOLDEN_APPLE,64));
                owner.addCommandTag(suppliesTag);
            }
            return;
        }
        boolean fresh=owner.getInventory().isEmpty();
        Item[] items={Items.IRON_SWORD,Items.BOW,Items.COOKED_BEEF,Items.COBBLESTONE,Items.OAK_LOG,
            Items.IRON_AXE,Items.WATER_BUCKET,Items.LAVA_BUCKET,Items.FLINT_AND_STEEL,
            Items.ARROW,Items.ENDER_PEARL,Items.TNT,Items.TORCH,Items.IRON_PICKAXE,Items.IRON_INGOT,Items.GOLDEN_APPLE};
        int[] amounts={1,1,64,192,64,1,1,1,1,64,64,64,64,1,64,64};
        int nextSlot=0;
        for(int i=0;i<items.length;i++) {
            int missing=amounts[i];
            if(!fresh)for(int slot=0;slot<36;slot++) {
                ItemStack existing=owner.getInventory().getStack(slot);
                if(existing.isOf(items[i]))missing-=existing.getCount();
            }
            while(missing>0) {
                ItemStack stack=new ItemStack(items[i],Math.min(missing,items[i].getMaxCount()));
                missing-=stack.getCount();
                if(fresh)owner.getInventory().setStack(nextSlot++,stack);
                else give(stack);
            }
        }
        EquipmentSlot[] slots={EquipmentSlot.HEAD,EquipmentSlot.CHEST,EquipmentSlot.LEGS,EquipmentSlot.FEET,EquipmentSlot.OFFHAND};
        Item[] equipment={Items.IRON_HELMET,Items.IRON_CHESTPLATE,Items.IRON_LEGGINGS,Items.IRON_BOOTS,Items.SHIELD};
        for(int i=0;i<slots.length;i++) {
            ItemStack previous=owner.getEquippedStack(slots[i]);
            if(previous.isOf(equipment[i]))continue;
            owner.equipStack(slots[i],new ItemStack(equipment[i]));
            if(!previous.isEmpty())give(previous);
        }
        if(fresh)owner.getInventory().selectedSlot=0;
        owner.addCommandTag(tag);
        owner.addCommandTag(pickTag);
        owner.addCommandTag(ingotTag);
        owner.addCommandTag(suppliesTag);
        owner.addCommandTag("hollowcraft_starter");
        LOG.info("Iron starter loadout granted; fresh={}",fresh);
    }
    private void execute(JsonObject command) throws IOException {
        try(var effects=RemoteEffects.begin()) {executeCommand(command);}
    }
    private void executeCommand(JsonObject command) throws IOException {
        String type = string(command, "type", "");
        if(type.equals("saveSession")) {
            collectLandingWater();closeGui();owner.clearActiveItem();owner.closeHandledScreen();
            pendingHit=null;pendingSwing=null;miningPos=null;miningProgress=0;
            campaigns.save(owner);server.getPlayerManager().saveAllPlayerData();server.save(false,true,true);remotePaused=true;
            var reply=new JsonObject();reply.addProperty("type","sessionSaved");reply.addProperty("request",string(command,"request",""));reply.addProperty("campaign",campaigns.id());
            if(peer!=null)peer.send(reply.toString());return;
        }
        if (type.equals("scene")) {
            towerReplies.clear();
            String name = string(command, "scene", "");
            if (name.isBlank() || name.length() > 120) return;
            closeGui();
            collectLandingWater();
            NativeProjectileSteps.clear();NativeTntSteps.clear();projectileImpacts.clear();
            String campaign=string(command,"campaign","legacy");
            if(owner.isDead())respawnOwner();
            boolean fresh=campaigns.select(owner,campaign,command.has("legacy")&&command.get("legacy").getAsBoolean());
            grantIronStarter();creative=owner.isCreative();campaigns.save(owner);
            introLanding=command.has("intro")&&command.get("intro").getAsBoolean()&&!campaigns.introDone&&name.equals("Tutorial_01");
            owner.closeHandledScreen(); openContainer=null; combo=0; activeAttack=0;
            enemies.clear();miningPos=null;miningProgress=0;
            pendingHit=null;pendingSwing=null;
            nextSwingTick=0;lastAttackEvent=null;lastAttackRequest=cancelledAttackRequest=0;
            for(var dropped:trackedDrops.values()) {dropped.setNoGravity(true);dropped.setVelocity(Vec3d.ZERO);}
            cleanupTerrain(); scene = name;sceneVisit=integer(command,"visit",0);
            String sceneKey=campaign+":"+name;
            // The first migrated legacy campaign keeps its previously placed blocks.
            if(!scenes.containsKey(sceneKey)&&campaigns.legacyOwner()&&scenes.containsKey(name))scenes.put(sceneKey,scenes.get(name));
            if (!scenes.containsKey(sceneKey)) {
                if (scenes.size() >= 10000) throw new IOException("Scene limit");
                scenes.put(sceneKey, scenes.values().stream().mapToInt(Integer::intValue).max().orElse(0)+1);
                Path temp = sceneFile.resolveSibling(sceneFile.getFileName() + ".tmp");
                Files.writeString(temp, GSON.toJson(scenes));
                Files.move(temp, sceneFile, StandardCopyOption.REPLACE_EXISTING);
            }
            slot = scenes.get(sceneKey);
            centerX = integer(command, "x", 0); centerY = integer(command, "y", 0);
            exactX=command.has("exactX")?command.get("exactX").getAsDouble():centerX+0.5;
            exactY=command.has("exactY")?command.get("exactY").getAsDouble():centerY;
            hadPose=false;fallen=0;footstepDistance=0;teleportWaiting=teleportSerial=0;
            owner.fallDistance=0;owner.clearActiveItem();owner.setVelocity(Vec3d.ZERO);owner.stopFallFlying();
            owner.setPosition(slot*1024+exactX,64+exactY,.5);
            status = "Minecraft connected: " + name;
            return;
        }
        if (scene.isEmpty() || !scene.equals(string(command, "scene", ""))) return;
        if(command.has("visit")&&integer(command,"visit",-1)!=sceneVisit)return;
        if(command.has("campaign")&&!campaigns.id().equals(string(command,"campaign","")))return;
        // Finish the short clutch before player actions mutate hand items or containers.
        if(landingWater!=null&&switch(type) {
            case "select","mode","place","towerPlace","interact","mine","mining","use","itemUse","offhand","drop","slotClick","inventoryOpen","creativeCatalog","creativeItem","creativeHotbar","creativeDiscard","craft","swap","kit","adventureKit","swing","hit","damage","respawn","checkpoint","rest","containerMove","containerButton","anvilName","close","eat","nativeProjectileHit" -> true;
            default -> false;
        })collectLandingWater();
        switch (type) {
            case "audioFile" -> {
                String key=string(command,"key","");if(peer!=null&&peer.audio&&key.matches("[0-9a-f]{64}")) {
                    if(command.has("ready")&&command.get("ready").getAsBoolean()) {if(peer.audioFiles.size()>=512)peer.audioFiles.clear();peer.audioFiles.add(key);}
                    else peer.audioFiles.remove(key);
                }
            }
            case "pose" -> {
                if(server.getTicks()<teleportWaiting) break;
                // Poses queued before the last pearl impact cannot reintroduce
                // its old coordinates/fall distance, even after the wait expires.
                if(command.has("teleport")&&integer(command,"teleport",-1)!=teleportSerial)break;
                centerX = clamp(integer(command, "x", 0), -400, 400); centerY = clamp(integer(command, "y", 0), -60, 240);
                exactX=command.has("exactX") ? command.get("exactX").getAsDouble() : centerX+0.5;
                exactY=command.has("exactY") ? command.get("exactY").getAsDouble() : centerY;
                reachY=exactY+(command.has("gliding")&&command.get("gliding").getAsBoolean()?.4:command.has("crouching")&&command.get("crouching").getAsBoolean()?1.27:1.62);
                boolean grounded=command.has("grounded") && command.get("grounded").getAsBoolean();
                boolean gliding=command.has("gliding")&&command.get("gliding").getAsBoolean();
                boolean flying=owner.getAbilities().allowFlying&&(owner.isSpectator()||command.has("flying")&&command.get("flying").getAsBoolean());
                if(owner.getAbilities().flying!=flying) {owner.getAbilities().flying=flying;owner.sendAbilitiesUpdate();}
                boolean scripted=command.has("scripted")&&command.get("scripted").getAsBoolean();
                if(gliding && !grounded && owner.getEquippedStack(net.minecraft.entity.EquipmentSlot.CHEST).isOf(Items.ELYTRA)) owner.startFallFlying();else if(owner.isFallFlying()) owner.stopFallFlying();
                if(scripted) {fallen=0;owner.fallDistance=0;footstepDistance=0;}
                if(scripted||owner.isTouchingWater()||owner.isClimbing()||gliding||flying)fallen=0;
                else if(hadPose&&!wasGrounded)fallen+=Math.max(0,(float)(lastPoseY-exactY));
                // A late connection can start after the first landing. The pending opening
                // must still place water instead of requiring a previously observed air pose.
                if(!scripted&&grounded&&((hadPose&&!wasGrounded)||introLanding)) {
                    boolean waterSave=owner.interactionManager.getGameMode()==GameMode.SURVIVAL&&(fallen>=11.999f||introLanding)&&placeLandingWater();
                    if(!creative&&!owner.isSpectator()&&!waterSave&&owner.handleFallDamage(fallen,1,owner.getDamageSources().fall()))
                        LOG.info("Native fall damage distance={} HP={} absorption={} teleport={}",fallen,owner.getHealth(),owner.getAbsorptionAmount(),teleportSerial);
                    if(creative&&fallen>12)landingEvent("hero",fallen);
                    if(introLanding&&(waterSave||creative||owner.isSpectator())) {
                        if(!waterSave&&!(creative&&fallen>12))landingEvent("plain",fallen);
                        introLanding=false;campaigns.introDone=true;if(!waterSave)saveCampaign();
                    }
                    fallen=0;owner.fallDistance=0;
                }
                if(hadPose && !creative && !scripted) {
                    if(command.has("sprinting") && command.get("sprinting").getAsBoolean()) owner.addExhaustion((float)Math.min(2,Math.abs(exactX-lastPoseX))*0.1f);
                    if(wasGrounded && !grounded && exactY>lastPoseY+0.02) owner.addExhaustion(0.05f);
                }
                if(!scripted&&hadPose&&wasGrounded&&grounded&&!owner.isSpectator()&&!(command.has("crouching")&&command.get("crouching").getAsBoolean())&&!owner.isTouchingWater()) {
                    footstepDistance+=Math.min(2,Math.abs(exactX-lastPoseX))*.6;
                    if(footstepDistance>=1) {
                        footstepDistance%=1;var floor=pos((int)Math.floor(exactX),(int)Math.floor(exactY-.05));var state=owner.getServerWorld().getBlockState(floor);
                        if(state.isAir()) {floor=floor.down();state=owner.getServerWorld().getBlockState(floor);}
                        if(!state.isAir())((dev.hollowcraft.mixin.StepSoundAccess)owner).hollowcraft$stepSound(floor,state);
                    }
                } else footstepDistance=0;
                hadPose=true;lastPoseX=exactX;lastPoseY=exactY;wasGrounded=grounded;
                owner.setOnGround(grounded);
                if(command.has("enemies")) enemies.observe(owner,command.getAsJsonArray("enemies"),slot*1024);
                if(command.has("drops")) for(var value:command.getAsJsonArray("drops")) {
                    var entry=value.getAsJsonObject();var entity=owner.getServerWorld().getEntityById(entry.get("entity").getAsInt());
                    double x=entry.get("x").getAsDouble(),y=entry.get("y").getAsDouble();
                    if(entity instanceof net.minecraft.entity.ItemEntity && Math.abs(x-exactX)<20 && Math.abs(y-exactY)<20 && Math.abs(entity.getX()-slot*1024-exactX)<20)
                        entity.setPosition(slot*1024+x,64+y,0.5);
                }
                owner.setSneaking(command.has("crouching") && command.get("crouching").getAsBoolean());owner.setSprinting(command.has("sprinting")&&command.get("sprinting").getAsBoolean()&&(creative||owner.getHungerManager().getFoodLevel()>6)); updateTerrain(command);
                FluidBridge.seal(owner.getServerWorld(),pos(centerX,centerY));
                if(openContainer!=null && (Math.abs(openContainer.getX()-slot*1024-centerX)>6 || Math.abs(openContainer.getY()-64-centerY)>6)) {owner.closeHandledScreen();openContainer=null;}
            }
            case "teleportAck" -> {
                if(teleportWaiting==0||integer(command,"teleport",-1)!=teleportSerial)break;
                double x=command.get("x").getAsDouble(),y=command.get("y").getAsDouble();
                if(!Double.isFinite(x)||!Double.isFinite(y)||Math.abs(x)>400||y<-60||y>240)break;
                teleportWaiting=0;exactX=x;exactY=y;centerX=(int)Math.floor(x);centerY=(int)Math.floor(y);lastPoseX=x;lastPoseY=y;
                owner.setPosition(slot*1024+x,64+y,.5);pearlLanded(owner);
            }
            case "checkpoint" -> {campaigns.save(owner);server.save(false,true,true);status="World and player data saved";}
            case "nativeDeath" -> enemies.confirmDeath(integer(command,"id",0),integer(command,"generation",-1));
            case "rest" -> {if(owner.isAlive()) {owner.setHealth(owner.getMaxHealth());owner.getHungerManager().setFoodLevel(20);owner.getHungerManager().setSaturationLevel(5);campaigns.save(owner);server.save(false,true,true);status="Rested and saved at campaign checkpoint";}}
            case "containerButton" -> {if(openContainer!=null&&integer(command,"syncId",-1)==owner.currentScreenHandler.syncId) {int button=integer(command,"button",-1);if(button>=0&&button<1000) {owner.currentScreenHandler.onButtonClick(owner,button);owner.currentScreenHandler.sendContentUpdates();snapshot();}}}
            case "anvilName" -> {if(openContainer!=null&&owner.currentScreenHandler instanceof net.minecraft.screen.AnvilScreenHandler anvil) anvil.setNewItemName(string(command,"name","").substring(0,Math.min(50,string(command,"name","").length())));}
            case "select" -> {owner.getInventory().selectedSlot = clamp(integer(command, "slot", 0), 0, 8);owner.networkHandler.sendPacket(new net.minecraft.network.packet.s2c.play.UpdateSelectedSlotS2CPacket(owner.getInventory().selectedSlot));}
            case "mode" -> {
                GameMode mode=GameMode.byName(string(command,"mode","survival"),GameMode.SURVIVAL);
                boolean enteringCreative=mode==GameMode.CREATIVE&&!owner.isCreative();
                owner.changeGameMode(mode);creative=mode==GameMode.CREATIVE;
                miningPos=null;miningProgress=0;owner.stopUsingItem();pendingSwing=null;pendingHit=null;
                status = "Game mode: "+mode.getName();
                if(enteringCreative) {var reward=CreativeTrident.grant(owner);if(!reward.isEmpty()) {give(reward);saveCampaign();}}
            }
            case "flight" -> {
                owner.getAbilities().flying=owner.getAbilities().allowFlying&&(owner.isSpectator()||command.has("flying")&&command.get("flying").getAsBoolean());
                owner.fallDistance=0;fallen=0;owner.sendAbilitiesUpdate();snapshot();
            }
            case "place" -> {
                boolean accepted=place(command);
                if(peer!=null) {JsonObject event=new JsonObject();event.addProperty("type","placeResult");event.addProperty("scene",scene);event.addProperty("x",integer(command,"x",0));event.addProperty("y",integer(command,"y",0));event.addProperty("accepted",accepted);peer.event(event);}
                snapshot();
            }
            case "towerPlace" -> towerPlace(command);
            case "mine" -> mine(command);
            case "mining" -> {
                if(command.has("active") && !command.get("active").getAsBoolean()) {miningPos=null;miningProgress=0;break;}
                BlockPos target=target(command);if(target==null || NativeTerrainBlock.proxy(owner.getServerWorld().getBlockState(target))||!owner.getAbilities().allowModifyWorld||owner.isSpectator()) break;
                if(!target.equals(miningPos)) {miningPos=target;miningProgress=0;miningStartedTick=server.getTicks();}
                lastMiningTick=server.getTicks();
            }
            case "craft" -> craft(command);
            case "use" -> use(command);
            case "interact" -> interact(command);
            case "containerMove" -> containerMove(command);
            case "close" -> {closeGui();owner.closeHandledScreen();openContainer=null;}
            case "guiInput" -> {if(peer!=null&&peer.gui)NativeGuiBridge.input(command);}
            case "swing" -> swing(command);
            case "cancelAttack" -> {
                int request=integer(command,"request",0);cancelledAttackRequest=Math.max(cancelledAttackRequest,request);
                if(lastAttackRequest<=request) {activeAttack=0;pendingHit=null;}
                if(pendingSwing!=null&&integer(pendingSwing,"request",0)<=request)pendingSwing=null;
            }
            case "damage" -> {if(!creative && !owner.isSpectator() && !owner.isDead()) {
                if(command.has("velocityX")&&command.has("velocityY")) {double vx=command.get("velocityX").getAsDouble(),vy=command.get("velocityY").getAsDouble();if(Double.isFinite(vx)&&Double.isFinite(vy))owner.setVelocity(Math.max(-4,Math.min(4,vx)),Math.max(-4,Math.min(4,vy)),0);}
                var source=enemies.source(integer(command,"enemy",0));String kind=string(command,"kind","mob");var damage=owner.getDamageSources().generic();
                if(kind.equals("projectile")&&command.has("attackerX")&&command.has("attackerY")) {
                    double x=command.get("attackerX").getAsDouble(),y=command.get("attackerY").getAsDouble();
                    if(Double.isFinite(x)&&Double.isFinite(y)) {var arrow=new net.minecraft.entity.projectile.ArrowEntity(owner.getServerWorld(),slot*1024+x,64+y,.5);damage=owner.getDamageSources().arrow(arrow,source!=null?source:arrow);}
                }
                else if(source!=null&&!kind.equals("hazard")) damage=owner.getDamageSources().mobAttack(source);
                owner.damage(damage,(float)Math.max(0,Math.min(10000,command.get("amount").getAsDouble())));
            }}
            case "eat" -> PlayerBridge.eat(owner);
            case "itemUse" -> ItemUseBridge.use(owner,command,target(command));
            case "projectileStep" -> {
                if(command.has("steps")&&command.get("steps").isJsonArray()) {
                    int count=0;for(var element:command.getAsJsonArray("steps")) {
                        if(++count>64)break;var step=element.getAsJsonObject();var entity=owner.getServerWorld().getEntityById(integer(step,"entity",-1));
                        if(entity instanceof net.minecraft.entity.projectile.ProjectileEntity projectile&&projectile.getOwner()==owner&&projectile.getUuidAsString().equals(string(step,"uuid","")))NativeProjectileSteps.allow(projectile,integer(step,"age",-1));
                    }
                }
            }
            case "nativeProjectileHit" -> {nativeProjectileHit(command);snapshot();}
            case "tntStep" -> {
                if(command.has("steps")&&command.get("steps").isJsonArray()) {
                    int count=0;for(var element:command.getAsJsonArray("steps")) {
                        if(++count>64)break;var step=element.getAsJsonObject();var entity=owner.getServerWorld().getEntityById(integer(step,"entity",-1));
                        if(entity instanceof TntEntity tnt&&tnt.getUuidAsString().equals(string(step,"uuid",""))&&step.has("dx")&&step.has("dy"))
                            NativeTntSteps.allow(tnt,integer(step,"age",-1),step.get("dx").getAsDouble(),step.get("dy").getAsDouble(),step.has("lift")?step.get("lift").getAsDouble():0);
                    }
                }
            }
            case "offhand" -> PlayerBridge.offhand(owner);
            case "drop" -> owner.dropSelectedItem(command.has("all") && command.get("all").getAsBoolean());
            case "slotClick" -> PlayerBridge.click(owner,command);
            case "inventoryOpen" -> {owner.clearActiveItem();pendingSwing=null;owner.closeHandledScreen();openContainer=null;openGui(true,integer(command,"uiRequest",0));snapshot();}
            case "respawn" -> {
                if(owner.isDead()) {respawnOwner();hadPose=false;}
            }
            case "hit" -> {
                if(activeAttack!=0 && integer(command,"attack",0)==activeAttack && server.getTicks()<hitReadyTick) {pendingHit=command;break;}
                if(activeAttack!=0 && integer(command,"attack",0)==activeAttack && server.getTicks()>=hitReadyTick) {
                    if(command.has("playerX")&&command.has("playerY")) {
                        double x=command.get("playerX").getAsDouble(),y=command.get("playerY").getAsDouble();
                        if(!Double.isFinite(x)||!Double.isFinite(y)||Math.abs(x)>400||y<-60||y>240)break;
                        owner.setPosition(slot*1024+x,64+y,.5);owner.setOnGround(command.has("grounded")&&command.get("grounded").getAsBoolean());
                    }
                    if(command.has("enemies"))enemies.observe(owner,command.getAsJsonArray("enemies"),slot*1024);
                    JsonArray targets=command.has("targets") ? command.getAsJsonArray("targets") : new JsonArray();
                    enemies.hit(owner,targets,command.has("falling")&&command.get("falling").getAsBoolean(),attackMultiplier);
                    if(targets.isEmpty()) {owner.resetLastAttackedTicks();if(command.has("terrain")&&command.get("terrain").getAsBoolean()&&!creative) owner.getMainHandStack().damage(1,owner,p->p.sendToolBreakStatus(Hand.MAIN_HAND));}
                    activeAttack=0;
                }
            }
            case "kit" -> {
                if(creative) {give(new ItemStack(Items.DIRT,16));give(new ItemStack(Items.COBBLESTONE,16));give(new ItemStack(Items.DIAMOND_SWORD));give(new ItemStack(Items.IRON_AXE));give(new ItemStack(Items.STONE_PICKAXE));give(new ItemStack(Items.CHEST,16));give(new ItemStack(Items.COAL,64));give(new ItemStack(Items.RAW_IRON,16));give(new ItemStack(Items.COOKED_BEEF,16));give(new ItemStack(Items.IRON_CHESTPLATE));give(new ItemStack(Items.SHIELD));give(new ItemStack(Items.TNT,8));give(new ItemStack(Items.FLINT_AND_STEEL));status="Combat and interaction kit added";}
            }
            case "adventureKit" -> {if(creative) {
                for(Item item:new Item[]{Items.BOW,Items.CROSSBOW,Items.TRIDENT,Items.ELYTRA,Items.WATER_BUCKET,Items.LAVA_BUCKET,Items.BUCKET,Items.SHEARS,Items.SHIELD,Items.DIAMOND_SWORD}) give(new ItemStack(item));
                for(Item item:new Item[]{Items.ARROW,Items.FIREWORK_ROCKET,Items.ENDER_PEARL,Items.SNOWBALL,Items.REDSTONE,Items.LEVER,Items.OAK_DOOR,Items.PISTON,Items.REPEATER,Items.REDSTONE_TORCH}) give(new ItemStack(item,16));
                give(net.minecraft.potion.PotionUtil.setPotion(new ItemStack(Items.POTION),net.minecraft.potion.Potions.STRONG_HEALING));
                give(net.minecraft.potion.PotionUtil.setPotion(new ItemStack(Items.POTION),net.minecraft.potion.Potions.LONG_SWIFTNESS));
                give(net.minecraft.potion.PotionUtil.setPotion(new ItemStack(Items.POTION),net.minecraft.potion.Potions.LONG_FIRE_RESISTANCE));
                give(net.minecraft.potion.PotionUtil.setPotion(new ItemStack(Items.SPLASH_POTION),net.minecraft.potion.Potions.STRONG_HARMING));
                status="Adventure kit added";
            }}
            case "creativeCatalog" -> {if(creative&&peer!=null) {
                owner.clearActiveItem();owner.closeHandledScreen();openContainer=null;
                openGui(true,integer(command,"uiRequest",0));
                creativeCatalog();snapshot();
            }}
            case "creativeHotbar" -> {if(creative)creativeHotbar(command);}
            case "creativeItem" -> {if(creative) {
                if(command.has("catalogIndex")) {
                    if(integer(command,"catalogRevision",-1)!=catalog.revision()) {notice("Catalogue updated; select the item again");break;}
                    ItemStack stack=catalog.stack(integer(command,"catalogIndex",-1),integer(command,"count",64));
                    if(!stack.isEmpty()) {
                        String destination=string(command,"destination","cursor");
                        if(destination.equals("cursor"))owner.playerScreenHandler.setCursorStack(stack);
                        else if(destination.equals("inventory"))give(stack);
                        else owner.getInventory().setStack(clamp(integer(command,"slot",owner.getInventory().selectedSlot),0,8),stack);
                        owner.playerScreenHandler.sendContentUpdates();snapshot();
                    }
                    break;
                }
                Identifier id=Identifier.tryParse(string(command,"id",""));
                if(id!=null&&Registries.ITEM.containsId(id)) {int index=clamp(integer(command,"slot",owner.getInventory().selectedSlot),0,35);ItemStack stack=new ItemStack(Registries.ITEM.get(id));stack.setCount(clamp(integer(command,"count",1),1,stack.getMaxCount()));Identifier potion=Identifier.tryParse(string(command,"potion",""));if(potion!=null&&Registries.POTION.containsId(potion)) net.minecraft.potion.PotionUtil.setPotion(stack,Registries.POTION.get(potion));owner.getInventory().setStack(index,stack);}
            }}
            case "creativeDiscard" -> {if(creative) {
                owner.playerScreenHandler.setCursorStack(ItemStack.EMPTY);
                if(command.has("all")&&command.get("all").getAsBoolean())owner.getInventory().clear();
                snapshot();
            }}
            case "clearTestFluids" -> {if(creative&&Boolean.getBoolean("hollowcraft.smoke")) {
                for(int x=centerX-12;x<=centerX+12;x++) for(int y=centerY-10;y<=centerY+10;y++) {BlockPos p=pos(x,y);BlockState state=owner.getServerWorld().getBlockState(p);if(state.isOf(Blocks.WATER)||state.isOf(Blocks.LAVA)||state.isOf(Blocks.FIRE)) owner.getServerWorld().setBlockState(p,Blocks.AIR.getDefaultState(),Block.NOTIFY_ALL);}
            }}
            case "swap" -> {
                int from = integer(command, "from", -1), to = integer(command, "to", -1);
                if (from >= 0 && from < 36 && to >= 0 && to < 36) {
                    ItemStack first = owner.getInventory().getStack(from);
                    owner.getInventory().setStack(from, owner.getInventory().getStack(to));
                    owner.getInventory().setStack(to, first);
                }
            }
            default -> { }
        }
    }
    private BlockPos pos(int x, int y) { return new BlockPos(slot * 1024 + x, 64 + y, 0); }
    private void creativeHotbar(JsonObject command) {
        int index=integer(command,"slot",-1);if(index<0||index>=9)return;
        var player=owner;var localServer=server;String campaign=campaigns.id(),room=scene;
        if(string(command,"action","").equals("save")) {
            var stacks=new ItemStack[9];for(int i=0;i<9;i++)stacks[i]=player.getInventory().getStack(i).copy();
            CreativeHotbars.save(index,stacks,error->afterHotbar(localServer,()-> {
                if(owner!=player||!campaigns.id().equals(campaign)||!scene.equals(room))return;
                notice(error.isEmpty()?"Saved hotbar "+(index+1)+" (X + "+(index+1)+" to load)":error);
                creativeCatalog();
            }));
        } else if(string(command,"action","").equals("load")) {
            CreativeHotbars.load(index,(stacks,error)->afterHotbar(localServer,()-> {
                if(owner!=player||!campaigns.id().equals(campaign)||!scene.equals(room)||!creative)return;
                if(!error.isEmpty()) {notice(error);return;}
                owner.clearActiveItem();
                for(int i=0;i<9;i++)owner.getInventory().setStack(i,stacks[i].isItemEnabled(owner.getWorld().getEnabledFeatures())?stacks[i]:ItemStack.EMPTY);
                owner.playerScreenHandler.sendContentUpdates();snapshot();notice("Loaded hotbar "+(index+1));
            }));
        }
    }
    private void afterHotbar(MinecraftServer expected,Runnable update) {
        if(expected==null||!expected.isRunning())return;
        expected.execute(()-> {if(server==expected&&owner!=null)update.run();});
    }
    private void notice(String text) {
        if(peer==null)return;var event=new JsonObject();event.addProperty("type","notice");event.addProperty("scene",scene);event.addProperty("text",text);peer.event(event);
    }
    private boolean placeLandingWater() {
        collectLandingWater();
        BlockPos cell=pos((int)Math.floor(exactX),(int)Math.floor(exactY+.08));
        var world=owner.getServerWorld();var before=world.getBlockState(cell);
        if(!before.isReplaceable()&&!NativeTerrainBlock.proxy(before))return false;
        // Keep the requested automatic rescue when no bucket is carried. The normal
        // starter kit uses a real bucket; the fallback adds no item to the inventory.
        var bucket=LandingBucket.begin(owner);
        if(bucket!=null)owner.clearActiveItem();boolean placed=false;
        try {
            // Unity keeps the fractional native floor while vanilla water occupies its proxy.
            if(NativeTerrainBlock.proxy(before))world.setBlockState(cell,Blocks.AIR.getDefaultState(),Block.NOTIFY_LISTENERS);
            FluidBridge.seal(world,cell);
            var item=(BucketItem)Items.WATER_BUCKET;
            placed=item.placeFluid(owner,world,cell,new BlockHitResult(Vec3d.ofCenter(cell),Direction.UP,cell,false));
            if(!placed)return false;
            if(bucket!=null) {
                item.onEmptied(owner,world,owner.getStackInHand(bucket.hand),cell);
                net.minecraft.advancement.criterion.Criteria.PLACED_BLOCK.trigger(owner,cell,owner.getStackInHand(bucket.hand));
                bucket.empty();owner.incrementStat(net.minecraft.stat.Stats.USED.getOrCreateStat(Items.WATER_BUCKET));owner.swingHand(bucket.hand);
            }
        } finally {
            if(!placed) {if(bucket!=null)bucket.abort();if(NativeTerrainBlock.proxy(before)&&world.getBlockState(cell).isAir())world.setBlockState(cell,before,Block.NOTIFY_LISTENERS);}
        }
        landingBucket=bucket;landingHand=bucket==null?Hand.MAIN_HAND:bucket.hand;
        landingWater=cell;landingWaterBefore=before;collectWaterTick=server.getTicks()+30;
        landingEvent("water",fallen);snapshot();LOG.info("Automatic landing water distance={} cell={}",fallen,cell);return true;
    }
    private void collectLandingWater() {
        if(landingWater==null||owner==null)return;
        var cell=landingWater;var before=landingWaterBefore;var bucket=landingBucket;
        var world=bucket==null?owner.getServerWorld():bucket.player.getServerWorld();var state=world.getBlockState(cell);
        ItemStack filled=ItemStack.EMPTY;
        if(state.isOf(Blocks.WATER)&&state.getBlock() instanceof FluidDrainable drainable) {
            filled=drainable.tryDrainFluid(world,cell,state);
            if(!filled.isEmpty()&&world.getBlockState(cell).isAir())world.setBlockState(cell,before,Block.NOTIFY_ALL);
        }
        landingWater=null;landingWaterBefore=null;landingBucket=null;
        boolean recovered=bucket==null?!filled.isEmpty():bucket.finish(filled,cell);
        if(recovered) {
            if(bucket!=null) {
                bucket.player.swingHand(bucket.hand);bucket.player.incrementStat(net.minecraft.stat.Stats.USED.getOrCreateStat(Items.BUCKET));
                net.minecraft.advancement.criterion.Criteria.FILLED_BUCKET.trigger(bucket.player,filled);
            }
            world.playSound(null,cell,net.minecraft.sound.SoundEvents.ITEM_BUCKET_FILL,net.minecraft.sound.SoundCategory.PLAYERS,1,1);
            world.emitGameEvent(owner,net.minecraft.world.event.GameEvent.FLUID_PICKUP,cell);
        }
        if(peer!=null) {JsonObject event=new JsonObject();event.addProperty("type","waterCollected");event.addProperty("scene",scene);event.addProperty("hand",landingHand==Hand.OFF_HAND?1:0);event.addProperty("recovered",recovered);peer.event(event);}
        saveCampaign();
    }
    private void landingEvent(String kind,float distance) {
        if(peer==null)return;JsonObject event=new JsonObject();event.addProperty("type","landing");event.addProperty("scene",scene);event.addProperty("kind",kind);event.addProperty("intro",introLanding);event.addProperty("distance",distance);event.addProperty("x",exactX);event.addProperty("y",exactY);event.addProperty("hand",landingHand==Hand.OFF_HAND?1:0);peer.event(event);
    }
    private BlockPos target(JsonObject command) {
        int x = integer(command, "x", 0), y = integer(command, "y", 0);
        double dx=exactX-Math.max(x,Math.min(x+1,exactX)),dy=reachY-Math.max(y,Math.min(y+1,reachY));
        double reach=creative?5:4.5;
        if (dx*dx+dy*dy>reach*reach+.001 || y < -60 || y > 240 || x < -400 || x > 400) return null;
        return pos(x, y);
    }
    private boolean place(JsonObject command) {return place(command,Hand.MAIN_HAND).isAccepted();}
    private static boolean towerBlock(ItemStack stack) {
        if(stack.isEmpty()||!(stack.getItem() instanceof BlockItem block))return false;
        return Block.isShapeFullCube(block.getBlock().getDefaultState().getCollisionShape(net.minecraft.world.EmptyBlockView.INSTANCE,BlockPos.ORIGIN));
    }
    private boolean nativeFoundation(JsonObject command,BlockPos target) {
        if(!string(command,"type","").equals("towerPlace")||!command.has("nativeFoundation")||!command.get("nativeFoundation").getAsBoolean())return false;
        if(!command.has("supportY")||!command.has("playerX")||!command.has("playerY"))return false;
        double support=command.get("supportY").getAsDouble(),x=command.get("playerX").getAsDouble(),feet=command.get("playerY").getAsDouble();
        int y=target.getY()-64;
        return Double.isFinite(support)&&Double.isFinite(x)&&Double.isFinite(feet)&&support>=y-.05&&support<y+1
            &&feet>=y+1.015&&feet<=support+3&&Math.abs(x-(target.getX()-slot*1024+.5))<=.85;
    }
    private void towerPlace(JsonObject command) {
        String request=string(command,"request","");if(!request.matches("[0-9a-f]{32}"))return;
        var cached=towerReplies.get(request);if(cached!=null) {if(peer!=null)peer.event(cached);return;}
        boolean accepted=false;String reason="No solid block selected";
        try {
            if(!owner.isAlive()||owner.isSpectator()||!owner.getAbilities().allowModifyWorld) {reason="Player cannot build";}
            else if(!command.has("playerX")||!command.has("playerY")) {reason="Missing player position";}
            else {
                double x=command.get("playerX").getAsDouble(),y=command.get("playerY").getAsDouble();
                int cellX=integer(command,"x",0),cellY=integer(command,"y",0);
                if(!Double.isFinite(x)||!Double.isFinite(y)||Math.abs(x-cellX-.5)>.85||y<cellY+1.015||y>cellY+4) {reason="Feet have not cleared the tower cell";}
                else for(var hand:Hand.values()) {
                    if(!towerBlock(owner.getStackInHand(hand)))continue;
                    // This is an explicit building action, separate from shield/food
                    // use or container interaction. Vanilla BlockItem still owns placement.
                    owner.clearActiveItem();
                    accepted=place(command,hand).isAccepted();reason=status;
                    if(accepted)itemSwing(hand);
                    break;
                }
            }
        } catch(RuntimeException error) {reason=error.getClass().getSimpleName();LOG.warn("Tower placement failed",error);}
        var reply=new JsonObject();reply.addProperty("type","towerResult");reply.addProperty("scene",scene);reply.addProperty("request",request);
        reply.addProperty("x",integer(command,"x",0));reply.addProperty("y",integer(command,"y",0));reply.addProperty("accepted",accepted);reply.addProperty("reason",reason);
        if(towerReplies.size()>=64)towerReplies.remove(towerReplies.keySet().iterator().next());towerReplies.put(request,reply);
        LOG.info("Tower result request={} cell={},{} accepted={} reason={}",request,integer(command,"x",0),integer(command,"y",0),accepted,reason);
        if(peer!=null)peer.event(reply);owner.currentScreenHandler.sendContentUpdates();snapshot();
    }
    private ActionResult place(JsonObject command,Hand hand) {
        if(command.has("playerX")&&command.has("playerY")) {
            double x=command.get("playerX").getAsDouble(),y=command.get("playerY").getAsDouble();
            if(!Double.isFinite(x)||!Double.isFinite(y)||Math.abs(x)>400||y<-60||y>240)return ActionResult.FAIL;
            exactX=x;exactY=y;reachY=y+(owner.isSneaking()?1.27:1.62);
        }
        BlockPos target = target(command);
        ServerWorld world = owner.getServerWorld();
        if (target == null) { status = "Target outside bridge reach or bounds"; return ActionResult.FAIL; }
        if(owner.isSpectator()||!owner.getAbilities().allowModifyWorld) {status="This game mode cannot place blocks";return ActionResult.FAIL;}
        var nativeShape=NativeTerrainBlock.shapes.get(target);
        boolean foundation=nativeFoundation(command,target)&&towerBlock(owner.getStackInHand(hand));
        if (!foundation&&terrain.contains(target)&&(nativeShape==null||nativeShape.getMax(Direction.Axis.Y)>=.999)) { status = "Hollow Knight terrain occupies target"; return ActionResult.FAIL; }
        if (!world.getBlockState(target).isAir()&&!world.getBlockState(target).isOf(NativeTerrainBlock.INSTANCE)&&!(foundation&&NativeTerrainBlock.proxy(world.getBlockState(target)))) { status = "Target already contains a Minecraft block"; return ActionResult.FAIL; }
        if (!(owner.getStackInHand(hand).getItem() instanceof BlockItem)) { status = "Select a block in either hand"; return ActionResult.FAIL; }
        boolean floorEdge=world.getBlockState(target).isOf(NativeTerrainBlock.INSTANCE)||foundation&&NativeTerrainBlock.proxy(world.getBlockState(target));
        // A native floor can sit a few hundredths above a voxel edge. Keep the physical
        // floor in Unity, but let vanilla put the requested block against its supporting cell.
        if(floorEdge)world.setBlockState(target,Blocks.AIR.getDefaultState(),Block.NOTIFY_LISTENERS);
        // Use Minecraft's BlockItem implementation, including inventory decrement and placement rules.
        BlockPos support = target.down(); Direction face = Direction.UP;
        for (Direction direction : new Direction[] {Direction.DOWN, Direction.WEST, Direction.EAST, Direction.UP}) {
            if (!world.getBlockState(target.offset(direction)).isReplaceable()) { support = target.offset(direction); face = direction.getOpposite(); break; }
        }
        boolean temporarySupport = world.getBlockState(support).isAir();
        // A 2D cursor can address the room's background plane. A transient back-plane anchor
        // lets vanilla place the exact requested cell; it is never persisted as terrain.
        if(temporarySupport) {support=target.south();face=Direction.NORTH;}
        temporarySupport=world.getBlockState(support).isAir();
        if (temporarySupport) world.setBlockState(support, Blocks.BARRIER.getDefaultState(), Block.NOTIFY_LISTENERS);
        try {
            Vec3d hit = Vec3d.ofCenter(support).add(Vec3d.of(face.getVector()).multiply(0.5));
            boolean crouching=owner.isSneaking(); owner.setSneaking(true);
            Vec3d original=owner.getPos();owner.setPosition(slot*1024+exactX,64+exactY,10.5);
            float yaw=owner.getYaw();owner.setYaw(target.getX()+.5>=original.x?-90:90);
            ActionResult result;
            try { result = owner.interactionManager.interactBlock(owner, world, owner.getStackInHand(hand), hand,
                new BlockHitResult(hit, face, support, false));
            } finally {owner.setPosition(original);owner.setYaw(yaw);owner.setSneaking(crouching);}
            status = result.isAccepted() ? "Placed " + Registries.BLOCK.getId(world.getBlockState(target).getBlock()) : "Minecraft rejected placement";
            if(!result.isAccepted()) LOG.info("Placement rejected target={} below={} above={} face={} support={}",target,world.getBlockState(target.down()),world.getBlockState(target.up()),face,world.getBlockState(support));
            return result;
        } finally { if (temporarySupport) world.setBlockState(support, Blocks.AIR.getDefaultState(), Block.NOTIFY_LISTENERS);if(floorEdge&&world.getBlockState(target).isAir())world.setBlockState(target,NativeTerrainBlock.INSTANCE.getDefaultState(),Block.NOTIFY_LISTENERS); }
    }
    private int itemSwingSerial;
    private void itemSwing(Hand hand) {
        owner.swingHand(hand,true);
        if(peer!=null) {var event=new JsonObject();event.addProperty("type","itemSwing");event.addProperty("scene",scene);event.addProperty("serial",++itemSwingSerial);event.addProperty("hand",hand==Hand.OFF_HAND?1:0);peer.event(event);}
    }
    private void interact(JsonObject command) {
        boolean[] placed={false};
        var placement=command.deepCopy();placement.addProperty("x",integer(command,"placeX",integer(command,"x",0)));placement.addProperty("y",integer(command,"placeY",integer(command,"y",0)));
        try {
            if(!owner.isAlive()||owner.isSpectator()||owner.isUsingItem()||!ItemUseBridge.aim(owner,command))return;
            var clicked=target(command);var world=owner.getServerWorld();
            try(var scope=RemotePlacement.begin(owner,hand->{
                if(!command.has("canPlace")||!command.get("canPlace").getAsBoolean())return ActionResult.FAIL;
                var result=place(placement,hand);placed[0]=result.isAccepted();return result;
            })) {
                for(var hand:Hand.values()) {
                    var stack=owner.getStackInHand(hand);
                    if(!stack.isItemEnabled(world.getEnabledFeatures()))return;
                    if(clicked!=null) {
                        var result=owner.interactionManager.interactBlock(owner,world,stack,hand,new BlockHitResult(Vec3d.ofCenter(clicked),Direction.UP,clicked,false));
                        if(owner.currentScreenHandler!=owner.playerScreenHandler)openContainer=clicked;
                        if(result.isAccepted()) {if(result.shouldSwingHand())itemSwing(hand);return;}
                        if(result==ActionResult.FAIL)return;
                    }
                    var result=ItemUseBridge.air(owner,hand,clicked);
                    if(result.isAccepted()) {if(result.shouldSwingHand())itemSwing(hand);return;}
                }
            }
        } finally {
            if(peer!=null) {var event=new JsonObject();event.addProperty("type","placeResult");event.addProperty("scene",scene);event.addProperty("x",integer(placement,"x",0));event.addProperty("y",integer(placement,"y",0));event.addProperty("accepted",placed[0]);peer.event(event);}
            owner.currentScreenHandler.sendContentUpdates();snapshot();
        }
    }
    private void use(JsonObject command) {
        BlockPos target=target(command); if(target==null || NativeTerrainBlock.proxy(owner.getServerWorld().getBlockState(target))||owner.isSpectator()) return;
        boolean crouching=owner.isSneaking(); owner.setSneaking(false);
        try {
            ActionResult result=owner.interactionManager.interactBlock(owner,owner.getServerWorld(),owner.getMainHandStack().getItem() instanceof FlintAndSteelItem || owner.getMainHandStack().getItem() instanceof FireChargeItem ? owner.getMainHandStack() : ItemStack.EMPTY,Hand.MAIN_HAND,
                new BlockHitResult(Vec3d.ofCenter(target),Direction.UP,target,false));
            if(owner.currentScreenHandler!=owner.playerScreenHandler) openContainer=target;
            status=result.isAccepted() ? "Interacted with "+Registries.BLOCK.getId(owner.getServerWorld().getBlockState(target).getBlock()) : "No interaction for this block";
        } finally {owner.setSneaking(crouching);}
    }
    private void containerMove(JsonObject command) {
        if(openContainer==null || owner.currentScreenHandler==owner.playerScreenHandler) return;
        ScreenHandler handler=owner.currentScreenHandler;
        int index=integer(command,"slot",-1);
        if(string(command,"direction","").equals("deposit")) {
            index=-1; int inventorySlot=integer(command,"slot",-1);
            for(int i=0;i<handler.slots.size();i++) {Slot s=handler.slots.get(i);if(s.inventory==owner.getInventory() && s.getIndex()==inventorySlot) {index=i;break;}}
        }
        if(index<0 || index>=handler.slots.size()) return;
        handler.onSlotClick(index,0,SlotActionType.QUICK_MOVE,owner);handler.sendContentUpdates();
    }
    private void swing(JsonObject command) {
        int request=integer(command,"request",0);
        if(request>0&&request<=cancelledAttackRequest)return;
        if(request>0&&request==lastAttackRequest&&lastAttackEvent!=null) {if(peer!=null)peer.event(lastAttackEvent);return;}
        if(request>0&&request<lastAttackRequest)return;
        if(!owner.isAlive()||owner.isSpectator()) {rejectAttack(request,"Player cannot attack");return;}
        int now=server.getTicks(); if(now<nextSwingTick) {pendingSwing=command;return;}
        owner.clearActiveItem();
        double direction=command.has("aimX")?command.get("aimX").getAsDouble():1;
        owner.setYaw(direction<0?90:-90);owner.setHeadYaw(owner.getYaw());owner.setPitch(0);
        JsonObject profile=CombatBridge.profile(owner);
        String weapon=profile.get("item").getAsString();
        if(!weapon.equals(lastWeapon) || now-nextSwingTick>30) combo=0;
        lastWeapon=weapon;
        JsonObject attributes=profile.getAsJsonObject("weapon");
        JsonArray attacks=attributes.getAsJsonArray("attacks");
        if(attacks==null || attacks.isEmpty()) {rejectAttack(request,"No attack profile");return;}
        JsonObject attack=attacks.get(combo%attacks.size()).getAsJsonObject(); combo++;
        double duration=1/Math.max(0.2,profile.get("speed").getAsDouble());
        double upswing=attack.has("upswing") ? attack.get("upswing").getAsDouble() : 0.5;
        nextSwingTick=now+(int)Math.ceil(duration*20);
        hitReadyTick=now+(int)Math.round(duration*upswing*20);
        activeAttack=++attackSerial;
        JsonObject event=new JsonObject();event.addProperty("type","attack");event.addProperty("scene",scene);
        event.addProperty("attack",activeAttack);event.addProperty("duration",duration);event.addProperty("upswing",upswing);
        if(command.has("request"))event.add("request",command.get("request"));
        attackMultiplier=attack.has("damage_multiplier") ? attack.get("damage_multiplier").getAsDouble() : 1;
        event.addProperty("damage",profile.get("damage").getAsDouble()*attackMultiplier);
        event.addProperty("range",creative?6:3);
        event.add("move",attack);event.addProperty("combo",combo);event.addProperty("item",weapon);
        event.addProperty("aimX",command.has("aimX") ? command.get("aimX").getAsDouble() : 1);
        event.addProperty("aimY",command.has("aimY") ? command.get("aimY").getAsDouble() : 0);
        lastAttackRequest=request;lastAttackEvent=event.deepCopy();
        Peer current=peer;if(current!=null) current.event(event);
        if(!creative) owner.addExhaustion(0.1f);
    }
    private void rejectAttack(int request,String reason) {
        if(peer==null)return;JsonObject event=new JsonObject();event.addProperty("type","attackRejected");event.addProperty("scene",scene);event.addProperty("request",request);event.addProperty("reason",reason);peer.event(event);
    }
    private void nativeProjectileHit(JsonObject command) {
        int id=integer(command,"entity",-1),request=integer(command,"request",0);String uuid=string(command,"uuid","");
        var cached=projectileImpacts.get(uuid);
        if(request>0&&cached!=null&&request<=integer(cached,"request",0)) {
            if(request==integer(cached,"request",0)&&peer!=null)peer.event(cached.deepCopy());
            return;
        }
        var entity=owner.getServerWorld().getEntityById(id);
        boolean accepted=false,removed=entity==null;boolean grounded=false;
        if(entity instanceof net.minecraft.entity.projectile.ProjectileEntity projectile&&projectile.getOwner()==owner&&projectile.getUuidAsString().equals(uuid)
            &&(!NativeProjectileSteps.required(projectile)||integer(command,"age",-1)==projectile.age)) {
            double x=command.get("x").getAsDouble(),y=command.get("y").getAsDouble();
            double nx=command.has("normalX")?command.get("normalX").getAsDouble():0,ny=command.has("normalY")?command.get("normalY").getAsDouble():1;
            if(Double.isFinite(x)&&Double.isFinite(y)&&Double.isFinite(nx)&&Double.isFinite(ny)&&Math.abs(x)<=400&&y>=-60&&y<=240) {
                Vec3d point=new Vec3d(slot*1024+x,64+y,.5);
                double tolerance=4+projectile.getVelocity().length()*5;
                if(projectile.getPos().squaredDistanceTo(point)<=tolerance*tolerance&&!projectile.isRemoved()&&!projectile.getCommandTags().contains("hollowcraft_native_anchor")&&!(projectile instanceof net.minecraft.entity.projectile.PersistentProjectileEntity arrow&&arrow.isNoClip())) {
                    if(command.has("enemies"))enemies.observe(owner,command.getAsJsonArray("enemies"),slot*1024);
                    var target=enemies.source(integer(command,"target",0));
                    Direction side=Math.abs(nx)>Math.abs(ny)?(nx<0?Direction.WEST:Direction.EAST):(ny<0?Direction.DOWN:Direction.UP);
                    Vec3d stepStart=projectile.getPos();
                    Vec3d position=point;
                    if(projectile instanceof net.minecraft.entity.projectile.thrown.EnderPearlEntity&&command.has("landingX")&&command.has("landingY")) {
                        double lx=command.get("landingX").getAsDouble(),ly=command.get("landingY").getAsDouble();
                        if(Double.isFinite(lx)&&Double.isFinite(ly)&&Math.abs(lx-x)<=3&&Math.abs(ly-y)<=3&&Math.abs(lx)<=400&&ly>=-60&&ly<=240)position=new Vec3d(slot*1024+lx,64+ly,.5);
                    }
                    NativeProjectileSteps.revoke(projectile);projectile.setPosition(position);
                    // These vanilla impacts remove the projectile immediately. Move its client
                    // copy before status/particle packets so the burst uses the actual native hit.
                    // Retained arrows stay under the normal tracker, avoiding a second relative move.
                    if(projectile instanceof net.minecraft.entity.projectile.FireworkRocketEntity firework&&((dev.hollowcraft.mixin.FireworkAccess)firework).hollowcraft$explosive()
                        ||projectile instanceof net.minecraft.entity.projectile.thrown.EnderPearlEntity
                        ||projectile instanceof net.minecraft.entity.projectile.thrown.EggEntity
                        ||projectile instanceof net.minecraft.entity.projectile.thrown.SnowballEntity
                        ||projectile instanceof net.minecraft.entity.projectile.thrown.PotionEntity
                        ||projectile instanceof net.minecraft.entity.projectile.thrown.ExperienceBottleEntity)
                        owner.networkHandler.sendPacket(new net.minecraft.network.packet.s2c.play.EntityPositionS2CPacket(projectile));
                    net.minecraft.util.hit.HitResult collision=target!=null?new net.minecraft.util.hit.EntityHitResult(target,point):new BlockHitResult(point,side,BlockPos.ofFloored(point.add(-nx*.02,-ny*.02,0)),false);
                    ((dev.hollowcraft.mixin.ProjectileAccess)projectile).hollowcraft$collide(collision);
                    accepted=true;removed=projectile.isRemoved();
                    if(target==null&&!removed&&projectile instanceof net.minecraft.entity.projectile.PersistentProjectileEntity arrow) {ItemUseBridge.anchor(arrow,point);grounded=true;}
                    // Piercing may hit several targets in this same permitted step. Keep its original
                    // start so each native hit does not add another full tick of travel distance.
                    if(target!=null&&!removed&&NativeProjectileSteps.required(projectile)&&projectile instanceof net.minecraft.entity.projectile.PersistentProjectileEntity arrow&&arrow.getPierceLevel()>0)projectile.setPosition(stepStart);
                    LOG.info("Native projectile collision entity={} target={} point={} removed={}",id,integer(command,"target",0),point,removed);
                }
            }
        }
        JsonObject event=new JsonObject();event.addProperty("type","nativeImpact");event.addProperty("scene",scene);event.addProperty("entity",id);event.addProperty("uuid",uuid);event.addProperty("request",request);event.addProperty("accepted",accepted);event.addProperty("removed",removed);event.addProperty("grounded",grounded);
        if(request>0&&!uuid.isEmpty())projectileImpacts.put(uuid,event);
        if(peer!=null)peer.event(event);
    }
    private boolean breakRemoteBlock(BlockPos position) {
        try(var effects=RemoteEffects.begin()) {return owner.interactionManager.tryBreakBlock(position);}
    }
    private void mine(JsonObject command) {
        BlockPos target = target(command);
        if (target == null || NativeTerrainBlock.proxy(owner.getServerWorld().getBlockState(target))) { status = "Hollow Knight terrain cannot be mined"; return; }
        BlockState state = owner.getServerWorld().getBlockState(target);
        if (state.isAir()) return;
        float progress = state.calcBlockBreakingDelta(owner, owner.getServerWorld(), target);
        if (!creative && progress <= 0) { status = "This block cannot be mined with this tool"; return; }
        // Client sends elapsed hold time; server checks the vanilla hardness/tool rate.
        if (!creative && integer(command, "heldTicks", 0) * progress < 1) { status = "Hold longer to mine"; return; }
        boolean broken = breakRemoteBlock(target);
        if (broken) {
            var items = owner.getServerWorld().getEntitiesByClass(net.minecraft.entity.ItemEntity.class,
                new Box(target).expand(2), entity -> true);
            for (var entity : items) {
                ItemStack stack = entity.getStack();
                owner.getInventory().insertStack(stack);
                if (stack.isEmpty()) entity.discard();
            }
        }
        status = broken ? "Mined " + Registries.BLOCK.getId(state.getBlock()) : "Minecraft rejected mining";
    }
    private void craft(JsonObject command) {
        JsonArray source = command.has("slots") ? command.getAsJsonArray("slots") : new JsonArray();
        if (source.size() != 9) return;
        ScreenHandler dummy = new ScreenHandler((ScreenHandlerType<?>) null, -1) {
            @Override public ItemStack quickMove(PlayerEntity p, int index) { return ItemStack.EMPTY; }
            @Override public boolean canUse(PlayerEntity p) { return false; }
        };
        CraftingInventory grid = new CraftingInventory(dummy, 3, 3);
        int[] used = new int[36];
        for (int i = 0; i < 9; i++) {
            int index = source.get(i).getAsInt();
            if (index < 0 || index >= 36) continue;
            ItemStack stack = owner.getInventory().getStack(index);
            if (++used[index] > stack.getCount()) { status = "Not enough ingredients"; return; }
            grid.setStack(i, stack.copyWithCount(1));
        }
        boolean table = false;
        for (int x = -3; x <= 3; x++) for (int y = -3; y <= 3; y++)
            if (owner.getServerWorld().getBlockState(pos(centerX + x, centerY + y)).isOf(Blocks.CRAFTING_TABLE)) table = true;
        if (!table && (usedGrid(source, 2) || usedGrid(source, 5) || usedGrid(source, 6) || usedGrid(source, 7) || usedGrid(source, 8))) {
            status = "Place a crafting table nearby for 3x3 recipes"; return;
        }
        Optional<CraftingRecipe> recipe = server.getRecipeManager().getFirstMatch(RecipeType.CRAFTING, grid, owner.getServerWorld());
        if (recipe.isEmpty()) { status = "No matching Minecraft recipe"; return; }
        ItemStack result = recipe.get().craft(grid, owner.getServerWorld().getRegistryManager());
        if (result.isEmpty()) { status = "Recipe has no result"; return; }
        var remainder = recipe.get().getRemainder(grid);
        // Consume only after the recipe has been validated. Overflow becomes vanilla dropped items.
        for (int i = 0; i < 36; i++) if (used[i] > 0) owner.getInventory().getStack(i).decrement(used[i]);
        for (ItemStack stack : remainder) if (!stack.isEmpty()) give(stack);
        String name = Registries.ITEM.getId(result.getItem()).toString();
        give(result); status = "Crafted " + name;
    }
    private boolean usedGrid(JsonArray source, int index) { return source.get(index).getAsInt() >= 0; }
    private void give(ItemStack stack) { owner.getInventory().insertStack(stack); if (!stack.isEmpty()) owner.dropItem(stack, false); }
    private void updateTerrain(JsonObject command) {
        if (!command.has("terrain")) return;
        Set<BlockPos> next = new HashSet<>();
        NativeTerrainBlock.shapes.clear();
        JsonArray list = command.getAsJsonArray("terrain");
        for (int i = 0; i < Math.min(512, list.size()); i++) {
            JsonArray cell = list.get(i).getAsJsonArray();
            if (cell.size() != 2 && cell.size()!=6) continue;
            int x = cell.get(0).getAsInt(), y = cell.get(1).getAsInt();
            if (Math.abs(x-centerX) > 12 || Math.abs(y-centerY) > 12 || y < -60 || y > 240) continue;
            BlockPos p = pos(x,y); next.add(p);
            var shape=cell.size()==6?VoxelShapes.cuboid(cell.get(2).getAsDouble(),cell.get(3).getAsDouble(),0,cell.get(4).getAsDouble(),cell.get(5).getAsDouble(),1):VoxelShapes.fullCube();
            NativeTerrainBlock.shapes.merge(p,shape,VoxelShapes::union);
            if (owner.getServerWorld().getBlockState(p).isAir()||NativeTerrainBlock.proxy(owner.getServerWorld().getBlockState(p))) owner.getServerWorld().setBlockState(p, NativeTerrainBlock.INSTANCE.getDefaultState(), Block.NOTIFY_LISTENERS);
        }
        for (BlockPos old : terrain) if (!next.contains(old) && NativeTerrainBlock.proxy(owner.getServerWorld().getBlockState(old)))
            owner.getServerWorld().setBlockState(old, Blocks.AIR.getDefaultState(), Block.NOTIFY_LISTENERS);
        // A prior process can have saved transient terrain before it disconnected.
        // Reconcile the native sample window on reconnect so those old plane barriers
        // cannot become invisible walls for projectiles or dropped stacks.
        for(int x=centerX-8;x<=centerX+8;x++) for(int y=centerY-6;y<=centerY+6;y++) {
            BlockPos p=pos(x,y);
            if(!next.contains(p)&&NativeTerrainBlock.proxy(owner.getServerWorld().getBlockState(p)))
                owner.getServerWorld().setBlockState(p,Blocks.AIR.getDefaultState(),Block.NOTIFY_LISTENERS);
        }
        terrain.clear(); terrain.addAll(next);
    }
    private void cleanupTerrain() {
        if (owner != null) for (BlockPos p : terrain)
            if (NativeTerrainBlock.proxy(owner.getServerWorld().getBlockState(p))) owner.getServerWorld().setBlockState(p, Blocks.AIR.getDefaultState(), Block.NOTIFY_LISTENERS);
        terrain.clear();
        NativeTerrainBlock.shapes.clear();
    }
    private JsonArray primedSnapshot() {
        var result=new JsonArray();if(owner==null||scene.isEmpty())return result;
        var area=new Box(slot*1024+centerX-32,64+centerY-32,-8,slot*1024+centerX+32,64+centerY+32,8);
        for(var tnt:owner.getServerWorld().getEntitiesByClass(TntEntity.class,area,e->!e.isRemoved())) {
            if(result.size()>=256)break;
            var value=new JsonObject();var next=NativeTntSteps.next(tnt);
            value.addProperty("id",tnt.getId());value.addProperty("uuid",tnt.getUuidAsString());value.addProperty("age",tnt.age);
            value.addProperty("x",tnt.getX()-slot*1024);value.addProperty("y",tnt.getY()-64);value.addProperty("fuse",tnt.getFuse());
            value.addProperty("width",tnt.getWidth());value.addProperty("height",tnt.getHeight());
            value.addProperty("nextX",next.x);value.addProperty("nextY",next.y);value.addProperty("stepRequired",NativeTntSteps.required(tnt));result.add(value);
        }
        return result;
    }
    private void snapshot() {
        if(peer!=null&&peer.gui) {
            var gui=NativeGuiBridge.session();
            if(!owner.isAlive()) {closeGui();owner.closeHandledScreen();openContainer=null;}
            else if(owner.currentScreenHandler!=owner.playerScreenHandler&&(gui==null||gui.syncId()!=owner.currentScreenHandler.syncId))openGui(false,0);
            else if(gui!=null&&gui.syncId()!=owner.currentScreenHandler.syncId)closeGui();
        }
        JsonObject result = new JsonObject();
        result.addProperty("type", "snapshot"); result.addProperty("scene", scene);result.addProperty("campaign",campaigns.id());result.addProperty("visit",sceneVisit);
        result.addProperty("worldTime",owner.getServerWorld().getTime());
        result.addProperty("tick",server.getTicks());result.addProperty("playerSyncId",owner.playerScreenHandler.syncId);
        if(peer!=null&&peer.gui)result.add("gui",NativeGuiBridge.description());
        if(peer!=null&&peer.hud)result.addProperty("hudSession",NativeHudBridge.SESSION);
        result.addProperty("playerX",owner.getX()-slot*1024);result.addProperty("playerY",owner.getY()-64);
        result.add("enemies",enemies.snapshot());
        result.add("primed",primedSnapshot());
        result.add("projectiles",ItemUseBridge.snapshot(owner,slot*1024,centerX,centerY));
        result.addProperty("status", status); result.addProperty("creative", creative);
        result.addProperty("selected", owner.getInventory().selectedSlot);
        JsonArray blocks = new JsonArray();
        for (int x = centerX-12; x <= centerX+12; x++) for (int y = centerY-10; y <= centerY+10; y++) {
            BlockState state = owner.getServerWorld().getBlockState(pos(x,y));
            if (state.isAir() || NativeTerrainBlock.proxy(state)) continue;
            JsonObject block = new JsonObject(); block.addProperty("x",x); block.addProperty("y",y);
            block.addProperty("id",Registries.BLOCK.getId(state.getBlock()).toString());
            boolean open=state.contains(net.minecraft.state.property.Properties.OPEN)&&state.get(net.minecraft.state.property.Properties.OPEN);
            block.addProperty("open",open);block.addProperty("state",state.toString());
            if(state.contains(net.minecraft.state.property.Properties.POWERED)) block.addProperty("powered",state.get(net.minecraft.state.property.Properties.POWERED));
            if(state.contains(net.minecraft.state.property.Properties.LIT)) block.addProperty("lit",state.get(net.minecraft.state.property.Properties.LIT));
            if(state.contains(net.minecraft.state.property.Properties.POWER)) block.addProperty("power",state.get(net.minecraft.state.property.Properties.POWER));
            var shape=state.getCollisionShape(owner.getServerWorld(),pos(x,y));
            block.addProperty("solid",!open&&!shape.isEmpty());JsonArray boxes=new JsonArray();
            if(!open) for(Box box:shape.getBoundingBoxes()) {JsonArray bounds=new JsonArray();bounds.add(box.minX);bounds.add(box.minY);bounds.add(box.maxX);bounds.add(box.maxY);boxes.add(bounds);}
            block.add("collision",boxes);
            var fluid=state.getFluidState();block.addProperty("fluid",!fluid.isEmpty());
            if(!fluid.isEmpty()) block.addProperty("height",fluid.getHeight(owner.getServerWorld(),pos(x,y)));
            block.addProperty("breakTicks",(int)Math.ceil(1.0f/Math.max(0.00001f,state.calcBlockBreakingDelta(owner,owner.getServerWorld(),pos(x,y)))));
            blocks.add(block);
        }
        result.add("blocks",blocks);
        JsonArray drops=new JsonArray();
        for(var dropped:owner.getServerWorld().getEntitiesByClass(net.minecraft.entity.ItemEntity.class,
            new Box(slot*1024+centerX-14,64+centerY-12,-2,slot*1024+centerX+14,64+centerY+12,2),e->true)) {
            JsonObject entry=PlayerBridge.item(dropped.getStack());entry.addProperty("entity",dropped.getId());entry.addProperty("x",dropped.getX()-slot*1024);entry.addProperty("y",dropped.getY()-64);drops.add(entry);
        }
        result.add("drops",drops);
        JsonArray inventory = new JsonArray();
        for (int i = 0; i < 36; i++) {
            ItemStack stack = owner.getInventory().getStack(i);
            inventory.add(PlayerBridge.item(stack));
        }
        result.add("inventory", inventory);
        result.add("stats",PlayerBridge.stats(owner));
        result.add("carried",PlayerBridge.item(owner.currentScreenHandler.getCursorStack()));
        JsonArray insertSlots=new JsonArray();var carried=owner.currentScreenHandler.getCursorStack();
        if(!carried.isEmpty())for(int i=0;i<owner.currentScreenHandler.slots.size();i++) {
            var s=owner.currentScreenHandler.slots.get(i);
            if(s.canInsert(carried)&&ScreenHandler.canInsertItemIntoSlot(s,carried,true))insertSlots.add(i);
        }
        result.add("insertSlots",insertSlots);
        JsonArray playerCraft=new JsonArray();for(int i=0;i<5;i++) {JsonObject entry=PlayerBridge.item(owner.playerScreenHandler.slots.get(i).getStack());entry.addProperty("slot",i);playerCraft.add(entry);}result.add("playerCraft",playerCraft);
        result.add("combat",CombatBridge.profile(owner));
        if(openContainer!=null && owner.currentScreenHandler!=owner.playerScreenHandler) {
            ScreenHandler handler=owner.currentScreenHandler;JsonObject container=new JsonObject();
            container.addProperty("syncId",handler.syncId);
            container.addProperty("id",Registries.BLOCK.getId(owner.getServerWorld().getBlockState(openContainer).getBlock()).toString());
            JsonArray slots=new JsonArray();
            for(int i=0;i<handler.slots.size();i++) {
                Slot s=handler.slots.get(i); if(s.inventory==owner.getInventory()) continue;
                JsonObject entry=PlayerBridge.item(s.getStack());entry.addProperty("slot",i);entry.addProperty("x",s.x);entry.addProperty("y",s.y);slots.add(entry);
            }
            container.add("slots",slots);
            JsonArray inventorySlots=new JsonArray();for(int i=0;i<handler.slots.size();i++) {Slot s=handler.slots.get(i);if(s.inventory==owner.getInventory()) {JsonObject entry=new JsonObject();entry.addProperty("inventory",s.getIndex());entry.addProperty("slot",i);entry.addProperty("x",s.x);entry.addProperty("y",s.y);inventorySlots.add(entry);}}container.add("inventorySlots",inventorySlots);
            if(handler instanceof AbstractFurnaceScreenHandler furnace) {
                container.addProperty("cook",Math.min(1,furnace.getCookProgress()/24.0));
                container.addProperty("fuel",furnace.isBurning()?Math.min(1,(furnace.getFuelProgress()+1)/14.0):0);
            }
            if(handler instanceof net.minecraft.screen.BrewingStandScreenHandler brewing) {container.addProperty("brew",brewing.getBrewTime());container.addProperty("brewFuel",brewing.getFuel());}
            if(handler instanceof net.minecraft.screen.AnvilScreenHandler anvil) container.addProperty("cost",anvil.getLevelCost());
            if(handler instanceof net.minecraft.screen.EnchantmentScreenHandler enchant) {
                JsonArray choices=new JsonArray();for(int i=0;i<3;i++) {JsonObject choice=new JsonObject();choice.addProperty("cost",enchant.enchantmentPower[i]);choice.addProperty("level",enchant.enchantmentLevel[i]);var kind=Registries.ENCHANTMENT.get(enchant.enchantmentId[i]);choice.addProperty("name",kind==null?"":kind.getName(enchant.enchantmentLevel[i]).getString());choices.add(choice);}container.add("enchants",choices);
            }
            ContainerView.append(container,owner,openContainer);
            result.add("container",container);
        }
        Peer current = peer;
        if (current != null) current.enqueue(result.toString());
    }
    private static String string(JsonObject object, String key, String fallback) { return object.has(key) ? object.get(key).getAsString() : fallback; }
    private static int integer(JsonObject object, String key, int fallback) { return object.has(key) ? object.get(key).getAsInt() : fallback; }
    private static int clamp(int value,int min,int max) { return Math.max(min, Math.min(max,value)); }
    private void saveCampaign() {if(landingWater!=null)return;try {if(campaigns!=null)campaigns.save(owner);}catch(IOException error){LOG.error("Cannot save campaign inventory",error);}}

    private static final class Peer {
        final Socket socket;
        final BufferedWriter writer;
        final boolean projectileSteps;
        final boolean audio;
        final boolean gui;
        final boolean hud;
        final boolean entityRender;
        final boolean blockRender;
        final boolean tntSteps;
        final Set<String> audioFiles=ConcurrentHashMap.newKeySet();
        final BlockingQueue<String> output = new ArrayBlockingQueue<>(4);
        final BlockingQueue<String> events = new LinkedBlockingQueue<>();
        Peer(Socket socket, BufferedWriter writer,boolean projectileSteps,boolean audio,boolean gui,boolean hud,boolean entityRender,boolean blockRender,boolean tntSteps) {
            this.projectileSteps=projectileSteps;
            this.audio=audio;
            this.hud=hud;
            this.entityRender=entityRender;
            this.blockRender=blockRender;
            this.tntSteps=tntSteps;
            this.gui=gui;
            this.socket=socket; this.writer=writer;
            Thread sender = new Thread(() -> {
                try { while (!socket.isClosed()) {
                    String event;while((event=events.poll())!=null) send(event);
                    String snapshot=output.poll(10,TimeUnit.MILLISECONDS);if(snapshot!=null) send(snapshot);
                } }
                catch (Exception e) { try { socket.close(); } catch (IOException ignored) { } }
            }, "HollowCraft snapshots");
            sender.setDaemon(true); sender.start();
        }
        void enqueue(String value) { if (!output.offer(value)) { output.poll(); output.offer(value); } }
        void event(JsonObject message) {
            if(message.has("scene")&&!message.has("campaign")&&instance!=null&&instance.campaigns!=null)message.addProperty("campaign",instance.campaigns.id());
            if(message.has("scene")&&!message.has("visit")&&instance!=null)message.addProperty("visit",instance.sceneVisit);
            if(!events.offer(message.toString())) {try {socket.close();}catch(IOException ignored){}}
        }
        synchronized void send(String value) throws IOException { writer.write(value); writer.newLine(); writer.flush(); }
    }
}
