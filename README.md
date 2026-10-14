# boss-with-infinite-health

This repository contains a DLL mod for Slay the Spire 2 `v0.107.1`.

## Behavior

- The button is visible only in encounters recognized as boss encounters.
- It is off by default. While on, `Hook.ShouldDie` denies boss death, target
  selection and hit checks continue to allow the boss, and the health bar shows
  its actual HP value, including negative values. The boss is exposed as alive to
  combat win-state checks so the combat manager does not enter its ending state.
- Turning it off keeps negative HP until the current player-turn boundary.
  Immediately before the enemy-turn death batch, negative HP is normalized to
  zero so vanilla death and resurrection/phase-change logic can proceed safely.
  Test Subject's SetMaxHp/Heal revival no longer receives negative HP.
- The state is reset at combat start/end.
- Hold the left mouse button and drag the switch to move it. A drag does not
  toggle survival. Position stays within the viewport and is remembered for the
  current game session.
- `affects_gameplay` is true, so every multiplayer player must install the same
  mod. The control is host-only.

## Default setting

Requires the installed BaseLib 3.4.7 or later (enable it in the mod list).
Open **MOD 配置 → boss-with-infinite-health** from the main menu or settings.
Toggle **每场 BOSS 战默认开启**. Changes save immediately and take effect in the
next boss encounter. The combat button remains host-only.

The existing configuration is preserved in `user://mod_configs/boss-with-infinite-health.cfg`:

```json
{
  "DefaultEnabled": false
}
```

Negative HP is still shown numerically for bosses and their minions. For primary
bosses only, while the switch is ON and HP is negative, the original health-bar
foreground is reused with a yellow tint, retaining its native texture and layer.
Extra damage fills the first yellow bar; completed bars overlap at full width. The
counter at the right reads **×N**, where N = floor(-HP / original maximum HP).
For an original maximum of 300: -299 has no completed layer, -300 shows ×1,
and -600 shows ×2. The initial red bar is not included in this extra-bar count.
Original maximum HP is captured when the creature's health display is attached;
subsequent maximum-HP changes do not alter this denominator.
The counter rolls toward its target with three strong bounce/scale pulses in
0.85 seconds, regardless of the number of bars gained. New hits replace the
active animation rather than queueing more animations; huge jumps skip numbers
between rendered frames and always end on the exact target.
Both visuals inherit native health-bar visibility and menu ordering. Toggle OFF
and encounter resets immediately clear the display and cancel queued animation.
These visuals do not intercept input or change combat logic.

## Build/install

```powershell
dotnet build .\boss-with-infinite-health.csproj -c Release
Copy-Item .\bin\Release\boss-with-infinite-health.dll 'F:\Steam\steamapps\common\Slay the Spire 2\mods\boss-with-infinite-health\'
Copy-Item .\boss-with-infinite-health.json 'F:\Steam\steamapps\common\Slay the Spire 2\mods\boss-with-infinite-health\'
```

The game must have mod loading enabled. The host-only rule is enforced locally.
STS2 v0.107.1 does expose `SendMessage<T>`/`RegisterMessageHandler<T>`, but its
message subtype table is internal and closed to third-party message types. This
build therefore does not claim cross-client state replication: install the mod
on every player, and use it only when all clients are running the same setting.
