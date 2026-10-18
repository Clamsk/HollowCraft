# Native alpha.6: Clamsk, creatures and potions

[简体中文](NATIVE_EXPANSION.md) | [Installation and migration status](NATIVE_MIGRATION.en.md)

This expands the Hollow Knight-only mod. No Minecraft/Java process or bridge service runs during play. Feature parity and a complete playthrough remain unverified.

## Clamsk items

Press T or /, enter `Clamsk`, and press Enter with two free inventory slots. Zenith and Stardust Wings are granted. Equip the wings in the chest slot or use them from a hand.

The articulated wing geometry and motion now follow the bridge's authored StardustWingsFeature: back joints, 1.45 scale, seven three-segment plumes per side, shell/core colors and the original folding angles. Fixed feather geometry is batched into eight renderers. Zenith uses vanilla handheld third-person transforms and prefers locally imported Terraria art for icons and extrusion. Its swept blades damage both HK and MC creatures, retain independent hit intervals and native boss-phase guards, and stop generating new blades when unequipped.

The installer optionally imports original sprites and sword audio from an installed Steam Terraria copy, writing only Native resources. Without Terraria, fallback resources remain available. Original Terraria assets are excluded from releases.

## 75 creature types

Offline model construction provides static part, pivot, geometry and UV descriptions for 75 spawnable types. Original textures are extracted from the pinned resource archive during installation. Sheep wool, selected eye layers and trader-llama decoration are included. Runtime builds Unity models from data and never loads Java classes.

Registry dimensions, health, speed and base attacks feed local walking, hopping, flying and swimming behaviors: wandering, pursuit, melee, ranged projectiles, creeper fuse, neutral retaliation, animal panic, food following and boat capture. Hostile projectiles can damage the player. Add milking, shearing, selected feeding/breeding, baby growth, wolf taming, fall/fire/lava damage and common immunities. The loot interpreter reads 97 vanilla entity tables with common predicates, uniform counts, Looting, cooked drops and sheep subtables.

These are original model descriptions with **local AI**, not complete vanilla AI. Advanced pathfinding, trading, every special ability, variants/equipment, all loot predicates and creature audio remain incomplete. Some ranged attacks use generic presentation. Unsupported boss/entity eggs stay filtered.

## Potion clouds and explosions

Lingering potions create visible, room-persistent clouds with a ten-tick wait, lifetime, shrinking radius, per-target cooldown, quarter-duration effects and half-strength instant effects. Splash/cloud/status effects reach MC creatures. Undead instant healing/damage inversion and common undead/spider immunities are included.

TNT uses imported blast resistance, decaying ray energy and occlusion-based damage, affects MC creatures, respects obsidian/bedrock and closes destroyed workstations without refund/drop duplication. This projects the calculation into the 2D host; full 3D parity and some details remain pending.

## Performance and acceptance

Share meshes by species/part and raster frames by type, direction, pose, vehicle, baby and wool state. Evict old frames against an eight-million-pixel target while preserving recently used frames; clear frames on scene changes. Actual frame rate still requires player review.

The main mod builds; 415 pure rule/protocol checks and 12 launcher checks pass, including every enabled egg, Clamsk equipment, Zenith hits, hostile arrows, cloud cooldowns, undead inversion, breeding, shearing, loot counts and explosion item conservation. No game was launched, closed or automatically played. Visuals, performance and full boss progression await player acceptance.

Complex item/NBT behavior, remaining enchantments and special recipes, full creature AI, Bendy/use poses, full fluids, advanced redstone and remaining sounds/particles are still on the migration list.
