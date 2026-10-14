using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace BossSurvival;

[HarmonyPatch(typeof(Hook), nameof(Hook.BeforeCombatStart))]
internal static class BossCombatStartHook
{
    private static void Postfix(IRunState runState, ICombatState combatState)
    {
        if (combatState is CombatState concreteCombat)
            BossCombatState.Begin(concreteCombat);
    }
}

[HarmonyPatch(typeof(Hook), nameof(Hook.AfterCombatEnd))]
internal static class BossCombatEndHook
{
    private static void Postfix()
    {
        BossCombatState.End(null);
    }
}

[HarmonyPatch(typeof(Hook), nameof(Hook.AfterSideTurnStart))]
internal static class EnemyTurnBoundaryHook
{
    private static void Postfix(ICombatState combatState, CombatSide side, ref Task __result)
    {
        if (combatState is CombatState concreteCombat &&
            ReferenceEquals(BossCombatState.Combat, concreteCombat) &&
            side.ToString().Contains("Enemy", StringComparison.OrdinalIgnoreCase))
            __result = EvaluateAfterSideTurnStart(__result);
    }

    private static async Task EvaluateAfterSideTurnStart(Task original)
    {
        await original;
        await BossCombatState.EvaluateAtEnemyTurnBoundaryAsync();
    }
}
