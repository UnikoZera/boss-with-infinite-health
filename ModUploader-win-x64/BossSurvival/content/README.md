# BOSS 无限生命 0.1.2

适用于《杀戮尖塔 2》；已在 `v0.113.1`、BaseLib `3.4.8` 环境完成启动加载验证。
需要启用 BaseLib（清单要求最低 `3.4.7`）。

BOSS 战中房主可操作无限生命开关。开启后，BOSS 与敌方爪牙可保留负数生命值；
黄色血条与 `×N` 计数器按各自初始最大生命值显示溢出伤害。
关闭后在回合边界交回原版死亡与阶段切换处理。开关位置和默认开启设置可保存。
多人游戏所有玩家必须安装相同版本；本构建不保证跨客户端开关状态同步。

## 本次修复

- 修复爪牙缺少溢出血条与计数器。
- 移除依赖旧 `CombatManager._state` 的补丁，修复启动报错。
- 添加当前胜利判定入口保护，调整 `CreatureCmd.Damage` 目标解析。
- 伤害目标没有直接 `IsDead` 调用时保留原 IL，避免初始化中断。
- 输出完整 Harmony 初始化异常堆栈。

## 发布文件

- `boss-with-infinite-health.dll`：编译后的 mod 程序集。
- `boss-with-infinite-health.json`：版本及依赖清单。
- `boss-with-infinite-health.deps.json`：程序集依赖描述。
- `boss-with-infinite-health.pdb`：调试符号。

本目录用于 Steam Workshop 上传；上传器不会自动编译。
本次验证覆盖 Release 构建及实际游戏启动初始化，未覆盖全部 BOSS 实战行为。
