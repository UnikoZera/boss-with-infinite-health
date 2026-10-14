# Slay the Spire 2 Mod 制作调查（v0.107.1）

调查日期：2026-10-14。目标安装目录：`F:\\Steam\\steamapps\\common\\Slay the Spire 2`。

## 证据范围与版本

- 本机 `release_info.json`：版本 `v0.107.1`，commit `59260271`。这是本机运行时的第一手版本标记。
- 本机 `data_sts2_windows_x86_64/sts2.xml`：游戏程序集公开 XML 文档，包含类型、方法签名和行为说明。
- 本机 `data_sts2_windows_x86_64/sts2.dll`：通过 .NET 反射检查实际类型、属性、字段和方法签名。
- 公开 mod 教程仓库（其 README 明确说明内容基于 STS2 Early Access，并记录 `ModManager`/Godot/PCK/Harmony 工作流）：
  - https://github.com/fresh-milkshake/Modding-Tutorial
  - https://fresh-milkshake.github.io/Modding-Tutorial/
  - Mod 加载布局：https://github.com/fresh-milkshake/Modding-Tutorial/blob/main/docs/02-vanilla-sts2-layout.md
  - Manifest/构建：https://github.com/fresh-milkshake/Modding-Tutorial/blob/main/docs/03-mod-loading-and-manifests.md
  - Hooks/Harmony：https://github.com/fresh-milkshake/Modding-Tutorial/blob/main/docs/05-entrypoints-harmony-and-hooks.md
  - 原生 UI：https://github.com/fresh-milkshake/Modding-Tutorial/blob/main/docs/14-custom-gui.md

Early Access 更新会改变签名；发布 mod 前必须重新核对本机 `release_info.json`、`sts2.runtimeconfig.json`、`sts2.dll` 和 XML。

## Mod loader、DLL 与 PCK

公开教程对当前 loader 的描述与本机 XML 一致：`ModManager.Initialize(IModManagerFileIo, ModSettings, SemanticVersion)` 会扫描游戏目录旁的 `mods` 以及 Steam Workshop；manifest 以 JSON 表示，按依赖拓扑排序，然后按 `has_dll`/`has_pck` 加载 `<id>.dll`/`<id>.pck`。`ModInitializerAttribute` 指定静态初始化方法；没有 initializer 时 loader 会对程序集自动执行 Harmony `PatchAll`。

manifest 的核心字段（教程记录）是 `id`、`name`、`author`、`description`、`version`、`has_dll`、`has_pck`、`dependencies`、`affects_gameplay`。游戏会递归读取 `mods` 下的 JSON；因此不要在该目录放不属于 manifest 的 JSON 配置文件。PCK 通过 Godot `ProjectSettings.LoadResourcePack` 加载，资源路径位于 `res://` 下。

本机 `data_sts2_windows_x86_64` 直接提供 `sts2.dll`、`GodotSharp.dll` 和 `0Harmony.dll`。教程示例项目目标框架为 `net9.0`，但应以本机 `sts2.runtimeconfig.json` 为准。

## 战斗与 BOSS 相关的一手 API

从本机 `sts2.xml`/`sts2.dll` 反射确认：

- `MegaCrit.Sts2.Core.Entities.Creatures.Creature` 暴露可写 `CurrentHp`、`MaxHp`，并有 `IsMonster`、`IsEnemy`、`IsPrimaryEnemy`、`IsAlive`、`IsDead`、`CombatState`、`Monster` 等属性。内部还有 `LoseHpInternal(Decimal, ValueProp)`、`SetCurrentHpInternal(Decimal)`，所以直接写属性不应被假定等同于正常伤害命令。
- 本机反射列出的 `RoomType` 枚举明确包含 `Boss`（另有 `Monster`、`Elite`、`Event` 等）；`CombatRoom.RoomType == RoomType.Boss` 是当前版本识别 BOSS 房间的直接依据。`NetGameType` 枚举包含 `None`、`Singleplayer`、`Host`、`Client`、`Replay`，可用于区分房主与客户端网络角色（具体服务实例仍需从游戏运行时取得）。
- `Creature.CurrentHp` 的运行时类型是 `System.Int32`，因此“负数血量”在字段类型层面可表示；但 XML 没有承诺 setter 是否允许负数，亦没有承诺 UI 或死亡检测对负数的处理。需要在目标版本游戏中实测/反编译调用链。
- `MegaCrit.Sts2.Core.Hooks.Hook` 有以下相关 hook：
  - `BeforeCombatStart(IRunState, ICombatState)`
  - `AfterCombatEnd(IRunState, ICombatState, CombatRoom)`
  - `AfterCombatVictory(IRunState, ICombatState, CombatRoom)`
  - `AfterCreatureAddedToCombat(ICombatState, Creature)`
  - `AfterCurrentHpChanged(IRunState, ICombatState, Creature, Decimal)`
  - `BeforeDamageReceived(...)` / `AfterDamageReceived(...)`
  - `BeforeDeath(IRunState, ICombatState, Creature)` / `AfterDeath(...)`
  - `BeforeSideTurnStart(...)` / `AfterSideTurnStart(...)`
  - `BeforeTurnEnd(ICombatState, CombatSide, IEnumerable<Creature>)` / `AfterTurnEnd(...)`
