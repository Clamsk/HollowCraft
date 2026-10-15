# HollowCraft 0.30.11 — Playtest Notes

[简体中文](试玩说明-0.30.11.md) | [English](PLAYTEST-0.30.11.en.md)

- Place actual vanilla-rendered boats, bamboo rafts and chest variants: two seats for regular boats, one for chest variants. Add one oak boat to starter backpacks and grant it to old campaigns once.
- Native enemies and recognized friendly/small creatures automatically board touching boats. Constrain motion while preserving native damage, attacks and death. Handle fast crossings, duplicate hurt parts and captures outside view. Destruction releases movement; point directly at boats to break them, avoiding accidental destruction while attacking passengers.
- Spawn real Minecraft mobs with original 3D models, animation, numerical rules and basic AI. Add melee targeting, vanilla entity right-click interactions, XY collision/path thickness, surrounding native terrain and out-of-region simulation suspension. Spawn eggs are offered through creative only, never added to survival starter inventories.
- Right-click boats to ride and Shift to dismount; players use vanilla riding model poses. Paddling and additional mount-driving controls are not implemented; complex 3D/dimension/structure behavior still needs adaptation.
- Vanilla entities/passengers save in the private MC world. Native scene reloads rebuild actors, so previous native capture identities are not guaranteed across reloads. Recording retries use a fresh campaign identity and do not reuse prior summons. Fixed night avoids false daylight inside HK caves.

Private runtimes, source and Windows package are updated together. Open **HollowCraft Launcher.cmd**; see the [boats and mobs guide](docs/MC_CREATURES.en.md).

Compilation, static resource inventory and package checks only; no game was launched. Individual boss captures, spawn-egg entities and complex interactions await player testing.
