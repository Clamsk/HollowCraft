# Boats and Minecraft Creatures

[简体中文](MC_CREATURES.md) | [English](MC_CREATURES.en.md)

0.30.13 addresses player riding jitter and native passengers hidden by the hull. Bridge boats use server-owned physics; riding suspends native player locomotion, gravity, jumping and collision with that boat. Native passengers use corrected height, local body anchors and foreground sorting while retaining native AI, damage and death. **Shift explicitly dismounts**; release restores collisions, physics and original rendering and checks player clearance.

0.30.12 updates spawn clearance, offscreen cameras and client tracking fallback. For invisible models or frame drops, see [performance and troubleshooting](PERFORMANCE.en.md).
Version 0.30.11 creates real Minecraft 1.20.1 entities. Minecraft's entity renderers provide transparent frames of their actual 3D models, materials and animation for the Hollow Knight scene. Boats and mobs are not replaced with item icons.

## Boats

Select a boat and right-click a clear location near the floor or water. Leave room for its original width. Boats fall onto floors or float in water. Vanilla wood boats, bamboo rafts and chest variants are supported; ordinary boats have two seats and chest variants one. Chest variants can open their original container screen.

Native enemies with health/hurt bodies and recognized NPCs/small mobile creatures automatically board a touching boat with room. Native actors bypass vanilla's width restriction so large bosses can board. Shared motion roots prevent multiple hurt parts taking multiple seats. Movement segments also detect fast crossings.

Native body positions are constrained without disabling complete FSMs, leaving original attacks, damage, phases and death under Hollow Knight's control. Native characters retain their 2D artwork while boats use MC models. Captures persist outside the camera view; death, boat destruction or scene changes clean up constraints. Native actors reconstructed by a scene reload are not guaranteed to retain their previous capture identity.

Directional attacks hit passengers normally. Point directly at the boat before left-clicking to destroy it. Players can right-click a free boat and use Shift to dismount; A/D paddling controls are not added. Actual Minecraft passengers use vanilla riding model poses.

Fresh campaigns and boss-recording attempts include one oak boat in the backpack. Old campaigns receive it once without changing the nine-slot hotbar. Vanilla workbench recipes and creative items remain available.

## Minecraft mobs

In creative, press E and use the Spawn Eggs category/search, then select an egg and right-click. **No eggs are added to survival starter inventories.** [creatures.json](../release/creatures.json) lists local vanilla spawn-egg resources; it is a static inventory, not evidence of individual gameplay acceptance.

Animals, hostile, aquatic and flying mobs and villagers use actual MC entity implementations. Models, baby/variant data, health, armour, attacks, loot, XP, feeding, breeding/taming interactions and container requests use their corresponding vanilla behavior. Melee can target these entities; arrows, potions, explosions and environmental damage are processed in Minecraft. Right-click first attempts an original entity interaction, then placement/item use.

Movement goals, collision depth and pathfinding thickness are constrained to the XY plane while original width/height are retained. Native terrain around these entities is synchronized; simulation pauses outside that region or before terrain arrives. The backend uses fixed night to avoid sunlight being invented inside Hollow Knight caves, while entity presentation uses clear MC model lighting.

Complex 3D navigation, additional mount-driving controls, structure/dimension dependencies and special MC boss flows still need adaptation. This is not a claim that every creature's entire gameplay is complete. Normal-campaign MC entities save in the private MC world; boss retries use a new campaign identity and room region, leaving old summons behind.

## Playtesting

Compilation and package checks only; no game was launched. Test a normal enemy/boat, then different native bosses through the launcher, plus cows, sheep, zombies, skeletons, spiders, wolves, villagers, fish and flying mobs. Report entity/scene, steps and both logs for failures.
