# HollowCraft 0.30.10 — Playtest Notes

[简体中文](试玩说明-0.30.10.md) | [English](PLAYTEST-0.30.10.en.md)

- Fresh campaigns and each boss recording put these items in hotbar slots 1–9: iron sword, bow, golden apples, cobblestone, water bucket, lava bucket, TNT, flint and steel, ender pearls. Arrows and all other existing equipment/supplies remain. Cobblestone totals 192; pearls total 64 across four legal stacks of 16.
- Add 64 bedrock to the backpack. Existing campaigns receive it once and swap existing item stacks into the new hotbar order, retaining counts, durability, enchantments and NBT. Consumed old supplies are not replenished. Grant/layout flags survive saves, reconnects and death.
- Continuously sweep native enemy bodies against bedrock, including dynamic, kinematic, trigger and direct-transform movement, retaining wall sliding. Notify only collision/landing transitions actually present in the original FSM; no death or phase-completion event is invented. Ordinary blocks retain their existing behavior.
- Resolve overlaps when a native phase body becomes active; reconcile enemy movement and bedrock visibility before native contact damage. Reject bedrock placement into cells occupied by a live enemy proxy.
- Prevent bedrock mining in survival; creative can directly remove it. Retain vanilla blast resistance and piston immovability. Blast exposure uses the XY plane when bedrock intersects rays to a bridged target. Fully shielded targets receive no explosion damage, including vanilla's zero-exposure minimum damage; partially exposed bodies use the computed exposure fraction.

Stop both games before updating, then open **HollowCraft Launcher.cmd**. Private runtimes, source and the Windows playtest package are updated together. Boss retries get a new campaign identity, resetting previous placed bedrock; normal campaign rooms retain placed bedrock.

Compilation and package integrity checks only; no game was launched. Please report native boss charges and TNT shielding behind bedrock walls. See the [boss recording guide](docs/BOSS_RECORDING.en.md).
