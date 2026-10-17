# Third-party Components and Asset Sources

[简体中文](THIRD_PARTY.md) | [English](THIRD_PARTY.en.md)

Native packages contain only the Hollow Knight-side HollowCraft.dll, scripts, registry facts and documentation. Static resource sources are pinned in [native-dependencies.lock.json](native-dependencies.lock.json). Texture/animation archives use SHA-1 verification; audio objects are checked against asset-index SHA-1 hashes and then addressed locally by SHA-256. Setup can read installed resources or download missing objects from official sources. Minecraft is not required to be installed or running. Extracted game/mod assets are excluded from Git and release ZIPs.

Windows release packages contain only the two compiled HollowCraft mods, installation scripts, optional asset-importer source and documentation. They do not distribute Minecraft, Hollow Knight, modified original game assemblies, extracted textures/audio/animations, user saves or the dependency binaries listed below.

The installer downloads fixed versions from official release sources using checksums in [dependencies.lock.json](dependencies.lock.json). Fabric Maven libraries use their official SHA-1 values; Modrinth dependencies use official SHA-512 values; Mono.Cecil uses a locked SHA-512 calculated from the official NuGet package.

| Component | Pinned version | Official source |
| --- | --- | --- |
| Fabric Loader | 0.16.14 for Minecraft 1.20.1 | [Fabric](https://fabricmc.net/), [source](https://github.com/FabricMC/fabric-loader) |
| Fabric API | 0.92.6+1.20.1 | [Modrinth](https://modrinth.com/mod/fabric-api), [source](https://github.com/FabricMC/fabric) |
| Better Combat | 1.8.5+1.20.1-fabric | [Modrinth](https://modrinth.com/mod/better-combat), [source](https://github.com/ZsoltMolnarrr/BetterCombat/tree/1.20.1); Daedelus, GPL-3.0 |
| Player Animator | 1.0.2-rc1+1.20-fabric | [Modrinth](https://modrinth.com/mod/playeranimator), [source](https://github.com/KosmX/minecraftPlayerAnimator) |
| Cloth Config | 11.1.118+fabric | [Modrinth](https://modrinth.com/mod/cloth-config), [source](https://github.com/shedaniel/cloth-config) |
| Mono.Cecil | 0.11.6 | [official NuGet](https://www.nuget.org/packages/Mono.Cecil/0.11.6), [source and license](https://github.com/jbevain/cecil) |
| ASM | 9.8, matching Fabric Loader | [official project](https://asm.ow2.io/); modules and checksums are listed in the lockfile |

Game assets are extracted locally from the player's own Minecraft 1.20.1 installation and downloaded Better Combat resources, and stay in local runtime directories. Minecraft belongs to Mojang/Microsoft; Hollow Knight belongs to Team Cherry. HollowCraft is unofficial and is not affiliated with or endorsed by these developers.


## Terraria import support

Terraria sprites and sounds belong to Re-Logic and are imported only from the player's local installation; none are included in Git or the release ZIP. The optional importer ships as source. Its MonoGame LZX decoder retains the Stuart Caie/Ali Scissons notices and is distributed under the MS-PL option; the stream wrapper uses MonoGame's MS-PL, with one documented exception-type change. See the [source notices](../tools/TerrariaAssetImporter/SOURCE.md) and [complete license](../tools/TerrariaAssetImporter/LICENSE.txt).
