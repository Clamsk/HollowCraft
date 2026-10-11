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
    public static boolean isRemotePlayer(net.minecraft.entity.LivingEntity entity) {return instance!=null&&instance.owner==entity&&instance.peer!=null&&!instance.scene.isEmpty();}
    public static void playerHurt(net.minecraft.entity.LivingEntity entity,net.minecraft.entity.damage.DamageSource source) {
        if(!isRemotePlayer(entity)) return;
        JsonObject event=new JsonObject();event.addProperty("type","hurt");event.addProperty("scene",instance.scene);
        event.addProperty("ticks",entity.hurtTime);event.addProperty("kind",source.getName());instance.peer.event(event.toString());
        LOG.info("Bridge player hurt kind={} HP={} hurtTicks={}",source.getName(),entity.getHealth(),entity.hurtTime);
    }
    public static volatile boolean remotePaused;
    public static void vanillaTeleport(ServerPlayerEntity player) {
        if(instance==null||instance.owner!=player||instance.peer==null||instance.scene.isEmpty()) return;
        instance.exactX=player.getX()-instance.slot*1024;instance.exactY=player.getY()-64;instance.fallen=0;
        instance.teleportWaiting=instance.server.getTicks()+40;
        JsonObject event=new JsonObject();event.addProperty("type","teleport");event.addProperty("scene",instance.scene);event.addProperty("x",instance.exactX);event.addProperty("y",instance.exactY);instance.peer.event(event.toString());
    }
    private static final Logger LOG = LoggerFactory.getLogger("HollowCraft");
    public static void projectileEvent(net.minecraft.entity.projectile.ProjectileEntity entity,String phase,Vec3d point) {
        var bridge=instance;
        if(bridge==null||bridge.owner==null||bridge.peer==null||bridge.scene.isEmpty()||entity.getWorld().isClient||entity.getWorld().getServer()!=bridge.server)return;
        JsonObject event=new JsonObject();event.addProperty("type","projectile");event.addProperty("scene",bridge.scene);event.addProperty("phase",phase);
        JsonObject value=ItemUseBridge.projectile(entity,bridge.slot*1024);
        if(phase.equals("land"))value.addProperty("grounded",true);
        if(point!=null){value.addProperty("x",point.x-bridge.slot*1024);value.addProperty("y",point.y-64);}
        event.add("value",value);bridge.peer.event(event.toString());
    }
    private static final Gson GSON = new GsonBuilder().setPrettyPrinting().create();
    private static final int PORT = 31674;
    private static final int MAX_LINE = 65536;
    private final BlockingQueue<JsonObject> commands = new ArrayBlockingQueue<>(128);
    private final Map<String, Integer> scenes = new LinkedHashMap<>();
    private final Set<BlockPos> terrain = new HashSet<>();
    private final Map<Integer,net.minecraft.entity.ItemEntity> trackedDrops=new HashMap<>();
    private final Map<Integer,Integer> dropObserved=new HashMap<>();
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
    private boolean hadPose,wasGrounded;
    private float fallen;
    private BlockPos landingWater;
    private BlockState landingWaterBefore;
    private int collectWaterTick;
    private int teleportWaiting;
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
    private JsonObject lastAttackEvent;
    private int lastAttackRequest,cancelledAttackRequest;

    @Override public void onInitialize() {
        instance=this;
        NativeTerrainBlock.register();
        ServerLifecycleEvents.SERVER_STARTED.register(this::start);
        ServerLifecycleEvents.SERVER_STOPPING.register(s -> stop());
        ServerTickEvents.END_SERVER_TICK.register(this::tick);
        ServerEntityEvents.ENTITY_LOAD.register((entity,world)-> {
            if(entity instanceof net.minecraft.entity.projectile.ProjectileEntity projectile)projectileEvent(projectile,"spawn",null);
            if(world.getServer()==server && entity.getCommandTags().contains("hollowcraft_proxy") && !enemies.owns(entity)) entity.discard();
            if(world.getServer()==server&&peer!=null&&!scene.isEmpty()&&entity instanceof net.minecraft.entity.ItemEntity dropped&&Math.abs(dropped.getX()-slot*1024)<=400) {trackedDrops.put(dropped.getId(),dropped);dropped.setNoGravity(true);dropped.setPosition(dropped.getX(),dropped.getY(),.5);}
        });
        ServerEntityEvents.ENTITY_UNLOAD.register((entity,world)-> {
            if(entity instanceof TntEntity tnt && tnt.getFuse()<=0 && server==world.getServer() && peer!=null && !scene.isEmpty()) {
                LOG.info("TNT explosion local {}, {}",tnt.getX()-slot*1024,tnt.getY()-64);
                JsonObject event=new JsonObject();event.addProperty("type","explosion");event.addProperty("scene",scene);
                event.addProperty("x",tnt.getX()-slot*1024);event.addProperty("y",tnt.getY()-64);peer.event(event.toString());
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
                Peer current = new Peer(socket, writer);
                current.send("{\"type\":\"hello\",\"protocol\":1,\"minecraft\":\"1.20.1\"}");
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
        saveCampaign();
        PlayerRenderState.clear();
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
            if (owner != null) {saveCampaign();collectLandingWater();for(var dropped:trackedDrops.values()) {dropped.setNoGravity(true);dropped.setVelocity(Vec3d.ZERO);}cleanupTerrain();enemies.clear(); owner = null; scene = ""; }
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
            JsonObject command;
            for (int budget = 0; budget < 32 && (command = commands.poll()) != null; budget++) execute(command);
            if(server.getTicks()%100==0)saveCampaign();
            if(landingWater!=null&&server.getTicks()>=collectWaterTick) collectLandingWater();
            if(pendingHit!=null && server.getTicks()>=hitReadyTick) {var ready=pendingHit;pendingHit=null;execute(ready);}
            if(pendingSwing!=null && server.getTicks()>=nextSwingTick) {var ready=pendingSwing;pendingSwing=null;swing(ready);}
            if (!scene.isEmpty()) {
                // Local coordinates occupy a one-block-thick plane, far from normal world spawn.
                Vec3d impulse=owner.getVelocity();
                if(owner.hurtTime>0&&impulse.lengthSquared()>.0001&&peer!=null) {JsonObject event=new JsonObject();event.addProperty("type","impulse");event.addProperty("scene",scene);event.addProperty("x",impulse.x);event.addProperty("y",impulse.y);peer.event(event.toString());}
                owner.teleport(owner.getServerWorld(), slot * 1024 + exactX, 64 + exactY, 0.5, owner.getYaw(), owner.getPitch());
                owner.setVelocity(Vec3d.ZERO);
                owner.setOnGround(wasGrounded);
                PlayerRenderState.publish(owner,scene);
                enemies.tick();
                ItemUseBridge.tick(owner,slot*1024,centerX,centerY);
                if(peer!=null) {
                    JsonObject flight=new JsonObject();flight.addProperty("type","projectiles");flight.addProperty("scene",scene);flight.addProperty("tick",server.getTicks());
                    flight.add("projectiles",ItemUseBridge.snapshot(owner,slot*1024,centerX,centerY));peer.event(flight.toString());
                }
                for(var tnt:owner.getServerWorld().getEntitiesByClass(TntEntity.class,new Box(slot*1024+centerX-24,64+centerY-24,-8,slot*1024+centerX+24,64+centerY+24,8),e->true)) {
                    tnt.setPosition(tnt.getX(),tnt.getY(),.5);tnt.setVelocity(tnt.getVelocity().multiply(1,1,0));
                }
                if(miningPos!=null) {
                    if(server.getTicks()-lastMiningTick>5) {miningPos=null;miningProgress=0;}
                    else {
                        BlockState state=owner.getServerWorld().getBlockState(miningPos);
                        if(state.isAir() || NativeTerrainBlock.proxy(state)) {miningPos=null;miningProgress=0;}
                        else {miningProgress=creative ? 1 : state.calcBlockBreakingDelta(owner,owner.getServerWorld(),miningPos)*(server.getTicks()-miningStartedTick+1);
                            if(miningProgress>=1) {owner.interactionManager.tryBreakBlock(miningPos);miningPos=null;miningProgress=0;}
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
                if (++ticks % 2 == 0) snapshot();
            }
        } catch (Exception e) { status = e.getClass().getSimpleName() + ": " + e.getMessage(); LOG.error("Bridge tick", e); }
    }
    private void grantIronStarter() {
        String tag="hollowcraft_starter_iron_v1";
        if(owner.getCommandTags().contains(tag))return;
        boolean fresh=owner.getInventory().isEmpty();
        Item[] items={Items.IRON_SWORD,Items.BOW,Items.COOKED_BEEF,Items.COBBLESTONE,Items.OAK_LOG,
            Items.IRON_AXE,Items.WATER_BUCKET,Items.LAVA_BUCKET,Items.FLINT_AND_STEEL,
            Items.ARROW,Items.ENDER_PEARL,Items.TNT,Items.TORCH};
        int[] amounts={1,1,64,64,64,1,1,1,1,64,64,64,64};
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
        owner.addCommandTag("hollowcraft_starter");
        LOG.info("Iron starter loadout granted; fresh={}",fresh);
    }
    private void execute(JsonObject command) throws IOException {
        String type = string(command, "type", "");
        if (type.equals("scene")) {
            String name = string(command, "scene", "");
            if (name.isBlank() || name.length() > 120) return;
            String campaign=string(command,"campaign","legacy");
            if(owner.isDead()) {owner=server.getPlayerManager().respawnPlayer(owner,false);owner.networkHandler.player=owner;owner.setNoGravity(true);owner.noClip=true;}
            boolean fresh=campaigns.select(owner,campaign,command.has("legacy")&&command.get("legacy").getAsBoolean());
            if(fresh)grantIronStarter();creative=owner.isCreative();campaigns.save(owner);
            introLanding=command.has("intro")&&command.get("intro").getAsBoolean()&&!campaigns.introDone&&name.equals("Tutorial_01");
            owner.closeHandledScreen(); openContainer=null; combo=0; activeAttack=0;
            enemies.clear();miningPos=null;miningProgress=0;
            pendingHit=null;pendingSwing=null;
            nextSwingTick=0;lastAttackEvent=null;lastAttackRequest=cancelledAttackRequest=0;
            for(var dropped:trackedDrops.values()) {dropped.setNoGravity(true);dropped.setVelocity(Vec3d.ZERO);}
            cleanupTerrain(); scene = name;
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
            collectLandingWater();exactX=centerX+0.5;exactY=centerY;hadPose=false;fallen=0;
            status = "Minecraft connected: " + name;
            return;
        }
        if (scene.isEmpty() || !scene.equals(string(command, "scene", ""))) return;
        switch (type) {
            case "pose" -> {
                if(server.getTicks()<teleportWaiting) break;
                centerX = clamp(integer(command, "x", 0), -400, 400); centerY = clamp(integer(command, "y", 0), -60, 240);
                exactX=command.has("exactX") ? command.get("exactX").getAsDouble() : centerX+0.5;
                exactY=command.has("exactY") ? command.get("exactY").getAsDouble() : centerY;
                reachY=exactY+(command.has("gliding")&&command.get("gliding").getAsBoolean()?.4:command.has("crouching")&&command.get("crouching").getAsBoolean()?1.27:1.62);
                boolean grounded=command.has("grounded") && command.get("grounded").getAsBoolean();
                boolean gliding=command.has("gliding")&&command.get("gliding").getAsBoolean();
                if(gliding && !grounded && owner.getEquippedStack(net.minecraft.entity.EquipmentSlot.CHEST).isOf(Items.ELYTRA)) owner.startFallFlying();else if(owner.isFallFlying()) owner.stopFallFlying();
                if(owner.isTouchingWater()||owner.isClimbing()||gliding) fallen=0;
                else if(hadPose&&!wasGrounded) {
                    fallen+=Math.max(0,(float)(lastPoseY-exactY));
                    if(grounded) {
                        boolean waterSave=owner.interactionManager.getGameMode()==GameMode.SURVIVAL&&(fallen>=11.999f||introLanding)&&placeLandingWater();
                        if(!creative&&!owner.isSpectator()&&!waterSave)owner.handleFallDamage(fallen,1,owner.getDamageSources().fall());
                        if(creative&&fallen>12)landingEvent("hero",fallen);
                        if(introLanding) {introLanding=false;campaigns.introDone=true;campaigns.save(owner);}
                        fallen=0;owner.fallDistance=0;
                    }
                }
                if(hadPose && !creative) {
                    if(command.has("sprinting") && command.get("sprinting").getAsBoolean()) owner.addExhaustion((float)Math.min(2,Math.abs(exactX-lastPoseX))*0.1f);
                    if(wasGrounded && !grounded && exactY>lastPoseY+0.02) owner.addExhaustion(0.05f);
                }
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
            case "teleportAck" -> {teleportWaiting=0;exactX=command.get("x").getAsDouble();exactY=command.get("y").getAsDouble();fallen=0;}
            case "checkpoint" -> {campaigns.save(owner);server.save(false,true,true);status="World and player data saved";}
            case "nativeDeath" -> enemies.confirmDeath(integer(command,"id",0),integer(command,"generation",-1));
            case "rest" -> {if(owner.isAlive()) {owner.setHealth(owner.getMaxHealth());owner.getHungerManager().setFoodLevel(20);owner.getHungerManager().setSaturationLevel(5);campaigns.save(owner);server.save(false,true,true);status="Rested and saved at campaign checkpoint";}}
            case "containerButton" -> {if(openContainer!=null) {int button=integer(command,"button",-1);if(button>=0&&button<1000) {owner.currentScreenHandler.onButtonClick(owner,button);owner.currentScreenHandler.sendContentUpdates();}}}
            case "anvilName" -> {if(openContainer!=null&&owner.currentScreenHandler instanceof net.minecraft.screen.AnvilScreenHandler anvil) anvil.setNewItemName(string(command,"name","").substring(0,Math.min(50,string(command,"name","").length())));}
            case "select" -> {owner.getInventory().selectedSlot = clamp(integer(command, "slot", 0), 0, 8);owner.networkHandler.sendPacket(new net.minecraft.network.packet.s2c.play.UpdateSelectedSlotS2CPacket(owner.getInventory().selectedSlot));}
            case "mode" -> {
                GameMode mode=GameMode.byName(string(command,"mode","survival"),GameMode.SURVIVAL);
                owner.changeGameMode(mode);creative=mode==GameMode.CREATIVE;
                miningPos=null;miningProgress=0;owner.stopUsingItem();pendingSwing=null;pendingHit=null;
                status = "Game mode: "+mode.getName();
            }
            case "place" -> {
                boolean accepted=place(command);
                if(peer!=null) {JsonObject event=new JsonObject();event.addProperty("type","placeResult");event.addProperty("scene",scene);event.addProperty("x",integer(command,"x",0));event.addProperty("y",integer(command,"y",0));event.addProperty("accepted",accepted);peer.event(event.toString());}
                snapshot();
            }
            case "mine" -> mine(command);
            case "mining" -> {
                if(command.has("active") && !command.get("active").getAsBoolean()) {miningPos=null;miningProgress=0;break;}
                BlockPos target=target(command);if(target==null || NativeTerrainBlock.proxy(owner.getServerWorld().getBlockState(target))||!owner.getAbilities().allowModifyWorld||owner.isSpectator()) break;
                if(!target.equals(miningPos)) {miningPos=target;miningProgress=0;miningStartedTick=server.getTicks();}
                lastMiningTick=server.getTicks();
            }
            case "craft" -> craft(command);
            case "use" -> use(command);
            case "containerMove" -> containerMove(command);
            case "close" -> { owner.closeHandledScreen(); openContainer=null; }
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
            case "nativeProjectileHit" -> {nativeProjectileHit(command);snapshot();}
            case "offhand" -> PlayerBridge.offhand(owner);
            case "drop" -> owner.dropSelectedItem(command.has("all") && command.get("all").getAsBoolean());
            case "slotClick" -> PlayerBridge.click(owner,command);
            case "inventoryOpen" -> {owner.clearActiveItem();pendingSwing=null;owner.closeHandledScreen();openContainer=null;}
            case "respawn" -> {
                if(owner.isDead()) {owner=server.getPlayerManager().respawnPlayer(owner,false);owner.networkHandler.player=owner;owner.setNoGravity(true);owner.noClip=true;owner.addCommandTag("hollowcraft_starter");hadPose=false;}
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
                peer.event(catalog.build(owner,scene).toString());snapshot();
            }}
            case "creativeItem" -> {if(creative) {
                if(command.has("catalogIndex")) {
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
            case "creativeDiscard" -> {if(creative) {owner.playerScreenHandler.setCursorStack(ItemStack.EMPTY);snapshot();}}
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
    private boolean placeLandingWater() {
        collectLandingWater();
        BlockPos cell=pos((int)Math.floor(exactX),(int)Math.floor(exactY+.08));
        var world=owner.getServerWorld();var before=world.getBlockState(cell);
        if(!before.isReplaceable()&&!NativeTerrainBlock.proxy(before))return false;
        // Native terrain remains physically present in Unity. Replacing its MC proxy
        // for four ticks lets water wet feet at fractional floor heights, then restores it.
        if(!world.setBlockState(cell,Blocks.WATER.getDefaultState(),Block.NOTIFY_LISTENERS))return false;
        landingWater=cell;landingWaterBefore=before;collectWaterTick=server.getTicks()+4;
        landingEvent("water",fallen);snapshot();LOG.info("Automatic landing water distance={} cell={}",fallen,cell);return true;
    }
    private void collectLandingWater() {
        if(landingWater==null||owner==null)return;
        var world=owner.getServerWorld();
        if(world.getBlockState(landingWater).isOf(Blocks.WATER))world.setBlockState(landingWater,landingWaterBefore,Block.NOTIFY_LISTENERS);
        landingWater=null;landingWaterBefore=null;
    }
    private void landingEvent(String kind,float distance) {
        if(peer==null)return;JsonObject event=new JsonObject();event.addProperty("type","landing");event.addProperty("scene",scene);event.addProperty("kind",kind);event.addProperty("intro",introLanding);event.addProperty("distance",distance);event.addProperty("x",exactX);event.addProperty("y",exactY);peer.event(event.toString());
    }
    private BlockPos target(JsonObject command) {
        int x = integer(command, "x", 0), y = integer(command, "y", 0);
        double dx=exactX-Math.max(x,Math.min(x+1,exactX)),dy=reachY-Math.max(y,Math.min(y+1,reachY));
        double reach=creative?5:4.5;
        if (dx*dx+dy*dy>reach*reach+.001 || y < -60 || y > 240 || x < -400 || x > 400) return null;
        return pos(x, y);
    }
    private boolean place(JsonObject command) {
        if(command.has("playerX")&&command.has("playerY")) {
            double x=command.get("playerX").getAsDouble(),y=command.get("playerY").getAsDouble();
            if(!Double.isFinite(x)||!Double.isFinite(y)||Math.abs(x)>400||y<-60||y>240)return false;
            exactX=x;exactY=y;reachY=y+(owner.isSneaking()?1.27:1.62);
        }
        BlockPos target = target(command);
        ServerWorld world = owner.getServerWorld();
        if (target == null) { status = "Target outside bridge reach or bounds"; return false; }
        if(owner.isSpectator()||!owner.getAbilities().allowModifyWorld) {status="This game mode cannot place blocks";return false;}
        var nativeShape=NativeTerrainBlock.shapes.get(target);
        if (terrain.contains(target)&&(nativeShape==null||nativeShape.getMin(Direction.Axis.Y)>.001||nativeShape.getMax(Direction.Axis.Y)>=.999)) { status = "Hollow Knight terrain occupies target"; return false; }
        if (!world.getBlockState(target).isAir()&&!world.getBlockState(target).isOf(NativeTerrainBlock.INSTANCE)) { status = "Target already contains a Minecraft block"; return false; }
        if (!(owner.getMainHandStack().getItem() instanceof BlockItem)) { status = "Select a block in the hotbar"; return false; }
        boolean floorEdge=world.getBlockState(target).isOf(NativeTerrainBlock.INSTANCE);
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
            try { result = owner.interactionManager.interactBlock(owner, world, owner.getMainHandStack(), Hand.MAIN_HAND,
                new BlockHitResult(hit, face, support, false));
            } finally {owner.setPosition(original);owner.setYaw(yaw);owner.setSneaking(crouching);}
            status = result.isAccepted() ? "Placed " + Registries.BLOCK.getId(world.getBlockState(target).getBlock()) : "Minecraft rejected placement";
            if(!result.isAccepted()) LOG.info("Placement rejected target={} below={} above={} face={} support={}",target,world.getBlockState(target.down()),world.getBlockState(target.up()),face,world.getBlockState(support));
            return result.isAccepted();
        } finally { if (temporarySupport) world.setBlockState(support, Blocks.AIR.getDefaultState(), Block.NOTIFY_LISTENERS);if(floorEdge&&world.getBlockState(target).isAir())world.setBlockState(target,NativeTerrainBlock.INSTANCE.getDefaultState(),Block.NOTIFY_LISTENERS); }
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
        if(request>0&&request==lastAttackRequest&&lastAttackEvent!=null) {if(peer!=null)peer.event(lastAttackEvent.toString());return;}
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
        Peer current=peer;if(current!=null) current.event(event.toString());
        if(!creative) owner.addExhaustion(0.1f);
    }
    private void rejectAttack(int request,String reason) {
        if(peer==null)return;JsonObject event=new JsonObject();event.addProperty("type","attackRejected");event.addProperty("scene",scene);event.addProperty("request",request);event.addProperty("reason",reason);peer.event(event.toString());
    }
    private void nativeProjectileHit(JsonObject command) {
        int id=integer(command,"entity",-1);var entity=owner.getServerWorld().getEntityById(id);
        boolean accepted=false,removed=entity==null;boolean grounded=false;
        if(entity instanceof net.minecraft.entity.projectile.ProjectileEntity projectile&&projectile.getOwner()==owner&&projectile.getUuidAsString().equals(string(command,"uuid",""))) {
            double x=command.get("x").getAsDouble(),y=command.get("y").getAsDouble();
            if(Double.isFinite(x)&&Double.isFinite(y)&&Math.abs(x)<=400&&y>=-60&&y<=240) {
                Vec3d point=new Vec3d(slot*1024+x,64+y,.5);
                double tolerance=4+projectile.getVelocity().length()*5;
                if(projectile.getPos().squaredDistanceTo(point)<=tolerance*tolerance&&!projectile.isRemoved()&&!projectile.getCommandTags().contains("hollowcraft_native_anchor")) {
                    if(command.has("enemies"))enemies.observe(owner,command.getAsJsonArray("enemies"),slot*1024);
                    var target=enemies.source(integer(command,"target",0));
                    double nx=command.has("normalX")?command.get("normalX").getAsDouble():0,ny=command.has("normalY")?command.get("normalY").getAsDouble():1;
                    if(!Double.isFinite(nx)||!Double.isFinite(ny))return;
                    Direction side=Math.abs(nx)>Math.abs(ny)?(nx<0?Direction.WEST:Direction.EAST):(ny<0?Direction.DOWN:Direction.UP);
                    Vec3d position=point;
                    if(projectile instanceof net.minecraft.entity.projectile.thrown.EnderPearlEntity&&command.has("landingX")&&command.has("landingY")) {
                        double lx=command.get("landingX").getAsDouble(),ly=command.get("landingY").getAsDouble();
                        if(Double.isFinite(lx)&&Double.isFinite(ly)&&Math.abs(lx-x)<=3&&Math.abs(ly-y)<=3&&Math.abs(lx)<=400&&ly>=-60&&ly<=240)position=new Vec3d(slot*1024+lx,64+ly,.5);
                    }
                    projectile.setPosition(position);
                    net.minecraft.util.hit.HitResult collision=target!=null?new net.minecraft.util.hit.EntityHitResult(target,point):new BlockHitResult(point,side,BlockPos.ofFloored(point.add(-nx*.02,-ny*.02,0)),false);
                    ((dev.hollowcraft.mixin.ProjectileAccess)projectile).hollowcraft$collide(collision);
                    accepted=true;removed=projectile.isRemoved();
                    if(target==null&&!removed&&projectile instanceof net.minecraft.entity.projectile.PersistentProjectileEntity arrow) {ItemUseBridge.anchor(arrow,point);grounded=true;}
                    LOG.info("Native projectile collision entity={} target={} point={} removed={}",id,integer(command,"target",0),point,removed);
                }
            }
        }
        if(peer!=null) {
            JsonObject event=new JsonObject();event.addProperty("type","nativeImpact");event.addProperty("scene",scene);event.addProperty("entity",id);event.addProperty("accepted",accepted);event.addProperty("removed",removed);event.addProperty("grounded",grounded);peer.event(event.toString());
        }
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
        boolean broken = owner.interactionManager.tryBreakBlock(target);
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
    private void snapshot() {
        JsonObject result = new JsonObject();
        result.addProperty("type", "snapshot"); result.addProperty("scene", scene);result.addProperty("campaign",campaigns.id());
        result.addProperty("worldTime",owner.getServerWorld().getTime());
        result.addProperty("tick",server.getTicks());result.addProperty("playerSyncId",owner.playerScreenHandler.syncId);
        result.addProperty("playerX",owner.getX()-slot*1024);result.addProperty("playerY",owner.getY()-64);
        result.add("enemies",enemies.snapshot());
        JsonArray primed=new JsonArray();
        for(var tnt:owner.getServerWorld().getEntitiesByClass(TntEntity.class,new Box(slot*1024+centerX-20,64+centerY-20,-2,slot*1024+centerX+20,64+centerY+20,2),e->true)) {
            JsonObject value=new JsonObject();value.addProperty("id",tnt.getId());value.addProperty("x",tnt.getX()-slot*1024);value.addProperty("y",tnt.getY()-64);value.addProperty("fuse",tnt.getFuse());primed.add(value);
        }
        result.add("primed",primed);
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
            result.add("container",container);
        }
        Peer current = peer;
        if (current != null) current.enqueue(result.toString());
    }
    private static String string(JsonObject object, String key, String fallback) { return object.has(key) ? object.get(key).getAsString() : fallback; }
    private static int integer(JsonObject object, String key, int fallback) { return object.has(key) ? object.get(key).getAsInt() : fallback; }
    private static int clamp(int value,int min,int max) { return Math.max(min, Math.min(max,value)); }
    private void saveCampaign() {try {if(campaigns!=null)campaigns.save(owner);}catch(IOException error){LOG.error("Cannot save campaign inventory",error);}}

    private static final class Peer {
        final Socket socket;
        final BufferedWriter writer;
        final BlockingQueue<String> output = new ArrayBlockingQueue<>(4);
        final BlockingQueue<String> events = new LinkedBlockingQueue<>();
        Peer(Socket socket, BufferedWriter writer) {
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
        void event(String value) {if(!events.offer(value)) {try {socket.close();}catch(IOException ignored){}}}
        synchronized void send(String value) throws IOException { writer.write(value); writer.newLine(); writer.flush(); }
    }
}
