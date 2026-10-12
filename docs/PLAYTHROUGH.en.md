# Campaign Compatibility Status

[简体中文](PLAYTHROUGH.md) | [English](PLAYTHROUGH.en.md)

The goal is to complete Hollow Knight while retaining its native maps, story, enemy phases and endings, using Minecraft's player, equipment, building and survival rules. Compilation only proves that the current interfaces build; it does not prove campaign completion. By the player's request, new versions from 0.8 onward have not been launched for automated gameplay acceptance.

| Required area | Current implementation | Evidence and work still needed |
| --- | --- | --- |
| New saves, introductory landing and campaign identity | `CampaignSession.NewGame`, `CampaignStore.select`, automatic water on the first grounded pose; 0.30.3 opening camera follow and a 30-tick water recovery delay | Different save slots, recreating the same slot, late backend connection and restart playtests |
| Scene transitions, building and movement | 0.30.5 restores native gravity/size before exit and waits for `HeroController.FinishedEnteringScene`; per-entry identifiers reject old messages; exact feet coordinates, low-entrance height protection and local overlap recovery | Left/right and vertical exits, doors, repeated visits, narrow passages and Minecraft blocks near entrances; complex elevators and special cutscenes |
| NPCs, benches, transport and dialogue | W sends native upward input; `CampaignBridge`, `NativeChoice`, `NativeShop`; W / Space leaves a bench | Native FSM event variants, shop confirmation, save benches, stations and story interactions |
| Dream Nail and progression flags | R retains native `dreamNail` input; native `PlayerData` unlock flags remain; 0.22 pauses Minecraft controls during native ownership and preserves release events and scripted movement | Obtaining Dream Nail, dream entrances/bosses/exits and Essence progression |
| Ordinary enemies, bosses and breakables | Minecraft mob proxies calculate damage; 0.30.6 calls enemy `HealthManager` directly, registers the attack FSM and repairs ordinary lethal hits; breakables use native `HitTaker` / trigger events while special death and phase FSMs remain native | Multiple phases, shields, summons, invulnerability windows, special damage triggers and death rewards; ordinary enemies are insufficient coverage |
| Grass and trigger-based objects | `CursorCombat` supplies the native `Nail Attack` tag and `OnTriggerEnter2D` | Local `GrassCut.ShouldCut` code reads the tag; actual grass variants, pillars and mechanisms need playtesting |
| Tutorial gate | 0.30.3 supplies `damages_enemy` fields `damageDealt`, `attackType` and `direction` to native `ReceivedDamage` / Great Door FSMs | Local tutorial data confirms the FSM lookup and transition to Town; actual damage phases and the cutscene need playtesting |
| Hazards, death, respawn and drops | Minecraft damage/drops; native `ReadyForRespawn` and bench checkpoints; 0.22 retains native cutscene/story damage exemptions and excludes scripted displacement from Minecraft fall damage | Acid, spikes, out-of-bounds falls, death sequences and item recovery need native/Minecraft state consistency checks |
| Late-game survival traversal | Equipment, enchantments, potions, bows, pearls, water buckets, building and elytra interfaces exist | Native ability gates and Minecraft alternatives are not adapted room by room; building does not establish compatibility with every story gate |
| Dreamers, Black Egg and endings | Native story and progression flags have not been removed | All three Dreamers, opening the Black Egg, final battles and ending animations lack sufficient compatibility evidence |

Next work prioritizes UI/interaction gaps, native terrain and projectile timing, then story gates, special bosses and cutscenes. Until player evidence establishes otherwise, complete campaign support remains an unverified goal.
