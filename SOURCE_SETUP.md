# 源码与本机构建

HollowCraft 当前是 Minecraft 与空洞骑士的本地桥接原型，运行时通过 localhost TCP 通信，不依赖 MCP 服务器。MCP 是开发工具，不参与游戏输入和战斗。

## 依赖

- 合法安装的 Steam 空洞骑士 1.5.12620（Unity 6000.0.61f1）。注入脚本针对该版本，其他版本需要调整。
- Minecraft Java 1.20.1、Fabric Loader 0.16.14、Fabric API 0.92.6+1.20.1。
- Better Combat 1.8.5+1.20.1、player-animation-lib、cloth-config，来自玩家本机的模组安装。
- Windows PowerShell、.NET SDK、JDK 17 或兼容的更新版本、Gradle 8.10。
- Mono.Cecil 及脚本所需的 Modding API 文件，放在本地 dependencies 目录；不随仓库提供。

## 构建入口

1. 阅读 scripts/prepare-assets.ps1、prepare-hk-test.ps1 和 prepare-mc-test.ps1，配置本机游戏及依赖目录。当前 0.8 基线含原开发机路径，不是通用安装器。
2. 运行 scripts/build-hollow-knight.ps1 构建 C# 模块和私有注入程序集。hollow-knight/HollowCraft.csproj 中 GameManaged 指向本机空洞托管程序集。
3. 运行 scripts/build-minecraft.ps1 -Gradle <本机 gradle.bat 路径> -JavaHome <JDK 目录> 构建 Fabric 模组。脚本也可从 PATH、用户的 Gradle 8.10 缓存和 JAVA_HOME 查找依赖；minecraft/build.gradle 可复用本机 PCL libraries 中的两项依赖。
4. 运行两个 prepare 脚本生成 runtime 内的私有试玩副本及素材缓存，再使用 Start HollowCraft.cmd 启动。

不要将 dependencies、artifacts 或 runtime 提交到 Git。私有副本中的硬链接及目录联接可能指向只读原始资源，不应编辑它们。不要将自己的游戏存档替换为试玩存档。

scripts 中的 validate、inspect 脚本和 *Smoke.cs 是旧版本开发工具。当前玩家要求新版本直接交付试玩，因此本次开发只编译和打包，不自动执行这些游戏测试；旧测试记录不构成当前版本的验收结果。

## 第三方资源

Minecraft 与空洞骑士程序、贴图和音效归各自权利人所有，需由用户自己的安装生成缓存。Better Combat 的上游源码与许可见 README 链接；本项目不在源码仓库中重新分发该模组或其动画资源。当前仓库没有授予额外的开源许可。
