using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace BossSurvival;

internal static class BossToggleButton
{
    private static readonly HashSet<Button> Buttons = new();
    private static readonly Dictionary<Button, PanelContainer> Panels = new();
    private static readonly HashSet<CanvasLayer> Layers = new();

    public static void Attach(NCombatRoom room)
    {
        var layer = new CanvasLayer
        {
            Name = "BossSurvivalOverlay",
            Layer = 1000,
            FollowViewportEnabled = true
        };
        room.AddChild(layer);

        var panel = new PanelContainer
        {
            Name = "BossSurvivalPanel",
            Position = new Vector2(34, 116),
            CustomMinimumSize = new Vector2(300, 74),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        panel.AddThemeStyleboxOverride("panel", MakePanelStyle());
        layer.AddChild(panel);

        var button = new Button
        {
            Name = "BossSurvivalToggle",
            Text = "BOSS SURVIVAL  |  OFF",
            CustomMinimumSize = new Vector2(300, 74),
            MouseFilter = Control.MouseFilterEnum.Stop,
            FocusMode = Control.FocusModeEnum.All,
            MouseDefaultCursorShape = Control.CursorShape.PointingHand,
            Alignment = HorizontalAlignment.Left
        };
        button.AddThemeFontSizeOverride("font_size", 18);
        button.AddThemeColorOverride("font_color", new Color("#f1ead9"));
        button.AddThemeColorOverride("font_hover_color", new Color("#fff4cf"));
        button.AddThemeColorOverride("font_pressed_color", new Color("#ffffff"));
        button.AddThemeColorOverride("font_disabled_color", new Color("#8d8a83"));
        button.AddThemeStyleboxOverride("normal", MakeButtonStyle("#252b31", "#626a70", 1));
        button.AddThemeStyleboxOverride("hover", MakeButtonStyle("#35414a", "#d39b45", 2));
        button.AddThemeStyleboxOverride("pressed", MakeButtonStyle("#1d382e", "#69b78a", 2));
        button.AddThemeStyleboxOverride("disabled", MakeButtonStyle("#202328", "#454a4f", 1));
        button.Pressed += () =>
        {
            Log.Info("[boss-with-infinite-health] toggle pressed");
            BossCombatState.TrySetEnabled(!BossCombatState.Enabled);
        };
        panel.AddChild(button);
        Layers.Add(layer);
        Buttons.Add(button);
        Panels[button] = panel;
        Log.Info($"[boss-with-infinite-health] toggle attached: boss={BossCombatState.IsBossCombat}, canChange={BossCombatState.CanChange}");
        Refresh(button);
    }

    public static void RefreshAll()
    {
        foreach (var button in Buttons.ToArray())
        {
            if (!GodotObject.IsInstanceValid(button))
            {
                Buttons.Remove(button);
                Panels.Remove(button);
                continue;
            }
            Refresh(button);
        }

        foreach (var layer in Layers.ToArray())
        {
            if (!GodotObject.IsInstanceValid(layer))
                Layers.Remove(layer);
        }
    }

    private static void Refresh(Button button)
    {
        button.Visible = BossCombatState.IsBossCombat;
        if (Panels.TryGetValue(button, out var panel) && GodotObject.IsInstanceValid(panel))
            panel.Visible = BossCombatState.IsBossCombat;
        button.Disabled = !BossCombatState.CanChange;
        button.Text = BossCombatState.Enabled
            ? "BOSS SURVIVAL  |  ON"
            : "BOSS SURVIVAL  |  OFF";
    }

    private static StyleBoxFlat MakePanelStyle() => new()
    {
        BgColor = new Color("#12161a"),
        BorderWidthLeft = 2,
        BorderWidthTop = 2,
        BorderWidthRight = 2,
        BorderWidthBottom = 2,
        BorderColor = new Color("#8d6a3b"),
        CornerRadiusTopLeft = 7,
        CornerRadiusTopRight = 7,
        CornerRadiusBottomLeft = 7,
        CornerRadiusBottomRight = 7,
        ShadowColor = new Color(0, 0, 0, 0.45f),
        ShadowSize = 8
    };

    private static StyleBoxFlat MakeButtonStyle(string background, string border, int borderWidth) => new()
    {
        BgColor = new Color(background),
        BorderWidthLeft = borderWidth,
        BorderWidthTop = borderWidth,
        BorderWidthRight = borderWidth,
        BorderWidthBottom = borderWidth,
        BorderColor = new Color(border),
        CornerRadiusTopLeft = 5,
        CornerRadiusTopRight = 5,
        CornerRadiusBottomLeft = 5,
        CornerRadiusBottomRight = 5,
        ContentMarginLeft = 22,
        ContentMarginRight = 18
    };
}

[HarmonyPatch(typeof(NCombatRoom), "_Ready")]
internal static class CombatRoomUiHook
{
    private static void Postfix(NCombatRoom __instance)
    {
        BossToggleButton.Attach(__instance);
    }
}
