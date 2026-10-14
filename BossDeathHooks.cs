using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Runs;

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

[HarmonyPatch(typeof(Hook), nameof(Hook.ShouldStopCombatFromEnding))]
internal static class BossCombatEndingHook
{
    private static void Postfix(CombatState combatState, ref bool __result)
    {
        if (BossCombatState.ShouldStopEnding(combatState))
            __result = true;
    }
}
