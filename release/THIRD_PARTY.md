# 第三方组件与素材来源

[简体中文](THIRD_PARTY.md) | [English](THIRD_PARTY.en.md)

Windows 发布包仅包含 HollowCraft 自编译的两端 Mod、安装脚本和文档。Minecraft、空洞骑士、修改后的原游戏程序集、提取的纹理/音频/动画、用户存档以及下列依赖二进制不随发布包分发。

安装器按 `dependencies.lock.json` 的固定版本与哈希值，从官方发布源获取依赖。Fabric Maven 的运行库使用其官方 SHA-1；Modrinth 依赖使用官方 SHA-512；Mono.Cecil 使用从官方 NuGet 包计算并锁定的 SHA-512。

| 项目 | 固定版本 | 官方来源 |
| --- | --- | --- |
| Fabric Loader | 0.16.14，Minecraft 1.20.1 | [Fabric](https://fabricmc.net/)、[源码](https://github.com/FabricMC/fabric-loader) |
| Fabric API | 0.92.6+1.20.1 | [Modrinth](https://modrinth.com/mod/fabric-api)、[源码](https://github.com/FabricMC/fabric) |
| Better Combat | 1.8.5+1.20.1-fabric | [Modrinth](https://modrinth.com/mod/better-combat)、[源码](https://github.com/ZsoltMolnarrr/BetterCombat/tree/1.20.1)，Daedelus，GPL-3.0 |
| Player Animator | 1.0.2-rc1+1.20-fabric | [Modrinth](https://modrinth.com/mod/playeranimator)、[源码](https://github.com/KosmX/minecraftPlayerAnimator) |
| Cloth Config | 11.1.118+fabric | [Modrinth](https://modrinth.com/mod/cloth-config)、[源码](https://github.com/shedaniel/cloth-config) |
| Mono.Cecil | 0.11.6 | [官方 NuGet](https://www.nuget.org/packages/Mono.Cecil/0.11.6)、[源码与许可证](https://github.com/jbevain/cecil) |
| ASM | Fabric Loader 配套 9.8 | [官方项目](https://asm.ow2.io/)，具体模块与校验值见锁定清单 |

游戏素材由安装器从玩家自己的 MC 1.20.1 与已下载 Better Combat 中提取，仅留在本地运行目录。Minecraft 属于 Mojang/Microsoft；空洞骑士属于 Team Cherry。HollowCraft 是非官方项目，与上述开发者没有隶属或背书关系。
