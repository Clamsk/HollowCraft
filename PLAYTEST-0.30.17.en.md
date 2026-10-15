# HollowCraft 0.30.17: rear wing folding and Radiance combat proxies

[简体中文](试玩说明-0.30.17.md) | [English](PLAYTEST-0.30.17.en.md)

- Corrects mirrored wing hinges folding forward across the chest. The roots sit outside the back armor; folding, spreading and flapping remain rearward and follow the MC torso rig.
- Addresses logs showing living Radiance proxies repeatedly removed: refresh temporary tickets for their occupied chunks, shared within each chunk and released when observation or the scene ends. The reduced backend view distance remains.
- Native proxies are not saved as ordinary MC zombies. Recovery retains damage awaiting native acknowledgement and logs removal reasons, positions and rejected spawns.
- MC left click reaches the original Radiance ending's `ListenForAttack` actions. Only an enabled action's authored event accepted by its current state is sent, including while native story control owns movement. Menus/pause still block it. No victory or ending state is directly changed.
- Retains the previous native HP phase gates and Zenith's 190 base damage / 14% critical chance.

Logs also show the failure after trident attacks. This build addresses proxy loading and scripted ending input; actual phase behavior still needs player testing. See the [Easter egg guide](docs/TERRARIA_EASTER_EGG.en.md).

Both private bridges and the Windows package are updated. Fully exit the old build, launch **HollowCraft Launcher.cmd**, and retry a stalled recording fight with **F5** or re-enter it. Continue left-clicking when the final scripted sequence requests an attack.

Both bridges compile and package integrity is checked. Per the player's request, neither game nor an automated boss fight was launched.
