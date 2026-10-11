package dev.hollowcraft;

import java.nio.file.Files;
import java.util.List;
import java.util.Optional;
import net.fabricmc.api.ClientModInitializer;
import net.fabricmc.fabric.api.client.event.lifecycle.v1.ClientTickEvents;
import net.minecraft.block.Blocks;
import net.minecraft.client.gui.screen.TitleScreen;
import net.minecraft.client.gui.screen.AccessibilityOnboardingScreen;
import org.slf4j.LoggerFactory;
import net.minecraft.registry.RegistryKeys;
import net.minecraft.registry.entry.RegistryEntryList;
import net.minecraft.resource.DataConfiguration;
import net.minecraft.world.Difficulty;
import net.minecraft.world.GameMode;
import net.minecraft.world.GameRules;
import net.minecraft.world.biome.BiomeKeys;
import net.minecraft.world.gen.GeneratorOptions;
import net.minecraft.world.gen.WorldPresets;
import net.minecraft.world.gen.chunk.FlatChunkGenerator;
import net.minecraft.world.gen.chunk.FlatChunkGeneratorConfig;
import net.minecraft.world.gen.chunk.FlatChunkGeneratorLayer;
import net.minecraft.world.level.LevelInfo;

/** Opt-in automatic world startup only in a launcher-created isolated profile. */
public final class HollowCraftClient implements ClientModInitializer {
    private boolean started;
    private Boolean previousPause;
    private String lastScreen = "";
    @Override public void onInitializeClient() {
        ClientTickEvents.END_CLIENT_TICK.register(client -> {
            boolean isolated = Files.exists(client.runDirectory.toPath().resolve(".hollowcraft-profile"));
            if (!isolated) return;
            ItemModelIcons.tick(client);
            PlayerVisualBridge.tick(client);
            String screen = client.currentScreen == null ? "in-game" : client.currentScreen.getClass().getSimpleName();
            if (!screen.equals(lastScreen)) { LoggerFactory.getLogger("HollowCraft").info("Client screen: {}",screen); lastScreen=screen; }
            if (client.getServer() != null) {
                if (previousPause == null) previousPause = client.options.pauseOnLostFocus;
                client.options.pauseOnLostFocus = false;
            } else if (previousPause != null) { client.options.pauseOnLostFocus = previousPause; previousPause = null; }
            if (started || !Boolean.getBoolean("hollowcraft.autoWorld") ||
                !(client.currentScreen instanceof TitleScreen || client.currentScreen instanceof AccessibilityOnboardingScreen)) return;
            started = true;
            client.options.onboardAccessibility = false;
            LoggerFactory.getLogger("HollowCraft").info("Starting isolated void world HollowCraft");
            if (Files.exists(client.runDirectory.toPath().resolve("saves/HollowCraft/level.dat"))) {
                client.createIntegratedServerLoader().start(client.currentScreen,"HollowCraft");
                return;
            }
            GameRules rules = new GameRules();
            rules.get(GameRules.DO_DAYLIGHT_CYCLE).set(false,null);
            rules.get(GameRules.DO_MOB_SPAWNING).set(false,null);
            LevelInfo info = new LevelInfo("HollowCraft",GameMode.SURVIVAL,false,Difficulty.NORMAL,true,rules,DataConfiguration.SAFE_MODE);
            client.createIntegratedServerLoader().createAndStart("HollowCraft",info,new GeneratorOptions(1,false,false), registries -> {
                var config = new FlatChunkGeneratorConfig(Optional.of(RegistryEntryList.of()),
                    registries.get(RegistryKeys.BIOME).getEntry(BiomeKeys.THE_VOID).orElseThrow(),List.of());
                config.getLayers().add(new FlatChunkGeneratorLayer(1,Blocks.AIR));
                config.updateLayerBlocks();
                return registries.get(RegistryKeys.WORLD_PRESET).get(WorldPresets.FLAT).createDimensionsRegistryHolder()
                    .with(registries,new FlatChunkGenerator(config));
            });
        });
        net.fabricmc.fabric.api.client.rendering.v1.WorldRenderEvents.END.register(context ->
            PlayerVisualBridge.render(net.minecraft.client.MinecraftClient.getInstance(),context.tickDelta()));
    }
}
