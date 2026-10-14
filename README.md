# Boss Survival Switch

This repository contains a DLL mod for Slay the Spire 2 `v0.107.1`.

## Behavior

- The button is visible only in encounters recognized as boss encounters.
- It is off by default. While on, `Hook.ShouldDie` denies boss death and
  `Hook.ShouldStopCombatFromEnding` keeps the combat alive when a boss is at or
  below zero HP.
- Turning it off does not resurrect or clamp HP. A boss already below zero is
  allowed to finish the current player-turn boundary, then vanilla death logic
  proceeds at the enemy-turn transition.
- The state is reset at combat start/end.
- `affects_gameplay` is true, so every multiplayer player must install the same
  mod. The control is host-only.

## Default setting

The first run creates `user://mod_configs/BossSurvival.cfg`. Edit it while the game is
closed:

```json
{
  "DefaultEnabled": false
}
```

This is the mod's persistent default for each new boss combat. STS2 v0.107.1
does not expose a public per-mod settings schema, so the file is used instead.

## Build/install

```powershell
dotnet build -c Release
Copy-Item .\bin\Release\BossSurvival.dll 'F:\Steam\steamapps\common\Slay the Spire 2\mods\BossSurvival\'
Copy-Item .\BossSurvival.json 'F:\Steam\steamapps\common\Slay the Spire 2\mods\BossSurvival\'
```

The game must have mod loading enabled. The host-only rule is enforced locally.
STS2 v0.107.1 does expose `SendMessage<T>`/`RegisterMessageHandler<T>`, but its
message subtype table is internal and closed to third-party message types. This
build therefore does not claim cross-client state replication: install the mod
on every player, and use it only when all clients are running the same setting.
