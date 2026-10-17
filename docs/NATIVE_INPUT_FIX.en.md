# Native alpha.4: real input, combos and workstations

[简体中文](NATIVE_INPUT_FIX.md) | [Migration status](NATIVE_MIGRATION.en.md)

This release repairs core native interactions and continues the port. It is still incomplete and has no verified full playthrough. Players run Hollow Knight alone, without Minecraft or Java.

## Root causes

The shared frontend sends `interact` with distinct pointer/placement cells and current player feet. The old backend only tried opening a container. Earlier checks called backend-only placement/use commands and missed the actual input failure. Frontend and protocol checks now share packet builders.

Right-click tries block interaction, then hand priority, placement or item use. Sneaking bypasses block interaction. Rejected predictions are removed. Tower placement uses current feet and actual player width, accepting a block as soon as the feet clear its top. Holding right-click no longer restarts bow charging; release fires through the shared input path. Unity sweep acknowledgements govern projectile movement, including placed blocks and MC creatures; a coarse terrain sample containing a thin ledge is no longer treated as a solid cube.

## Animation and rendering

The native adapter lacked the whole-model X reflection present in the bridge GUI matrix. Player, equipment, held items and portrait now use a consistent root transform. The same Better Combat weapon presets and slash/slash/stab clips are retained, with default transmission speed changes, faster upswing, downwind and three-tick stop fade. Combo transitions start from the previous pose. Canceled attacks invalidate their damage requests. Sharpness bonus is added after critical base damage.

GUI block models use vanilla GUI transforms. Potion/arrow, spawn egg and leather item icons use registry/NBT colors, including corresponding held texture layers. Armor trims, complete dyed armor layers and enchantment glint remain incomplete.

## Additional porting

- Creative tabs now use the original 13-tab schema, ordering, icons and 1523 source stacks, including potion/enchantment NBT. Unported spawn eggs are filtered. Saved hotbar previews, search, number-key selection and C/X hotbar storage are connected.
- Anvils support material repair, tool/enchantment merging, renaming, prior-work penalties and levels. Grindstones repair, remove non-curse enchantments, preserve curses from both inputs and award XP.
- Enchanting uses lapis, XP, shelves, enchantability, power ranges and compatibility. Native random rolls are not Java-seed-identical. Stonecutters select vanilla recipes and support batch output. Smithing consumes template/base/addition and preserves NBT; trim NBT is written but trim overlays are not rendered.
- XP total, level progress and enchanting seed persist. Workstation output is virtual; closing returns inputs without duplicating results.
- Multishot produces three trajectories; Piercing continues through living targets without repeated damage. Infinity/creative ammunition cannot be duplicated through pickup. Loyalty tridents visibly return and are recovered on room changes.
- Nine existing mob species use authored articulated geometry and static vanilla skins with basic species loot. Shared quantized pose frames reduce repeated rasterization; temporary meshes are released. Full vanilla geometry, AI and species coverage remain pending.
- Local preparation adds 149 vanilla/Better Combat audio files and 76 event mappings. Steps, blocks, swings, damage, shields, totems, food/drinks, bow and explosions are connected. Not all mapped events are emitted yet. Assets stay local and are excluded from Git/releases.

## Validation and play

111 pure rule/protocol checks and 12 launcher checks passed. No game was launched, closed or automatically played. These checks cover actual frontend packets, combo order/timing, placement boundaries, bow release, menu schema, workstation conservation, projectile acknowledgements and Loyalty return. Gameplay appearance, collision feel, performance and full Boss/story progression still need player acceptance.

Run **Start HollowCraft Native.cmd** after updating. Existing native saves are preserved.

Full creature AI/species, special recipes/items, lingering clouds, advanced redstone/fluid details and remaining presentation are still tracked in the [machine-readable checklist](../release/native-migration.json). Static data coverage is not behavior parity.

References: [Better Combat 1.20.1](https://github.com/ZsoltMolnarrr/BetterCombat/tree/1.20.1), [PlayerAnimator](https://github.com/KosmX/minecraftPlayerAnimator/tree/1.20).
