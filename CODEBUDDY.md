# CODEBUDDY.md This file provides guidance to WorkBuddy when working with code in this repository.

## Commands

### Build
```powershell
cd "e:\Bond-Defense\Project"
dotnet build BondDefense.csproj
```
编译结果输出到 `.godot/mono/temp/bin/Debug/BondDefense.dll`。0 errors 表示成功，Nullable 已关闭，所有 53 条 nullable 警告均被抑制。

### Run
用 **Godot 4.6** 打开 `e:\Bond-Defense\Project` 目录后直接运行主场景（`res://Scenes/Main.tscn`，900×680 固定分辨率）。目前无 CLI 启动方式；必须通过 Godot Editor 或 Godot 可执行文件运行。

### Check / Lint
```powershell
cd "e:\Bond-Defense\Project"
dotnet build BondDefense.csproj --verbosity minimal
```
项目无独立 linter；编译时错误即代码质量关卡。

### 配置表导出（GDExcelExporter）
```powershell
# 双击执行（推荐）
e:\Bond-Defense\Project\tables\gen_all.bat

# 或命令行
cd "e:\Bond-Defense\Project\tables"
py -m gd_excelexporter.cli gen-all
# 导出后需手动同步到 Resources/Config
xcopy /E /Y /I dist ..\Resources\Config\
```
- 工具：`gd_excelexporter 3.0.1`（`py -m pip install gd_excelexporter packaging`）
- Excel 源文件位于 `tables/data/*.xlsx`
- 导出为 JSON → `tables/dist/`，再同步到 `Resources/Config/`
- 支持字段类型：`int`, `float`, `string`, `bool`, `array`, `array_str`, `array_bool`, `dict`
- `~` 开头的 Sheet 跳过，`*` 开头的字段跳过

---

## Architecture

### Overview

**Bond-Defense** 是一个 2D 角色塔防 + 自走棋羁绊系统游戏，使用 **Godot 4.6 + C# (.NET 8)** 编写。所有游戏逻辑通过 **代码构建场景树**（不依赖 .tscn 节点路径），以避免 TSCN 兼容性问题。

窗口尺寸：900×680，不可调整大小。

---

### Scene Tree（运行时）

```
Main (Node2D)           ← Main.cs，入口，BuildSceneTree() 动态创建所有子节点
├── SynergyManager      ← 羁绊管理单例，必须先于 GameManager 创建
├── Battlefield         ← 战场，位于 y=48
├── GameManager         ← 主控，依赖 Battlefield + SynergyManager
├── HeroStorage         ← 由 GameManager 懒创建，用于临时持有待部署英雄（触发 _Ready）
└── UILayer (CanvasLayer, layer=10)
    ├── TopBarUI        ← 顶部状态栏（金币/生命/波次/开始战斗）
    ├── SynergyPanel    ← 右侧羁绊状态面板
    ├── BenchUI         ← 待部署区（y=450）
    └── ShopUI          ← 商店（y=535）
```

节点查找规则：**所有节点通过 `GetParent().GetNode<T>("Name")` 获取兄弟节点**，或通过 `GetTree().Root.GetNode<Main>("Main").GetNode<T>(...)` 从根节点往下查找。不使用字符串路径 `"/root/Main/..."` 风格（易出错）。

---

### Core Systems

#### 1. 数据层 (`Scripts/Data/`)

| 类 | 职责 |
|---|---|
| `HeroData : Resource` | 英雄静态定义：名称/稀有度/价格/攻击/攻速/范围/HP/Tags/技能/颜色 |
| `SynergyData : Resource` | 羁绊定义：激活阈值数组 + 各阶效果描述 + 攻击/攻速/范围加成数组 |

**配置表系统（`Scripts/Config/`）**：
| 文件 | 职责 |
|---|---|
| `ConfigModels.cs` | 强类型配置 POCO：HeroConfig / SynergyConfig / WaveConfig / EnemyConfig / ShopConfig |
| `ConfigLoader.cs` | Node，`_Ready()` 时从 `res://Resources/Config/*/` 加载 JSON，提供静态 `Instance` 访问 |

JSON 源文件由 `tables/gen_all.bat` 一键从 Excel 导出并同步，路径：`res://Resources/Config/{表名}/{表名}.json`。

两个类均使用 `[GlobalClass]` 注册为 Godot 资源，可在 Inspector 中编辑。**当前实际上以内联方式在 `GameManager.InitHeroPool()` 和 `SynergyManager.LoadSynergyData()` 中直接 `new` 出来**，不依赖 `.tres` 资源文件。

升星倍率（`HeroData.GetStarMultiplier`）：1星=1.0×，2星=1.8×，3星=3.5×（仅攻击力）。

#### 2. 英雄实例 (`Scripts/Hero.cs`)

- 继承 `Node2D`，包含一个子节点 `BuffComponent`
- 视觉：八边形 `Polygon2D` + 边框 `Line2D` + 名称/星级 `Label` + `Area2D` 点击区域
- **战斗逻辑**：`_Process` 每帧遍历 `Battlefield.ActiveEnemies`，优先攻击 `PathProgress` 最大（最靠近终点）的射程内敌人
- 最终属性通过 `BuffComponent` 计算：`FinalAttack = baseAtk * starMult * (1 + buffBonus)`

