using System.Text.Json;
using Godot;

namespace BossSurvival;

public sealed class BossSurvivalConfig
{
    private const string FileName = "boss-with-infinite-health.cfg";
    private const string LegacyFileName = "BossSurvival.cfg";
    private static readonly object Sync = new();
    public static BossSurvivalConfig Current { get; private set; } = new();

    public bool DefaultEnabled { get; set; }
    public float? TogglePositionX { get; set; }
    public float? TogglePositionY { get; set; }

    public static Vector2 GetTogglePosition(Vector2 fallback)
    {
        var x = Current.TogglePositionX;
        var y = Current.TogglePositionY;
        return x.HasValue && y.HasValue && float.IsFinite(x.Value) && float.IsFinite(y.Value)
            ? new Vector2(x.Value, y.Value)
            : fallback;
    }

    public static void SaveTogglePosition(Vector2 position)
    {
        lock (Sync)
        {
            Current.TogglePositionX = position.X;
            Current.TogglePositionY = position.Y;
            Save();
        }
    }

    public static void Load()
    {
        lock (Sync)
        {
            var path = ProjectSettings.GlobalizePath($"user://mod_configs/{FileName}");
            try
            {
                if (File.Exists(path))
                    Current = JsonSerializer.Deserialize<BossSurvivalConfig>(File.ReadAllText(path)) ?? new();
                else
                {
                    var legacyPath = ProjectSettings.GlobalizePath($"user://mod_configs/{LegacyFileName}");
                    if (File.Exists(legacyPath))
                        Current = JsonSerializer.Deserialize<BossSurvivalConfig>(File.ReadAllText(legacyPath)) ?? new();
                    Save();
                }
            }
            catch
            {
                Current = new();
            }
        }
    }

    public static void Save()
    {
        lock (Sync)
        {
            var path = ProjectSettings.GlobalizePath($"user://mod_configs/{FileName}");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(Current, new JsonSerializerOptions { WriteIndented = true }));
        }
    }
}
