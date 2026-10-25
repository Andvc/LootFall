using System.Collections.Generic;
using System.Text.Json;
using Godot;

namespace LootFall.View;

/// <summary>A monster's picture: its texture and where it stands (from assets/monsters/monsters.json).</summary>
public sealed class MonsterArt
{
    public Texture2D Texture;
    public Vector2 Foot;
    public float Height => Texture?.GetHeight() ?? 1;
}

/// <summary>A realm's colours, sampled from its backdrop (assets/backdrops/realms.json): the sky, its horizon and the
/// ground, for what the screen draws itself (the ground band, the foreground, the fog).</summary>
public sealed class RealmColors
{
    public Color SkyTop, SkyHorizon, Ground;
}

/// <summary>
/// Every picture the screens draw, by name, loaded once. Missing art falls back (an icon to "unknown"), so a new slot
/// or monster shows up before its picture exists. Paths follow docs/art-direction.md: assets/monsters, assets/icons,
/// assets/backdrops, assets/vfx.
/// </summary>
public static class Assets
{
    static readonly Dictionary<string, Texture2D> Textures = new();
    static Dictionary<string, MonsterJson> _monsters;
    static Dictionary<string, RealmJson> _realms;
    static readonly Dictionary<string, MonsterArt> Monsters = new();

    sealed class MonsterJson
    {
        public int w { get; set; }
        public int h { get; set; }
        public int footX { get; set; }
        public int footY { get; set; }
    }

    sealed class RealmJson
    {
        public string skyTop { get; set; }
        public string skyHorizon { get; set; }
        public string ground { get; set; }
    }

    public static Texture2D Tex(string path)
    {
        if (Textures.TryGetValue(path, out var t)) return t;
        t = ResourceLoader.Exists(path) ? GD.Load<Texture2D>(path) : null;
        Textures[path] = t;
        return t;
    }

    static T ReadJson<T>(string path) where T : class
    {
        if (!FileAccess.FileExists(path)) return null;
        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
        return JsonSerializer.Deserialize<T>(file.GetAsText());
    }

    public static MonsterArt Monster(string key)
    {
        if (Monsters.TryGetValue(key, out var art)) return art;
        _monsters ??= ReadJson<Dictionary<string, MonsterJson>>("res://assets/monsters/monsters.json") ?? new();
        art = new MonsterArt { Texture = Tex($"res://assets/monsters/{key}.png") };
        if (_monsters.TryGetValue(key, out var j)) art.Foot = new Vector2(j.footX, j.footY);
        else if (art.Texture != null) art.Foot = new Vector2(art.Texture.GetWidth() / 2f, art.Texture.GetHeight());
        Monsters[key] = art;
        return art;
    }

    /// <summary>An item family's icon (Describe.Family), or the fallback.</summary>
    public static Texture2D Icon(string family) => Tex($"res://assets/icons/{family}.png") ?? Tex("res://assets/icons/unknown.png");

    public static Texture2D Backdrop(string realm) => Tex($"res://assets/backdrops/{realm}.jpg");

    public static RealmColors Realm(string realm)
    {
        _realms ??= ReadJson<Dictionary<string, RealmJson>>("res://assets/backdrops/realms.json") ?? new();
        if (!_realms.TryGetValue(realm, out var j)) return new RealmColors { SkyTop = new Color("#2a2340"), SkyHorizon = new Color("#5a4f70"), Ground = new Color("#3a3328") };
        return new RealmColors { SkyTop = new Color(j.skyTop), SkyHorizon = new Color(j.skyHorizon), Ground = new Color(j.ground) };
    }

    public static Texture2D Vfx(string name) => Tex($"res://assets/vfx/{name}.png");

    /// <summary>Lets go of everything loaded (before quitting).</summary>
    public static void Release()
    {
        Textures.Clear();
        Monsters.Clear();
    }
}
