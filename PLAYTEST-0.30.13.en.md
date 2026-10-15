# HollowCraft 0.30.13 playtest notes

[简体中文](试玩说明-0.30.13.md) | [English](PLAYTEST-0.30.13.en.md)

- Bridge boats retain server-owned physics when a player boards. Suppress the hidden MC client's vehicle motion, paddle and passenger-input packets for those boats. Ordinary MC mobs retain vanilla models and riding behavior.
- The native player uses a controlled kinematic body while riding, suspending native walking, jumping, gravity and gliding and temporarily ignoring collision with the mounted boat. Align the body again before drawing Steve, preventing collision correction from repeatedly fighting the seat anchor.
- Shift is an explicit dismount input; the intersecting boat is no longer treated as a low ceiling that automatically crouches/dismounts the player. Ignore queued mounted positions after dismount. Restore body type, gravity, interpolation and collision state and check native landing clearance. Death, boat destruction and scene changes release the constraints.
- Native passengers no longer inherit the zombie proxy's negative seat offset that buried small sprites under the hull. Use an above-hull height, the actual hurt body's motion root, a local foot anchor and visible native sorting. Restore original sorting on release.
- Retain native AI, attacks, damage, phases and death; do not force-enable renderers hidden by legitimate phases. Keep 0.30.12 performance work, creative-only spawn eggs, bedrock and starter equipment.

The private installation and Windows package are updated together. Close both games and restart through **HollowCraft Launcher.cmd** or the existing desktop entry. Right-click to board; **Shift to dismount**. A/D rowing is not implemented. See [boats and mobs](docs/MC_CREATURES.en.md).

Both bridges compile and package integrity checks pass. No games were launched at the player's request; player riding, different native passengers and release behavior await playtesting.
