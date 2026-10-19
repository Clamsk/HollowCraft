# Native alpha.9: consumption audio and fluid fixes

[简体中文](NATIVE_FLUID_FOOD.md) | English

Food now plays original chewing sounds throughout use, followed by a single completion burp. Potions and milk play drinking sounds. Releasing use cancels later sounds and consumption. Samples are preloaded and retain the existing native SFX-volume routing.

Fluids use clipped meshes with continuous sloping edges instead of scaling static square sprites. Shallow levels crop geometry without stretching the whole texture. Still/flowing water and lava each use their original texture and animation metadata, including lava's reversing frame sequence. Water uses transparent blending.

The sealed 2D simulation updates water every five ticks and lava every thirty ticks, with seven- and three-cell horizontal reach respectively. Flow prioritizes downward movement and searches nearby lower outlets. Falling columns regain full flow amount; disconnected flows recede. Source renewal, stone/cobblestone/obsidian reactions and sponges remain supported.

Flow frontiers request actual Hollow Knight collision data. Unknown cells are not treated as air, scene arrival waits for the first survey, and previously sampled geometry remains until refreshed. Thin ledges block downward flow even when they do not occupy an entire voxel. Frontier-only terrain is surveyed once, while the player's nearby area is refreshed periodically.

Slab collision arrays are no longer flattened, slabs render at half height, and waterlogged slabs display water only in their free volume. Changing fluid levels invalidates the visual state. Existing fluid meshes update in place rather than recreating GameObjects. Immersion depth and drowning use the new liquid surface.

Run the installer after updating to prepare missing flow-animation metadata. Minecraft and Java do not run. Original texture/audio pixels remain local and excluded from Git and ZIPs. Existing world and inventory saves are retained.

The main mod builds and 549 pure rule/protocol checks pass. Added cases cover consumption timing/cancellation, flow reach, shared corners, changing water levels, thin platforms, unknown terrain, arrival gating and waterlogged slabs. No game was launched, closed or automatically played. Visual/input acceptance remains with the player; the 2D simulation is not complete Minecraft 3D fluid parity.
