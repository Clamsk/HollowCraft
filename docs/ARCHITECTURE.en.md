# HollowCraft Runtime Boundaries and a Standalone Mod Route

[简体中文](ARCHITECTURE.md) | [English](ARCHITECTURE.en.md)

HollowCraft bridges two local game processes using localhost TCP. MCP is not in the runtime path and does not handle individual mouse clicks. Reported issues involving eating paths, missing native collision, jump-release resets, failed file replacement and replies crossing campaign saves are implementation problems in this project.

## Current responsibilities

| Hollow Knight side | Minecraft backend |
| --- | --- |
| Window click queue, crosshair and left/right swing selection | Attack attributes, weapon configuration, cooldowns, durability and enchantments |
| 2D movement, jump integration and actual native terrain collision | Health, hunger, food, armor and status effects |
| Predicted block collision and reconciliation | Block placement rules, item consumption and block updates |
| Continuous projectile collision and safe landing positions | Vanilla projectile damage, throws, potion and pearl effects |
| Native AI, story, hit feedback and map objects | Vanilla inventory, recipes, containers, furnaces and redstone |
| Display of Minecraft-style UI within the native scene | Minecraft third-person model and Better Combat animation rendering |

Immediate controls stay close to Hollow Knight's physics frames while Minecraft supplies its original rules. The player is rendered by Minecraft's actual third-person renderer into transparent frames with depth, then displayed as a complete frame in Hollow Knight. This is not yet a standalone skeletal renderer inside Hollow Knight.

## Fully standalone option

A mod that runs only Hollow Knight is technically feasible, but it has not been implemented. It would require:

1. Local skeletons, vanilla model coordinates and UVs, armor layers, grip transforms and animation blending, including shields, bow draw, two-handed weapons, crouching and gliding.
2. Item definitions, NBT, crafting recipes, container rules, enchantments, potions, combat and survival values, with migration of existing saves.
3. Local block states, mining, fluids, lighting, redstone and entity systems that interact with native maps.
4. An independent resource importer that extracts models and textures from the user's own legitimate installations. Game assets must not be published in the source repository.
5. Behavior comparisons against the Minecraft backend and player feedback, with the original backend available as a fallback until replacements reach comparable behavior.

Moving immediate input and collision to the Hollow Knight side is a reusable step. Removing the Minecraft backend does not by itself resolve animation, recipe or numerical errors. The bridge remains the reference for fidelity rather than being replaced with a substantially simplified implementation.

## Development priorities

First address player reports involving eating, pearls, the creative catalog, jump-building and missed clicks. Then address save isolation, teleport/landing state, container interaction, story progression and route obstacles. Check conflicts between these systems and compile/deliver each version.

The player requested direct playtesting without automatic game launches. Version notes must distinguish **compiled**, **implementation adjusted** and **confirmed in gameplay**. A successful build does not prove that a full campaign can be completed. See [campaign compatibility](PLAYTHROUGH.en.md).
