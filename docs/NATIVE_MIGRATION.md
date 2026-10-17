# HollowCraft Native 0.40.0-alpha.2

[English](NATIVE_MIGRATION.en.md) | 简体中文

项目从这一版正式转向**只运行空洞的独立 Mod**。默认后端改为同进程 C#，不会创建 Minecraft 客户端、Java 进程或 TCP 连接。旧桥接源码保留，稳定桥接版停留在 v0.30.18，可用于对照与回退。

**这是迁移预览，尚未完成桥接版全部功能的等价移植，也未验证可完整通关。** 编译通过及规则检查不能代替实机验收。本轮没有启动游戏或自动游玩。

## 安装与启动

需要 Windows x64 和 Steam 空洞骑士 **1.5.12620**。不要求 Minecraft、PCL、Java、Fabric 或 Better Combat 运行时。发布包不需要 .NET SDK；第一次安装联网获取经 SHA512 校验的 Mono.Cecil，并从校验过的资源存档中提取静态贴图和攻击动画（贴图来源约 23 MB）。不加载或执行 MC/模组代码；离线安装可加 -SkipVisualResources，使用自有占位素材。

1. 将 **Native** 安装包完整解压到自己的目录，放在原版空洞目录以外。
2. 运行 **Install HollowCraft Native.cmd**，自动查找 Steam 安装目录。
3. 运行 **Start HollowCraft Native.cmd**，选择普通冒险或 Boss 录制。
4. 安装器未找到游戏时，可在 PowerShell 中运行：

```powershell
.\scripts\install-standalone.ps1 -GameRoot '你的 Steam 空洞目录'
```

本地试玩入口同上。首次进入会创建新的独立版背包与方块数据，**不会自动读取旧 Minecraft 世界中的背包**。

原版安装目录只作为资源来源；补丁在 `runtime/Hollow Knight Native` 私有副本中生成。普通独立版存档保存在 `runtime/hk-native-saves`，Boss 录制使用 `runtime/hk-native-boss-saves` 下每场单独的目录。关闭游戏后可备份整个保存目录。不要删除或移动保存目录来更新程序。

## 当前迁移情况

“已接入”表示已有本地实现，并不表示与 MC 完全一致或实机验收通过。

| 系统 | 当前状态 | 限制 |
|---|---|---|
| 本地后端、单进程启动、独立存档 | 已接入 | 旧 MC 世界/NBT 存档转换待做 |
| 空洞场景、剧情、原生敌人和 Boss 阶段 | 沿用已有空洞端适配 | 仍需完整通关验收 |
| A/D、正面锁定、鼠标攻击、跳搭、创造飞行、F3+F4 | 沿用已有输入及物理接管 | 依然需要玩家检查手感 |
| 背包、副手、初始装备、耐久、模式 | 已接入 | 全物品、复杂 NBT 和附魔未齐全 |
| 血量、护甲、饱食度、恢复、吸收、珍珠伤害 | 已接入 | 完整伤害类型和附魔减免待补 |
| 食物、金苹果、盾牌、图腾 | 已接入 | 图腾为本地动画；声音未等价迁移 |
| 基础合成、工作台、熔炉、箱子 | 已接入 | 完整原版配方、酿造、附魔台、铁砧等待迁移 |
| 拖拽均分、拆分、Shift 快移、数字键交换 | 已接入 | 高级工作站专属手势待补 |
| 方块放置、挖掘、基岩、TNT | 已接入 | 特殊方块碰撞形状和爆炸算法不是全部原版实现 |
| 弓箭、末影珍珠、投掷三叉戟 | 已接入 | 弩目前沿用拉弓逻辑；忠诚返航暂为直接回包 |
| 水、岩浆、落地水、收水、基础水中移动和溺水 | 已接入简化实现 | 二维流动算法与原版流体更新不完全一致 |
| 简化红石 | 已接入电源、导线、灯、门与点燃 TNT | 活塞、侦测器、复杂时序与自动化待补 |
| 鞘翅、烟花推进、天顶剑、星尘翅膀 | 已接入 | 全部特效、声音及外观仍需对照与完善 |
| 船与生物捕获 | 已接入基础实现 | 船为本地模型，非全部原版细节 |
| MC 生物 | 9 种基础实体及行为 | 非完整原版 AI；其余生物、模型、战利品待迁移 |
| 创造物品菜单 | 本地目录 | 当前只展示本地已登记物品 |
| 第三人称人物和装备 | Unity 关节、手部绑定、本地整角色合批渲染 | 未直接复用 Minecraft PlayerEntityRenderer；透明面排序仍需实机检查 |
| UI、贴图、音效 | 静态原版贴图 + 自有占位资源 + 可选本机模型导入 | 部分外观和声音尚不等价桥接版 |

机器可读清单见 [native-migration.json](../release/native-migration.json)。没有把未实现的生物、方块和工作站作为“已完成”功能计数。

## 本机静态资源复用

独立后端可以读取本机已经提取过的静态贴图、模型和攻击动画，不需要运行 MC。原有桥接工程用户可运行：

```powershell
.\scripts\import-native-assets.ps1 -AssetsRoot '.\artifacts\HollowCraft\HollowCraftAssets'
```

这些文件只复制到本机的 `artifacts/StandaloneAssets`，不会进入发布包。不会导入人物、实体、HUD 或 GUI 的实时帧缓存。未导入旧模型时使用本地生成模型；静态资源准备失败时使用自有占位贴图；这一步可选，不是独立版的启动前提。

## 开发与后续工作

源码构建需要 .NET SDK 和本机空洞托管程序集：

```powershell
.\scripts\install-standalone.ps1 -GameRoot '你的 Steam 空洞目录' -Build
dotnet run --project tools/NativeRulesCheck/NativeRulesCheck.csproj -c Release
.\scripts\package-standalone.ps1
```

纯规则检查工具需要 .NET 9 SDK，其测试不启动 Unity、空洞、MC 或 Java。主 Mod 面向游戏内的 .NET Framework/Mono，不要求玩家安装 .NET 9。

本版规则检查覆盖存档隔离、损坏存档保护、物品守恒、重复攻击和放置去重、铁剑伤害、盾牌、图腾、熔炉进度、落地水与 TNT 场景持久化。渲染、碰撞手感、Boss 全流程和完整通关仍由玩家试玩验收。

接下来的迁移顺序是：人物装备与透明材质保真 → 战斗及通关回归修复 → 完整物品、配方和工作站 → 生物模型与行为 → 声音、粒子、红石及流体细节。直到清单全部迁移并经试玩确认之前，不宣称独立版已达到桥接版全部功能。
