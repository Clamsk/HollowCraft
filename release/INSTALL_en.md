# HollowCraft 0.30.13 — Installation and Configuration

[简体中文](INSTALL_zh.md) | [English](INSTALL_en.md)

Version 0.30.13 adds vanilla boat models, native actor boat captures, spawn-egg mobs, basic AI and XY terrain adaptation. Starter backpacks gain an oak boat once; eggs remain creative-only. Stop both games before updating. Individual creature behaviors await gameplay acceptance.

This is a **Windows 64-bit playtest package**. It brings items, numerical rules and a 3D player from your local Minecraft installation into Hollow Knight. Both games must run during play. Full survival campaign support is still being adapted; the current build was compiled and handed to players without gameplay acceptance testing.

## Requirements

- Your own Steam copy of **Hollow Knight 1.5.12620**. Keep Steam signed in. The installer refuses to patch other versions.
- Your own **Minecraft Java 1.20.1** installation. Launch vanilla 1.20.1 once through PCL or another launcher so its game files, assets and libraries are downloaded, then close it.
- **64-bit Java 17 or 21**. An existing launcher-provided Java installation can be used. If needed, Java 21 is available from [Microsoft OpenJDK](https://learn.microsoft.com/java/openjdk/download). The installer searches PATH, JAVA_HOME and common installation directories; if none is found, provide the full path to `java.exe`.
- Internet access to the official Fabric, Modrinth and NuGet download sources during initial installation. This package does not include original games, accounts or dependency binaries.

## Install and start

1. Download **HollowCraft-v0.30.13-windows.zip** from [GitHub Releases](https://github.com/Clamsk/HollowCraft/releases). The automatically generated **Source code** archives require a local build.
2. Extract the entire ZIP into a writable directory such as `D:\Games\HollowCraft`. Do not run files inside the ZIP or install into the original Steam or `.minecraft` directory. Built-in Windows PowerShell is sufficient; Codex, Git and development tools are not needed.
3. Double-click **Install HollowCraft.cmd**. It searches for Steam's game folder and `.minecraft`; enter the paths if discovery fails. The Minecraft root must contain `versions\1.20.1\1.20.1.jar`, rather than merely the launcher executable.
4. Wait until downloads, checksums and preparation finish and the installer reports `installed`. Installation prepares files without launching games.
5. Keep Steam signed in and double-click **HollowCraft Launcher.cmd** (**Start HollowCraft.cmd** opens the same launcher), then press Play on the normal-play tab. Wait for Minecraft to enter its separate world and keep its window running. Then create or load a playtest save in the Hollow Knight window.

For recording, open the **Boss Recording** tab, search and select a boss, tier and resolution, then press Start. The launcher creates a separate recording save and each fight restores the survival starter kit. **F5** retries, **F6** returns to the Hall of Gods and **F8** hides hints. See the [recording guide](../docs/BOSS_RECORDING.en.md).

The bridge uses a separate local world. Online account login is not part of bridge communication. No passwords or access tokens are included, and original launcher configuration is not modified.

## Specify paths

If automatic discovery selects the wrong paths, open PowerShell in the extracted `HollowCraft` folder and run the following, replacing each example path with your own:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\install-hollowcraft.ps1 -GameRoot "D:\SteamLibrary\steamapps\common\Hollow Knight" -MinecraftRoot "D:\Games\MC\.minecraft" -JavaPath "C:\Program Files\Microsoft\jdk-21\bin\java.exe"
```

The installer creates `hollowcraft.local.json`; an example is included as `release/hollowcraft.example.json`. Configuration stays on your computer and is not uploaded. Existing runtime copies must stay associated with the same original game directory. If changing that directory, extract and install into a new folder.

## Stop and upgrade

Save Hollow Knight progress at a bench, then run **Stop HollowCraft.cmd**. It closes only the two processes recorded by this installation and requests Minecraft save acknowledgement. If saving fails, Minecraft stays open; close that window normally. Hollow Knight story progress still depends on bench saves.

Before upgrading, stop both games and back up:

- `runtime\hk-test-saves`: bridged Hollow Knight saves and story progress.
- `runtime\minecraft\saves`: the separate Minecraft world and item progress for each Hollow Knight campaign.
- `runtime\hk-boss-saves`: separate recording saves, unused by normal play.

Copy the contents of the new ZIP's `HollowCraft` folder over the existing installation, then run Install and Start again. The installer preserves runtime saves and local path configuration; it does not clear the inventory.

Runtime resources use directory junctions, hard links or copies across drives. Do not edit native resources inside `runtime\Hollow Knight`. Close both games before moving the package. Back up both save directories before deleting an installation.

## Troubleshooting

| Problem | Action |
| --- | --- |
| Unsupported Hollow Knight version | Only Steam 1.5.12620 is currently targeted; do not force the patch onto another version |
| Missing Minecraft libraries, natives or assets | Launch vanilla 1.20.1 once through the original launcher, close it and rerun Install |
| Download or checksum failure | Restore network access and rerun Install; verified dependencies are reused, while damaged downloads are rejected |
| Old build still running / port 31674 occupied | Run Stop, close any older windows normally, then Start; do not run two installations at once |
| Path-encoding errors | Java arguments use the selected Java's local encoding; move to a simple ASCII path if it cannot represent characters in your path |
| Installation fails | Read the specific error, correct it and rerun Install; use the Windows release ZIP, which contains compiled files and a package manifest |

For bug reports, include the version, reproduction steps and `runtime\hollowcraft-hk.log` / `runtime\minecraft\logs\latest.log`. Do not upload original games, complete saves or account data.

## Playtest notes

See `README.en.md` for the full controls. With cobblestone selected, try holding **B** for assisted pillar building, then test normal Space + right-click placement beneath your feet. A first foundation on native ground can partially overlap a fractional-height floor; native terrain is not removed. Results need player confirmation.

The 0.30.3 opening camera and automatic landing water are best viewed in a new playtest save. Iron ingots are granted to old campaigns once. Entering creative grants the special Loyalty III trident. The tutorial gate needs native destruction hits before its transition can proceed.

Version 0.30.4 changes ender pearl base teleport damage to 3 points and strengthens pose synchronization around teleportation. Existing campaigns receive two extra stacks of cobblestone and one stack of regular golden apples once; new campaigns have three cobblestone stacks in total.

Version 0.30.5 restores native gravity before scene transitions, waits for native entrance actions before resuming Minecraft controls and rejects old scene teleport messages. Low entrances keep a passable collision height until there is standing room. If placed Minecraft blocks overlap an entrance, the bridge searches nearby for clear space. Stop before upgrading so both ends use matching versions.
