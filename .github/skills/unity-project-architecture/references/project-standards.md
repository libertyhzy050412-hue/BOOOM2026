# 项目架构规范

这份规范用于统一项目目录结构、模块职责、依赖方向和允许使用的技术栈，目标是保证可扩展性、易读性和后期重构成本可控。

## 1. 总体原则

1. 功能优先于类型分类。优先按玩法或业务模块组织代码，而不是全局按 Manager、Controller、Utils 平铺。
2. 单一职责。一个脚本只负责一个明确层级的问题，不把输入、渲染、物理、状态和 UI 混进同一个组件。
3. 明确依赖方向。低层不依赖高层，通用模块不依赖具体玩法模块。
4. 显式接线。优先使用序列化引用、初始化注入、轻量注册表，不依赖到处查找场景对象。
5. 可测试逻辑下沉。纯规则和状态转换尽量写成普通 C# 类，MonoBehaviour 只做装配、驱动和生命周期桥接。

## 2. 目录结构

推荐的 Assets 目录演进目标如下：

```text
Assets/
  Art/
  Audio/
  Data/
    Config/
    Runtime/
  Prefabs/
  Scenes/
    Bootstrap/
    Gameplay/
    Sandbox/
  Scripts/
    Core/
      Bootstrap/
      SceneFlow/
      Input/
      Services/
      Save/
      Utilities/
    Features/
      VisionBoundary/
      Player/
      Interaction/
      Puzzle/
      Enemy/
    UI/
      Common/
      HUD/
      Menus/
      Dialog/
    Data/
    Tests/
  Settings/
  UI/
```

当前项目还很早期，可以逐步迁移，不要求一次性重排所有目录，但新增内容必须往这个目标结构收敛。

## 3. 模块职责

### Core

放全项目通用基础设施，只解决跨模块都会遇到的问题。

允许内容：

- Bootstrap 启动流程
- SceneFlow 场景切换与初始化
- Input 接入层
- Services 轻量服务注册与访问
- Save 存档读写
- Utilities 真正通用的工具代码

禁止内容：

- 某个具体玩法的规则实现
- 只给单一 Feature 用的临时代码
- UI 细节和表现逻辑

### Features

每个玩法系统单独成模块，内部自包含。

每个 Feature 推荐包含：

- Components：挂场景对象的 MonoBehaviour
- Runtime：纯运行逻辑类
- Data：该 Feature 自己的 ScriptableObject 和数据定义
- Integration：与 UI、场景或其他系统对接的桥接层

Feature 对外暴露明确入口，不允许其他模块直接访问它的内部零散脚本。

### UI Stack

运行时 UI 统一放在 Scripts/UI 和相关 UGUI 资源目录下。

职责：

- 页面显示
- HUD 刷新
- 菜单交互
- 文本与动画表现

禁止：

- 直接写底层玩法计算
- 直接控制物理碰撞和渲染裁切细节

### Data

放全局配置定义和跨模块共享的数据模型。

职责：

- 游戏参数配置
- 全局枚举与常量定义
- 可序列化数据结构

### Editor

只放编辑器工具、构建脚本、批量生成器、验证工具。

Runtime 绝不能依赖 Editor。

### Tests

只放测试代码。

- EditMode：测纯规则、状态转换、工具类
- PlayMode：测关键玩法链路和场景交互

## 4. 场景规范

项目采用启动场景和游戏场景分离。

### Bootstrap 场景

职责：

- 初始化核心服务
- 加载输入、全局配置和必要持久对象
- 决定进入主菜单或游戏场景

约束：

- 不承载具体玩法对象
- 不堆测试内容

### Gameplay 场景

职责：

- 承载实际游戏内容
- 包含当前关卡、角色、交互对象和运行时 UI 挂载点

### Sandbox 场景

职责：

- 原型试验
- 单功能验证

约束：

- 不作为正式入口
- 不把临时测试逻辑反向污染 Core 和正式 Feature

## 5. 技术栈白名单

### 渲染与 2D

- 使用 URP 2D
- 使用 SpriteRenderer、SpriteMask、Tilemap、Sorting Layer、PolygonCollider2D 作为 2D 主链路
- 单个系统内只允许一套主显示裁切方案

例如 VisionBoundary 系统中，显示由 SpriteMask 驱动，物理由代理碰撞体驱动，二者职责不能混用。

