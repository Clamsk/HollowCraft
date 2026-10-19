# 第三方组件与素材来源

[简体中文](THIRD_PARTY.md) | [English](THIRD_PARTY.en.md)

Native 安装包仅包含空洞端 HollowCraft.dll、脚本、注册表和模型数值描述、可选素材导入器源码与文档。独立版静态资源使用 [native-dependencies.lock.json](native-dependencies.lock.json) 中的来源；纹理与动画归档固定版本并校验 SHA-1，原版音频对象按资源索引的 SHA-1 校验，再以 SHA-256 命名供本地加载。本机已有资源可就地读取，缺失时从官方资源源获取，不要求安装或运行 MC。提取的纹理、音频、动画与原始游戏文件不进入 Git 和发布 ZIP。release/native-creature-models.json 包含离线整理的模型关节、坐标和 UV 数值描述及来源标记，不含可执行游戏代码或贴图像素。release/native-trades.json 记录固定版本交易工厂的确定性数值样本，并列出因需要实际世界／实体上下文而排除的工厂；不含原版游戏程序。

Windows 发布包仅包含 HollowCraft 自编译的两端 Mod、安装脚本、素材导入工具和文档。Minecraft、空洞骑士、修改后的原游戏程序集、提取的纹理/音频/动画、用户存档以及下列依赖二进制不随发布包分发。

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


## 泰拉瑞亚素材导入

泰拉瑞亚贴图与音效属于 Re-Logic，只从玩家自己的安装目录导入，不随 Git 或发布包分发。可选导入工具以源码随包提供；其中 MonoGame LZX 解码器保留 Stuart Caie / Ali Scissons 原始声明并采用 MS-PL 许可选项，流封装使用 MonoGame 的 MS-PL，仅替换一处异常类型以独立编译。见 [代码来源](../tools/TerrariaAssetImporter/SOURCE.md) 与 [完整许可](../tools/TerrariaAssetImporter/LICENSE.txt)。
