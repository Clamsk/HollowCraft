# HollowCraft 0.30.6 — Held Items and Native Enemy Hits

[简体中文](试玩说明-0.30.6.md) | [English](PLAYTEST-0.30.6.en.md)

- Campaign selection, scene entry and respawn synchronize the full Minecraft inventory and selected slot, fixing Steve holding an iron sword while the hotbar shows cobblestone.
- Player and HUD render frames carry a scene-entry identifier. Reentering the same room rejects old frames. The vanilla HUD waits for client-held equipment to match the server state before exporting.
- The attack descriptor keeps an enabled idle FSM so native pillars, grass and story objects can locate `damages_enemy`. Previously, disabling the component caused the native lookup to return null; pillar errors were recorded in logs.
- Enemy hits call the target's `HealthManager` directly with a complete native attack source. Damage already accepted by Minecraft is no longer swallowed by the additional native hit-evasion timer.
- Ordinary Minecraft lethal damage finishes the native death process instead of incorrectly treating remaining native health as a new phase. Special death FSMs and actual boss phase health increases remain under native control.
- Both ends log target names, held weapons and health before/after hits to help diagnose enemies with additional damage rules.

This build includes the 0.30.5 scene-transition fixes. Run Stop before upgrading, then use the existing Start launcher. Compilation and release package integrity checks were completed; the games were not launched for validation. Actual behavior is awaiting player feedback.
