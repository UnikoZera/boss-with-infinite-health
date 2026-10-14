# BOSS 无限生命

这个工作区用于发布《杀戮尖塔 2》的 `boss-with-infinite-health` mod。
Mod 会在 BOSS 战中添加只有房主可以操作的开关。开启后，BOSS 在生命值低于
0 时仍可继续行动；关闭后，会在敌方回合边界恢复原版死亡处理。

当前版本：`0.1.0`，适用于游戏版本 `0.107.1`。

开关位置会保存到 mod 配置，并在之后的游戏中恢复。负数生命值会显示为分段
黄色进度：每完成一条原始血量，`xN` 计数器增加，下一条黄色血条从空开始。
需要 BaseLib `3.4.7` 或更高版本。多人游戏中的所有玩家都必须使用相同版本。

## 目录结构

* `workshop.json` -- Steam Workshop 工作区配置。
* `content` -- 要上传到 Steam Workshop 的 mod 文件，包含 JSON 清单和 DLL。
* `previews` -- 可选的附加预览图，单个文件必须小于 1 MB。
* `image.png` -- Steam Workshop 中显示的主图片，必须小于 1 MB。
* `README.md` -- 本说明文件。

## 上传方式

登录 Steam 后，在上传器目录运行 `..\Upload.bat`。脚本只上传当前工作区，
不会自动编译 mod。上传前请确认 `content` 中是最新构建文件。
