# Sandbox Arena Prototype 搭建说明

这套脚本只服务于 Sandbox 试验，不接正式模块。

## 本次提供的脚本

- `SandboxArenaBounds`：生成矩形地图边界墙体
- `SandboxPlayerController`：玩家移动
- `SandboxCorrosionRing`：玩家脚下的侵蚀范围圆环
- `SandboxRevealRing`：独立的鼠标 reveal 圆环，用于决定怪物表里世界状态
- `SandboxMouseFollowCursor`：让 Sandbox 圆环跟随鼠标移动
- `SandboxRevealRingEnergySystem`：鼠标圆环能量、缩圈、失效与冷却复原
- `SandboxCorrosionSystem`：统计圆环范围内怪物并累计侵蚀值
- `SandboxCorrosionDebugGUI`：临时 OnGUI 面板，用于显示侵蚀值与范围内怪物数量
- `SandboxPlayerAutoAttack`：角色自动攻击攻击范围内且被 reveal 圆环覆盖的怪物
- `SandboxActorVitals`：受击/生命占位脚本，暂时不接伤害流程
- `SandboxEnemyAgent`：怪物移动逻辑
- `SandboxEnemySpawner`：怪物自动生成，可选对象池

## 场景搭建顺序

### 1. 地图边界

1. 创建一个空物体，命名为 `ArenaBounds`。
2. 挂上 `SandboxArenaBounds`。
3. 直接在 Inspector 里修改 `Arena Size` 和 `Wall Thickness`。
4. 脚本会自动在子物体下生成四面碰撞墙。

### 2. 玩家

1. 创建玩家物体。
2. 添加 `Rigidbody2D`，保持 `Gravity Scale = 0`。
3. 添加一个 `Collider2D`，推荐 `CircleCollider2D` 或 `CapsuleCollider2D`。
4. 挂上 `SandboxPlayerController`。
5. 挂上 `SandboxCorrosionRing`，这个圆环固定跟着玩家脚下，只服务侵蚀值系统。
6. 挂上 `SandboxCorrosionSystem`，用于累计玩家脚下侵蚀环范围内的侵蚀值。
7. 挂上 `SandboxPlayerAutoAttack`，用于自动攻击攻击范围内且处于可攻击状态的怪物。
8. 挂上 `SandboxCorrosionDebugGUI`，会用简单 OnGUI 显示侵蚀值、圆环能量和失败状态。
9. 可选：挂上 `SandboxActorVitals`，作为后续受击扩展入口。
10. 玩家移动、自动攻击、侵蚀系统的数值仍来自 `Assets/Sandbox/Resources/ArenaPrototypeConfigs/` 下对应的 JSON 文件；地图边界是例外，直接在 `SandboxArenaBounds` 组件里调。

### 3. 鼠标圆环

1. 创建一个单独的空物体，命名为 `MouseRing`。
2. 挂上 `SandboxRevealRing`。
3. 再挂上 `SandboxMouseFollowCursor`。
4. 再挂上 `SandboxRevealRingEnergySystem`。
5. 如果你的主相机没有 `MainCamera` 标签，就手动把相机拖到 `Target Camera`。
6. 圆环会跟随鼠标移动。
7. 这个鼠标圆环只负责决定怪物处于表世界还是里世界，不参与侵蚀值统计。
8. 怪物会根据自己是否位于鼠标圆环内自动切换视觉状态：圈内保持正常，圈外会被冷色调并降低透明度。
9. 按住鼠标右键会消耗圆环能量并降低侵蚀值；能量减少后 reveal 圆环半径会缩小。
10. 当圆环能量耗尽时，reveal 圆环会暂时消失，过一段时间后自动复原。
11. 鼠标圆环的视觉参数、跟随速度、能量参数现在分别放在 `reveal-ring.json`、`mouse-follow-cursor.json`、`reveal-ring-energy.json`。

### 4. 怪物预设体

1. 创建怪物对象并制作成 Prefab。
2. 给怪物加上 `Rigidbody2D`，保持 `Gravity Scale = 0`。
3. 给怪物加上 `Collider2D`。
4. 挂上 `SandboxEnemyAgent`。脚本会自动把怪物碰撞体切成 `Trigger`，因此怪物不会阻挡玩家和其他怪物移动。
5. 可选：挂上 `SandboxActorVitals`，后续死亡和掉血会直接用它。
6. 怪物的移速、血量、表里世界颜色、受击反馈都来自 `enemy-basic.json`。后续要做新怪物类型，直接复制一份新的敌人配置文件，再把对应 Prefab 上的 `Config Resource Path` 改到新路径即可。

