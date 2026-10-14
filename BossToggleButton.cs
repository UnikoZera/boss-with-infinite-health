using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace BossSurvival;

internal static class BossToggleButton
{
    private static readonly HashSet<Button> Buttons = new();

    public static void Attach(NCombatRoom room)
    {
        var button = new Button
        {
            Name = "BossSurvivalToggle",
            Text = "Boss survival: OFF",
            CustomMinimumSize = new Vector2(220, 48),
            Position = new Vector2(28, 28),
            ZIndex = 100,
            MouseDefaultCursorShape = Control.CursorShape.PointingHand
        };
        button.Pressed += () => BossCombatState.TrySetEnabled(!BossCombatState.Enabled);
        room.AddChild(button);
        Buttons.Add(button);
        Refresh(button);
    }

    public static void RefreshAll()
    {
        foreach (var button in Buttons.ToArray())
        {
            if (!GodotObject.IsInstanceValid(button))
            {
                Buttons.Remove(button);
                continue;
            }
            Refresh(button);
        }
    }

    private static void Refresh(Button button)
    {
        button.Visible = BossCombatState.IsBossCombat;
        button.Disabled = !BossCombatState.CanChange;
        button.Text = BossCombatState.Enabled ? "Boss survival: ON" : "Boss survival: OFF";
    }
}

[HarmonyPatch(typeof(NCombatRoom), "_Ready")]
internal static class CombatRoomUiHook
{
    private static void Postfix(NCombatRoom __instance)
    {
        BossToggleButton.Attach(__instance);
    }
}
