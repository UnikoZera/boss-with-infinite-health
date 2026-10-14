using System.Runtime.CompilerServices;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace BossSurvival;

// Visual only: reuse the native foreground without writing combat HP.
internal static class BossOverkillHealthBar
{
    private sealed class Baseline(int maxHp)
    {
        public int MaxHp { get; } = Math.Max(1, maxHp);
    }

    private sealed class Visual(Label counter)
    {
        public Label Counter { get; } = counter;
        public long Displayed;
        public long Target;
        public Tween? Tween;
        public bool Active;
    }

    private static readonly ConditionalWeakTable<Creature, Baseline> Baselines = new();
    private static readonly ConditionalWeakTable<NHealthBar, Visual> Visuals = new();
    private static readonly List<WeakReference<NHealthBar>> Bars = new();
    private static readonly System.Reflection.FieldInfo CreatureField = AccessTools.Field(typeof(NHealthBar), "_creature");
    private static readonly System.Reflection.FieldInfo ForegroundField = AccessTools.Field(typeof(NHealthBar), "_hpForeground");
    private static readonly System.Reflection.PropertyInfo WidthProperty = AccessTools.Property(typeof(NHealthBar), "MaxFgWidth");

    public static void Capture(Creature creature) => Baselines.GetValue(creature, c => new Baseline(c.MaxHp));

    public static void RefreshAll()
    {
        for (var i = Bars.Count - 1; i >= 0; i--)
        {
            if (!Bars[i].TryGetTarget(out var bar) || !GodotObject.IsInstanceValid(bar))
                Bars.RemoveAt(i);
            else
                bar.RefreshValues(); // Restore vanilla styles first on toggle/reset.
        }
    }

    public static void Refresh(NHealthBar bar)
    {
        if (CreatureField.GetValue(bar) is not Creature creature)
            return;
        var eligible = BossCombatState.Enabled && BossCombatState.IsBossCombat &&
            ReferenceEquals(creature.CombatState, BossCombatState.Combat) &&
            creature.IsMonster && creature.IsPrimaryEnemy && creature.CurrentHp < 0;
        if (!eligible)
        {
            if (Visuals.TryGetValue(bar, out var old))
                Reset(old);
            return;
        }

        var maxHp = Baselines.GetValue(creature, c => new Baseline(c.MaxHp)).MaxHp;
        var progress = OverkillBarProgressCalculator.Calculate(creature.CurrentHp, maxHp);
        var count = progress.CompletedBars;
        // Each completed bar increments the counter; the visible bar tracks only
        // the damage remainder and starts empty at every threshold.
        if (ForegroundField.GetValue(bar) is Control foreground && WidthProperty.GetValue(bar) is float width)
        {
            foreground.Visible = true;
            foreground.SelfModulate = new Color("#f4cf42");
            foreground.OffsetRight = width * progress.CurrentBarFraction - width;
        }

        var visual = Visuals.GetValue(bar, Create);
        visual.Active = true;
        var previousTarget = visual.Target;
        visual.Target = count;
        if (count == 0)
        {
            StopAnimation(visual);
            visual.Displayed = 0;
            visual.Counter.Visible = false;
        }
        else
        {
            visual.Counter.Visible = true;
            if (count < previousTarget)
            {
                StopAnimation(visual);
                visual.Displayed = count;
                visual.Counter.Text = $"×{count}";
            }
            else if (count > previousTarget)
                AnimateCount(visual);
        }
    }

    private static void AnimateCount(Visual visual)
    {
        if (!visual.Active || !GodotObject.IsInstanceValid(visual.Counter) || visual.Displayed >= visual.Target)
            return;
        // Replace any in-flight animation instead of queuing another number/bounce.
        // Duration is independent of the number of full HP bars gained.
        StopAnimation(visual);
        var start = visual.Displayed;
        var target = visual.Target;
        var label = visual.Counter;
        label.Text = $"×{visual.Displayed}";
        label.PivotOffset = label.Size * 0.5f;
        var tween = label.CreateTween();
        visual.Tween = tween;
        tween.TweenMethod(Callable.From<double>(progress =>
        {
            if (!visual.Active || !GodotObject.IsInstanceValid(label)) return;
            // Finish counting by 0.60s, leaving time for the final landing.
            var countProgress = Math.Min(1, progress / 0.7);
            visual.Displayed = start + (long)Math.Floor((target - start) * countProgress);
            label.Text = $"×{visual.Displayed}";
            // Three conspicuous jumps with a decaying envelope, all in one timeline.
            var jump = (float)(Math.Abs(Math.Sin(progress * Math.PI * 3)) * (1 - progress * 0.45));
            label.OffsetTop = -22f * jump;
            label.Scale = Vector2.One * (1 + 0.55f * jump);
            label.Modulate = Colors.White.Lerp(new Color("#fff5a0"), jump);
        }), 0.0, 1.0, 0.85);
        tween.Finished += () =>
        {
            if (!ReferenceEquals(visual.Tween, tween)) return;
            visual.Tween = null;
            visual.Displayed = target;
            label.Text = $"×{target}";
            label.Scale = Vector2.One;
            label.OffsetTop = 0;
            label.Modulate = Colors.White;
        };
    }

    private static void StopAnimation(Visual visual)
    {
        visual.Tween?.Kill();
        visual.Tween = null;
        visual.Counter.Scale = Vector2.One;
        visual.Counter.OffsetTop = 0;
        visual.Counter.Modulate = Colors.White;
    }

    private static void Reset(Visual visual)
    {
        StopAnimation(visual);
        visual.Active = false;
        visual.Displayed = visual.Target = 0;
        visual.Counter.Visible = false;
        visual.Counter.Text = "";
    }

    private static Visual Create(NHealthBar bar)
    {
        var counter = new Label
        {
            Name = "BossExtraHpBarCount", MouseFilter = Control.MouseFilterEnum.Ignore,
            ZIndex = 0, ZAsRelative = true,
            AnchorLeft = 1, AnchorRight = 1,
            OffsetLeft = 10, OffsetRight = 110, OffsetTop = 0, OffsetBottom = 30
        };
        counter.AddThemeFontSizeOverride("font_size", 23);
        counter.AddThemeColorOverride("font_color", new Color("#f4cf42"));
        counter.AddThemeColorOverride("font_outline_color", new Color("#18131a"));
        counter.AddThemeConstantOverride("outline_size", 6);
        // Same parent/canvas as vanilla HP: menus cover both, and hiding HP hides both.
        bar.HpBarContainer.AddChild(counter);
        Bars.Add(new WeakReference<NHealthBar>(bar));
        return new Visual(counter);
    }
}

[HarmonyPatch(typeof(NHealthBar), nameof(NHealthBar.SetCreature))]
internal static class BossOriginalHealthCaptureHook
{
    private static void Postfix(Creature creature) => BossOverkillHealthBar.Capture(creature);
}

[HarmonyPatch(typeof(NHealthBar), nameof(NHealthBar.RefreshValues))]
internal static class BossOverkillHealthRefreshHook
{
    private static void Postfix(NHealthBar __instance) => BossOverkillHealthBar.Refresh(__instance);
}

[HarmonyPatch(typeof(BossToggleButton), nameof(BossToggleButton.RefreshAll))]
internal static class BossOverkillToggleRefreshHook
{
    private static void Postfix() => BossOverkillHealthBar.RefreshAll();
}