### UI

- 运行时 UI：UGUI + TextMeshPro
- 编辑器工具 UI：可使用 UI Toolkit
- Sandbox 调试 UI 可以临时使用 OnGUI，但布局参数必须配置化，避免依赖硬编码坐标导致元素重叠

禁止在运行时 UI 中同时并行推进 UGUI 和 UI Toolkit 两套主方案，除非有明确迁移计划。

### 输入

- 统一使用 Input System
- 旧 Input API 只允许出现在极少数兼容层，不允许在业务模块里直接混用

### 数据

- 静态配置和可调参数：ScriptableObject
- 存档和运行期持久化数据：JSON 或 Unity 序列化数据结构

Sandbox 原型阶段允许的补充规则：

- Sandbox 内的临时可调参数可以使用带注释的 JSON TextAsset，但文件必须放在 Sandbox 自己的目录中，并由专用加载器负责去注释和解析
- JSON 配置必须写明文件作用与字段含义，不允许只留下裸字段名
- 参数归属优先跟随实际拥有该行为或属性的对象。例如单个怪物提供多少侵蚀值，应写在怪物配置中，而不是写进全局侵蚀系统配置
- 全局系统配置只保留系统级阈值、共享规则和跨实体公共开关，不吞并实体局部参数
- 场景结构型参数如果更适合编辑器内即时迭代，可以保留 Inspector 直调，例如 Sandbox 地图边界，而不强行改成文件配置

### 依赖管理

- 使用轻量 Bootstrap + Service Registry
- 不引入 Zenject 之类的重型 DI 框架，除非项目规模已明显超出当前复杂度

Service Registry 只用于少量全局基础服务：

- 场景流转
- 存档
- 音频总线
- 全局输入门面

不要把所有东西都注册成全局服务。

## 6. 编码约束

1. MonoBehaviour 不负责复杂业务编排和大段规则计算。
2. 禁止在普通业务代码中泛滥使用 Find、FindObjectOfType、FindFirstObjectByType。
3. 公共工具类必须证明是跨模块复用，否则留在所属模块内部。
4. 新增系统时必须先决定模块归属，再创建脚本。
5. 模块对外暴露入口类或 Facade，不直接暴露内部细碎实现。
6. 状态变化要有清晰命名，不使用含义模糊的 flag 拼装流程。
7. 不把“临时能跑”的测试代码长期留在正式模块里。
8. 调试或原型 UI 的位置、尺寸、间距等布局参数要配置化，不使用一组难以维护的魔法数硬编码到底。

## 7. 命名建议

- 目录名使用清晰英文名，按业务含义命名
- Feature 名称使用完整业务词，不用含糊缩写
- ScriptableObject 以用途命名，例如 PlayerConfig、AudioBusConfig
- 页面类命名体现页面含义，例如 MainMenuView、HUDView
- 桥接和入口类可使用 Facade、Installer、Bootstrap、Controller，但前提是职责明确

## 8. 新功能接入流程

1. 判断这是 Core、Feature、UI、Data 还是 Editor 的工作。
2. 明确这个模块解决什么问题，不解决什么问题。
3. 先定场景接入点，再定脚本目录。
4. 明确使用哪套技术栈，不允许边做边换主方案。
5. 定义最小公开接口。
6. 再实现组件和数据结构。
7. 最后补测试、调试入口和文档。

## 9. 反模式

- Scripts 下不断追加全局 Manager、Controller、Helper 文件
- 一个 MonoBehaviour 同时读输入、改动画、改碰撞、改 UI、改存档
- 每个模块都自己维护一套事件系统或单例
- UI 直接调用底层物理对象细节
- Feature A 直接访问 Feature B 的内部组件字段
- 临时 Sandbox 逻辑混入正式场景和正式模块

## 10. 当前项目的执行基线

从现在开始，新增内容遵守下面几条底线：

1. 运行时 UI 固定为 UGUI + TextMeshPro。
2. 启动场景与游戏场景分离。
3. 使用轻量 Bootstrap + Service Registry 管理少量全局服务。
4. 输入统一走 Input System。
5. 玩法系统优先放到 Scripts/Features/对应模块名 下。
6. VisionBoundary 这类玩法特性继续保持“视觉系统”和“物理系统”职责分离，不回退到混合代理渲染方案。
