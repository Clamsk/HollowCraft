# Native alpha.7: world rules and creature interactions

[简体中文](NATIVE_SYSTEMS.md) | [Installation and migration table](NATIVE_MIGRATION.en.md)

Runtime remains a single Hollow Knight process. Updating does not launch Minecraft/Java, patch the Steam original, or reset native saves.

## Added in this release

- Persistent local fluid generations with separate 5/30 tick water/lava clocks, falling streams, source regeneration, recession, lava/water products, sponge absorption, slab waterlogging, bucket recovery and player currents. Work is limited to the active area; distant room data is retained.
- Attenuating dust, directional delayed repeaters and side locking, container comparator output/subtraction, inverting torches, delayed lamps, timed buttons, pressure plates and observer pulses.
- Atomic piston line validation, 12-block push limit, sticky retraction, protected blocks/native terrain/storage constraints, head cleanup and occupant displacement. Right-click repeaters/comparators to configure them.
- Eight-tick hopper transfers with container/drop intake, furnace/brewing side rules and redstone locking. Edge-triggered droppers/dispensers support inventory transfer, ejection, selected projectiles, buckets and TNT.
- Collision-aware local path nodes, one-block ascents, ledge restrictions, swimming/flying nodes, shared calculation budgets and ranged line-of-sight checks.
- Job-site villager professions, an inventory-backed trade selector, persistent uses, experience/level unlocks and restocking near the job. Data includes 1,148 deterministic original factory samples; three live-world/entity-dependent factories are explicitly excluded. Refreshing never rerolls offers.
- Tamed-wolf following/sitting, allied golem/wolf combat, slime/magma splitting, eggs, sheep grazing/regrowth, drowning, Enderman water response, pufferfish poison, dolphin grace, witch healing and selected Warden/guardian/shulker abilities.
- Original part/pivot/UV descriptions and installed textures for all 18 boats/chest boats, including bamboo rafts; existing capture and passenger behavior is retained.
- Dynamic repairs, leather/shulker dye, shield banners, banner/book/map copying, tipped arrows, firework stars/rockets and suspicious stew. Relevant NBT, durability and crafting remainders survive.
- Packed shulker inventories and shared persistent Ender storage; nested shulkers are rejected. Collect-all cannot steal computed workstation or merchant outputs.
- Common conditions/functions from 925 original block loot tables, including tool predicates, Silk Touch, Fortune, states and explosion decay. Common custom Java potion NBT effects work through drinking/splash/cloud/arrow paths.
- Mending, Thorns, Frost Walker, Depth Strider, Soul Speed, Respiration, Binding and Fire Aspect paths. Basic tilling, four crops, growth, bone meal and original growth-stage textures.

## Updating

Run `Install HollowCraft Native.cmd`, then `Start HollowCraft Native.cmd`. Preserve saves. Static numerical trade/model descriptions are included; game binaries, JARs, extracted pixels/audio and personal data are excluded. Enter `Clamsk` with two free inventory slots for Zenith and Stardust Wings; equip wings in the chest slot.

## Limits

**Feature parity is not complete.** Navigation is a 2D adaptation. Full vanilla goals, abilities/variants/equipment, villager reputation/discounts and world-dependent trades remain incomplete. Redstone lacks full slime/honey assemblies, quasi-connectivity, every dust connection and machine rule. Fluids lack complete slope selection, bucketed entities and physics details.

Remaining work includes all block states/models/shapes, loot predicates, complex NBT/workstations, complete map/banner visuals, remaining enchantment/projectile rules, Bendy deformation, particles/audio, bridge-save import and full gameplay/performance acceptance. Some abilities reuse generic projectile presentation. Offline checks do not establish visual or full-playthrough acceptance. No game was launched, closed or controlled for this release.
