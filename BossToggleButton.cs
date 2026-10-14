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
    private static readonly Dictionary<Button, (Label State, Panel Track, Panel Thumb)> Indicators = new();
    private static Vector2 SavedPosition = new(34, 116);

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
            Position = SavedPosition,
            CustomMinimumSize = new Vector2(324, 86),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        panel.AddThemeStyleboxOverride("panel", MakePanelStyle());
        layer.AddChild(panel);

        var button = new Button
        {
            Name = "BossSurvivalToggle",
            Text = "",
            CustomMinimumSize = new Vector2(324, 86),
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
        button.AddThemeStyleboxOverride("normal", MakeButtonStyle("#18232e", "#4e6070", 1));
        button.AddThemeStyleboxOverride("hover", MakeButtonStyle("#263746", "#f5d478", 2));
        button.AddThemeStyleboxOverride("pressed", MakeButtonStyle("#152e2c", "#73dec0", 2));
        button.AddThemeStyleboxOverride("disabled", MakeButtonStyle("#202328", "#454a4f", 1));
        var dragging = false;
        var moved = false;
        var dragStart = Vector2.Zero;
        var panelStart = Vector2.Zero;
        button.GuiInput += input =>
        {
            if (input is InputEventMouseButton mouse && mouse.ButtonIndex == MouseButton.Left)
            {
                if (mouse.Pressed)
                {
                    dragging = true;
                    moved = false;
                    dragStart = panel.GetGlobalMousePosition();
                    panelStart = panel.Position;
                }
                else
                {
                    dragging = false;
                    if (moved)
                    {
                        SavedPosition = panel.Position;
                        // Consume drag release before BaseButton treats it as a click.
                        button.SetPressedNoSignal(false);
                        button.AcceptEvent();
                    }
                }
            }
            else if (input is InputEventMouseMotion && dragging)
            {
                if (!Input.IsMouseButtonPressed(MouseButton.Left))
                {
                    dragging = false;
                    return;
                }
                var delta = panel.GetGlobalMousePosition() - dragStart;
                moved |= delta.LengthSquared() >= 36;
                if (moved)
                {
                    var viewport = panel.GetViewportRect().Size;
                    var destination = panelStart + delta;
                    panel.Position = new Vector2(
                        Mathf.Clamp(destination.X, 0, Math.Max(0, viewport.X - panel.Size.X)),
                        Mathf.Clamp(destination.Y, 0, Math.Max(0, viewport.Y - panel.Size.Y)));
                    SavedPosition = panel.Position;
                    button.AcceptEvent();
                }
            }
        };
        button.Pressed += () =>
        {
            if (moved)
                return;
            Log.Info("[boss-with-infinite-health] toggle pressed");
            BossCombatState.TrySetEnabled(!BossCombatState.Enabled);
        };
        panel.AddChild(button);
        var title = new Label { Text = "BOSS 无限生命", Position = new Vector2(18, 12), MouseFilter = Control.MouseFilterEnum.Ignore };
        title.AddThemeFontSizeOverride("font_size", 21);
        title.AddThemeColorOverride("font_color", new Color("#f5e6c6"));
        button.AddChild(title);
        var state = new Label { Position = new Vector2(18, 45), MouseFilter = Control.MouseFilterEnum.Ignore };
        state.AddThemeFontSizeOverride("font_size", 15);
        button.AddChild(state);
        var track = new Panel { Position = new Vector2(250, 27), Size = new Vector2(54, 30), MouseFilter = Control.MouseFilterEnum.Ignore };
        button.AddChild(track);
        var thumb = new Panel { Size = new Vector2(22, 22), MouseFilter = Control.MouseFilterEnum.Ignore };
        thumb.AddThemeStyleboxOverride("panel", MakeIndicatorStyle("#f5eedc", "#f5eedc", 0));
        track.AddChild(thumb);
        Indicators[button] = (state, track, thumb);
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
                Indicators.Remove(button);
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
        button.TooltipText = BossCombatState.CanChange ? "点击切换；按住左键拖动。关闭后，负血敌人在回合交界死亡。" : "仅房主可操作";
        if (Indicators.TryGetValue(button, out var indicator))
        {
            var enabled = BossCombatState.Enabled;
            indicator.State.Text = (enabled ? "已开启 · 可持续打出负血" : "已关闭 · 正常死亡") + (BossCombatState.CanChange ? "" : " · 房主控制");
            indicator.State.AddThemeColorOverride("font_color", new Color(enabled ? "#73dec0" : "#aab5c2"));
            indicator.Track.AddThemeStyleboxOverride("panel", MakeIndicatorStyle(enabled ? "#247d68" : "#3a4856", enabled ? "#73dec0" : "#657787", 1));
            indicator.Thumb.Position = new Vector2(enabled ? 28 : 4, 4);
        }
    }

    private static StyleBoxFlat MakeIndicatorStyle(string background, string border, int borderWidth)
    {
        var style = MakeButtonStyle(background, border, borderWidth);
        style.ContentMarginLeft = 0;
        style.ContentMarginRight = 0;
        style.ContentMarginTop = 0;
        style.ContentMarginBottom = 0;
        return style;
    }

    private static StyleBoxFlat MakePanelStyle() => new()
    {
        BgColor = new Color("#12161a"),
        BorderWidthLeft = 2,
        BorderWidthTop = 2,
        BorderWidthRight = 2,
        BorderWidthBottom = 2,
        BorderColor = new Color("#8d6a3b"),
        CornerRadiusTopLeft = 14,
        CornerRadiusTopRight = 14,
        CornerRadiusBottomLeft = 14,
        CornerRadiusBottomRight = 14,
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
        CornerRadiusTopLeft = 12,
        CornerRadiusTopRight = 12,
        CornerRadiusBottomLeft = 12,
        CornerRadiusBottomRight = 12,
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
