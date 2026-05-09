# Rebuild Devlog

## 2026-05-09 基础模块阶段记录

### 本阶段目标

- 重建最小可运行框架，先完成地图初始化、相机控制、玩家生成与移动。
- 这一阶段只写脚本，预制体与场景对象由手动搭建。

### 已完成内容

- 地图初始化
  - `MapInitializer` 从 `Resources/Map` 动态加载地图块 prefab。
  - 地图块按名称排序实例化，保留 prefab 已经写好的根节点位置。
  - 地图根对象 `MapRoot` 作为场景根对象创建或复用，不再挂在 `GameManager` 下。

- 相机控制
  - `GameCameraController` 挂在主相机上使用。
  - 相机自动查找场景中的 `MapRoot` 和 `Player`。
  - 正交相机缩放被限制在地图边界允许范围内，镜头边缘不会露出地图外区域。
  - 增加开场镜头：先靠近玩家，再进一步拉近，停顿后缓慢拉回正常游玩视角，并在末端加入轻微回弹。

- 玩家基础
  - `Player` 脚本已建立基础属性：移速、生命、攻击力百分比、物攻、魔攻、闪避。
  - `GameInitializer` 暂时只负责玩家初始化。
  - 玩家会优先复用场景中已有对象；若场景中没有，则实例化 `Player` 预制体。
  - 玩家生成位置改为自动计算地图整体边界中心，不再手动挂出生点。

- 玩家移动与碰撞
  - `PlayerMovement` 使用手动位移，不再用之前的刚体步进移动方式。
  - 输入兼容 WASD 与方向键。
  - 为了保留边缘阻挡效果，位移前会使用 `Rigidbody2D.Cast` 做扫掠检测，再裁剪可移动距离。
  - 当前方案兼顾了移动平滑度和地图边缘碰撞阻挡。

### 本阶段排查与修正

- 玩家被地图盖住
  - 排查后确认是地图前景层 `SpriteRenderer` 的排序值高于玩家，不是生成顺序问题。

- 玩家移动抖动
  - 排查后确认与 `Rigidbody2D` 驱动移动和相机跟随组合有关。
  - 已切换为以手动位移为主的移动方案。

- 地图边缘碰撞失效
  - 排查后确认不是地图 collider 丢失，而是纯 `Transform` 位移绕过了物理阻挡。
  - 已补回基于扫掠检测的移动阻挡逻辑。

### 当前模块状态

- 地图加载：可用
- 相机跟随与开场镜头：可用
- 玩家生成：可用
- 玩家移动：可用
- 地图边缘碰撞阻挡：已恢复

### 当前涉及脚本

- `Assets/Scripts/MapInitializer.cs`
- `Assets/Scripts/GameCameraController.cs`
- `Assets/Scripts/GameInitializer.cs`
- `Assets/Scripts/Player.cs`
- `Assets/Scripts/PlayerMovement.cs`