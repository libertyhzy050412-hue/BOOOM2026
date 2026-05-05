# Sandbox DreamBattle — 开发记录

## 项目背景

**游戏类型**：2D 俯视角双摇杆射击 Roguelite

**核心玩法**：玩家控制地雷系少女，在噩梦中用颜料将世界粉刷成美梦。核心机制为涂地系统（美梦/噩梦地板）+ 武器构筑（召唤/弓箭/冲撞三流派）+ 轮次生存（20 轮）。

**技术栈**：Unity 6000.3.12f1 / URP 2D / Input System / UGUI + TextMeshPro / ScriptableObject 配置

---

## 架构设计

基于项目规范 `.github/skills/unity-project-architecture/`，设计了 7 个 Feature 模块：

| 模块 | 职责 |
|------|------|
| **FloorSystem** | 涂地核心——程序化 Mesh + Texture2D 纹理，管理地板颜色状态 |
| **Player** | WASD 移动、鼠标瞄准、属性、生命 |
| **Weapon** | 三流派武器（召唤/弓箭/冲撞）+ 升级 |
| **Enemy** | 6 基础 + 4 精英怪物、AI、刷怪 |
| **Combat** | 伤害结算、命中判定 |
| **RoundFlow** | 20 轮管理、难度递进 |
| **Economy** | 资源结算、商店 |

---

## Sandbox Demo 当前状态

### 文件清单

**脚本**（9 个，`Assets/Sandbox/Scripts/ArenaPrototype/`，命名空间 `Sandbox.DreamBattle`）：

| 脚本 | 功能 |
|------|------|
| `SandboxFloorGrid.cs` | 程序化 Quad Mesh + Texture2D 地板；`SetDreamCircle` / `SetDreamLine` / `SetNightmareLine` 直接填色（无 lerp，颜色均匀）；`IsDreamAt` 基于颜色距离判定 |
| `SandboxPlayerController.cs` | WASD 移动（加减速）+ 鼠标瞄准 + 按住左键倾倒颜料 + 行走尾迹 + 受击闪白 |
| `SandboxEnemyAgent.cs` | 追击玩家 + 移动涂噩梦尾迹 + 受击闪白 + Dream 格上受 DoT/减速；无死亡爆发 |
| `SandboxEnemySpawner.cs` | 对象池刷怪，间隔生成，最大活跃数限制 |
| `SandboxMouseRing.cs` | LineRenderer 绘制鼠标跟随圆环准星 |
| `SandboxDebugGUI.cs` | OnGUI 调试面板：Dream%、敌人数、倾倒状态、操作提示 |
| `SandboxActorVitals.cs` | 通用 HP 组件（事件驱动：Damaged / Died / ResetOccurred） |
| `SandboxArenaBounds.cs` | 程序化围墙 + 边缘随机生成点 |
| `SandboxJsonConfigLoader.cs` | 注释剥离 JSON 加载 + 字典缓存 |

**JSON 配置**（`Assets/Sandbox/Resources/DreamBattleConfigs/`）：

| 文件 | 内容 |
|------|------|
| `floor-grid.json` | 世界尺寸、网格分辨率 |
| `player-movement.json` | 移速、加速度、减速度 |
| `enemy-basic.json` | 生命、移速、涂色宽度/间隔 |
| `enemy-spawner.json` | 延迟、间隔、最大数、对象池 |
| `debug-gui.json` | 面板布局 |

### 涂色系统

| 来源 | 方法 | 颜色 |
|------|------|------|
| 按住左键 | `SetDreamCircle` 扩展圆 + `SetDreamLine` 帧间连线 | 美梦 |
| WASD 行走 | `SetDreamLine` 尾迹（间隔 0.08s，宽度 1.2） | 美梦 |
| 敌人移动 | `SetNightmareLine` 尾迹（间隔 0.25s，宽度 0.8） | 噩梦 |

### 地板效果

- 美梦格上的敌人：持续 DoT（5/s）+ 减速 50%
- 噩梦格上的玩家：暂无效果（已预留 `nightmareSlowFactor` 参数）
- 判定方式：`IsDreamAt(worldPos)` 基于颜色距离阈值

---

## 涂地技术演进

1. **顶点色 Mesh**（100×100 网格）→ 染色精度太低，GPU 顶点插值导致模糊，完全不可用
2. **Texture2D 像素级**（720×432，24 ppu）→ 精度大幅提升，像素直接操作
3. **Color32.Lerp 混合** → 叠加导致颜色深浅不均 → 改为**直接赋值**（`pixelBuffer[i] = targetColor`）
4. **弓箭投射物**（`SandboxProjectile.cs`）→ 改为**召唤倾倒**（按住左键在光标区域扩展圆形涂色）
5. **敌人死亡爆发** → 移除，仅保留移动尾迹

---

## 笔刷行为演进

**目标效果**：拖动越快笔触越细，按住不动颜料逐渐扩散

尝试过的方案：

| 方案 | 问题 |
|------|------|
| 面积恒增模型（`pourRadius` 持续增长，不响应速度） | 快拖慢拖都一样粗 |
| 速度→宽度 Lerp 映射 | 过渡太陡，慢拖粗细变化剧烈 |
| 墨水流量模型（`width = ink / distance`） | 慢速拖拽 1/dist 变化太剧烈 |
| Dynadraw 弹簧阻尼物理 | stiffness 参数错误导致笔刷原地不动 |
| 指数平滑跟随（`Lerp(pos, mouse, 1-exp(-35*dt))`） | 用户仍不满意手感 |
| 软笔刷戳印（`StampDreamBrush` + smoothstep 渐变） | 用户回退到当前硬边版本 |

**当前方案**（用户回退到的版本）：
- `pourRadius` 按住期间持续增长，松手归零
- 每帧画扩展圆 + 帧间连接线
- 无速度感应

---

## 目标要求

### 已实现
- 美梦/噩梦双色地板系统
- 玩家 WASD 移动（加减速手感）+ 鼠标瞄准
- 按住左键倾倒颜料（召唤类武器雏形）
- 敌人 AI 追击 + 移动涂噩梦尾迹
- 美梦格对敌人 DoT + 减速
- 对象池刷怪
- OnGUI 调试面板
- 受击闪白反馈（敌人 + 玩家）

### 待实现
- 笔刷手感：速度→粗细 + 按住→扩散（需找到合理方案）
- 软边笔刷消除硬边锯齿
- 弓箭类、冲撞类武器
- 6 种怪物类型 + 4 种精英
- 20 轮生存流程 + 经济系统
- 正式 UGUI HUD（替换 OnGUI）
- 玩家行走路径上的噩梦减速效果
- 武器进阶升级（心灵控制/导弹引爆/压路机）
