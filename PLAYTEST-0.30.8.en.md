# HollowCraft 0.30.8 — Playtest Notes

[简体中文](试玩说明-0.30.8.md) | [English](PLAYTEST-0.30.8.en.md)

- Audit all local original Hollow Knight scene/resource files: 1,003 serialized files. See the repository's `docs/ENEMY_AUDIT.en.md` and actor inventory for the exact scope. This is an offline resource/script audit plus existing log review, without launching either game or playtesting every enemy.
- Export the original `IsBlockingByDirection` results separately for melee and ranged/spell damage in all four directions. A native invincibility flag no longer automatically makes the MC proxy immune from every direction and damage type. Directional armour, Spell Vulnerable exceptions, encounter introductions and phase invulnerability remain native-owned; blocked hits also produce native feedback.
- Let native scenes own proxy lifetime, recover removed proxies for living native enemies, and retain lethal proxies until native death confirmation or a new phase. Contact overlaps are valid on both sides of the bridge. Rejected melee hits log missing/dead proxies or reach failures.
- Exclude disabled, dead and zero-health phase components when choosing a health responder. Use enabled native colliders and keep original ordinary/special death handling. The audit includes False Knight/Failed Champion phases, other bosses and armoured enemies.
- Arrows damage and pass through native breakables without consuming the arrow or a piercing slot. Native grass/pole responders also react. Solid native walls and solid Minecraft blocks still stop arrows. Scuttlers retain their original one-hit responder; regular arrows are consumed on impact.
- Pointing at an MC block now gives mining priority over an overlapping enemy. The window click queue preserves hold state. Server mining adds vanilla breaking speed per tick, including hardness, tools, effects and airborne/water penalties, and reports progress to the UI. Release, slot changes, loss of reach and scene changes cancel mining. Creative clicks break immediately, subject to original item restrictions.
- Fix the Better Combat fallback animation namespace and use a vanilla hand swing when an animation is missing, preventing null-animation errors for non-specialized weapons/tools such as tridents.

Stop both games before updating, then use the existing **Start HollowCraft.cmd**. Both ends must be updated together. Existing campaigns remain usable. Start feedback with an iron-pickaxe mining check, False Knight and armoured/flying enemy combat, then arrows through grass, poles and breakable decorations.

Compilation, offline checks and package integrity only: no game was launched for verification. A complete survival playthrough and every boss's actual phase timing remain unverified.
