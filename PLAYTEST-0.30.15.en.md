# HollowCraft 0.30.15: chat Enter submission fix

[简体中文](试玩说明-0.30.15.md) | [English](PLAYTEST-0.30.15.en.md)

Fixes Return failing to submit Clamsk. Enter is handled before the text field consumes the event, with independent physical-key detection. Main Enter and numpad Enter share one submission method, preventing duplicate grants after chat closes. IME candidate confirmation remains available, and gameplay shortcuts stay blocked for the closing frame.

Stop the old build, update/install, then open **Start HollowCraft.cmd**. Press **T** or **/** in gameplay, enter **Clamsk**, and press Enter. Keep two backpack slots empty. See the [Easter egg guide](docs/TERRARIA_EASTER_EGG.en.md) for Zenith and Stardust Wings controls.

Both bridges are compiled and package integrity checked. Per the player's request, no game was launched; chat submission awaits playtesting.
