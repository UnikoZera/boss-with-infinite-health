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
        Log.Info($"[{ModId}] Applying combat patches for STS2 v0.107.1.");
        new Harmony(ModId).PatchAll(Assembly.GetExecutingAssembly());
        Log.Info($"[{ModId}] loaded for STS2 v0.107.1.");
    }
}
