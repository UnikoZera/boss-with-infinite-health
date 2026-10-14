using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Runs;

namespace BossSurvival;

/// <summary>
/// Host authority boundary. STS2 v0.107.1 exposes the net service, but no public
/// mod-settings message registry. The combat state is therefore authoritative on
/// the host and this adapter is the single place to add a registered net message
/// when that API is available in a future game build.
/// </summary>
internal static class BossAuthority
{
    public static bool IsHost
    {
        get
        {
            var service = FindNetService();
            return service is null || service.Type.ToString() is "Singleplayer" or "Host";
        }
    }

    public static void Broadcast(bool enabled)
    {
        // State-changing UI is host-only. Vanilla action synchronization keeps
        // the combat state deterministic; no client is allowed to write locally.
    }

    private static INetGameService? FindNetService()
    {
        return RunManager.Instance?.NetService;
    }
}