新英雄创建流程：`GameManager.CreateHeroInstance()` → 加入 `HeroStorage`（触发 `_Ready` 初始化 `BuffComponent`）→ 存入 `_bench` 列表。放置到战场时通过 `hero.GetParent()?.RemoveChild(hero)` 再 `Battlefield.AddChild(hero)` 完成 reparent。

#### 3. 战场 (`Scripts/Battlefield.cs`)

- 7×4 格子网格，`GridOrigin=(60,80)`，`CellSize=80px`
- 内部维护 `Hero[,] _grid` 二维数组和 `ColorRect[,]` 格子视觉
- **敌人路径**：水平直线穿越战场中央（4个路径点），`_enemyPath` 在 `BuildPath()` 生成
- **拖拽系统**：通过 `_Input` 捕获鼠标事件，支持：
  - 从待部署区（`_isDraggingFromBench=true`）拖入战场
  - 战场格子间互换英雄
- 关键信号：`HeroPlaced(Hero, col, row)` / `EnemyReachedEnd()` / `EnemyKilled(int reward)`

#### 4. 敌人 (`Scripts/Enemy.cs`)

- 沿 `_path` 数组逐点移动，记录 `PathProgress(0-1)` 供英雄瞄准优先
- `TakeDamage()` 扣血 + 受击闪白 Tween；到达终点或 HP≤0 时触发 `EnemyDied(enemy, reachedEnd)` 信号后 QueueFree

#### 5. GameManager (`Scripts/GameManager.cs`)

**状态机**：`GameState { Prepare, Battle, GameOver }`，通过 `StateChanged(int)` 信号广播（用 `int` 而非枚举，以确保 C# 委托签名兼容 Godot 信号系统）。

**商店**：5 个槽位，从 8 种英雄的内联池（`_heroPool`）随机抽取。刷新消耗 2 金币；商店锁定时波次结束不自动刷新。

**波次参数**（第 N 波）：
- 敌人数量：`5 + N*2`
- 基础 HP：`80 + N*30`
- 速度：`70 + N*5` px/s
- 生成间隔：`1.5 - N*0.05` 秒

**升星合成**：收集场上+待部署区同名同星级英雄，满 3 个时发出 `MergeAvailable` 信号提示。`TryMerge()` 消耗 3 个英雄后创建升星英雄放入待部署区。

波次结束奖励：`10 + wave*2` 金币。

#### 6. SynergyManager (`Scripts/SynergyManager.cs`)

单例（`SynergyManager.Instance`），在 `Main.cs` 中作为普通节点存在（非 `AutoLoad`）。

**羁绊计算流程**（每次战场英雄变化时 `GameManager.UpdateSynergies()` 触发）：
1. 清空 `_tagCounts`；遍历所有战场英雄，统计每个 Tag 的数量
2. 按各 `SynergyData.Thresholds` 判断激活阶级（Tier）
3. 清除所有英雄的 `BuffComponent`，重新按激活羁绊为每个英雄叠加对应 Buff
4. 发出 `SynergiesUpdated` 信号通知 `SynergyPanel` 刷新 UI

5 种羁绊：人类（攻击）/ 精灵（攻速+范围）/ 战士（攻击+攻速）/ 法师（范围）/ 野兽（攻速），各 2 个阈值阶级。

#### 7. UI 层 (`Scripts/UI/`)

| 文件 | 职责 |
|---|---|
| `TopBarUI.cs` | 监听 `GoldChanged` / `LifeChanged` / `WaveChanged` / `StateChanged` 信号，显示顶部信息 |
| `ShopUI.cs` | 监听 `ShopRefreshed` 信号，动态创建 5 个英雄购买卡片；状态枚举引用为 `GameManager.GameState` |
| `BenchUI.cs` | 监听 `BenchChanged` 信号，展示最多 8 个待部署英雄；点击/拖拽启动 `Battlefield.StartDragFromBench()` |
| `SynergyPanel.cs` | 监听 `SynergiesUpdated` 信号，动态刷新所有羁绊条目（含激活状态颜色和进度） |

所有 UI 节点通过 `GetTree().Root.GetNode<Main>("Main").GetNode<T>(...)` 获取游戏逻辑节点引用。

---

### Key Conventions

- **无 AutoLoad 单例**：`SynergyManager` 虽有 `static Instance`，但作为普通子节点加入场景，不是 Godot AutoLoad。
- **信号枚举参数用 `int`**：跨 C# 委托传递枚举时，一律用 `int` 强转，接收端再还原 `(GameManager.GameState)state`。
- **所有游戏数据内联**：英雄池和羁绊数据直接在 C# 代码中 `new` 创建，不依赖外部 `.tres` 资源文件，方便快速迭代。
- **`<Nullable>disable</Nullable>`**：项目关闭了 nullable，所有引用类型无需 `?` 注解。
