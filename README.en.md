# HollowCraft Native · Standalone migration

0.40.0-alpha.5 matches the installed original animation player, corrects legacy bone mapping and model transforms, repairs interrupted jump-building input, and restores provider-based font glyphs, advances and spaces. [Release details](docs/NATIVE_ANIMATION_FIX.en.md).

The main development line is now a Hollow Knight-only C# mod. **0.40.0-alpha.5 is an incomplete migration preview**, using the in-process backend by default. Running Minecraft or Java is unnecessary in Native mode.

- [Native installation, launch and migration checklist](docs/NATIVE_MIGRATION.en.md)
- **Install HollowCraft Native.cmd** → **Start HollowCraft Native.cmd**
- The previous bridge remains at [v0.30.18](https://github.com/Clamsk/HollowCraft/releases/tag/v0.30.18) for comparison.

The following documents the legacy bridge. Its two-game requirements do not apply to Native.

- [Native alpha download](https://github.com/Clamsk/HollowCraft/releases/tag/v0.40.0-alpha.5)

---

# HollowCraft 0.30.18 — Playtest Build

[简体中文](README.md) | [English](README.en.md)

Minecraft-style survival and combat in Hollow Knight.

Use Minecraft's player, survival, equipment, building and combat systems in Hollow Knight's 2D world. **Both games must run at the same time.** This is a playable bridge prototype; a complete survival playthrough has not been verified.

**Quick links:** [Windows download](https://github.com/Clamsk/HollowCraft/releases/tag/v0.30.18) · [Boats and mobs](docs/MC_CREATURES.en.md) · [Boss recording launcher](docs/BOSS_RECORDING.en.md) · [Installation and configuration](release/INSTALL_en.md) · [Build from source](SOURCE_SETUP.en.md) · [Architecture](docs/ARCHITECTURE.en.md) · [Enemy audit](docs/ENEMY_AUDIT.en.md) · [Campaign compatibility](docs/PLAYTHROUGH.en.md) · [Third-party sources](release/THIRD_PARTY.en.md)

## Current release

Version 0.30.18 holds native HP while the phase controller waits for its transition handshake, then resumes damage when ready and rechecks accepted damage when needed. It bridges Radiance's actual final nail contact and fixes story-scene aliases/F5 retries. Wings are 45% larger, fold down the back and use wider render padding without shrinking Steve. Both bridges compile; gameplay awaits player testing.

Version 0.30.17 corrects wing folding toward the back and moves the mount beyond the back armor. Native combat proxies retain their occupied chunks and are no longer saved as ordinary zombies. MC left click reaches Radiance ending states that explicitly wait for native attack input. Existing phase gates and Zenith damage remain. Both bridges compile; gameplay awaits player testing.

Version 0.30.16 replaces flat wing sprites with an articulated 3D voxel rig attached to the actual MC torso, with distinct ground, flight, glide and fall poses. Landing transfers movement ownership once, preserving normal ground friction. Native HP checkpoints prevent high-damage attacks from exhausting a boss before its event-driven phase controller advances. Both bridges compile; gameplay awaits player testing.

Version 0.30.15 fixes Return submission in local chat. Enter and numpad Enter are handled before the text field, with a physical-key fallback and a single submission path. IME confirmation is kept separate. Both bridges compile; the fix is delivered for player testing without a game launch.

Version 0.30.14 adds the **Clamsk Easter egg**: press T or /, enter Clamsk, and receive Zenith plus chest-slot Stardust Wings. Sword cycles use original component sprites, colored trails, 190 base damage and local target hit timers; wings provide finite flight, flapping and landing-damage immunity. Terraria assets are imported locally from Steam. See the [Easter egg guide](docs/TERRARIA_EASTER_EGG.en.md). Both bridges compile; gameplay awaits player testing.

Version 0.30.13 gives bridge boats one server-side physics owner, suspends conflicting native player locomotion/collision during riding, and uses explicit Shift dismount input. Native passengers receive corrected seat height, body anchors and render ordering, with state restored on release. Both bridges compile; riding behavior awaits player testing.

Version 0.30.12 corrects spawn-plane clearance, placement acknowledgements and native camera bounds; offscreen entities initialize their own camera and can use a vanilla render replica before client tracking arrives. Enemy discovery scans and obsolete texture uploads are reduced, the HUD defaults to native GUI pixels, and the isolated MC backend receives an FPS/chunk budget. Both sides compile; entity appearance and FPS changes await playtesting. See [performance and troubleshooting](docs/PERFORMANCE.en.md).

Version 0.30.11 adds vanilla-rendered boats/rafts/chest variants, native actor capture constraints and real spawn-egg mobs with original models, stats and basic AI. Spawn eggs are creative-only; starter backpacks gain an oak boat. Complex XY adaptation remains in progress; see [boats and mobs](docs/MC_CREATURES.en.md). Gameplay remains unverified.

Version 0.30.10 fixes the starter hotbar order and adds 64 bedrock, continuous native charge barriers, survival mining protection with creative removal and XY explosion shielding. Existing campaigns migrate existing stacks and receive bedrock once. Gameplay awaits player feedback.

Version 0.30.9 repairs Gruz Mother brood-count bookkeeping and replaces pose-sampled fall accumulation with native physics/landing events, preventing repeated sprint-jumps from sharing fall distance. The new bilingual GUI offers normal play or independent boss recordings with fresh survival starter equipment, 45 Godhome entries and two story arenas, native difficulty, F5 retries and F8 hint toggling. See the [recording guide](docs/BOSS_RECORDING.en.md). Runtime fights remain unverified.

The shield, contact melee and vanilla totem effects from 0.30.7 remain included. New campaigns receive two totems; old campaigns receive two once. A totem must be held in the main hand or offhand to activate.

Both bridges were compiled and package integrity checked, then handed directly to players. **No automated gameplay verification was performed.** See the [0.30.18 playtest notes](PLAYTEST-0.30.18.en.md). Older test records below apply only to their stated versions.

## Download and start

Requirements:

- Windows 64-bit.
- Your own Steam installation of **Hollow Knight 1.5.12620**; keep Steam signed in.
- Your own **Minecraft Java 1.20.1** installation. Launch vanilla 1.20.1 once through PCL or another launcher to download its assets and libraries.
- **64-bit Java 17 or 21**.

Download **HollowCraft-v0.30.18-windows.zip** from [Releases](https://github.com/Clamsk/HollowCraft/releases/tag/v0.30.18), extract it fully to a writable folder, run **Install HollowCraft.cmd**, then **Start HollowCraft.cmd** for the GUI. Choose normal play or a boss recording. Wait for Minecraft to enter its separate world and keep it running. Normal play uses the existing campaign; recording creates a separate save and loads a selected fight.

Save Hollow Knight's progress at a bench, then run **Stop HollowCraft.cmd** to save the Minecraft world and inventory and close both recorded processes. If only Hollow Knight was closed, restarting reuses the Minecraft backend of the same build. Stop before upgrading. Older backends without save acknowledgement must be closed normally through their Minecraft window; the script does not forcibly terminate them.

See [the installation guide](release/INSTALL_en.md) for paths, upgrades and troubleshooting. GitHub's automatically generated **Source code** archives require a local build; see [source setup](SOURCE_SETUP.en.md).

The repository contains source and scripts. Release packages also contain the two compiled HollowCraft mods. Neither includes game files, extracted assets, third-party dependency binaries, runtime copies, logs or saves. The installer downloads pinned dependencies and creates local runtime copies from your own installations.

Playtest data lives in `runtime/minecraft` and `runtime/hk-test-saves`. Older automated fixtures use separate `minecraft-smoke` and `hk-smoke-saves` locations. The original Steam assemblies, PCL configuration and personal saves are not modified. Some runtime resources use hard links or directory junctions to read-only originals; do not edit these linked resources. Java argument files are generated using the local encoding and checked for a lossless round trip to avoid corrupted paths.

## Controls

| Input | Action |
| --- | --- |
| T / / | Open local chat; enter Clamsk for Zenith and Stardust Wings |
| A / D | Move left / right and turn to a side view |
| Space | Jump approximately 1.5 blocks; double-tap in creative to toggle flight; ascend while flying; press again in mid-air with an elytra equipped to start gliding |
| Hold B | With a full block such as cobblestone selected, repeatedly jump and place beneath your feet; at most one block per jump, consuming items in survival; disabled while flying or gliding and in adventure or spectator mode |
| Shift | Crouch, move slowly and avoid walking off platform edges; descend while flying |
| Left Ctrl | Sprint, consuming hunger |
| S / Down arrow | Tap once to keep facing forward; A / D returns to a side view |
| W | Native upward interaction, including dialogue and entrances |
| Move mouse | Move the Minecraft crosshair within the reach measured from eye level: survival attack 3 blocks / block interaction 4.5; creative attack 6 / block interaction 5 |
| Left mouse | Swing toward the side of the crosshair, targeting enemies in that side's reach; empty swings are allowed; hold over a Minecraft block to mine it |
| Right mouse | Place at the target cell; interact with containers, doors and levers; use buckets. Hold to draw a bow, raise a shield, drink or eat; release to fire or throw a trident. Click to throw potions, pearls or snowballs; use fireworks while gliding |
| E / Tab | Survival inventory, crafting or container UI; categorized item catalog in creative |
| Click an inventory slot | Left-click to pick up, place or swap; right-click to split stacks or place one item |
| Shift + click | Vanilla quick transfer, equipment placement or crafting-result collection |
| Drag over slots while holding items | Left-drag distributes evenly; right-drag places one per slot; release to finish. Creative middle-drag fills slots |
| Double left-click | Collect matching items onto the cursor |
| 1–9 / F / Q over an inventory slot | Swap with a hotbar slot / offhand / drop; Ctrl + Q drops the full stack |
| Click outside the inventory | Drop the items held by the cursor |
| 1–9 / mouse wheel | Select a hotbar slot |
| F | Swap main hand and offhand |
| Q / Shift + Q | Drop one / a stack of main-hand items during gameplay |
| Esc | Close the inventory or open the Minecraft-style pause panel |
| F7 | Development shortcut: toggle survival / creative |
| F3 + F4 | Hold F3 and press F4 to open the four-mode selector; press F4 again to cycle; release F3 to confirm; Esc cancels |
| F9 | Creative-only equipment test kit: weapons, chests, fuel, food and armor |
| F10 | Creative-only test kit: bows, crossbows, tridents, elytra, buckets, potions and redstone |
| F11 | Creative item catalog; pick up an item and place it in the hotbar, or point at an item and press 1–9 |
| C + 1–9 / X + 1–9 | Save / load nine creative hotbar presets using vanilla `hotbar.nbt` in the private Minecraft configuration directory |
| R | Native Dream Nail interaction after obtaining it |

The Knight's appearance, mask/soul HUD, nail keys, spells, dash, double jump, wall jump and super dash are replaced in player controls. Native progression flags remain so cutscenes and map progression can use them. Startup and save selection still use Hollow Knight's main menu; native maps and NPCs remain.

Walking and sprinting use Minecraft 1.20.1's `LimbAnimator` / `BipedEntityModel` timing, smoothing and amplitude. Crouching uses vanilla body rotation and shoulder/hip offsets. Better Combat keyframes are layered on top. Both hands export complete vanilla grip transforms. Clicking native ground targets its available upper surface; the placement preview and actual target use the same cell. Thin-platform proxies retain their actual height.

Minecraft determines damage immunity, movement reduction and knockback. Steve flashes red without becoming transparent, held items keep their colors, and the HUD retains and flashes the previous health value. Native 2D collision handles walls; extra suffocation from projected Minecraft terrain is disabled to avoid damage beside doors. Real hazards, enemies, lava and drowning still use Minecraft damage rules.

The mode selector supports creative, survival, adventure and spectator. Adventure restricts ordinary block edits. Spectator passes through terrain with A / D, Space, Shift and Ctrl, and is immune to ordinary contact damage. Leaving spectator restores physical collision. Switching modes does not grant the test kits.

## Survival and interfaces

Health, hunger, armor, XP, hotbar, offhand and equipment slots read the real Minecraft player. One native Hollow Knight contact-damage unit becomes 3 points of base Minecraft damage, using a normal-difficulty zombie attack as a reference. Minecraft then applies armor, toughness and resistance. Ordinary contact damage does not teleport the player to the spawn point; hazards and actual death are handled separately.

Hunger, regeneration, food consumption, death, equipment drops and respawn use Minecraft rules. Dropped items fall with Minecraft gravity in the current scene, render as 3D models or transparent icons, and are collected according to pickup delay and inventory capacity.

Inventory, crafting table, furnace, chest and HUD graphics come from your local vanilla Minecraft installation. The inventory uses real 2×2 crafting; crafting tables use 3×3. Actions call real `ScreenHandler` logic for pickup, splitting, quick transfer, equipment and recipes. Furnaces use real fuel and smelting progress. Opening a UI blocks ordinary gameplay input; existing elytra gliding continues. Collision dimensions are 0.6×1.8 blocks standing, 1.5 high crouching and 0.6 high gliding, with feet kept in place on pose changes.

A new campaign starts with full iron armor and an offhand shield, plus:

- Iron sword, bow, 64 arrows, 64 steaks and 64 ender pearls (four stacks of 16).
- 192 cobblestone, 64 oak logs, flint and steel, 64 TNT, water bucket and lava bucket.
- Iron axe, iron pickaxe, 64 torches, 64 iron ingots, 64 regular golden apples and two separate totems of undying, plus 64 bedrock and one oak boat. Spawn eggs are available through creative, not survival starter inventories.

Hotbar slots 1–9 contain iron sword, bow, golden apples, cobblestone, water bucket, lava bucket, TNT, flint and steel, ender pearls. Arrows and other supplies stay in the backpack.

Existing campaigns keep their items. Version 0.30.11 grants an oak boat once. Version 0.30.10 grants bedrock once and swaps existing stacks into the new hotbar order without replenishing consumed supplies. Version 0.30 grants a missing iron pickaxe once; 0.30.3 grants iron ingots once; 0.30.4 grants two extra cobblestone stacks and golden apples once. Version 0.30.7 grants two totems once. Grant flags are saved per campaign and survive death. Excess items drop when the inventory is full, and reconnecting does not grant them again. Each connection returns to survival mode.

Regular `minecraft:golden_apple` items use vanilla eating, including at full hunger. They grant Regeneration II for 5 seconds and Absorption I for 2 minutes (4 absorption health points), plus vanilla hunger and saturation. The vanilla HUD shows yellow absorption hearts and status icons; the fallback HUD also reads real absorption health.

Hold right-click to raise a shield toward the crosshair side. Vanilla controls the shield warmup, directional blocking, durability and damage-type rules. Totems must be held in the main hand or offhand to activate; lethal damage consumes one and applies vanilla healing, absorption and fire resistance, with the original activation animation in the native HUD.

## Combat, building and rendering

Weapons use the installed Better Combat 1.8.5 registry for cooldowns, windup, damage multipliers and combos. Better Combat and player-animation-lib animate the real Minecraft player model. Reach uses Minecraft 1.20.1's 3 / 6 blocks for attacks and 4.5 / 5 for block interactions in survival / creative.

Attacks select the side of the crosshair and respect walls, without requiring a narrow cursor angle or thrust box. Empty swings are allowed; the arm returns after a click, and a click during cooldown can buffer one follow-up attack. Hits use native `HealthManager` / `HitTaker` and damage events for enemies, knockback, breakables and obstacles. Permanent map terrain cannot be mined. Story doors follow their native Hollow Knight destruction rules.

Mining runs at 20 server ticks per second using vanilla `calcBlockBreakingDelta` accumulated over ticks. Hardness, tools, correct-tool requirements, Efficiency, effects and airborne or underwater penalties are calculated by Minecraft, rather than trusting client-reported hold duration.

Minecraft's third-person `PlayerEntityRenderer` renders the whole player, armor and hand layers, item-use models, Better Combat animations, transparency and depth. Hollow Knight displays the resulting transparent frame instead of rebuilding individual equipment parts. Render state uses a separate immutable equipment/pose copy and does not modify the real inventory. Character frames usually update at approximately 20–30 FPS while position follows Hollow Knight every frame. The scene remains 2D; this does not turn Hollow Knight's maps into 3D levels.

Inventory icons are baked by Minecraft's `DrawContext` / `ItemRenderer` into transparent 64×64 images with vanilla lighting and block faces. Ground items still use `BakedQuad` data. Potion colors are exported per NBT. Minecraft calculates durability; the UI uses vanilla's 13-pixel rounded bar and HSV colors. Arrows use vanilla textures and crossed-plane models, with tick positions and spawn/hit events interpolated in Hollow Knight. Caches are generated on first world entry. GUI caches use RGBA; older black-background caches are not used.

Invisible Minecraft mob proxies call real `PlayerEntity.attack`, retaining cooldown, 1.5× falling critical hits, enchantments, durability and fractional damage. Ordinary enemies with native health no greater than 60 map to 18 Minecraft health, aiming for about three full-damage iron sword hits. Stronger enemies retain more health, capped at 1024. Remaining health is converted back to the native `HealthManager`. Ghosts and shades are classified as undead; other enemies default to arthropods. Proxies currently have zero armor; individual resistances and behavior are not fully configured.

TNT uses Minecraft ignition, an 80-tick fuse, explosions, block resistance, distance and obstruction damage. Version 0.30 adds native Hollow Knight floor/wall collision. TNT pauses while waiting for native geometry checks, so bridge latency can lengthen the actual fuse. Explosions affect native enemy and breakable proxies. Temporary background support permits ordinary blocks in empty target cells; special blocks still require vanilla placement rules. Native terrain obstruction is approximated on a block grid.

Water, lava, buckets and mixing-generated stone use Minecraft rules. Bows, crossbows, tridents, shields, thrown potions, pearls, elytra and fireworks use Minecraft entities or states. Gravity is integrated at 20 Hz. Older 0.5 measurements recorded a vanilla jump of approximately 1.252 blocks; **the current ordinary jump is intentionally raised to approximately 1.5 blocks**. Elytra uses a 2D projection of vanilla flight formulas, while movement in water is approximate. Redstone uses vanilla updates; lever/lamp behavior was checked in an older build, but complex piston machines remain unverified.

Brewing stands use real 400-tick recipes and fuel. Anvils use native naming, costs and outputs; enchanting tables use vanilla random enchantments and lapis. Container slots and coordinates come from `ScreenHandler`. Generic slots can be connected, but specialized selection controls such as the loom and stonecutter are incomplete. Water, lava and flames use vanilla animated frames and `.mcmeta` timing. Special built-in models, enchantment glint and full Better Combat conditional / dual-wield coverage still have gaps.

## Campaign compatibility and limitations

Each new Hollow Knight save has separate Minecraft inventory, armor, offhand, durability, XP and room blocks. Recreating the same save slot generates a new campaign identity. Position updates run at 20 Hz. Right-click supports repeated placement, placement on existing blocks and foundations on some native surfaces. Attacks use reliable messages and an ordered click queue.

Native enemies and breakables grant custom Minecraft survival materials only after confirmed native destruction or death, to avoid early rewards at boss phase changes. These drops are bridge progression design, not vanilla Minecraft mob loot tables. Benches save both sides and restore Minecraft state; death returns to the native checkpoint. Native NPC text uses the new dialogue UI. Minecraft-style shops read native products, names, prices and conditions, then call native purchase confirmation to spend Geo and set progression flags.

Older checks covered purchasing a lantern, cancelling purchases without spending currency, and paying 50 Geo to unlock the Crossroads Stag Station. Dream transitions, station destination selection and endings still require individual playtests. See the [campaign compatibility checklist](docs/PLAYTHROUGH.en.md). Native AI, maps, story and collision remain in Hollow Knight; Minecraft supplies numerical rules and item behavior. **A complete survival campaign cannot yet be claimed.**

Pause is synchronized between both games. Disconnection freezes the Hollow Knight scene while awaiting reconnection and retains block collision. Nearby drops fall with Minecraft gravity; drops outside the current scene freeze. Meshes are released when switching held models, while materials and sorting buffers are reused.

## Recent implementation history

These are implementation notes, not confirmation that each version passed gameplay acceptance.

| Version | Changes |
| --- | --- |
| 0.30.11 | Original boat/mob renderers, native capture constraints, spawn-egg entities, vanilla stats/basic AI and XY terrain/path adaptation |
| 0.30.10 | Requested hotbar layout, 64 starter bedrock, continuous native charge barriers, survival-protected bedrock and XY blast shielding |
| 0.30.9 | Gruz brood gate bookkeeping, per-landing physics fall events, bilingual launcher and fresh survival boss recording sessions |
| 0.30.8 | Full offline enemy-resource audit, native direction/type guards and proxy lifetime, arrows through breakable props, reliable pointed-block mining, attack-animation fallback |
| 0.30.7 | Player-collider melee coverage, float-residue lethal hit fix, vanilla shield/item-use poses and blocking, native shield recoil, totem activation animation/effects and two starter totems |
| 0.30.6 | Inventory / held-item synchronization, per-entry player and HUD frame isolation, registered attack FSM, native hit/death routing and diagnostic health logs |
| 0.30.5 | Restore native gravity, collider size and controls before leaving a scene; wait for native entrance actions before resuming Minecraft movement; reject old scene messages; initialize at actual entrance feet coordinates; protect low entrances and search locally when blocks overlap the entrance |
| 0.30.4 | Ender pearl base teleport damage changed from 5 to 3 points (1.5 hearts) by request; reset the previous fall distance and reject old poses; add 128 cobblestone and 64 regular golden apples to existing campaigns once |
| 0.30.3 | Delay automatic water recovery approximately 1.5 seconds; follow the introductory fall with the camera; rebuild superhero landing joint transforms; grant iron ingots; grant one special Loyalty III trident with base melee/projectile damage 100 on entering creative; supply the tutorial door's native damage FSM fields |
| 0.30.2 | Dedicated jump-placement path for full blocks, predicted placement after feet clear the cell and server-confirmed item consumption; idempotent retries; hold B for repeated assisted pillar building; allow foundations partially inside fractional-height native floors |
| 0.30.1 | Keep a short placement request for up to 0.8 seconds during temporary body overlap; lock the target through camera/player height changes; read native held-button state |
| 0.30 | Native terrain collision for primed TNT, approximately 1.5-block jumps, starter iron pickaxe and Windows installer package |
| 0.29 | Herobrine white eyes in creative, double-Space flight and locked underfoot placement targets |
| 0.9 | Implementation fixes for eating, native projectile collision, creative catalog, jump-building and missed attacks; player acceptance still required |
| 0.8 | Ordinary enemy health conversion, campaign inventory isolation, automatic landing water and reliable attack input |
| 0.7 | Real third-person Minecraft rendering for the player, armor, equipment and combat animations |
| 0.5 | Fluid/bucket support, projectiles, elytra, containers, native campaign interfaces and isolated integration fixtures |

The GitHub `v0.8.0` tag is the source baseline before later fixes. Survival automatically places and recovers water on the opening fall and on later falls of at least 12 blocks. Pearl damage uses the vanilla damage type: ordinary armor points do not reduce it, while Protection / Feather Falling enchantments and absorption still apply. The 3-point pearl damage, 1.5-block jump and starter kit are deliberate project adjustments.

## Source and validation

- `hollow-knight/`: controls, cursor, player view, damage bridge, combat, UI, 2D blocks and drops.
- `minecraft/`: Fabric server rules, rendering and automatic startup of a separate world.
- `scripts/build-hollow-knight.ps1`, `scripts/build-minecraft.ps1`: build entry points.
- `runtime/hollowcraft-hk.log`, `runtime/minecraft/logs/latest.log`: local playtest logs.
- `runtime/hk-smoke-saves/hollowcraft-smoke.json`: the last older two-game fixture result.
- `runtime/hk-smoke-saves/hollowcraft-*.png`: older local game screenshots.

Older automated fixtures use separate saves and cover cursor reach, placement/mining, real inventory and crafting slots, chests, furnaces, Better Combat combos, empty swings, native enemies/breakables, survival states, offhand, death/respawn and item recovery. Version 0.5 evidence was stored per feature in `runtime/hk-smoke-saves/*-0.5-pass.json` and associated logs. Its boss fixture used creative mode with native AI/phases; it does not demonstrate a complete survival playthrough. New builds from 0.8 onward are delivered for player testing without automated game launches.

Injection targets the stated local game version, validates it during preparation and creates a private copy. Game updates require compatibility review. Game files and extracted textures are for local use and are not distributed in the repository.

Better Combat: [official 1.20.1 source branch](https://github.com/ZsoltMolnarrr/BetterCombat/tree/1.20.1), by Daedelus, GPL-3.0. Preparation uses version 1.8.5 with its animations, Player Animator and Cloth Config. The Windows installer verifies dependencies against the [pinned lockfile](release/dependencies.lock.json); see [third-party sources](release/THIRD_PARTY.en.md).

Minecraft belongs to Mojang/Microsoft and Hollow Knight to Team Cherry. HollowCraft is unofficial and is not affiliated with or endorsed by their developers. This repository currently grants no additional open-source license; see [source setup](SOURCE_SETUP.en.md).
