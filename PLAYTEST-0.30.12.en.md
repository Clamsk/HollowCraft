# HollowCraft 0.30.12 playtest notes

[简体中文](试玩说明-0.30.12.md) | [English](PLAYTEST-0.30.12.en.md)

- Correct native viewport calculation and invalid-viewport fallback. Entity rendering initializes its own camera; a same-type, same-NBT vanilla render replica can bridge delayed client tracking, preventing spawned boats/mobs from having only collision bodies.
- Apply the XY collision thickness during initial spawn-egg clearance. Accepted entity placements return success; failures show and log a reason. Eggs remain creative-only.
- Merge allocating enemy discovery searches into one reusable-buffer search at a lower frequency. Existing positions, guards and boat contacts still synchronize at 20 Hz. Cache terrain collider exclusions per sample.
- Upload only the latest complete player/world/UI frame per refresh. Native-pixel HUD output uses 75% less pixel data at equal GUI size. The isolated backend defaults to 30 FPS, view distance 2 and simulation distance 5; Hollow Knight is not capped.
- Increase the bounded request limit so expanded creature terrain samples do not disconnect at the former 64 KiB limit. Retain bedrock rules, native combat, recording launcher and starter equipment.

The private installation and Windows package are updated together. Start through **HollowCraft Launcher.cmd** or the existing desktop entry. Close both games before restarting an updated build.

Both bridges compile and package integrity checks pass. No games were launched; entity appearance, every egg and actual FPS improvements await player testing. See [performance and troubleshooting](docs/PERFORMANCE.en.md) for details, logs and remaining work.
