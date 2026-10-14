using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.ValueProps;

namespace BossSurvival;

// NTargetManager performs its own target check and can have IsDead inlined.
// Preserve card-specific node filters, then allow a protected boss through its
// final AnyEnemy check explicitly.
[HarmonyPatch(typeof(NTargetManager), nameof(NTargetManager.AllowedToTargetNode))]
internal static class BossTargetingHook
{
    private static readonly FieldInfo ValidTargetsTypeField =
        AccessTools.Field(typeof(NTargetManager), "_validTargetsType") ??
        throw new MissingFieldException(typeof(NTargetManager).FullName, "_validTargetsType");
    private static readonly FieldInfo NodeFilterField =
        AccessTools.Field(typeof(NTargetManager), "_nodeFilter") ??
        throw new MissingFieldException(typeof(NTargetManager).FullName, "_nodeFilter");

    private static bool Prefix(NTargetManager __instance, Node node, ref bool __result)
    {
        if (node is not NCreature nCreature ||
            ValidTargetsTypeField.GetValue(__instance) is not TargetType.AnyEnemy ||
            nCreature.Entity.Side != CombatSide.Enemy ||
            !BossCombatState.ShouldExposeAsAlive(nCreature.Entity))
            return true;

        if (NodeFilterField.GetValue(__instance) is Func<Node, bool> nodeFilter && !nodeFilter(node))
        {
            __result = false;
            return false;
        }

        __result = true;
        return false;
    }
}

// PlayCardAction validates the selected creature again after targeting ends.
// Its IsValidTarget check otherwise cancels the card and returns it to hand
// because the game's IsAlive getter is based on CurrentHp > 0.
[HarmonyPatch(typeof(CardModel), nameof(CardModel.IsValidTarget))]
internal static class BossSelectedTargetValidationHook
{
    private static void Postfix(CardModel __instance, Creature? target, ref bool __result)
    {
        if (target is not null &&
            __instance.TargetType == TargetType.AnyEnemy &&
            target.Side != __instance.Owner.Creature.Side &&
            BossCombatState.ShouldExposeAsAlive(target))
            __result = true;
    }
}

// The play action is asynchronous, and a precompiled/inlined IsValidTarget
// check can bypass its getter postfix. Replace that check in the action itself.
[HarmonyPatch]
internal static class BossPlayCardValidationHook
{
    private static MethodBase TargetMethod()
    {
        var executeAction = AccessTools.Method(typeof(PlayCardAction), "ExecuteAction") ??
            throw new MissingMethodException(typeof(PlayCardAction).FullName, "ExecuteAction");
        var stateMachine = executeAction.GetCustomAttribute<AsyncStateMachineAttribute>()?.StateMachineType;
        return stateMachine?.GetMethod("MoveNext", BindingFlags.Instance | BindingFlags.NonPublic) ?? executeAction;
    }

    private static bool IsValidTargetIncludingProtectedEnemies(CardModel card, Creature? target)
    {
        if (target is not null &&
            card.TargetType == TargetType.AnyEnemy &&
            target.Side != card.Owner.Creature.Side &&
            BossCombatState.ShouldExposeAsAlive(target))
            return true;

        return card.IsValidTarget(target);
    }

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var isValidTarget = AccessTools.Method(typeof(CardModel), nameof(CardModel.IsValidTarget), [typeof(Creature)])!;
        var protectedTargetCheck = AccessTools.Method(typeof(BossPlayCardValidationHook), nameof(IsValidTargetIncludingProtectedEnemies))!;

        foreach (var instruction in instructions)
        {
            if (instruction.Calls(isValidTarget))
            {
                instruction.opcode = OpCodes.Call;
                instruction.operand = protectedTargetCheck;
            }
            yield return instruction;
        }
    }
}

// CreatureCmd.Damage is async, so its dead-target checks live in the generated
// MoveNext method. Replace those checks as well, ensuring selected negative-HP
// bosses proceed through ordinary damage, VFX and hit reactions.
[HarmonyPatch]
internal static class BossDamageTargetHook
{
    private static MethodBase TargetMethod()
    {
        var damageMethod = AccessTools.Method(typeof(CreatureCmd), nameof(CreatureCmd.Damage),
            [typeof(PlayerChoiceContext), typeof(IEnumerable<Creature>), typeof(decimal),
             typeof(ValueProp), typeof(Creature), typeof(CardModel)]) ??
            throw new MissingMethodException(typeof(CreatureCmd).FullName, "Damage(IEnumerable<Creature>, decimal)");

        var stateMachine = damageMethod.GetCustomAttribute<AsyncStateMachineAttribute>()?.StateMachineType;
        return stateMachine?.GetMethod("MoveNext", BindingFlags.Instance | BindingFlags.NonPublic) ?? damageMethod;
    }

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var isDeadGetter = AccessTools.PropertyGetter(typeof(Creature), nameof(Creature.IsDead))!;
        var protectedBossCheck = AccessTools.Method(typeof(BossCombatState), nameof(BossCombatState.IsDeadForCombatResolution))!;
        var patchedInstructions = new List<CodeInstruction>();
        var replacedChecks = 0;

        foreach (var instruction in instructions)
        {
            if (instruction.Calls(isDeadGetter))
            {
                instruction.opcode = OpCodes.Call;
                instruction.operand = protectedBossCheck;
                replacedChecks++;
            }
            patchedInstructions.Add(instruction);
        }

        if (replacedChecks == 0)
            throw new InvalidOperationException("CreatureCmd.Damage contains no IsDead checks to protect.");

        Log.Info($"[{ModEntry.ModId}] protected {replacedChecks} IsDead check(s) in CreatureCmd.Damage.");
        return patchedInstructions;
    }
}
