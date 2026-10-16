# HollowCraft 0.30.18: boss phase handshakes and larger wings

[简体中文](试玩说明-0.30.18.md) | [English](PLAYTEST-0.30.18.en.md)

- Addresses the latest log stopping Radiance at 1999 native HP. While the original phase FSM waits for transition events such as `RAGE START` or `ARENA 2 START`, damage is held at current HP instead of an unreachable future checkpoint.
- Damage resumes when the controller can consume it. A missed check after an accepted boundary-crossing hit is replayed only into its native HP controller. Stale damage rejected by a native phase or guard also reconciles both HP copies. Logs now include phase state, HP and readiness.
- Bosses with these phase controllers share a minimum damage interval of about 0.2 seconds, preventing simultaneous Zenith swords from racing the phase flow. Zenith retains 190 base damage and 14% critical chance; ordinary targets keep their existing local hit timer.
- Bridges the native Radiance final nail collision after server-confirmed melee or swept Zenith contact. HP is preserved for this sequence only when the original controller explicitly uses a `FINAL HIT` nail trigger. Menu clicks, distant empty swings and ordinary arrows do not directly finish the fight.
- Recognizes both `Dream_Final` / `Dream_Final_Boss` story-scene names for ending input. F5 resets through the workshop before reloading, avoiding reuse of a live scene's scripted state.
- Wing bones are 45% larger and fold down behind the torso. Equipped wings use a 384 × 384 transparent frame while Steve remains at 64 pixels per block; removing them restores 256 × 256. Both bridges handle the dimensions together, preventing clipping or shrinking the player.

Fully exit the old build and reopen **HollowCraft Launcher.cmd**. Retry a stalled fight with **F5** or re-enter it. Update both the Minecraft and Hollow Knight bridges. Enter Clamsk through T or / to obtain the equipment; see the [Easter egg guide](docs/TERRARIA_EASTER_EGG.en.md).

Both bridges compile and package integrity is checked. Per the player's request, neither game nor an automated boss fight is launched. Actual phase progression, final defeat and visual proportions await player testing.