- `CombatManager` 的 XML 明确提供事件：`CombatSetUp`、`CombatEnded`、`CombatWon`、`TurnStarted`、`TurnEnded`、`PlayerEndedTurn(Player, Boolean)`、`PlayerUnendedTurn(Player)`、`AboutToSwitchToEnemyTurn(CombatState)`。其中 `AboutToSwitchToEnemyTurn` 的说明是：所有玩家已经完全提交结束回合且所有玩家行动（包括结束回合 hooks，例如 Well-Laid Plans）完成，但玩家手牌 flush 之前。这个时机最符合“所有玩家结束回合后，按开关与 BOSS HP 决定继续行动或死亡”的需求。
- `CombatManager.IsEnding` 的 XML 说明：当所有敌人死亡且没有阻止战斗结束的因素时为 true；`IsOverOrEnding` 可用于跳过已结束或正在结束的效果。若要保留 BOSS 行动，必须在原版死亡/结束检查之前拦截，而只在 `AboutToSwitchToEnemyTurn` 判断可能已经太晚；实际顺序需要反编译 v0.107.1 `CombatManager`/`CreatureCmd`/死亡检查调用者确认。
- `CreatureCmd` 提供 `Damage(...)`、`Kill(Creature, Boolean)`、`KillWithoutCheckingWinCondition(...)`、`SetCurrentHp(Creature, Decimal)`。XML 明确指出 `Kill` 的 `forceDeath` 参数会绕过死亡预防。直接调用 `Kill` 或直接写 `CurrentHp` 可能绕过动作队列、动画、死亡预防和联机同步。
- 反射显示 `AbstractModel`/`MonsterModel` 还有 `ShouldStopCombatFromEnding(): Boolean`、`ShouldCreatureBeRemovedFromCombatAfterDeath(Creature): Boolean`、`AfterPreventingDeath(Creature): Task` 等模型钩子；但 XML 未给出这些方法的完整调用时序。`ShouldStopCombatFromEnding` 可能是阻止 `CheckWinCondition` 结束战斗的更合适扩展点，仍需反编译确认它是否在 BOSS死亡标记前调用、以及是否会保留已死亡 Creature。
- 更直接的死亡判定 hook 是 `AbstractModel.ShouldDie(Creature): Boolean`；本机 XML 原文说明为“If a creature is about to die, should they still?”，返回“Whether the creature should still die”。另有 `ShouldDieLate(Creature): Boolean`，但文档警告通常应使用 `ShouldDie`。`Hook.ShouldDie(IRunState, ICombatState, Creature, ref AbstractModel)` 会聚合该谓词。若通过 mod-owned `AbstractModel`/combat hook subscriber 接入，理论上可在 BOSS 且开关开启时返回 false，保留其行动并避免直接死亡；仍需验证谓词聚合规则、负 HP UI、以及关闭开关后的延迟死亡行为。

## 联机与房主权威

本机 XML/API 对联机动作给出明确的主机模型：

