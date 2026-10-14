using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Multiplayer;
using MegaCrit.Sts2.Core.Runs;

namespace BossSurvival;

internal static class BossAuthority
{
    private static INetGameService? _registeredService;
    private static readonly MessageHandlerDelegate<BossSurvivalToggleMessage> ToggleHandler = OnToggleMessage;

    public static bool IsHost
    {
        get
        {
            var service = FindNetService();
            return service is null || service.Type.ToString() is "None" or "Singleplayer" or "Host";
        }
    }

    public static void Begin(bool enabled)
    {
        var service = FindNetService();
        if (!ReferenceEquals(service, _registeredService))
        {
            _registeredService?.UnregisterMessageHandler(ToggleHandler);
            _registeredService = service;
            _registeredService?.RegisterMessageHandler(ToggleHandler);
        }

        if (IsHost)
            Broadcast(enabled);
        else if (service is { IsConnected: true })
            service.SendMessage(new BossSurvivalToggleMessage { Request = true });
    }

    public static void End()
    {
        _registeredService?.UnregisterMessageHandler(ToggleHandler);
        _registeredService = null;
    }

    public static void Broadcast(bool enabled)
    {
        var service = FindNetService();
        if (IsHost && service is { IsConnected: true })
            service.SendMessage(new BossSurvivalToggleMessage { Enabled = enabled });
    }

    private static void OnToggleMessage(BossSurvivalToggleMessage message, ulong senderId)
    {
        var service = FindNetService();
        if (service is { Type: NetGameType.Host })
        {
            if (message.Request && BossCombatState.IsBossCombat)
                Broadcast(BossCombatState.Enabled);
            return;
        }

        if (service is not NetClientGameService client || message.Request || senderId != client.HostNetId)
            return;

        BossCombatState.SetSynchronizedEnabled(message.Enabled);
    }

    private static INetGameService? FindNetService()
    {
        return RunManager.Instance?.NetService;
    }
}
