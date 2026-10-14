# HollowCraft 0.30.9 — Playtest Notes

[简体中文](试玩说明-0.30.9.md) | [English](PLAYTEST-0.30.9.en.md)

- Repair the normal-map Gruz Mother brood counter race. Retain its seven linked native health responders after destruction; if all seven are natively dead and the original Battle Control is in Start with a leftover count, reconcile that count. Original FSMs still control gates, waves and animation.
- Add idempotent native death/late battle-binding bookkeeping compensation using the original notifiedBattleScene flag. Unconfirmed boss phases are excluded; diagnostics record repairs.
- Track falling on native physics frames and reliably report each physical landing or grounded jump boundary. New-protocol fall damage no longer accumulates descent across 50 ms poses or settles the same landing twice. Scene changes, flight, gliding, water, climbing and pearl teleports reset old fall state. Real fall damage, water clutches and creative superhero landings remain.
- Add a bilingual GUI launcher for normal play, 45 Godhome challenge entries and two story boss arenas, original difficulty tiers, 1080p / 720p, live boss switching and Stop & Save. Start HollowCraft.cmd and the existing desktop entry open the launcher.
- Separate recording saves; every fight restores survival and the complete starter kit. F5 retries, F6 returns to the Hall of Gods, F8 hides hints, and death-screen Respawn prepares another attempt. See the [recording guide](docs/BOSS_RECORDING.en.md).

Stop both games before updating, then run **HollowCraft Launcher.cmd**. Normal campaigns remain usable and separate from recording saves. Evaluate the Gruz Mother brood/gate fix in a normal campaign; the original Godhome encounter differs.

Compilation, PowerShell 5.1 control initialization and package integrity checks only. No game was launched; gates, repeated sprint-jumps, every recording entry and story/ending timing await player feedback.
