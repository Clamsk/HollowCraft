# 0.30.8 原版敌人受击检查

[简体中文](ENEMY_AUDIT.md) | [English](ENEMY_AUDIT.en.md)

2026-10-14，针对本机 Steam 空洞骑士 1.5.12620 / Unity 6000.0.61f1，离线读取全部 `level*`、`sharedassets*.assets` 和 `resources.assets` 文件，并检查原版 `HealthManager.Hit`、`IsBlockingByDirection`、`HitTaker` 和 `ScuttlerControl.Hit` 代码。没有启动游戏。

扫描 1,003 个文件，读取 2,558 个 HealthManager 和 74 个 ScuttlerControl，读取错误为 0。[逐名称清单](enemy-coverage-0.30.8.csv) 有 1,083 行，覆盖普通、感染、梦境、斗兽场及神居等场景中的对应对象。名称包含编号、阶段副本和预制体，不等于 1,083 个独立敌人种类。此清单记录原生血量与组件标记；MC 血量仍按桥接换算规则计算。

| 检查项 | 资源中情况 | 本版处理 |
| --- | --- | --- |
| 初始无敌 | 260 个血量组件 | 不再直接映射成 MC 全方向无敌；实时向原版函数查询防护 |
| 方向防护 | 39 个组件，序列化方向值 4 和 7 | 分别生成四方向近战/远程掩码；保留原版 Spell Vulnerable 例外及格挡反馈 |
| 特殊死亡 | 106 个组件 | 原版 Hit / 特殊死亡 FSM 保留；MC 死亡等待原生确认或新阶段 |
| 多血量组件 | 26 个血量组件的对象同时带多个 HealthManager | 查找最近的启用、存活、正血量受击组件，不向所有重复组件同时扣血 |
| 根对象无碰撞体 | 72 个组件，例如 Plant Trap、Moss Charger | 观察使用实际触发查询的子碰撞体；根对象有启用碰撞体时优先使用它 |
| 原生一击对象 | 74 个 ScuttlerControl | 近战保留 HitTaker 原生受击；箭命中时调用原生一击死亡并消耗普通箭 |
| 代理存活、尺寸与距离 | 通用路径 | 原生场景管理存活；恢复失效代理；两端允许人物与敌人碰撞体重叠的命中；记录拒绝原因 |

重点读过假骑士、失败冠军、螳螂领主、灵魂大师梦境第二阶段、黄蜂女、空洞骑士、辐光和神居变体的组件配置。截图对应的假骑士使用特殊死亡与阶段 FSM；不能绕过原版护甲/阶段逻辑直接强杀。现有日志中能收到假骑士目标编号，却没有对应目标伤害日志，因此本版同时修复失效/死亡代理与防护规则，并增加缺失代理、等待阶段及距离拒绝日志。尚不能仅凭旧日志断定这个实例的唯一原因。

全部 HealthManager 对象走同一套 MC 数值计算、原生方向/类型防护和原生受击/死亡接口，不靠敌人名称白名单。非血量受击类型按原版接口区分：Breakable、BreakablePole、Grass 与 ScuttlerControl；可破坏场景和敌人不会统一当成墙壁。新增箭穿草、柱子和可破坏装饰的处理，实体墙壁与 MC 方块保持阻挡。

静态检查可以证明组件配置和代码接入覆盖范围，不能证明每个 Boss 的运行时阶段时序已经通过。所有敌人的实机效果、召唤/池化对象在具体阶段的行为、奖励和完整生存通关仍由玩家试玩反馈确认。

开发者可单独安装 UnityPy / TypeTreeGenerator 后运行：

```powershell
python scripts/audit-hk-enemies.py --data '<自己的空洞目录>/hollow_knight_Data' --libs '<UnityPy 所在目录>' --out artifacts/enemy-audit.json --coverage-csv artifacts/enemy-coverage.csv
```

原始资源字段与反汇编保存在忽略的本地 artifacts；仓库和发布包仅包含检查脚本、摘要及逐名称统计，不包含原版程序集、场景文件或素材。
