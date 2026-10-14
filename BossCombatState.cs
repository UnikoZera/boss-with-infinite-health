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
                _combat.Enemies.Any(e => IsBossCreature(e) && e.CurrentHp <= 0 && !e.IsDead))
                _protectedUntilBoundary = true;
            BossAuthority.Broadcast(value);
        }
        BossToggleButton.RefreshAll();
        return true;
    }

    public static bool ShouldSuppressDeath(Creature creature)
    {
        lock (Sync)
        {
            if (!IsBossCombat || !IsBossCreature(creature)) return false;
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

    public static bool ShouldStopEnding(CombatState combat)
    {
        lock (Sync)
        {
            if (!ReferenceEquals(_combat, combat) || !_bossCombat) return false;
            if (_protectedUntilBoundary && combat.Enemies.Any(e => IsBossCreature(e) && e.CurrentHp <= 0 && !e.IsDead))
                return true;
            if (!_enabled) return false;
            return combat.Enemies.Any(e => IsBossCreature(e) && e.CurrentHp <= 0 && !e.IsDead);
        }
    }

    public static void EvaluateAtEnemyTurnBoundary()
    {
        lock (Sync)
        {
            if (!_bossCombat || _combat is null || _enabled || _pendingDeath) return;
            var deadBoss = _combat.Enemies.FirstOrDefault(e => IsBossCreature(e) && e.CurrentHp <= 0 && !e.IsDead);
            if (deadBoss is null) return;
            _pendingDeath = true;
            _protectedUntilBoundary = false;
            // ShouldDie was intentionally held during the player turn. Release
            // that hold by entering the normal death pipeline at the boundary.
            MegaCrit.Sts2.Core.Commands.CreatureCmd.Kill(deadBoss, false);
        }
    }

    public static bool IsBossCreature(Creature creature) =>
        creature.IsMonster && creature.IsPrimaryEnemy;

    private static bool IsBossEncounter(CombatState combat)
    {
        var room = combat.RunState.CurrentRoom as CombatRoom;
        if (room?.Encounter is null) return false;
        return room.RoomType == RoomType.Boss;
    }
}
