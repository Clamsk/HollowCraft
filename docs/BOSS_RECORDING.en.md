# Boss Recording Launcher

[简体中文](BOSS_RECORDING.md) | [English](BOSS_RECORDING.en.md)

Run **HollowCraft Launcher.cmd** or **Start HollowCraft.cmd** to open the GUI. First-time users still run **Install HollowCraft.cmd** first. The existing desktop shortcut opens the same launcher.

1. Choose **Boss Recording** and search in Chinese or English.
2. Select vanilla Attuned / Ascended / Radiant difficulty and a 1080p / 720p game window.
3. Click **Start / Switch Boss**. Both games start, a separate recording save is created and the native challenge loads automatically. First launch waits for Minecraft, Hollow Knight and the original Godseeker entry flow.
4. While recording, switch bosses through the launcher without restarting either game. Capture video with your own OBS or other recording tool.

| Action | Result |
| --- | --- |
| F5 | Retry the current boss with fresh MC starter equipment, health, hunger, XP, effects and placed-block state |
| F6 | Return to the Hall of Gods, `GG_Workshop` |
| F8 | Toggle the in-game recording hint overlay |
| MC death-screen Respawn | Prepare another attempt against the current boss |
| Stop & Save | Save and close this tool's game processes; closing the launcher itself leaves games running |

Each fight begins in **survival** with the existing full iron starter kit: iron armour/sword, bow, 64 arrows, 64 steak, 64 pearls, 192 cobblestone, 64 oak logs, flint and steel, 64 TNT, water/lava buckets, shield, iron axe/pickaxe, 64 torches, 64 iron ingots, 64 golden apples and two totems, plus 64 bedrock. Hotbar slots 1–9 are iron sword, bow, golden apples, cobblestone, water bucket, lava bucket, TNT, flint and steel, ender pearls; arrows and remaining supplies stay in the backpack. Bedrock cannot be mined in survival but can be removed in creative; retrying a recording resets placed blocks. The creative-only trident is not automatically granted. Durability, consumption, hunger, blocking and damage remain active.

The catalogue contains **45 original Godhome statue challenge entries**, including Hornet, Grimm, Mantis Lords, Pure Vessel, dream variants, Sisters of Battle and The Eternal Ordeal. Difficulty selects the original tier-specific arena, including changed Ascended/Radiant layouts. Two additional **story arenas**, Hollow Knight and Radiance, offer standard difficulty only and have not been accepted in gameplay, including their story/ending timing. Challenge entries include special modes such as The Eternal Ordeal and are not a count of distinct boss species.

Local recording saves use `runtime/hk-boss-saves/<session ID>`. Every fight gets a fresh MC campaign identity. Normal play retains its existing save, equipment and progress. Stop normal play before starting recording. Switching/retrying discards the current fight and that fight's equipment changes to restore the starter kit.

The reported Gruz Mother brood/gate issue concerns the normal-map `Crossroads_04` encounter. Its original Godhome variant uses a different sequence, so assess that gate fix in a normal campaign.

PowerShell 5.1 GUI initialization, the 47-entry catalogue and both builds were checked without launching games. Complete runtime challenge/story compatibility remains unverified.
