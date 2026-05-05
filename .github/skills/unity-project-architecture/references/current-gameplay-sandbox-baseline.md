# 当前玩法与 Sandbox 基线

这份文档的目标是让一个第一次进入这个项目的新窗口模型，在最短时间内理解：

- 这个项目当前在验证什么玩法
- Sandbox 已经实现了哪些链路
- 哪些只是设计目标，还没有正式实现
- 后续扩展应该往哪里落，不该破坏什么

## 1. 项目当前定位

- 游戏类型：2D 俯视角 arena survival
- 当前阶段：原型验证阶段
- 当前主要工作场所：Sandbox
- GameScene / Sandbox 场景用途：实验新机制，不是正式主流程入口

## 2. 核心玩法框架

当前游戏的核心不是单纯打怪，而是围绕两条资源做风险管理：

- `侵蚀值`：核心生存压力资源，达到上限直接失败
- `圆环能量`：决定 reveal 圆环的有效半径，同时也是净化侵蚀值的消耗资源

玩家的核心循环如下：

1. 在 arena 中移动、控距离、维持生存。
2. 怪物靠近会制造侵蚀压力。
3. 鼠标 reveal 圆环决定局部区域内是否开启真实战斗关系。
4. 玩家通过圆环选择何时把怪物纳入可交互战斗区。
5. 玩家消耗圆环能量净化侵蚀值时，会缩小圆环并降低自己的局部战斗覆盖能力。

## 3. 双世界交互规则

### 设计意图

- 怪物默认处于表世界。
- 表世界怪物会继续制造侵蚀压力。
- 进入 reveal 圆环后，怪物进入局部可交互状态。
- 局部可交互状态下，玩家可以攻击怪物，怪物的攻击也应该对玩家生效。

### 当前 Sandbox 实现状态

- 玩家只能攻击 reveal 圆环内且位于攻击范围内的怪物。
- 怪物圈内外有明确视觉差异。
- 怪物攻击玩家的生命链路还未正式接上，因此“互相伤害”目前只完成了一半。

## 4. 侵蚀值与圆环能量

### 侵蚀值

- 玩家脚下有独立的侵蚀环。
- 侵蚀环内的怪物会提高侵蚀值。
- 当前实现不是“全局固定每怪增量写在系统配置里”，而是每个怪物自己在配置里定义 `corrosionPerSecond`。
- 侵蚀值不会自动回落。
- 玩家按住鼠标右键时，会消耗 reveal 圆环能量来降低侵蚀值。

### reveal 圆环能量

- reveal 圆环跟随鼠标。
- 净化侵蚀值会持续消耗圆环能量。
- 圆环能量越低，半径越小。
- 圆环能量耗尽时，圆环暂时失效。
- 经过恢复时间后，圆环自动复原。

## 5. 当前 Sandbox 已落地系统

### Arena

- `SandboxArenaBounds`
- 作用：生成矩形边界和四面墙
- 当前规则：直接在 Inspector 调 `Arena Size` 和 `Wall Thickness`

### Player

- `SandboxPlayerController`
- 作用：玩家移动
- 输入：Input System

### Corrosion

- `SandboxCorrosionRing`
- `SandboxCorrosionSystem`
- 作用：统计玩家脚下侵蚀环内怪物并累计侵蚀值

### Reveal

- `SandboxRevealRing`
- `SandboxMouseFollowCursor`
- `SandboxRevealRingEnergySystem`
- 作用：鼠标 reveal 圆环、能量、缩圈、失效、恢复、净化侵蚀

### Enemy

- `SandboxEnemyAgent`
- `SandboxEnemySpawner`
- `SandboxActorVitals`
- 作用：怪物追击、触发器碰撞、视觉切换、生命值占位、刷怪与对象池

### Combat

- `SandboxPlayerAutoAttack`
- 作用：玩家自动攻击 reveal 圆环内怪物

### Debug

- `SandboxCorrosionDebugGUI`
- 作用：Sandbox 临时调试数据显示
- 注意：这是原型调试 UI，不是正式 HUD 实现方式

## 6. 当前数据与配置策略

### 正式项目规则

- 正式静态配置优先使用 ScriptableObject

### Sandbox 当前例外

- Sandbox 原型允许使用带注释的 JSON TextAsset 作为临时配置载体
- JSON 文件必须写清文件作用和字段含义
- JSON 只允许存在于 Sandbox 自己的目录内
- 必须由专用加载器解析注释和 JSON 内容

### 当前实际做法

- 大多数 Sandbox 原型参数位于 `Assets/Sandbox/Resources/ArenaPrototypeConfigs/`
- 怪物类型通过复制 `enemy-basic.json` 并修改 `Config Resource Path` 扩展
- 地图边界是例外：`SandboxArenaBounds` 直接在 Inspector 调整

### 参数归属规则

- 实体属性跟实体配置走，例如怪物的移速、血量、侵蚀贡献
- 系统配置只保留系统级阈值和共享规则，例如 `maxCorrosion`
- 不要把本应属于单个实体的参数塞进全局系统配置

## 7. 当前已知缺口

这些内容在玩法草案里已经定义，但 Sandbox 还没有完整闭环：

- 玩家生命值失败链路
- 怪物攻击玩家
- 击杀怪物恢复圆环能量的收益闭环
- 正式胜利条件
- 更完整的敌人状态机
- 正式运行时 HUD

实现新功能前，先判断自己是在：

- 补齐上述缺口
- 扩展 Sandbox 机制验证
- 开始把已验证机制迁移进正式 Feature

## 8. 后续扩展的推荐方向

后续应逐步拆出这些正式模块：

- `Player`
- `Enemy`
- `Combat`
- `Corrosion`
- `Reveal`
- `Progression`
- `Items`

迁移原则：

1. 先抽规则与数据，再迁 MonoBehaviour。
2. 先从 Sandbox 里拿到稳定边界，再决定正式目录归属。
3. 不把 OnGUI、临时调试文本、临时 Gizmo 和试验性查找逻辑直接搬入正式模块。
4. 进入正式 Feature 后，配置要优先回到 ScriptableObject 方案。

## 9. 新窗口模型的最低认知要求

新窗口里的模型在动手前，至少要知道下面这些事实：

- 侵蚀环和鼠标 reveal 圆环是两个完全独立的系统
- reveal 圆环不是纯视觉效果，而是局部开启战斗关系的核心机制
- 当前 Sandbox 已经实现玩家打怪，但怪物打玩家还没闭环
- 怪物侵蚀贡献是怪物配置的一部分，不是侵蚀系统的全局固定值
- 地图边界当前直接在 Inspector 调，不走 JSON
- Sandbox JSON 可以带注释，但这是 Sandbox 原型期特例，不是整个项目的永久主配置策略
