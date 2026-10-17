# HollowCraft Native 0.40.0-alpha.2

[简体中文](NATIVE_MIGRATION.md) | English

HollowCraft is transitioning to a **Hollow Knight-only mod**. The default backend now runs C# rules inside the Hollow Knight process. Native mode does not launch Minecraft or Java, open a bridge socket, or receive live Minecraft-rendered frames. The v0.30.18 bridge remains available as a comparison and fallback.

**This is an incomplete migration preview, not feature parity or a verified full playthrough.** Builds and pure rule checks passed; no game or automated gameplay was launched for this version.

## Install and play

Requires Windows x64 and Steam Hollow Knight **1.5.12620**. Minecraft, PCL, Java, Fabric and Better Combat are not runtime requirements. Release users do not need the .NET SDK. First installation downloads SHA512-verified Mono.Cecil and checksum-verified static texture/animation resources (the texture source archive is about 23 MB). No MC or Java mod code is loaded or executed. Use -SkipVisualResources for offline installation with authored placeholders.

1. Extract the Native package completely outside the original game directory.
2. Run **Install HollowCraft Native.cmd**.
3. Run **Start HollowCraft Native.cmd** and choose adventure or Boss recording.
4. If automatic detection fails, run:

```powershell
.\scripts\install-standalone.ps1 -GameRoot 'Your Steam Hollow Knight folder'
```

The installer patches a private copy at `runtime/Hollow Knight Native`; it does not replace the original Steam assembly. Adventure saves live in `runtime/hk-native-saves`; recording sessions use separate folders in `runtime/hk-native-boss-saves`. Back up the complete save folder after closing the game. Existing Minecraft worlds and NBT inventories are not automatically converted.

## Migration status

Implemented means a local implementation exists, not exact vanilla fidelity or gameplay acceptance.

| System | Status and limits |
|---|---|
| Single-process backend and storage | Implemented; old MC world import pending |
| Hollow Knight scenes, story and Boss phases | Existing native adapters retained; complete playthrough unverified |
| Controls, cursor combat, tower building, creative flight, F3+F4 | Existing native controllers retained; feel needs player review |
| Inventory, offhand, starter equipment, durability and modes | Implemented; complete catalog, NBT and enchantments pending |
| Health, armor, hunger, regeneration, absorption, pearl damage | Implemented; full damage/enchantment parity pending |
| Food, golden apples, shields, totems | Local rules and totem presentation; sound parity pending |
| Crafting, table, furnace, chest, inventory gestures | Basic implementation; full recipes, brewing, enchanting and anvil pending |
| Blocks, mining, bedrock and TNT | Implemented; special collision shapes and exact explosion algorithm pending |
| Arrows, pearls and tridents | Implemented; crossbow uses bow-style charging, loyalty returns directly to inventory |
| Fluids, landing water, swimming and drowning | Simplified 2D implementation; not the full vanilla fluid algorithm |
| Redstone | Power, wire, lamp, doors and TNT; pistons, observers and advanced timing pending |
| Elytra, rockets, Zenith and Stardust Wings | Local implementations; visual and audio parity pending |
| Boats and mobs | Boats and 9 basic species; not complete vanilla models, AI or loot tables |
| Creative menu | Local registered items only |
| Player and equipment rendering | Unity joints and hand attachments, combined posed mesh, local GPU render target; not Minecraft's PlayerEntityRenderer |
| Textures, UI and sound | Static vanilla textures, authored fallbacks and optional local model import; visual/audio parity is incomplete |

See [native-migration.json](../release/native-migration.json) for the machine-readable checklist. Unimplemented entries are not counted as completed features.

## Optional local resources

Existing bridge users can reuse locally extracted textures, static model meshes and attack animation data without running Minecraft:

```powershell
.\scripts\import-native-assets.ps1 -AssetsRoot '.\artifacts\HollowCraft\HollowCraftAssets'
```

Resources remain local under `artifacts/StandaloneAssets` and are excluded from releases. The importer excludes live player, creature, HUD and GUI frame caches. The mod starts without this optional model import using local generated models. Authored texture placeholders are used if static resource preparation fails.

## Source builds

```powershell
.\scripts\install-standalone.ps1 -GameRoot 'Your Steam Hollow Knight folder' -Build
dotnet run --project tools/NativeRulesCheck/NativeRulesCheck.csproj -c Release
.\scripts\package-standalone.ps1
```

The standalone rule check tool uses .NET 9 SDK and launches no games. The actual mod targets the game's .NET Framework/Mono runtime; players do not need .NET 9.

Checks cover save separation, corrupt-save protection, item conservation, idempotent attacks and placement, iron-sword damage, shields, totems, furnace progress, landing water and TNT room persistence. Rendering, collision feel and full Boss/story progression still require player testing.

Remaining work proceeds through rendering fidelity, combat/playthrough fixes, complete items/recipes/workstations, entity models and AI, then sound, particles, redstone and fluid details. Full parity will not be claimed until migration and player acceptance are complete.
