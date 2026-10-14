using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace BossSurvival;

[HarmonyPatch(typeof(Hook), nameof(Hook.BeforeCombatStart))]
internal static class BossCombatStartHook
{
    private static void Postfix(IRunState runState, CombatState combatState)
    {
        BossCombatState.Begin(combatState);
    }
}

[HarmonyPatch(typeof(Hook), nameof(Hook.AfterCombatEnd))]
internal static class BossCombatEndHook
{
    private static void Postfix(IRunState runState, CombatState? combatState, CombatRoom combatRoom)
    {
        BossCombatState.End(combatState);
    }
}

[HarmonyPatch(typeof(Hook), nameof(Hook.BeforeSideTurnStart))]
internal static class EnemyTurnBoundaryHook
{
    private static void Postfix(CombatState combatState, CombatSide side)
    {
        if (ReferenceEquals(BossCombatState.Combat, combatState) &&
            side.ToString().Contains("Enemy", StringComparison.OrdinalIgnoreCase))
            BossCombatState.EvaluateAtEnemyTurnBoundary();
    }
}