### 5. 刷怪器

1. 创建一个空物体，命名为 `EnemySpawner`。
2. 挂上 `SandboxEnemySpawner`。
3. 将 `ArenaBounds` 拖到 `Arena Bounds`。
4. 将玩家拖到 `Player Target`。
5. 将怪物 Prefab 拖到 `Enemy Prefab`。这是必填项，不填会导致刷怪器无法生成怪物。
6. 可选：创建一个 `Enemies` 空物体，拖到 `Spawned Enemy Container`。不填也能运行，刷出来的怪物会默认挂在 `EnemySpawner` 自己下面。
7. 刷怪频率、上限、对象池设置都来自 `enemy-spawner.json`；默认不再直接在组件上调这些数值。
8. `Spawn Padding` 现在表示怪物出生点距离地图边界内侧的偏移量，不是出生在墙外的距离。

## 配置文件目录

- 当前 Sandbox 原型的运行参数统一放在 `Assets/Sandbox/Resources/ArenaPrototypeConfigs/`。
- 组件上现在保留的是“引用”和“配置路径”，大部分数值字段已经转为隐藏运行态，不再作为主要调参入口。
- 如果要新增一类怪物，优先复制 `enemy-basic.json` 为一个新文件，然后把对应怪物 Prefab 上 `SandboxEnemyAgent` 的 `Config Resource Path` 指向新资源路径。
- 资源路径写法不带扩展名，例如 `ArenaPrototypeConfigs/enemy-fast` 会对应 `Assets/Sandbox/Resources/ArenaPrototypeConfigs/enemy-fast.json`。

## 对象池怎么选

当前脚本已经内置了一个轻量对象池开关。

### 方案 A：直接 Instantiate/Destroy

适合：

- 当前只是少量怪物试验
- 想先快速验证玩法

做法：

- 在 `SandboxEnemySpawner` 里关闭 `Use Pooling`

优点：

- 逻辑最直接
- 最容易理解

缺点：

- 怪物数量多时，频繁生成和销毁会带来额外开销

### 方案 B：当前内置轻量对象池

适合：

- 想保持试验阶段也有基本性能余量
- 后续怪物会频繁死亡和重生

做法：

- 保持 `Use Pooling` 打开
- 根据预期同屏怪物数量调整 `Prewarm Count`

优点：

- 不需要引入额外框架
- 已经够这次 Sandbox 试验使用

缺点：

- 目前还是单一怪物类型池
- 后面若有多种怪物，需要升级成多 Prefab 池管理

### 当前建议

先用内置轻量对象池，不上第三方方案。

原因：

- 你当前阶段是玩法验证，不是性能极限优化
- 但 arena survival 类型后面怪物数量很容易上来，提前保留池化结构更稳

## 现阶段没有接的内容

- 玩家掉血
- 怪物攻击判定
- 表世界 / 圆环内战斗切换

## 当前侵蚀值规则

- 侵蚀值系统现在已经接入 Sandbox 原型。
- 默认规则是：玩家脚下的侵蚀环范围内每存在一只怪物，侵蚀值按固定速率持续增长。
- 侵蚀值不会自动回落，只能通过按住鼠标右键消耗鼠标 reveal 圆环能量来降低。
- 当侵蚀值达到上限时，GUI 会显示“侵蚀失控”状态。
- 玩家脚下的侵蚀环和鼠标 reveal 圆环是两个完全独立的系统。
- 怪物会根据是否处于鼠标 reveal 圆环内自动切换视觉状态，方便直接观察圈内圈外的差异。

## 当前战斗与净化规则

- 玩家会自动攻击自身攻击范围内，且被鼠标 reveal 圆环覆盖的怪物。
- 玩家攻击范围和侵蚀环范围无关。
- 玩家按住鼠标右键时，会持续消耗鼠标 reveal 圆环能量并降低侵蚀值。
- reveal 圆环能量越低，圆环半径越小。
- reveal 圆环能量耗尽后，圆环会暂时消失并进入冷却，之后自动复原。
- 当前版本仍未接入怪物攻击逻辑。

这次先把移动、边界、侵蚀圈显示、刷怪链路和侵蚀值试验链路跑起来。
