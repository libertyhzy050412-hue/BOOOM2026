# 玩法扩展方向

这份文档用于约束后续玩法扩展的落位方式，避免在原型推进过程中把系统写散、写乱。

## 1. 怪物状态扩展

怪物后续不应长期停留在“只有移动和死亡”的单状态模型，建议逐步扩展为明确状态机。

推荐状态方向：

- Idle / Spawn：出生与入场阶段
- Chase：追击玩家
- AttackReady：满足攻击条件后的准备阶段
- AttackActive：攻击执行阶段
- Stagger / HitReact：受击反馈与短暂硬直
- DisabledByWorldState：处于表世界、不可交互或不可攻击状态
- Dead / Despawn：死亡和回收阶段

落位建议：

- 状态枚举与状态切换规则放 Feature 的 Runtime 层
- 具体表现反馈和动画桥接放 Components 或 Integration 层
- 不要把全部状态切换、寻路、攻击、表现都塞进一个 MonoBehaviour

## 2. 玩家攻击手段扩展

当前 Sandbox 使用的是最小可玩自动攻击。正式推进时，建议把“攻击模型”与“角色控制”拆开。

推荐扩展方向：

- 自动近战 / 自动射弹
- 穿透型攻击
- 连锁型攻击
- 区域型攻击
- 持续伤害型攻击
- 依赖 reveal 圆环状态的特化攻击

结构建议：

- PlayerAttackController：驱动攻击节奏和目标选择
- AttackDefinition：ScriptableObject，描述伤害、频率、射程、弹道、命中特效
- AttackResolver：处理命中、伤害和特殊效果应用

这样后面加武器时，不需要修改玩家移动或 reveal 系统核心逻辑。

## 3. 经验系统与升级奖励

如果项目继续往类幸存者方向推进，经验系统和升级奖励系统应该尽早模块化。

推荐拆分：

- ExperienceSystem：经验累计、等级推进、经验阈值管理
- LevelUpRewardSystem：升级时生成奖励选项并等待玩家选择
- RewardDefinition：ScriptableObject，描述奖励类型、数值和稀有度

奖励类型可包括：

- 提升攻击范围
- 提升攻击频率
- 提升 reveal 圆环能量上限
- 提升侵蚀恢复效率
- 增强某类攻击效果
- 改变怪物处于 reveal 内外时的交互权重

## 4. 道具系统

道具系统建议从一开始就区分“掉落物”和“持有后的效果定义”。

推荐拆分：

- PickupItem：场景中的可拾取实体
- ItemDefinition：ScriptableObject，定义道具效果
- Inventory / PassiveModifierSystem：持有后对玩家属性或战斗规则施加影响

常见扩展方向：

- 被动攻击强化
- reveal 圆环能量恢复加成
- 侵蚀值增长减缓
- 击杀回复收益提升
- 特殊条件触发的额外效果

## 5. Sandbox 到正式模块的迁移规则

当前很多机制会先在 Sandbox 验证。验证通过后，迁移到正式模块时应遵守以下原则：

1. 先抽出纯规则与数据定义，再迁 MonoBehaviour。
2. 先确定归属到 Player、Enemy、Combat、Progression 还是 Interaction 等 Feature。
3. 不把 OnGUI、临时 Gizmo、调试按钮直接迁入正式 Gameplay 模块。
4. 通过 ScriptableObject 承接可调参数，而不是继续扩张单个脚本上的 SerializeField。

## 6. 推荐的正式模块方向

随着功能增多，建议逐步收敛到以下 Feature：

- Player：移动、生命、攻击、成长
- Enemy：状态、AI、生成、掉落
- Combat：伤害、命中、攻击规则、世界状态可攻击判定
- Corrosion：侵蚀值、净化、失败判定
- Reveal：鼠标圆环、能量、表里世界状态切换
- Progression：经验、升级奖励、局内成长
- Items：道具、被动效果、拾取逻辑

## 7. 当前建议

后续推进优先级建议如下：

1. 完整怪物状态与怪物攻击接入
2. 击杀收益与 reveal 能量恢复闭环
3. 经验与升级奖励的最小版本
4. 被动道具系统
5. 更多攻击模型与敌人种类
