# Native alpha.5: animation reference, jump building and fonts

[简体中文](NATIVE_ANIMATION_FIX.md) | [Installation](NATIVE_MIGRATION.en.md)

This release focuses on three Native regressions. Feature parity and a full playthrough remain unverified.

## Combat animation

The installed Better Combat 1.8.5 and PlayerAnimator 1.0.2-rc1 bytecode were inspected. The original PlayerAnimator player was executed offline to generate numerical reference samples.

- These animation files omit `version` and use the legacy format: `torso` means the global `body`. The previous adapter incorrectly twisted only the chest, leaving the head, limbs and their equipment behind.
- Correct default degree conversion, preceding-key easing, virtual boundary keys, angle normalization, looping poses and imported turns.
- Global body transforms precede vanilla model-axis conversion; ModelPart transforms follow it. Apply their respective coordinate conversions, the original 0.7-block global pivot, and block versus 1/16-block units.
- Replace Unity Euler-order decomposition with Minecraft ZYX conversion and Euler-axis transition blending. Retain Better Combat head-pitch ownership, transmission timing and stop transitions.

All 260,232 sampled axes across 29 animations match the original player within 0.0001. All 4,782 articulated reference points match JOML/Minecraft matrices within 0.0001 blocks; maximum error is approximately 0.000070 blocks. These checks establish sampling and coordinate compatibility, not gameplay appearance or feel. Original JARs, disassembly and generated fixtures are excluded from Git and releases. Players still run only the C# mod inside Hollow Knight.

## Jump building

The local gameplay log showed destroyed Unity enemy references throwing during state observation, aborting the rest of that frame's pointer/tower processing. Use Unity lifetime checks before accessing components.

Retain the requested 1.5-block jump assistance, locked underfoot target, native terrain foundations and predicted collision. Cache the support surface before takeoff, recover missed jump notifications from actual upward motion, and check queued placement clearance in physics steps as well as rendered frames. Placement requests carry current feet coordinates; retries consume one block only.

Aim underfoot with a solid block and hold right-click plus Space; `B` remains the assisted tower shortcut. Collision behavior and success rate require player testing.

## Font rendering

Prepare the original provider character tables, 2,413 bitmap glyphs and actual advances from the pinned resource archive. Spaces advance four pixels without drawing atlas pixels. Narrow/accented glyphs, counts and centered labels use their actual width. Use Point/Clamp textures and integer pixel positions/scales. Unsupported characters, including CJK, use a readable system font fallback.

Font resources stay local; the package includes only the extraction script. Running the new installer prepares missing font data for existing installations.

## Delivery checks

The main mod builds successfully. 161 rule/protocol/font checks and 12 launcher checks pass, in addition to the original-player/matrix comparisons above. No game was launched or closed, and original Steam assemblies and player saves were not changed. Visuals, jump-building reliability, boss progression and a complete playthrough await player review.

Developers with the local original JARs and cached build dependencies can run `tools/export-native-animation-reference.ps1`, followed by:

```powershell
dotnet run --project tools/NativeAnimationCheck -c Release -- artifacts/StandaloneAssets/bettercombat artifacts/native-alpha5-reference/samples.json
```
