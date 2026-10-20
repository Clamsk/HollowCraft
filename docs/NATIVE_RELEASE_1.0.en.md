# HollowCraft Native 1.0.0 · First regular release

English | [简体中文](NATIVE_RELEASE_1.0.md)

Released October 20, 2026. Version 1.0.0 promotes 0.40.0-alpha.12 with the same gameplay, balance, save schema and features. This release updates version labels, launcher documentation, packaging and GitHub release status without adding gameplay changes.

## Installation and updates

Requires Windows x64 and Steam Hollow Knight 1.5.12620. Minecraft, PCL, Java and Better Combat are unnecessary at runtime. Release users do not need the .NET SDK. Initial installation downloads checksum-verified dependencies/static resources; original game files, extracted assets and personal saves are excluded from the package.

1. Extract `HollowCraft-Native-Windows-1.0.0.zip` outside the Steam game directory.
2. Run `Install HollowCraft Native.cmd`.
3. Run `HollowCraft Launcher.cmd`, choose an adventure or Boss recording, and configure the survival loadout/death drops as desired.

To update alpha.12, close the game, overwrite package files in the existing HollowCraft workspace and rerun the installer. Preserve `runtime`, `hollowcraft.native.local.json` and `hollowcraft.native.settings.json`. A new adventure save is unnecessary. Retry individual encounters already stuck in the older version.

## Included features

- Minecraft-style 3D player/equipment/attack animation, survival values, inventory, workstations, building and jump placement.
- Survival/creative modes, creative item selection, launcher loadouts and Boss recording entry points.
- Weapons, arrows, potions, fluids, shields, totems, boats and the currently supported Minecraft creature behaviors.
- Type `Clamsk` for Zenith and Stardust Wings. A complete Zenith volley deals at most 200 native HP per target while respecting native phase boundaries.
- Alpha.12 fixes for armored boss head transitions, lava damage without hurt pull, replacing fluids with blocks, and potion splash particles/sounds.

## Known limitations and validation

Complete Minecraft/legacy-bridge feature parity remains unfinished. Complex items, creature behaviors, fluid details and advanced redstone continue to evolve; see the [feature checklist](NATIVE_MIGRATION.en.md). All Boss/story combinations and a full playthrough are not comprehensively verified. Regular release status does not imply those tasks are complete.

The baseline passed 630 offline rule/protocol checks and retains previous launcher validation records. This version is rebuilt with functional-code and package comparisons; no game was launched, closed or played automatically. Steam game files remain unchanged. Alpha.12 and the v0.30.18 bridge remain available for rollback.
