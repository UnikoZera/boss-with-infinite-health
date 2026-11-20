using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;

namespace BossSurvival;

[ModInitializer(nameof(Initialize))]
public static class ModEntry
{
    public const string ModId = "boss-with-infinite-health";

    public static void Initialize()
    {
        BossSurvivalConfig.Load();
        BaseLib.Config.ModConfigRegistry.Register(ModId, new BossSettingsMenu());
        Log.Info($"[{ModId}] Applying combat patches.");
        try
        {
            new Harmony(ModId).PatchAll(Assembly.GetExecutingAssembly());
        }
        catch (Exception exception)
        {
            Log.Error($"[{ModId}] Harmony patch initialization failed: {exception}");
            throw;
        }
        Log.Info($"[{ModId}] loaded.");
    }
}
