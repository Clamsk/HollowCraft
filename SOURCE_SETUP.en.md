# Source and Local Builds

[简体中文](SOURCE_SETUP.md) | [English](SOURCE_SETUP.en.md)

HollowCraft is currently a local Minecraft / Hollow Knight bridge prototype. Runtime communication uses localhost TCP. MCP is a development tool and does not process gameplay input or combat.

## Dependencies

- Your own Steam installation of Hollow Knight **1.5.12620**, Unity **6000.0.61f1**. Injection targets this version; other versions require changes.
- Minecraft Java **1.20.1**, Fabric Loader **0.16.14**, Fabric API **0.92.6+1.20.1**.
- Better Combat **1.8.5+1.20.1**, player-animation-lib / Player Animator and Cloth Config from local mod installations.
- Windows PowerShell, .NET SDK, JDK 17 or a compatible newer version, and Gradle **8.10**.
- Mono.Cecil and the Modding API files required by the scripts in the local `dependencies` directory. They are not included in the repository.

Pinned dependency versions, sources and checksums are recorded in [dependencies.lock.json](release/dependencies.lock.json). See [third-party sources](release/THIRD_PARTY.en.md).

## Build entry points

1. To play, use the [Windows release package](https://github.com/Clamsk/HollowCraft/releases/tag/v0.30.10) and [installation guide](release/INSTALL_en.md); a source build is unnecessary. Developers should inspect `scripts/prepare-assets.ps1`, `scripts/prepare-hk-test.ps1` and `scripts/prepare-mc-test.ps1` and configure local game/dependency paths. `hollowcraft.local.json` overrides the default development paths.
2. Run `scripts/build-hollow-knight.ps1` to build the C# module and private injected assembly. `GameManaged` in `hollow-knight/HollowCraft.csproj` points to the local Hollow Knight managed assemblies.
3. Run `scripts/build-minecraft.ps1 -Gradle <path to gradle.bat> -JavaHome <JDK directory>` to build the Fabric mod. The script can also discover tools through PATH, the user's Gradle 8.10 cache and JAVA_HOME. `minecraft/build.gradle` can reuse two dependencies from the local PCL libraries directory.
4. Run both preparation scripts to create private playtest copies and asset caches inside `runtime`, then start with **Start HollowCraft.cmd**.
5. Before a new release, run `scripts/package-release.ps1 -Version 0.30.10` with the intended build version. It checks both mod versions and creates a ZIP and SHA256 file from an explicit allowlist. Only compiled HollowCraft mods, scripts and documentation are included. The installer prepares the prebuilt route with Mono.Cecil on the player's computer, so players do not need the .NET SDK. The packager refuses to overwrite an existing ZIP; use a new version for a new release.

Do not commit `dependencies`, `artifacts`, `runtime`, `hollowcraft.local.json` or other machine-specific data. Hard links and directory junctions in runtime copies may point to original read-only resources; do not edit them. Do not replace personal game saves with playtest saves.

## Validation policy

The `validate` / `inspect` scripts and `*Smoke.cs` files are development tools from older builds. By the player's request, new builds are compiled and delivered directly for playtesting without automatic game launches. Older test records do not constitute acceptance of the current version. Full campaign compatibility remains tracked in [PLAYTHROUGH.en.md](docs/PLAYTHROUGH.en.md).

## Third-party resources

Minecraft and Hollow Knight programs, textures and sounds belong to their respective rights holders. Local caches must be generated from the user's own installations. Better Combat's upstream source and license are linked in the [README](README.en.md#source-and-validation); its mod binary and animations are not redistributed in this source repository.

This repository currently grants no additional open-source license.
