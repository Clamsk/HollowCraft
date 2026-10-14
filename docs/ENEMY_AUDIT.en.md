# 0.30.8 — Original Enemy Damage Audit

[简体中文](ENEMY_AUDIT.md) | [English](ENEMY_AUDIT.en.md)

On 2026-10-14, inspect the local Steam Hollow Knight 1.5.12620 / Unity 6000.0.61f1 installation offline. Read every `level*`, `sharedassets*.assets` and `resources.assets` serialized file and inspect original `HealthManager.Hit`, `IsBlockingByDirection`, `HitTaker` and `ScuttlerControl.Hit` code. No game was launched.

The scan reads 1,003 files, 2,558 HealthManager components and 74 ScuttlerControl components, with zero read errors. The [actor inventory](enemy-coverage-0.30.8.csv) contains 1,083 names across ordinary, infected, dream, Colosseum and Godhome scenes. Names include numbered instances, phase copies and prefabs; they are not 1,083 distinct species. The inventory lists native HP/configuration; bridge conversion still determines MC health.

| Check | Serialized evidence | Handling in this version |
| --- | --- | --- |
| Initially invincible | 260 health components | Query original guards instead of making the MC proxy universally invulnerable |
| Directional protection | 39 components, serialized direction values 4 and 7 | Separate four-direction melee/spell masks, original Spell Vulnerable exceptions and native blocked-hit feedback |
| Special death | 106 components | Keep original Hit and special-death FSMs; lethal MC proxies wait for native death confirmation or a new generation |
| Multiple health components | 26 health components share their actor with another HealthManager | Resolve the nearest enabled/living/positive-health responder, without damaging every duplicate |
| No collider on the health object | 72 components, including Plant Trap and Moss Charger | Use the queried child collider; prefer an enabled collider on the health object when available |
| Native one-hit actors | 74 ScuttlerControl components | Original melee HitTaker path; arrows invoke original one-hit death and consume regular arrows |
| Proxy lifetime, size and reach | Shared code path | Native-owned lifetime, removed-proxy recovery, contact overlap accepted by both ends, logged rejection reasons |

Configurations reviewed include False Knight, Failed Champion, Mantis Lords, Soul Tyrant's second phase, Hornet, Hollow Knight, Radiance and Godhome variants. The reported False Knight uses special-death/phase FSMs and must not be force-killed through armour or a scripted phase. Existing logs show its target ID arriving without a matching target-damage log. This release addresses shared removed/dead proxy and guard handling and logs missing proxies, phase waits and reach failures; the old logs do not establish a unique cause for that instance.

All HealthManager actors share vanilla MC combat arithmetic and original native guard/hit/death interfaces, rather than an enemy-name allowlist. Other original responders are routed according to their interfaces: Breakable, BreakablePole, Grass and ScuttlerControl. Arrows cut grass/poles and damage breakable decorations while solid walls and MC blocks retain collision.

This audit establishes static configuration and integration coverage, not acceptance of every runtime boss phase. Actual enemy behaviour, summoned/pooled instances, phase timing, rewards and complete survival campaign support still require player feedback.

Install UnityPy / TypeTreeGenerator separately, then reproduce with:

```powershell
python scripts/audit-hk-enemies.py --data '<your Hollow Knight folder>/hollow_knight_Data' --libs '<UnityPy directory>' --out artifacts/enemy-audit.json --coverage-csv artifacts/enemy-coverage.csv
```

Raw fields and disassembly remain in ignored local artifacts. The repository and release contain only the audit script, summary and actor statistics, not original assemblies, scenes or assets.
