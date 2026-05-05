---
name: unity-project-architecture
description: '规划 Unity 项目结构、模块边界、技术栈、当前玩法框架与 Sandbox 基线。Use for project architecture, gameplay-system placement, feature folder layout, scene split, UGUI/TextMeshPro UI stack, Input System, config strategy, and understanding the current arena-survival prototype quickly.'
argument-hint: '描述你要新增、重构或评审的模块/系统'
user-invocable: true
---

# Unity Project Architecture And Gameplay Baseline

用于这个项目的架构规划、模块落位、技术栈选择、玩法上下文速读和规范自检。

目标不是只告诉模型“代码该放哪”，而是让新窗口里的模型在最短时间内同时理解下面四件事：

- 这个项目当前在做什么游戏
- Sandbox 里已经验证和已经落地了什么
- 还没落地的部分有哪些，不要误当成现成能力
- 后续扩展时必须遵守哪些目录、技术栈和数据归属规则

## 何时使用

- 新增一个玩法系统、敌人机制、战斗规则、资源系统、UI 模块或基础设施模块之前
- 重构脚本目录、场景结构、Sandbox 原型或模块依赖之前
- 决定某段逻辑应该放在 Core、Features、UI、Data、Editor 还是继续留在 Sandbox 之前
- 审查一个实现是否越过模块边界、混用了技术栈、写错了参数归属时
- 新窗口对话需要快速理解“当前玩法框架 + 当前实现状态 + 后续扩展方向”时

## 项目速览

- 游戏类型：2D 俯视角 arena survival
- 当前阶段：原型验证阶段
- 当前主要玩法母题：侵蚀值压力、鼠标 reveal 圆环、双世界交互、圆环能量资源、局部开启战斗关系
- 当前场景定位：GameScene / Sandbox 场景只用于试验机制，不是正式游戏流程入口

## 当前玩法框架

以 [Docs/gameplay-foundation.md](../../../../Docs/gameplay-foundation.md) 为设计基线，当前玩法框架可以压缩成下面这组关系：

1. 玩家在 arena 中移动、生存、控场。
2. 怪物默认提供侵蚀压力，但不一定默认进入可伤害/可被伤害状态。
3. 鼠标跟随的 reveal 圆环决定局部区域内是否开启真实战斗关系。
4. 玩家可以消耗圆环能量来压低侵蚀值，但这会缩小圆环，减少可交互战斗区。
5. 玩家需要在“降低侵蚀”“保留战斗半径”“承担近身战风险”“击杀获得收益”之间做资源管理。

## 当前 Sandbox 已落地实现

当前 Sandbox 不是纯概念，而是已经有一条可运行的原型链路：

- `SandboxArenaBounds`：矩形 arena 边界，直接在 Inspector 调 `Arena Size` 和 `Wall Thickness`
- `SandboxPlayerController`：Input System 驱动的玩家移动
- `SandboxCorrosionRing`：玩家脚下侵蚀环，只服务侵蚀统计
- `SandboxCorrosionSystem`：统计侵蚀环内怪物并累计侵蚀值；侵蚀值可被 reveal 能量主动净化
- `SandboxRevealRing`：鼠标 reveal 圆环，只决定表/里世界交互状态
- `SandboxMouseFollowCursor`：鼠标跟随
- `SandboxRevealRingEnergySystem`：圆环能量、缩圈、耗尽、失效和恢复
- `SandboxEnemyAgent`：怪物追击、Trigger 碰撞、圈内外视觉状态、受击反馈、每怪独立侵蚀贡献
- `SandboxEnemySpawner`：刷怪与轻量对象池
- `SandboxPlayerAutoAttack`：攻击 reveal 圆环内且进入攻击范围的怪物
- `SandboxActorVitals`：生命值与受击事件占位
- `SandboxCorrosionDebugGUI`：临时 OnGUI 调试信息

## 设计基线与当前实现的区别

必须区分“设计意图”和“当前已经做完的原型能力”：

### 已经实现

- 侵蚀值失败链路
- reveal 圆环能量消耗、缩圈、耗尽与恢复
- 玩家对 reveal 圆环内怪物的自动攻击
- 怪物圈内外视觉差异
- 怪物独立侵蚀贡献配置
- Sandbox 调试 GUI 与 JSON 配置链路

### 设计上明确，但当前还未正式落地完

- 玩家生命值失败链路
- 怪物攻击玩家
- 击杀怪物恢复圆环能量的完整收益闭环
- 正式胜利条件
- 更完整的敌人状态机

新窗口模型在继续开发前，必须先判断需求属于哪一类：

- “继续补全当前原型缺口”
- “扩展 Sandbox 机制验证”
- “把验证通过的系统迁入正式 Feature”

## 项目固定决策

- 运行时 UI 主栈使用 UGUI + TextMeshPro
- 启动场景与游戏场景分离
- 依赖管理采用轻量 Bootstrap + Service Registry，不引入重型 DI 框架
- 2D 渲染基于 URP 2D
- 输入系统统一使用 Input System
- 配置数据优先使用 ScriptableObject
- Sandbox 原型允许使用带注释的 JSON TextAsset 作为临时配置载体，但必须限制在 Sandbox 目录内并由专用加载器解析
- Sandbox 中的场景结构型参数允许保留 Inspector 直调，例如 `SandboxArenaBounds`

## 使用流程

1. 先读 [项目架构规范](./references/project-standards.md)，确认目录、依赖方向和允许使用的技术栈。
2. 再读 [当前玩法与 Sandbox 基线](./references/current-gameplay-sandbox-baseline.md)，确认当前设计意图、已实现链路、未实现缺口和数据策略。
3. 对照 [新模块检查清单](./references/new-module-checklist.md) 逐项检查，避免把输入、渲染、物理、状态和 UI 混写进同一层。
4. 如果需求是新增玩法，先判断它是继续留在 Sandbox 验证，还是已经该进入正式 Feature。
5. 如果涉及配置，先判断参数归属：实体参数跟实体走，系统参数只保留系统阈值和共享规则。
6. 如果涉及 Sandbox JSON，文件内必须写清“文件作用”和“字段含义”注释，避免新窗口模型或后续开发者失去上下文。
7. 如果需求要改动战斗、侵蚀、reveal、怪物或奖励闭环，先明确是“设计更新”还是“实现补齐”，不要混写。

## 结果要求

- 目录归属明确
- 模块边界清楚
- 技术栈不混用
- 场景职责单一
- 可读性和扩展性优先于一时堆功能
- 配置归属稳定，注释足够支撑后续扩展与交接
- 能让新窗口模型分清“当前已实现”和“当前只是设计目标”

## 未来扩展的推荐收敛方向

随着 Sandbox 机制逐步验证完成，正式模块建议收敛到这些 Feature：

- `Player`：移动、生命、攻击、成长
- `Enemy`：状态、AI、生成、掉落
- `Combat`：伤害、命中、世界状态下的可攻击判定
- `Corrosion`：侵蚀累计、净化、失败判定
- `Reveal`：鼠标圆环、能量、表里世界交互切换
- `Progression`：经验、升级奖励、局内成长
- `Items`：道具、被动效果、拾取逻辑

原则是：先在 Sandbox 验证，再抽出规则和数据，最后迁入正式 Feature。不要把临时调试链路直接拷进正式模块。

## 相关文档

- [项目架构规范](./references/project-standards.md)
- [当前玩法与 Sandbox 基线](./references/current-gameplay-sandbox-baseline.md)
- [新模块检查清单](./references/new-module-checklist.md)
- [玩法扩展方向](./references/feature-expansion-directions.md)