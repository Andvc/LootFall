using Godot;
using LootFall.Game;

namespace LootFall;

/// <summary>
/// The player's settings, kept in user://settings.cfg: volumes, fullscreen, how much the lane shakes, how strong flashes
/// are, whether damage numbers show, and how numbers are written (docs/art-direction.md: clutter has its own
/// switches).
/// </summary>
public static class Settings
{
    const string Path = "user://settings.cfg";

    /// <summary>0 to 1.</summary>
    public static float Volume { get; set; } = 0.8f;
    public static float MusicVolume { get; set; } = 0.5f;
    public static float SfxVolume { get; set; } = 0.85f;

    public static bool Fullscreen { get; set; }

    /// <summary>Screen shake: 1 full, 0.5 reduced, 0 off.</summary>
    public static float Shake { get; set; } = 1f;
    /// <summary>Flashes: 1 full, 0.5 reduced, 0 off.</summary>
    public static float Flashes { get; set; } = 1f;
    public static bool DamageNumbers { get; set; } = true;
    public static Notation Notation { get => Format.Notation; set => Format.Notation = value; }

    public static void Load()
    {
        var file = new ConfigFile();
        if (file.Load(Path) == Error.Ok)
        {
            Volume = (float)file.GetValue("audio", "volume", Volume);
            MusicVolume = (float)file.GetValue("audio", "music", MusicVolume);
            SfxVolume = (float)file.GetValue("audio", "sfx", SfxVolume);
            Fullscreen = (bool)file.GetValue("video", "fullscreen", Fullscreen);
            Shake = (float)file.GetValue("video", "shake", Shake);
            Flashes = (float)file.GetValue("video", "flashes", Flashes);
            DamageNumbers = (bool)file.GetValue("video", "damage_numbers", DamageNumbers);
            Notation = (Notation)(int)file.GetValue("game", "notation", (int)Notation);
        }
        Apply();
    }

    public static void Save()
    {
        var file = new ConfigFile();
        file.SetValue("audio", "volume", Volume);
        file.SetValue("audio", "music", MusicVolume);
        file.SetValue("audio", "sfx", SfxVolume);
        file.SetValue("video", "fullscreen", Fullscreen);
        file.SetValue("video", "shake", Shake);
        file.SetValue("video", "flashes", Flashes);
        file.SetValue("video", "damage_numbers", DamageNumbers);
        file.SetValue("game", "notation", (int)Notation);
        file.Save(Path);
    }

    /// <summary>Makes the Music and Sfx buses if they aren't there, and sets every volume and the window mode.</summary>
    public static void Apply()
    {
        foreach (var name in new[] { "Music", "Sfx" })
        {
            if (AudioServer.GetBusIndex(name) >= 0) continue;
            AudioServer.AddBus();
            int index = AudioServer.BusCount - 1;
            AudioServer.SetBusName(index, name);
            AudioServer.SetBusSend(index, "Master");
            // A burst of hits, coins and a fanfare at once stays clean.
            if (name == "Sfx") AudioServer.AddBusEffect(index, new AudioEffectHardLimiter { CeilingDb = -1f });
        }
        SetBus("Master", Volume);
        SetBus("Music", MusicVolume);
        SetBus("Sfx", SfxVolume);
        // Headless runs have no window to change.
        if (DisplayServer.GetName() != "headless")
            DisplayServer.WindowSetMode(Fullscreen ? DisplayServer.WindowMode.Fullscreen : DisplayServer.WindowMode.Windowed);
    }

    static void SetBus(string name, float volume)
    {
        int bus = AudioServer.GetBusIndex(name);
        AudioServer.SetBusVolumeDb(bus, Mathf.LinearToDb(Mathf.Max(0.0001f, volume)));
        AudioServer.SetBusMute(bus, volume <= 0.001f);
    }

    public static string Level(float v) => v >= 1f ? "Full" : v > 0f ? "Reduced" : "Off";
}
