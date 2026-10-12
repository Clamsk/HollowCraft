# HollowCraft 0.30.7 — Contact Hits, Shields and Totems

[简体中文](试玩说明-0.30.7.md) | [English](PLAYTEST-0.30.7.en.md)

- Melee compares the actual player and enemy collider shapes. Enemies overlapping or touching Steve are hittable with either left or right swings. Targets outside the player still require the chosen side, reach and native wall visibility. Each enemy is processed once per swing.
- Fix the flying enemy death mismatch confirmed in logs: Minecraft reported death while Hollow Knight retained 1 HP. Tiny proxy health residue is normalized to zero, and the native bridge no longer ignores the final small-positive-to-zero change. Actual boss phases and special death FSMs remain native-owned.
- Explicitly copy item-use and offhand flags to the rendering avatar, restoring vanilla third-person shield, bow, drinking and eating poses. Shield use interrupts residual combat animations.
- Native contact damage includes current enemy observations so vanilla directional shield checks have an attacker. Vanilla controls the warmup, front-facing requirement, durability and unblockable damage. Actual shield recoil is forwarded to the native enemy.
- Totems use vanilla lethal-damage checks, item consumption, healing, absorption and fire resistance. A totem must be in the main hand or offhand; an ordinary inventory slot does not activate it.
- Export the original GameRenderer totem activation animation into the native HUD. Existing sound/particle bridges carry the original effects. Client effect origins follow the bridge player instead of the backend window's stale position.
- New campaigns receive two separate totems. Existing campaigns receive two once, with the grant marker saved per campaign and retained across death; reconnecting or switching campaigns does not grant them repeatedly.
- Fix an exception when a destroyed native entrance object was referenced during respawn logging.

Run **Stop HollowCraft.cmd** before upgrading, then use the existing **Start HollowCraft.cmd**. Both ends must use the same version. Existing saves remain usable. This release is compiled and its package integrity checked without automatic game launches; actual gameplay awaits player feedback.
