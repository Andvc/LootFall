using Godot;

namespace LootFall;

/// <summary>
/// The player's settings, kept in user://settings.cfg: volume, fullscreen, how much the screen shakes and flashes,
/// whether Uncle Bram gives tips (for new games, and for the one in progress), and which sound each kind of thing makes.
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

    /// <summary>For each kind of thing, which of its sounds to use (an index into <see cref="Audio.SoundSets.For"/>).</summary>
    static readonly int[] _soundSets = new int[System.Enum.GetValues<Core.Category>().Length];

    public static int SoundSet(Core.Category kind) => _soundSets[(int)kind];

    public static void SetSoundSet(Core.Category kind, int option) => _soundSets[(int)kind] = option;

    public static void Load()
    {
        var file = new ConfigFile();
        if (file.Load(Path) == Error.Ok)
        {
            Volume = (float)file.GetValue("audio", "volume", Volume);
            Fullscreen = (bool)file.GetValue("video", "fullscreen", Fullscreen);
            Shake = (float)file.GetValue("video", "shake", Shake);
            Tips = (bool)file.GetValue("game", "tips", Tips);
            foreach (var kind in System.Enum.GetValues<Core.Category>())
                _soundSets[(int)kind] = (int)file.GetValue("sounds", kind.ToString().ToLowerInvariant(), 0);
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
        foreach (var kind in System.Enum.GetValues<Core.Category>())
            file.SetValue("sounds", kind.ToString().ToLowerInvariant(), _soundSets[(int)kind]);
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
