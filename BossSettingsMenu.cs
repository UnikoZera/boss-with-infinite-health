using BaseLib.Config;
using Godot;

namespace BossSurvival;

// The existing config remains authoritative, including values from older releases.
public sealed class BossSettingsMenu : ModConfig
{
    public BossSettingsMenu() : base("BossSurvivalMenu") { }

    public override bool VisibleInModList() => true;

    public override void SetupConfigUI(Control optionContainer)
    {
        var title = new Label { Text = "BOSS 无限生命", CustomMinimumSize = new Vector2(0, 64) };
        title.AddThemeFontSizeOverride("font_size", 32);
        title.AddThemeColorOverride("font_color", new Color("#f5d478"));
        optionContainer.AddChild(title);

        var toggle = new CheckButton
        {
            Text = "每场 BOSS 战默认开启",
            ButtonPressed = BossSurvivalConfig.Current.DefaultEnabled,
            CustomMinimumSize = new Vector2(620, 72),
            MouseDefaultCursorShape = Control.CursorShape.PointingHand
        };
        toggle.AddThemeFontSizeOverride("font_size", 26);
        toggle.Toggled += value =>
        {
            BossSurvivalConfig.Current.DefaultEnabled = value;
            BossSurvivalConfig.Save();
            Changed();
        };
        optionContainer.AddChild(toggle);
        var hint = new Label
        {
            Text = "关闭：正常死亡。开启：BOSS 与爪牙可持续承受负血伤害。\n设置自动保存，下场 BOSS 战生效；联机使用房主的默认值。",
            CustomMinimumSize = new Vector2(620, 100),
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        hint.AddThemeFontSizeOverride("font_size", 20);
        hint.AddThemeColorOverride("font_color", new Color("#b8bdc7"));
        optionContainer.AddChild(hint);
        var reset = new Button { Text = "恢复默认：关闭", CustomMinimumSize = new Vector2(260, 56) };
        reset.Pressed += () => toggle.ButtonPressed = false;
        optionContainer.AddChild(reset);
        SimpleModConfig.SetupFocusNeighbors(optionContainer);
    }
}
