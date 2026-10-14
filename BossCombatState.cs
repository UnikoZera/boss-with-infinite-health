using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Rooms;

namespace BossSurvival;

internal static class BossCombatState
{
    private static readonly object Sync = new();
    private static CombatState? _combat;
    private static bool _enabled;
    private static bool _bossCombat;
    private static bool _pendingDeath;
    private static bool _protectedUntilBoundary;

    public static bool IsBossCombat => _bossCombat && _combat is not null;
    public static bool Enabled => _enabled;
    public static CombatState? Combat => _combat;

    public static void Begin(CombatState combat)
    {
        lock (Sync)
        {
            _combat = combat;
            _bossCombat = IsBossEncounter(combat);
            _enabled = BossSurvivalConfig.Current.DefaultEnabled;
            _pendingDeath = false;
            _protectedUntilBoundary = false;
        }
        BossAuthority.Begin(_enabled);
        BossToggleButton.RefreshAll();
    }

    public static void End(CombatState? combat)
    {
        lock (Sync)
        {
            if (combat is not null && !ReferenceEquals(_combat, combat)) return;
            _combat = null;
            _bossCombat = false;
            _enabled = false;
            _pendingDeath = false;
            _protectedUntilBoundary = false;
        }
        BossAuthority.End();
        BossToggleButton.RefreshAll();
    }

    public static bool CanChange => IsBossCombat && BossAuthority.IsHost;

    public static bool TrySetEnabled(bool value)
    {
        lock (Sync)
        {
            if (!CanChange) return false;
            _enabled = value;
            if (!value && _combat is not null &&
                _combat.Enemies.Any(e => IsProtectedEnemy(e) && e.CurrentHp <= 0))
                _protectedUntilBoundary = true;
            BossAuthority.Broadcast(value);
        }
        BossToggleButton.RefreshAll();
        return true;
    }

    public static void SetSynchronizedEnabled(bool value)
    {
        lock (Sync)
        {
            if (!IsBossCombat) return;
            _enabled = value;
            if (!value && _combat is not null &&
                _combat.Enemies.Any(e => IsProtectedEnemy(e) && e.CurrentHp <= 0))
                _protectedUntilBoundary = true;
        }
        BossToggleButton.RefreshAll();
    }

    public static bool ShouldSuppressDeath(Creature creature)
    {
        lock (Sync)
        {
            if (!IsBossCombat || !IsProtectedEnemy(creature)) return false;
            if (_enabled)
            {
                _protectedUntilBoundary = true;
                return true;
            }
            // A boss whose death was prevented earlier in this player turn must
            // remain alive until the all-players-end-turn boundary.
            return _protectedUntilBoundary && creature.CurrentHp <= 0;
        }
    }

    public static bool ShouldExposeAsAlive(Creature creature)
    {
        lock (Sync)
        {
            return IsSurvivalActiveForBoss(creature);
        }
    }

    public static bool IsDeadForCombatResolution(Creature creature) =>
        creature.CurrentHp <= 0 && !ShouldExposeAsAlive(creature);

    public static bool IsAliveForCombatResolution(Creature creature) =>
        !IsDeadForCombatResolution(creature);

    public static bool ShouldAllowNegativeHp(Creature creature)
    {
        lock (Sync)
        {
            return IsSurvivalActiveForBoss(creature);
        }
    }

    public static bool ShouldKeepCombatAlive(ICombatState combat)
    {
        lock (Sync)
        {
            if (!ReferenceEquals(_combat, combat) || !_bossCombat)
                return false;
            if (_pendingDeath) return true;
            return (_enabled || _protectedUntilBoundary) &&
                combat.Enemies.Any(e => IsProtectedEnemy(e) && e.CurrentHp <= 0);
        }
    }

    public static bool ShouldStopEnding(ICombatState combat)
    {
        lock (Sync)
        {
            if (!ReferenceEquals(_combat, combat) || !_bossCombat) return false;
            // Keep the combat open while the normal death pipeline is removing
            // the whole dead-enemy batch. The boundary caller checks victory
            // immediately after this task completes.
            if (_pendingDeath) return true;
            if (_protectedUntilBoundary && combat.Enemies.Any(e => IsProtectedEnemy(e) && e.CurrentHp <= 0))
                return true;
            if (!_enabled) return false;
            return combat.Enemies.Any(e => IsProtectedEnemy(e) && e.CurrentHp <= 0);
        }
    }

    public static async Task EvaluateAtEnemyTurnBoundaryAsync()
    {
        Creature[] deadEnemies;
        CombatState combat;
        lock (Sync)
        {
            if (!_bossCombat || _combat is null || _enabled || _pendingDeath) return;
            combat = _combat;
            deadEnemies = combat.Enemies
                .Where(e => IsProtectedEnemy(e) && e.CurrentHp <= 0 && IsSurvivalActiveForBoss(e))
                .OrderBy(e => e.IsPrimaryEnemy)
                .ToArray();
            if (deadEnemies.Length == 0) return;
            _pendingDeath = true;
            _protectedUntilBoundary = false;
        }

        try
        {
            // Vanilla death/phase transitions assume dead creatures have zero HP.
            // SetMaxHpInternal reassigns CurrentHp, and revival healing is additive;
            // retaining overkill here throws or subtracts it from the next phase.
            foreach (var enemy in deadEnemies)
                NormalizeForVanillaDeath(enemy);
            // Run only after AfterSideTurnStart finishes. StartTurn has already
            // called AfterTurnStart and AfterBlockCleared for its participant
            // snapshot, so removing enemies here cannot leave a detached
            // creature in that setup loop.
            await MegaCrit.Sts2.Core.Commands.CreatureCmd.Kill(deadEnemies, false);
        }
        finally
        {
            lock (Sync)
            {
                if (ReferenceEquals(_combat, combat))
                    _pendingDeath = false;
            }
        }
    }

    public static void NormalizeForVanillaDeath(Creature creature)
    {
        if (creature.CurrentHp < 0)
            creature.SetCurrentHpInternal(0);
    }

    public static bool IsProtectedEnemy(Creature creature) =>
        creature.IsMonster && creature.Side == CombatSide.Enemy;

    private static bool IsSurvivalActiveForBoss(Creature creature) =>
        _bossCombat && _combat is not null && ReferenceEquals(creature.CombatState, _combat) &&
        IsProtectedEnemy(creature) &&
        (_enabled || _protectedUntilBoundary) && !_pendingDeath;

    private static bool IsBossEncounter(CombatState combat)
    {
        var room = combat.RunState.CurrentRoom as CombatRoom;
        if (room?.Encounter is null) return false;
        return room.RoomType == RoomType.Boss;
    }
}
