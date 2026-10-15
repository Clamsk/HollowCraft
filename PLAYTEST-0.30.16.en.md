# HollowCraft 0.30.16: wing rig and native boss phases

[简体中文](试玩说明-0.30.16.md) | [English](PLAYTEST-0.30.16.en.md)

- Wings now use an articulated 3D voxel rig with connected shoulder, outer-wing and tip joints, copying the actual MC torso transform. Dark-blue rims and luminous cyan cores retain Stardust colors.
- Ground folding, flight flapping, gliding spread and falling poses have separate wing/limb animation. Model/state layering references Confluence and TerraCurio; see the [Easter egg guide](docs/TERRARIA_EASTER_EGG.en.md).
- Fixes grounded wing updates repeatedly resetting movement accumulators. Landing transfers control once, restoring MC walking/sprinting/friction and limiting carried flight speed.
- Native boss FSM HP comparisons gate high damage at the next phase boundary. Native damage events and phase transitions finish before subsequent phases take damage, avoiding Radiance reaching zero HP before its event-driven phase flow completes. Native guards, scripted invulnerability and death handling remain active.

Both private bridges and the release package are updated. Re-enter the boss fight; F5 retries a stalled fight in recording mode. Enter Clamsk through T or /, equip wings in the chest slot, and hold Space airborne to fly or slow descent after exhaustion.

Both bridges compile and package integrity checks pass. Per the player's request, neither game was launched. Please try ground stopping, flight-to-walk landings, wing attachment during crouching/combat, flight/glide poses and Radiance phase transitions/final defeat.
