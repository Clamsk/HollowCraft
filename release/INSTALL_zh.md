# HollowCraft 0.30.15 安装与配置

[简体中文](INSTALL_zh.md) | [English](INSTALL_en.md)

0.30.15 修复聊天栏回车提交。按 T 或 / 打开聊天栏，输入 Clamsk，再按主键盘回车或小键盘回车领取天顶剑与星尘之翼。更新前先停止旧版本，泰拉瑞亚素材继续从本机导入；见 [彩蛋指南](../docs/TERRARIA_EASTER_EGG.md)。本版直接交付玩家试玩，未启动实机验证。

这是 Windows 64 位试玩包。它把本机 Minecraft 的物品、数值与三维人物接入空洞骑士；游玩时两端都需要运行。完整生存通关仍在适配，本版编译后交给玩家试玩，未进行实机验收。

## 准备

- 自己的 Steam《空洞骑士》**1.5.12620**，保持 Steam 登录。其他版本会在安装时拒绝打补丁。
- 自己的 Minecraft Java **1.20.1**。先通过 PCL 或其他启动器启动一次原版 1.20.1，让游戏、资源、运行库完整下载，再关闭它。
- **64 位 Java 17 或 21**。启动器已有 Java 可以使用；若没有，可从 [Microsoft OpenJDK](https://learn.microsoft.com/java/openjdk/download) 获取 21 版。安装器会寻找 PATH、JAVA_HOME 和常见安装目录中的 Java；找不到时输入 `java.exe` 完整路径。
- 首次安装能访问 Fabric、Modrinth 和 NuGet 官方下载地址。此包不含原游戏、账号或依赖的副本。

## 安装

1. 在 [GitHub Releases](https://github.com/Clamsk/HollowCraft/releases) 下载 **HollowCraft-v0.30.15-windows.zip**。GitHub 自动生成的 Source code 压缩包是源码，需要自行构建。
2. 完整解压到自己的可写目录，例如 `D:\Games\HollowCraft`。不要直接从 ZIP 内运行，也不要放进 Steam 或 `.minecraft` 原目录。Windows 内置 PowerShell 即可，无需 Codex、Git 或开发工具。
3. 双击 **Install HollowCraft.cmd**。它寻找 Steam 游戏目录和 `.minecraft`；未找到时按提示输入目录。Minecraft 目录必须包含 `versions\1.20.1\1.20.1.jar`，不是启动器 EXE 所在目录。
4. 等待下载、校验和准备结束，看到 `installed`。安装器只准备文件，不启动游戏。
5. 保持 Steam 登录，双击 **HollowCraft Launcher.cmd**（**Start HollowCraft.cmd** 也会打开同一启动器），选择普通游玩的启动按钮。等待 Minecraft 自动进入独立世界，保持其窗口运行；然后在空洞窗口新建或读取试玩存档。

录制演示可选择启动器的 **Boss 录制** 页，搜索并选择 Boss、难度与分辨率，点击开始。首次启动会自动创建独立录制存档；每场刷新为生存初始装备。**F5** 重打，**F6** 回神居雕像大厅，**F8** 隐藏画面提示。详见[录制指南](../docs/BOSS_RECORDING.md)。

程序以本地独立世界运行，网络登录不参与桥接。安装包没有账号密码或访问令牌；原启动器配置不修改。

## 指定目录

自动找到的目录不合适时，可在解压目录打开 PowerShell，执行：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\install-hollowcraft.ps1 -GameRoot "D:\SteamLibrary\steamapps\common\Hollow Knight" -MinecraftRoot "D:\Games\MC\.minecraft" -JavaPath "C:\Program Files\Microsoft\jdk-21\bin\java.exe"
```

安装器生成 `hollowcraft.local.json`，参考格式见 `release/hollowcraft.example.json`。配置只保存在自己电脑，不上传。已有运行副本需要保持与同一原游戏目录关联；更换原目录时解压到新文件夹，再安装。

## 退出与升级

先在空洞长椅保存进度，再运行 **Stop HollowCraft.cmd**。它只关闭本文件夹记录的两端进程，并要求 MC 保存确认；保存失败时保留 MC 窗口，请正常关闭该窗口。空洞剧情进度仍以长椅保存为准。

升级时先 Stop，备份下面两个目录，再将新版 ZIP 的 `HollowCraft` 文件夹内容覆盖到现有安装目录，重新运行 Install、Start。安装器保留 `runtime` 存档和本地路径配置，不会清空背包。

- `runtime\hk-test-saves`：桥接空洞存档和剧情进度。
- `runtime\minecraft\saves`：独立 MC 世界及按空洞存档区分的物品进度。
- `runtime\hk-boss-saves`：独立录制存档；普通游玩不读取此目录。

资源运行副本使用目录联接、硬链接或跨盘文件复制。不要在 `runtime\Hollow Knight` 内编辑原生游戏资源；先退出两端再移动整个包。删除整个包之前可单独备份这两个存档目录。

## 常见问题

- **不支持的空洞版本**：当前编译仅对应 Steam 1.5.12620，不强行打补丁。
- **缺少 Minecraft library / natives / assets**：用原启动器先运行一次原版 1.20.1，再重新 Install。
- **下载失败或校验失败**：恢复网络后重新 Install，已校验的依赖会复用。不会继续使用损坏的下载。
- **旧版本仍运行 / 31674 被占用**：先 Stop；对旧版窗口正常退出后再 Start，不要同时启动两份包。
- **中文路径编码错误**：Java 参数按所选 Java 的本地编码生成；若路径包含该编码不能表达的字符，可改到简单英文目录。
- **安装过程中失败**：窗口会显示具体原因；修正后重新 Install。不要从源码 ZIP 直接使用安装器，Windows 发布包包含所需的预编译文件与校验清单。

反馈时附上版本、复现步骤和 `runtime\hollowcraft-hk.log`、`runtime\minecraft\logs\latest.log`。不要上传原游戏、完整存档或账号数据。

完整操作表见 README.md。本版建议先手持圆石，按住 **B** 尝试辅助垫高，再尝试空格加脚下右键的普通跳搭。首次从空洞地面起步的方块下部可能嵌入地面，以适配小数高度；不会删除原生地面。具体效果请以本版试玩反馈为准。

0.30.3 的开场镜头与落地水建议新建试玩存档观看；铁锭会在旧档补发一次，专用忠诚 III 三叉戟在切入创造模式时领取。教程大门需要挥击破坏，随后使用原生转场流程。

0.30.4 将珍珠传送基础伤害调为 3 点，并加强传送前后姿态同步；旧档追加两组圆石和一组普通金苹果，每个存档补发一次。新档圆石共三组。

0.30.5 修复切场景时原生重力未恢复、入口动作尚未完成就启用 MC 控制、以及旧场景传送消息覆盖新入口的问题。窄入口暂时保持可通过的高度，离开低矮处后自动站起；入口被已放置的 MC 方块覆盖时尝试附近空位。请先 Stop 再升级，确保两端版本一致。旧存档可以继续使用。
