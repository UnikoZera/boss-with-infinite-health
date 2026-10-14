using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace BossSurvival;

[HarmonyPatch(typeof(Hook), nameof(Hook.ShouldDie))]
internal static class BossDeathHooks
{
    private static void Postfix(Creature creature, ref bool __result)
    {
        if (BossCombatState.ShouldSuppressDeath(creature))
            __result = false;
    }
}

[HarmonyPatch(typeof(Creature), nameof(Creature.LoseHpInternal))]
internal static class BossNegativeHpDamageHook
{
    private static bool Prefix(Creature __instance, decimal amount, ValueProp props, ref DamageResult __result)
    {
        if (amount < 0m || !BossCombatState.ShouldAllowNegativeHp(__instance))
            return true;

        // Vanilla LoseHpInternal clamps CurrentHp to zero. Keep its normal damage
        // result semantics while letting a protected boss retain overkill as negative HP.
        var hpBefore = __instance.CurrentHp;
        var hpLoss = (int)Math.Min(amount, 999999999m);
        var hpAfter = hpBefore - hpLoss;
        __instance.SetCurrentHpInternal(hpAfter);

        __result = new DamageResult(__instance, props)
        {
            UnblockedDamage = hpBefore - hpAfter,
            WasTargetKilled = false,
            OverkillDamage = 0
        };
        return false;
    }
}

[HarmonyPatch(typeof(Creature), nameof(Creature.CurrentHp), MethodType.Setter)]
internal static class BossNegativeHpSetterHook
{
    private static readonly System.Reflection.FieldInfo CurrentHpField =
        AccessTools.Field(typeof(Creature), "_currentHp") ??
        throw new MissingFieldException(typeof(Creature).FullName, "_currentHp");
    private static readonly System.Reflection.FieldInfo CurrentHpChangedField =
        AccessTools.Field(typeof(Creature), "CurrentHpChanged") ??
        throw new MissingFieldException(typeof(Creature).FullName, "CurrentHpChanged");

    private static bool Prefix(Creature __instance, int value)
    {
        if (value >= 0 || !BossCombatState.ShouldAllowNegativeHp(__instance))
            return true;

        var previousHp = __instance.CurrentHp;
        if (previousHp == value)
            return false;

        // The game's setter rejects negative HP. Update its backing field and
        // raise the same change event so health bars and listeners still refresh.
        CurrentHpField.SetValue(__instance, value);
        if (CurrentHpChangedField.GetValue(__instance) is Action<int, int> changed)
            changed(previousHp, value);
        return false;
    }
}

[HarmonyPatch(typeof(Hook), nameof(Hook.ShouldStopCombatFromEnding))]
internal static class BossCombatEndingHook
{
    private static void Postfix(ICombatState combatState, ref bool __result)
    {
        if (BossCombatState.ShouldStopEnding(combatState))
            __result = true;
    }
}

[HarmonyPatch(typeof(Creature), nameof(Creature.IsDead), MethodType.Getter)]
internal static class BossIsDeadHook
{
    private static bool Prefix(Creature __instance, ref bool __result)
    {
        if (BossCombatState.ShouldExposeAsAlive(__instance))
        {
            __result = false;
            return false;
        }

        return true;
    }
}

[HarmonyPatch(typeof(Creature), nameof(Creature.IsAlive), MethodType.Getter)]
internal static class BossIsAliveHook
{
    private static bool Prefix(Creature __instance, ref bool __result)
    {
        if (BossCombatState.ShouldExposeAsAlive(__instance))
        {
            __result = true;
            return false;
        }

        return true;
    }
}

[HarmonyPatch(typeof(Creature), nameof(Creature.IsHittable), MethodType.Getter)]
internal static class BossIsHittableHook
{
    private static void Postfix(Creature __instance, ref bool __result)
    {
        if (BossCombatState.ShouldExposeAsAlive(__instance))
            __result = true;
    }
}

[HarmonyPatch(typeof(Hook), nameof(Hook.ShouldAllowTargeting))]
internal static class BossAllowTargetingHook
{
    private static void Postfix(Creature target, ref AbstractModel? preventer, ref bool __result)
    {
        if (!BossCombatState.ShouldExposeAsAlive(target)) return;
        __result = true;
        preventer = null;
    }
}

[HarmonyPatch(typeof(Hook), nameof(Hook.ShouldAllowHitting))]
internal static class BossAllowHittingHook
{
    private static void Postfix(Creature creature, ref bool __result)
    {
        if (BossCombatState.ShouldExposeAsAlive(creature))
            __result = true;
    }
}

[HarmonyPatch(typeof(NHealthBar), "RefreshText")]
internal static class BossHealthBarTextHook
{
    private static readonly System.Reflection.FieldInfo? CreatureField =
        AccessTools.Field(typeof(NHealthBar), "_creature");
    private static readonly System.Reflection.FieldInfo? HpLabelField =
        AccessTools.Field(typeof(NHealthBar), "_hpLabel");
    private static readonly System.Reflection.MethodInfo? SetTextAutoSize = HpLabelField is null
        ? null
        : AccessTools.Method(HpLabelField.FieldType, "SetTextAutoSize", [typeof(string)]);

    private static void Postfix(NHealthBar __instance)
    {
        if (CreatureField?.GetValue(__instance) is not Creature creature ||
            !BossCombatState.ShouldExposeAsAlive(creature) ||
            HpLabelField?.GetValue(__instance) is not Godot.Control hpLabel)
            return;

        // Vanilla RefreshText() returns early for CurrentHp <= 0 and writes "Dead".
        // Restore the normal numeric readout, preserving the actual negative HP.
        hpLabel.Visible = true;
        SetTextAutoSize?.Invoke(hpLabel, [$"{creature.CurrentHp}/{creature.MaxHp}"]);
    }
}

[HarmonyPatch(typeof(CombatManager), nameof(CombatManager.IsEnding), MethodType.Getter)]
internal static class BossCombatEndingStateHook
{
    private static void Postfix(CombatState ____state, ref bool __result)
    {
        if (BossCombatState.ShouldKeepCombatAlive(____state))
            __result = false;
    }
}

[HarmonyPatch(typeof(CombatManager), nameof(CombatManager.IsOverOrEnding), MethodType.Getter)]
internal static class BossCombatOverStateHook
{
    private static void Postfix(CombatState ____state, ref bool __result)
    {
        if (BossCombatState.ShouldKeepCombatAlive(____state))
            __result = false;
    }
}
