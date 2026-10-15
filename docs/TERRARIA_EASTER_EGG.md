# Clamsk 彩蛋：天顶剑与星尘之翼

[简体中文](TERRARIA_EASTER_EGG.md) | [English](TERRARIA_EASTER_EGG.en.md)

0.30.14 加入两件独立彩蛋装备。进入游戏后按 **T** 或 **/** 打开聊天栏，输入 **Clamsk** 后回车；`/Clamsk` 也可以。背包需要两个空格，每次成功输入各发一件。它们不会加入生存初始装备，也不加入普通创造分类。Esc 关闭聊天，聊天期间屏蔽移动、攻击和背包快捷键，世界继续运行。

## 天顶剑

选中天顶剑，左键单击放出一轮三把回旋剑，按住持续攻击。光标可指向整个可见画面，协议限制距离人物最多 48 格；普通武器和放置仍使用原来的范围。实体剑与彩色拖尾按同一套轨迹运动，手中物品仍由 MC 第三人称渲染器绑定手臂。

| 原版参数与机制 | 本次移植 |
| --- | --- |
| 基础伤害 190、暴击率 14% | 服务端逐剑结算，暴击双倍，再进入既有 MC 护甲及空洞血量换算 |
| 一轮 30 个 Terraria 游戏刻，连续投出三剑 | 每 0.5 秒一轮；单击释放也会完成当前一轮 |
| Final Fractal 回旋、原版合成剑外观、彩色残影 | 采用原版轨迹参数，120 Hz 子步判定；空洞端本地插值绘制，无须传输整屏特效帧 |
| 无限穿透、不碰撞地形、局部命中间隔 | 每把剑对每个目标单独计时，普通墙体不挡剑；不是全屏直接扣血 |

近身瞄准会缩小最低回旋半径，让剑阵也能覆盖与史蒂夫重叠的小型敌人。

原生敌人的方向防御、不可受击阶段、阶段变换、受击动作和死亡流程仍走原来的桥接路径。它不能攻击 NPC 或绕过剧情触发条件。录制福光时可用启动器选择对应 Boss 战，进入战斗后输入口令。

0.30.16 从 Boss 当前状态机中读取接下来可达的血量检查，单次高伤害止于下一阶段边界，等待原版受击事件和阶段转场完成；最后一次转阶段短暂保留 1 点原生血量，给原版控制器安装无敌状态的时间。这项保护同时覆盖天顶剑、创造三叉戟等桥接伤害，保留剧情无敌。旧版本已经卡在 0 血的战斗请重新进入。

0.30.17 为受击代理刷新按占用区块合并的临时加载票据，离开场景或目标不再被观察时释放；代理不写入存档为普通僵尸，异常恢复保留尚未确认的扣血，并记录移除原因。MC 左键还会转交给辐光当前状态中明确启用的 `ListenForAttack` 原版动作，覆盖伤害碰撞体消失后的终结输入；不会设置胜利状态或跳过剧情。天顶剑仍为 190 基础伤害、14% 暴击。翅膀镜像关节改为向背部收拢，飞行摆动保持朝后，挂点置于背甲外侧。

## 星尘之翼

将星尘之翼拖到 **胸甲栏**，也可以手持后右键装备；会替换原胸甲，不提供护甲值。0.30.16 使用重新制作的 3D 体素骨骼模型，包含背部挂点、翼根、外翼和弯曲翼尖；深蓝轮廓与青色发光脉络跟随实际 MC 躯干变换，覆盖下蹲与战斗姿态，并区分地面收翼、飞行振翅、滑翔展开和下落收拢。

- 空中按住空格飞行，累计可用约 **3 秒**；落地恢复，不因松开空格立即补满。
- 松开空格下降；耗尽后继续按空格缓降。A / D 控制方向，有加速、减速和地形碰撞。
- 装备期间免落差摔伤；不会自动取消末影珍珠本身既定的传送伤害。
- 原版水平速度参数 9 像素/刻、加速倍率 2.5；上升使用星尘翼的 .135 / .85 / 3 / 1 / .15 参数。空间按 1 Terraria 像素 = 0.0375 MC 格投影，水平上限约 20.25 格/秒。
- 不提供星旋之翼的悬停功能；创造飞行、骑船和剧情接管期间分别保留各自控制。

这是两种游戏间的二维移植。原生碰撞、MC 伤害管线与 Terraria 的完整游戏引擎不同，不能保证逐帧一致；本版完成编译后直接交付试玩，未运行实机 Boss 战验收。

## 本机原版素材

安装器会寻找 Steam《泰拉瑞亚》的安装目录，从本机 XNB 提取天顶剑、合成剑、星尘之翼和天顶剑挥动音效。已在本机 **1.4.5.8** 素材上成功完成导入。游戏不必运行；素材留在私有 `artifacts` 和 `runtime` 目录，Git 与安装包不携带原版贴图或音频。

先装好泰拉瑞亚，再运行新版 **Install HollowCraft.cmd**。已经安装 HollowCraft 的开发目录可运行 `scripts/prepare-terraria-assets.ps1`；非标准路径使用 `-TerrariaRoot` 指向包含 `Content` 的目录。导入后重启两端，私有 MC 配置会启用 `HollowCraft-Terraria` 资源包。没有泰拉瑞亚素材时，普通 HollowCraft 仍可运行，口令会提示先导入素材。

原版参考：[天顶剑](https://terraria.wiki.gg/wiki/Zenith)、[星尘之翼](https://terraria.wiki.gg/wiki/Stardust_Wings)。导入工具采用 MonoGame 的 LZX 解码器，来源和许可证见 [第三方说明](../release/THIRD_PARTY.md)。


模型与状态分层参考：汇流来世的 [翅膀骨骼层级](https://github.com/Magic-team-jvav/confluence/blob/neoforge/1.21.1/ConfluenceOtherworld/src/main/resources/assets/confluence/geo/curio/fledgling_wings.geo.json)、[待机/飞行/滑翔动画](https://github.com/Magic-team-jvav/confluence/blob/neoforge/1.21.1/ConfluenceOtherworld/src/main/resources/assets/confluence/animations/curio/fledgling_wings.animation.json)，以及泰拉饰品的 [躯干跟随渲染层](https://github.com/Magic-team-jvav/TerraCurio/blob/neoforge/1.21.1/src/main/java/org/confluence/terra_curio/client/renderer/accessory/WormScarfRenderer.java)。本项目自行实现 Fabric 1.20.1 模型与二维控制。
