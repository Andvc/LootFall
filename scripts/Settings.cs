using Godot;

namespace LootFall;

/// <summary>
/// The player's settings, kept in user://settings.cfg: volume, fullscreen, how much the screen shakes and flashes, and
/// whether Uncle Bram gives tips (for new games, and for the one in progress).
/// </summary>
public static class Settings
{
    const string Path = "user://settings.cfg";

    /// <summary>0 to 1.</summary>
    public static float Volume { get; set; } = 0.8f;

    public static bool Fullscreen { get; set; }

    /// <summary>How strong screen shake and flashes are: 1 full, 0.5 reduced, 0 off.</summary>
    public static float Shake { get; set; } = 1f;

    public static bool Tips { get; set; } = true;

    public static void Load()
    {
        var file = new ConfigFile();
        if (file.Load(Path) == Error.Ok)
        {
            Volume = (float)file.GetValue("audio", "volume", Volume);
            Fullscreen = (bool)file.GetValue("video", "fullscreen", Fullscreen);
            Shake = (float)file.GetValue("video", "shake", Shake);
            Tips = (bool)file.GetValue("game", "tips", Tips);
        }
        Apply();
    }

    public static void Save()
    {
        var file = new ConfigFile();
        file.SetValue("audio", "volume", Volume);
        file.SetValue("video", "fullscreen", Fullscreen);
        file.SetValue("video", "shake", Shake);
        file.SetValue("game", "tips", Tips);
        file.Save(Path);
    }

    public static void Apply()
    {
        int master = AudioServer.GetBusIndex("Master");
        AudioServer.SetBusVolumeDb(master, Mathf.LinearToDb(Mathf.Max(0.0001f, Volume)));
        AudioServer.SetBusMute(master, Volume <= 0.001f);
        // Headless runs (the sim, the shake test) have no window to change.
        if (DisplayServer.GetName() != "headless")
            DisplayServer.WindowSetMode(Fullscreen ? DisplayServer.WindowMode.Fullscreen : DisplayServer.WindowMode.Windowed);
    }

    public static string ShakeWord => Shake >= 1f ? "Full" : Shake > 0f ? "Reduced" : "Off";
}
