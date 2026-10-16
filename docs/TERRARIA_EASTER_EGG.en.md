# Clamsk Easter egg: Zenith and Stardust Wings

[简体中文](TERRARIA_EASTER_EGG.md) | [English](TERRARIA_EASTER_EGG.en.md)

Version 0.30.14 adds two optional items. Press **T** or **/** in gameplay, type **Clamsk** and press Enter. `/Clamsk` also works. Keep two backpack slots empty; each successful command grants one of each item. They are absent from the survival starter kit and ordinary creative categories. Esc closes chat; gameplay shortcuts are blocked while typing, and the world keeps running.

## Zenith

Select Zenith and click to cast one three-sword cycle, or hold left click to repeat. Aim across the visible screen, within a protocol limit of 48 blocks. Ordinary melee and placement retain their existing reach. Flying sword sprites and colored trails follow the same trajectory used for hit detection; the held item remains attached to the vanilla MC player arm.

| Original parameter or behavior | Port |
| --- | --- |
| 190 base damage, 14% critical chance | Server-authoritative individual hits, double critical damage, then existing MC armor/native HP conversion |
| Three swords per 30 Terraria ticks | One cycle per 0.5 seconds; releasing a click finishes the active cycle |
| Final Fractal motion, component swords and colored afterimages | Original trajectory parameters with 120 Hz substeps and interpolated native effects, without full-screen frame transfers |
| Unlimited penetration, no tile collision, local NPC hit timer | Separate timer per sword and target; ordinary walls do not block swords; no direct screen-wide HP subtraction |

Body-range aiming compresses the minimum loop so that small enemies overlapping Steve remain reachable.

Native enemies retain directional guards, invulnerable phases, phase changes, hit reactions and death handling. NPCs and story triggers remain protected. For Radiance footage, select the appropriate fight in the recording launcher and enter the command after arriving.

Version 0.30.16 reads native HP comparisons reachable from the active boss FSM state. A hit stops at the next threshold while the original damage event/phase transition completes. The terminal transition briefly retains one HP while the native shield is installed. This applies to bridged native damage generally, including Zenith and the creative trident; scripted invulnerability is preserved. Re-enter a fight already stalled at zero HP in an older build.

Version 0.30.17 refreshes temporary chunk tickets shared by native proxies in the same chunk, releasing them when targets leave observation or the scene ends. Proxies are excluded from vanilla zombie saves; recovery preserves unacknowledged damage and logs removal reasons. MC left click also forwards the explicitly enabled native `ListenForAttack` event in Radiance ending states, including after its damage collider disappears. It does not set victory flags or skip story states. Zenith retains 190 base damage and 14% critical chance. Mirrored wing hinges now fold toward the back, keep the flight stroke rearward and mount outside the back armor.

Version 0.30.18 holds current native HP while a phase controller cannot yet consume damage events. It resumes when ready and replays an accepted HP-boundary notification only into the appropriate native controller if needed. Bosses with these phase controllers share a minimum 0.2-second damage interval; Zenith retains 190 base damage. For a Radiance controller explicitly authored to finish through a `FINAL HIT` nail trigger, actual melee/swept Zenith contact reaches the original collision and ending flow while HP is preserved. Both `Dream_Final` and `Dream_Final_Boss` aliases are recognized, and F5 resets through the workshop. Wing bones are scaled to 1.45 and fold downward behind the torso. Equipped wings use a 384-pixel transparent frame instead of 256, preserving Steve at 64 pixels per block.

## Stardust Wings

Place the wings in the **chest slot**, or hold them and right-click to equip. They replace the chestplate and add no armor. Version 0.30.16 uses an authored 3D voxel rig with a back mount, shoulder, outer-wing and curved-tip joints. Blue shells and luminous cyan cores follow the actual MC torso transform, including crouching and combat animation, with separate ground, flight, glide and fall poses.

- Hold Space in the air to fly for a cumulative **3 seconds**. Landing restores the budget; releasing Space does not refill it.
- Release Space to descend; hold it after exhaustion to slow descent. A / D steer with acceleration, deceleration and terrain collision.
- Equipped wings prevent landing damage. The existing ender-pearl teleport cost remains.
- Original horizontal speed is 9 pixels/tick with a 2.5 acceleration multiplier; ascent uses the Stardust .135 / .85 / 3 / 1 / .15 parameters. The projection uses 0.0375 MC blocks per Terraria pixel, giving a horizontal limit of about 20.25 blocks/second.
- These wings do not provide Vortex-style hovering. Creative flight, boats and native story control keep their separate movement paths.

This is a 2D port between different games. Native collision, the MC damage pipeline and Terraria's complete engine differ; frame-perfect equivalence is not claimed. This build is compiled and delivered for player testing without launching a boss fight.

## Local original assets

The installer detects Steam Terraria and imports the Zenith/component-sword sprites, Stardust inventory/wing textures and Zenith swing sound from local XNB files. Import completed with local **1.4.5.8** content. Terraria does not need to run. Extracted files remain in private `artifacts` and `runtime` directories; neither Git nor the release ZIP contains original game images/audio.

Install Terraria, then run the new **Install HollowCraft.cmd**. A development checkout can run `scripts/prepare-terraria-assets.ps1`; use `-TerrariaRoot` for a custom directory containing `Content`. Restart both games after importing. The private MC profile enables the `HollowCraft-Terraria` resource pack. Normal HollowCraft remains available without Terraria; the Easter egg command requests asset import first.

Original references: [Zenith](https://terraria.wiki.gg/wiki/Zenith), [Stardust Wings](https://terraria.wiki.gg/wiki/Stardust_Wings). The importer uses MonoGame's LZX decoder; see [third-party notices](../release/THIRD_PARTY.en.md).


Model/state design references: Confluence’s [wing skeleton](https://github.com/Magic-team-jvav/confluence/blob/neoforge/1.21.1/ConfluenceOtherworld/src/main/resources/assets/confluence/geo/curio/fledgling_wings.geo.json) and [standby/fly/glide animation states](https://github.com/Magic-team-jvav/confluence/blob/neoforge/1.21.1/ConfluenceOtherworld/src/main/resources/assets/confluence/animations/curio/fledgling_wings.animation.json), and TerraCurio’s [torso-following renderer](https://github.com/Magic-team-jvav/TerraCurio/blob/neoforge/1.21.1/src/main/java/org/confluence/terra_curio/client/renderer/accessory/WormScarfRenderer.java). HollowCraft implements its own Fabric 1.20.1 model and XY controls.
