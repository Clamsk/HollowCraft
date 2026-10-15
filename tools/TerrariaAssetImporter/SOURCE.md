# Local XNB import support

`Importer.cs` is HollowCraft's local-only texture/audio importer. It reads the player's Terraria installation and writes PNG/WAV files into private runtime folders. No game content is included here.

`LzxDecoder.cs` and `LzxDecoderStream.cs` were obtained from the official MonoGame repository on 2026-10-15:

- https://github.com/MonoGame/MonoGame/blob/develop/MonoGame.Framework/Content/LzxDecoder.cs
- https://github.com/MonoGame/MonoGame/blob/develop/MonoGame.Framework/Utilities/LzxStream/LzxDecoderStream.cs
- https://github.com/MonoGame/MonoGame/blob/develop/LICENSE.txt

The decoder retains its original Stuart Caie (2003–2004) and Ali Scissons (2011) copyright and dual-license header; HollowCraft distributes it under the **Microsoft Public License (MS-PL)** option. The stream wrapper is covered by MonoGame's MS-PL. The complete MS-PL text is in `LICENSE.txt`.

HollowCraft modification to the stream wrapper: replace MonoGame's `ContentLoadException` with `System.IO.InvalidDataException`, permitting standalone compilation without the MonoGame runtime. The decoder is otherwise unmodified. Imported Terraria content belongs to Re-Logic and is not covered by this code license.
