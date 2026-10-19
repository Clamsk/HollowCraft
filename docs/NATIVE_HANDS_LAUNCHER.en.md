# Native alpha.8: held-item tracks and loadout launcher

[简体中文](NATIVE_HANDS_LAUNCHER.md) | English

This release addresses weapon binding, shield geometry, landing water, death-item rules and launcher configuration. Existing Better Combat body animation and combo timing are retained. Complete migration and a full playthrough remain unverified.

## Weapons and shields

Better Combat supplies independent `rightItem` / `leftItem` position and rotation tracks. The previous Native renderer applied body joints but omitted these item tracks. They now run after the vanilla grip transform, with coordinate reflection and combo transitions. Meshes with a baked grip use `G * A * inverse(G)` so the grip is not applied twice.

Held shields and tridents now use original static part/pivot/UV descriptions. Blank shields use `shield_base_nopattern.png`. Equipment and skin share the existing pixel depth and transparent-texel test. Switching from an empty hand to a builtin model also re-enables its holder.

## Launcher

Open **HollowCraft Launcher.cmd** or **Start HollowCraft Native.cmd** from the repository or extracted package.

- **Adventure**: play/install, keep inventory, automatic landing water and recovery delay.
- **Starting loadout**: select any of 36 inventory/hotbar slots, four armor slots or offhand to open the picker; right-click clears a slot.
- **Creative item library**: original categories, Chinese/English/ID search, stack counts, potion and enchanted-book variants. Local static models and textures generate block icons without running Minecraft.
- **Boss recording**: boss/difficulty selection; each fight receives a separate survival save and the configured kit.

Six presets cover classic iron, light exploration, ranger, netherite, Clamsk and empty-handed survival. Clamsk includes Zenith and Stardust Wings in the chest slot.

Kits normally apply only to new campaigns and recording sessions. The explicit existing-save checkbox replaces inventory/equipment once per kit revision per campaign. Continuing again or changing survival rules does not reissue equipment. A changed kit creates a new revision.

Preferences live in `hollowcraft.native.settings.json` beside the launcher, with a `.bak` on replacement. Preferences, backups and player saves are excluded from Git and packages. Restart the game to apply changes.

## Survival and landing

Keep inventory remains enabled by default. Disabling it drops backpack, armor, offhand, crafting and carried stacks in the death room for retrieval. Vanishing-cursed items disappear; a successful totem rescue does not drop gear. The option controls items, not the current experience-retention rule.

Survival falls of **at least 12 blocks** deploy water when a water bucket is available; the first new tutorial landing is also protected. Recovery defaults to **two seconds**, with 1/2/3/4/6-second options. Recovery deadlines persist in saves. Occupied foot cells use an available position; slab waterlogging preserves the slab on recovery. Existing natural water is not collected. Disabling the option restores ordinary fall damage. Creative falls **greater than 12 blocks** retain the superhero landing.

Recording hints at the top left now wrap and size to their content; Geo is placed below them.

## Validation and artwork

The main mod builds. 511 pure rule/protocol checks, 12 launcher process checks and 17 loadout/offscreen UI interaction checks pass. Comparison with the locally installed original PlayerAnimator/JOML covers 29 clips, 334,584 axis samples, 4,782 articulated body points and 1,594 item-grip points, all within tolerance. No game was launched or closed. Appearance, input feel and Boss progression remain for player testing.

The launcher uses actual WPF controls over a generated illustration, with `produced by clamsk` at the bottom right. [Artwork file and complete generation prompt](LAUNCHER_ARTWORK.md). Original Minecraft textures, language and animations are prepared locally from pinned sources rather than distributed in Git or ZIPs.
