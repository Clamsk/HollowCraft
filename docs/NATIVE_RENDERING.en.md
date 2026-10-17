# Native alpha.3: player rendering, combat and data migration

[简体中文](NATIVE_RENDERING.md)

This release corrects hand transforms and surface occlusion, then migrates more crafting, brewing, crossbow and enchantment rules. It remains an incomplete native migration preview, with no verified full playthrough.

## Rendering and animation

Skin, armor and both held items share per-pixel depth testing. Transparent texels do not write depth. This replaces triangle-centroid sorting, which could not resolve overlapping surfaces. Static vanilla item/block JSON supplies inherited geometry, textures and third-person transforms, including distinct left/right grips and shield/trident entity transforms. Bow and crossbow use states select the corresponding models.

Armor uses the same joint pivots, with separate inner leggings and outer armor dilation. Boots remain visible with leggings. Skin outer layers are retained. Better Combat weapon presets supply the sword's right slash, left slash and stab. Weapon changes or combo timeouts reset the sequence. Global `body`, chest `torso`, and all six limb rotation/position channels are sampled independently, including preparation and recovery.

This is an independent C# compatibility implementation. Minecraft's PlayerEntityRenderer, Better Combat and PlayerAnimator do not run inside Unity. Bendy deformation, every vanilla use pose, effects and transition details are not equivalent yet.

Because the available Hollow Knight shaders do not provide a general cutout depth pass, a bounded software depth buffer renders the articulated 3D geometry into a transparent texture: 256×256 normally, 384×384 with special wings, capped at 40 updates per second. Mesh/texture pixels are cached; the inventory portrait updates continuously only while its UI is open. There is no second game, live frame bridge or per-frame disk transfer. Gameplay performance still needs player review.

## Additional migration

- Imports 1255 vanilla 1.20.1 item records for stack sizes, durability, attributes, food, fuels, armor and default block properties. Registry coverage does not mean every special behavior is implemented. Spawn eggs without migrated models/AI are not advertised as working creatures.
- Imports 822 shaped/shapeless crafting recipes and 112 cooking recipes, with recursive item tags, alternatives, mirrored shapes, complete shapeless matching and crafting remainders. Dynamic special recipes, smithing and stonecutting remain pending.
- Furnace, blast furnace and smoker use separate recipe categories and durations. Faster stations shorten fuel time accordingly. Changing input resets progress.
- Brewing supports three bottles per batch, blaze powder fuel, 400-tick processing, 251 vanilla transformations, 43 potion records and bottle-type conversion. Timers, fuel and potion NBT persist locally.
- Creative potion/arrow variants, drinking, splash healing/harming, selected timed enemy effects and tipped arrows are connected. Water no longer deals invented damage. Lingering clouds, complete status visuals and species-specific immunity remain pending.
- Crossbows load before firing and persist their charged state. Quick Charge, bow ammo selection, Power, Infinity, Sharpness, Efficiency, Unbreaking, Protection and selected specialized protection rules are connected. This is not every enchantment.
- Default block collision boxes are projected into 2D. Tool-required harvesting, Efficiency and Aqua Affinity are connected; complete block-state orientation, waterlogging and loot tables remain pending.

## Installation and validation

Close Native, run **Install HollowCraft Native.cmd**, then **Start HollowCraft Native.cmd**. Updating preserves native saves and existing inventories. Setup extracts checksum-pinned static textures, models, recipes and animation data. Players do not need Minecraft, Java or Fabric installed or running.

`release/native-registry.json` contains pre-exported numerical registry facts. The developer-only `tools/export-native-rules.ps1` uses cached mapped dependencies and a JDK to bootstrap registries without creating a client, server, world or window. The exporter and Java binaries are excluded from the player package.

Pure checks cover depth/alpha behavior, animation channels/recovery, tags, cooking categories, brewing, crossbows, potions and previous storage/combat rules. No game was launched, closed or automatically played. Equipment appearance, feel, frame rate and Boss progression await player acceptance. See the [migration checklist](NATIVE_MIGRATION.en.md) and [machine-readable status](../release/native-migration.json).

Animation semantics: [PlayerAnimator](https://github.com/KosmX/minecraftPlayerAnimator), [Better Combat 1.20.1](https://github.com/ZsoltMolnarrr/BetterCombat/tree/1.20.1).