- `ActionQueueSynchronizer.RequestEnqueue(GameAction)`：房主直接入队并通知客户端；客户端不直接入队，而是向房主请求，房主确认后保持动作顺序。
- `RequestEnqueueHookAction(GenericHookGameAction)`：hook action 也由房主确认并广播；所有 peer 执行 hook，因此必须保持确定性。
- `SetCombatState(...)` 的说明要求在战斗开始、结束或回合变化时同步动作队列。
- `INetGameService` 暴露 `NetId`、`IsConnected`、`Type`、`SendMessage<T>`、`RegisterMessageHandler<T>`；`NetHostGameService` 额外暴露 `ConnectedPeers`、`NetHost` 和广播/客户端连接事件；`NetClientGameService` 暴露 `HostNetId`。这些 API 可用于识别当前网络角色和发送自定义消息，但 XML 没有现成的“mod 设置同步”接口。
- `ModManager.GetGameplayRelevantModNameList()` 的 XML 说明明确写着“for multiplayer - used to compare mods between different players”。因此 manifest 应标记 `affects_gameplay: true`，并要求所有联机玩家安装同一 mod；否则可能触发联机 mod 不一致检查。
- 原生 UI 示例 `IOverlayScreen` 使用 `NetScreenType.None` 时并不会自动同步。教程明确警告：改变 `NetScreenType` 枚举不会产生同步；改变共享游戏状态需要单独的网络消息/命令设计。

结论：房主单独控制开关应在房主侧验证并修改状态，状态变化通过 `INetGameService`/游戏动作队列广播；客户端 UI 只显示房主下发的状态。具体自定义消息类型、序列化注册点和“判断是否为房主”的全局单例名称在本机 XML 中没有一个可直接复用的 mod API，需针对 `NetHostGameService`/`NetClientGameService` 以及消息总线反编译验证。

## 设置与 UI

本机 `MegaCrit.Sts2.Core.Modding.ModSettings` 只有两个属性：

- `PlayerAgreedToModLoading: Boolean`
- `ModList: List<SettingsSaveMod>`（手动加载顺序与启用/禁用状态）

这不是 mod 自定义配置容器；它没有每个 mod 的任意键值字段。要提供“默认开关状态可在 mod 设置中调整”，需要在 mod 自己的持久化文件中保存布尔值，或将选项注入原生 Modding/Settings UI。由于 loader 会把 `mods` 下所有 JSON 当作潜在 manifest，任意配置文件应放在 Godot `user://` 数据目录或使用非 JSON 扩展名，并在代码中明确读写。

公开教程的原生 UI 路径是：在 `NRun._Ready` 后挂载控制器，创建实现 `IOverlayScreen` 的 Godot Control，再通过 `NOverlayStack.Push/Remove` 管理生命周期、焦点和遮罩。若开关出现在战斗画面，可在战斗 UI 生命周期（例如 `NCombatRoom`/`NCombatUi`）挂载控制器；具体节点路径和可复用场景必须从本机 PCK 与当前版本场景树核对。

## 对需求的实现风险与未知项

已确认可作为实现基础：

1. 仅 BOSS：可通过 `CombatRoom.RoomType`/`IsVictoryRoom`/`Encounter` 以及敌人 `IsPrimaryEnemy`、`Monster` 等信息识别战斗，但 XML 没有一个名为 `IsBoss` 的通用 `Creature` 属性；需按 `CombatRoom`/`EncounterModel` 的实际字段或 BOSS 模型基类确认。
2. 开关默认值与每场战斗重置：可由 mod 自己的运行时状态 + 自定义持久化实现；`BeforeCombatStart`/`AfterCombatEnd` 提供生命周期点。
3. “血量到 0 不死亡且允许负数”：存在 `BeforeDeath` 和可写 `CurrentHp`，但 XML 没有承诺 `BeforeDeath` 是可否决 hook 的返回值，也没有承诺死亡检查只在回合结束执行。必须反编译/运行测试确认；不能仅凭 API 名称实现。
4. “关闭时本回合不死、回合结束死亡”：`AboutToSwitchToEnemyTurn` 是最接近需求的公共事件，但需要验证死亡检查发生在它之前还是之后，以及切换开关时是否已触发 `BeforeDeath`。
5. 房主控制：网络动作队列和 `INetGameService` 的房主请求/广播语义已确认；自定义消息与权限校验的完整注册 API 未在公开 XML 中找到。

未知且应在编码前确认：

- 当前 BOSS 判定的稳定类型/属性（不能假定所有 BOSS 都是同一个类）。
- `Creature.CurrentHp` setter 是否允许负数，`IsAlive` 是否严格按 `CurrentHp > 0` 判断。
- BOSS 死亡触发点、`CheckWinCondition` 调用链以及 `BeforeDeath` 的确切返回/修改语义。
- v0.107.1 原生 Modding 设置页面是否允许第三方注入任意复选框；教程只证明可注入运行时 overlay，不证明 settings screen 扩展契约。
- 自定义网络消息的协议版本、消息注册和所有 peer 状态恢复流程。

