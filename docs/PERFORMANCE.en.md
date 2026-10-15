# Performance and entity visibility

[简体中文](PERFORMANCE.md) | [English](PERFORMANCE.en.md)

0.30.12 includes the following code changes. Both bridges compile and package integrity is checked. Games were not launched at the player's request, so there are no before/after FPS measurements.

| Area | Change |
| --- | --- |
| Native enemy discovery | Two allocating physics searches become one reusable-buffer search every 0.15 seconds. Existing target transforms, guards and boat contact paths still update every 0.05 seconds; an explicitly targeted attack forces immediate discovery |
| Terrain sampling | Reuse the filter and cache component exclusions per collider during each sample |
| World, player and UI frames | Inspect newest files first and upload at most one complete texture per refresh; retain session, scene and footer validation |
| Continuous HUD | Default to one texel per GUI pixel instead of doubling both dimensions: 75% less RGBA pixel data at equal GUI size, not a claimed 75% FPS gain |
| Private MC backend | Default 30 FPS, view distance 2, simulation distance 5, VSync off. This affects the isolated backend only; it does not cap Hollow Knight's frame rate |
| Distant creatures | Retain dormant simulation and avoid redundant position/velocity corrections while dormant |
| Large terrain messages | Raise the bounded request limit from 64 KiB to 1 MiB so expanded creature samples do not disconnect the bridge |

## Invisible entities or failed placement

Hollow Knight's main camera uses perspective projection. The native viewport is now derived from screen-ray intersections with the player's plane, rather than `orthographicSize`. Invalid viewport data also gets a backend fallback. Entity rendering initializes its own camera instead of waiting for the minimized backend to render a regular world frame.

Before vanilla client tracking arrives, mobs/boats can use a same-type, same-NBT vanilla render replica. It never joins the client world or runs duplicate AI; the real entity remains authoritative. Models are not replaced by icons. Initial spawn clearance now uses the same thin XY plane as subsequent simulation, and accepted placements return success. Boats still need approximately 1.4 blocks of clear width. The room limit remains 160 creatures/boats.

Placement failures show a reason and log it. Close both games and restart through the existing launcher after updating: an already running JVM cannot load a replacement JAR automatically. Spawn eggs remain creative-only.

## If frame drops persist

Reduce large concentrations of summoned mobs, dropped items, explosions and fluid updates, and consider a 1280×720 Hollow Knight recording window. Avoid launching duplicate backends. Increasing the Java heap alone is not a general fix.

Relevant logs are `runtime/hollowcraft-hk.log` and `runtime/minecraft/logs/latest.log`. Useful entries include `Placed creature`, `Entity placement rejected`, `Native world entity frames ready`, `Native entity frame unavailable`, `Oversized message` and `Can't keep up`. Include the room, creature type and approximate count to distinguish spawn, rendering and simulation problems.

Developers may use JVM properties `-Dhollowcraft.backendFps=60` or `-Dhollowcraft.hudScale=2` to restore a higher backend cap or HUD supersampling. These are optional developer controls, not required installation steps.

## Further work

Synchronous GPU readback and RGBA frame-file transport remain. Measured follow-ups include asynchronous readback, shared memory, smaller dirty-region transfers and gradual native rendering migration. Shared memory alone does not remove AI or GPU readback costs.

A standalone mod requires original-compatible models, materials, animations, item rules, AI and saves. It is a staged migration; see [architecture](ARCHITECTURE.en.md).
