using LootFall.Progression;

namespace LootFall.Game;

/// <summary>How a monster moves on the lane (its picture is one still illustration; the screen animates it by body).</summary>
public enum Body : byte
{
    /// <summary>Walks and bobs (wolves, skeletons).</summary>
    Walker,
    /// <summary>Squashes and hops (slimes).</summary>
    Hopper,
    /// <summary>Hovers on a slow wave, never touching the ground (bats, ghosts, wisps).</summary>
    Flyer,
    /// <summary>Stomps slowly and shakes the ground (golems, giants).</summary>
    Heavy,
    /// <summary>Ripples along the ground (snakes, wyrms).</summary>
    Slither,
}

public sealed class MonsterDef
{
    public string Key, Name;
    public Body Body;
    /// <summary>Height on the lane relative to the hero (1 = as tall as the hero).</summary>
    public double Size;
}

/// <summary>A realm: where one codex era is fought (its gear is that era's material), with four monsters, a boss (the
/// biggest of them, every depth) and a guardian (every tenth depth).</summary>
public sealed class RealmDef
{
    public string Key, Name;
    public MonsterDef[] Monsters;
    public MonsterDef Guardian;
}

/// <summary>
/// The places and the monsters in them. A realm per codex era (depths 1-99, 100-199, 200-399, doubling), so the
/// journey goes somewhere new as the gear's material changes; past the eighth they come round again, deeper
/// ("Greenwood II"). Engine-free: the screen picks pictures by key, the art manifest lists the same keys.
/// </summary>
public static class Bestiary
{
    static MonsterDef M(string key, string name, Body body, double size) => new() { Key = key, Name = name, Body = body, Size = size };

    public static readonly RealmDef[] Realms =
    {
        new()
        {
            Key = "greenwood", Name = "Greenwood",
            Monsters = new[] { M("slime", "Moss Slime", Body.Hopper, 0.55), M("wolf", "Grey Wolf", Body.Walker, 0.7), M("boar", "Thornback Boar", Body.Walker, 0.75), M("treant", "Sapling Treant", Body.Heavy, 1.1) },
            Guardian = M("old_tusk", "Old Tusk", Body.Heavy, 1.6),
        },
        new()
        {
            Key = "dunes", Name = "Sunscorch Dunes",
            Monsters = new[] { M("scorpion", "Dune Scorpion", Body.Walker, 0.6), M("viper", "Sand Viper", Body.Slither, 0.5), M("mummy", "Wrapped Dead", Body.Walker, 1.0), M("vulture", "Bone Vulture", Body.Flyer, 0.7) },
            Guardian = M("sand_warlord", "The Sand Warlord", Body.Walker, 1.5),
        },
        new()
        {
            Key = "frostpeak", Name = "Frostpeak",
            Monsters = new[] { M("ice_wolf", "Rime Wolf", Body.Walker, 0.75), M("wisp", "Frost Wisp", Body.Flyer, 0.5), M("yeti", "Yeti", Body.Heavy, 1.2), M("ice_golem", "Ice Golem", Body.Heavy, 1.15) },
            Guardian = M("frost_giant", "The Frost Giant", Body.Heavy, 1.8),
        },
        new()
        {
            Key = "coast", Name = "Sunken Coast",
            Monsters = new[] { M("crab", "Reef Crab", Body.Walker, 0.55), M("drowned", "The Drowned", Body.Walker, 1.0), M("siren", "Siren", Body.Flyer, 0.9), M("octopus", "Tidal Octopus", Body.Slither, 0.9) },
            Guardian = M("deep_one", "The Deep One", Body.Heavy, 1.8),
        },
        new()
        {
            Key = "ashlands", Name = "Ashlands",
            Monsters = new[] { M("imp", "Ember Imp", Body.Flyer, 0.55), M("salamander", "Salamander", Body.Slither, 0.7), M("magma_golem", "Magma Golem", Body.Heavy, 1.2), M("hellhound", "Hellhound", Body.Walker, 0.8) },
            Guardian = M("cinder_colossus", "The Cinder Colossus", Body.Heavy, 1.9),
        },
        new()
        {
            Key = "ruins", Name = "Fallen Ruins",
            Monsters = new[] { M("skeleton", "Risen Soldier", Body.Walker, 1.0), M("ghost", "Restless Shade", Body.Flyer, 0.85), M("gargoyle", "Gargoyle", Body.Flyer, 0.9), M("mimic", "Mimic", Body.Hopper, 0.7) },
            Guardian = M("mimic_king", "The Mimic King", Body.Hopper, 1.6),
        },
        new()
        {
            Key = "caverns", Name = "Deep Caverns",
            Monsters = new[] { M("bat", "Cave Bat", Body.Flyer, 0.5), M("spider", "Broodmother's Spawn", Body.Walker, 0.7), M("troll", "Cave Troll", Body.Heavy, 1.3), M("drake", "Young Drake", Body.Walker, 1.0) },
            Guardian = M("vermithrax", "Vermithrax", Body.Flyer, 2.0),
        },
        new()
        {
            Key = "starfall", Name = "Starfall Rift",
            Monsters = new[] { M("void_eye", "Void Eye", Body.Flyer, 0.6), M("star_spawn", "Star Spawn", Body.Hopper, 0.7), M("shade_knight", "Shade Knight", Body.Walker, 1.05), M("construct", "Astral Construct", Body.Heavy, 1.2) },
            Guardian = M("hollow_star", "The Hollow Star", Body.Flyer, 2.0),
        },
    };

    /// <summary>The realm a depth is in (its codex era, round the eight realms).</summary>
    public static RealmDef Realm(int depth) => Realms[Hero.Era(depth) % Realms.Length];

    /// <summary>How many times the realms have come round by this depth (0 the first time).</summary>
    public static int Cycle(int depth) => Hero.Era(depth) / Realms.Length;

    public static string RealmName(int depth)
    {
        int c = Cycle(depth);
        return c == 0 ? Realm(depth).Name : $"{Realm(depth).Name} {Roman(c + 1)}";
    }

    /// <summary>A wave's monster: varied by depth, wave and place in the wave, the same every time it's shown.</summary>
    public static MonsterDef Monster(int depth, int wave, int slot)
    {
        var r = Realm(depth);
        uint h = Hash((uint)depth * 2654435761u ^ (uint)wave * 40503u ^ (uint)slot * 9973u);
        return r.Monsters[h % (uint)r.Monsters.Length];
    }

    /// <summary>The boss of a depth: one of the realm's monsters, grown (a guardian on every tenth depth).</summary>
    public static MonsterDef Boss(int depth)
    {
        var r = Realm(depth);
        if (Combat.IsGuardian(depth)) return r.Guardian;
        return r.Monsters[Hash((uint)depth * 2246822519u) % (uint)r.Monsters.Length];
    }

    static uint Hash(uint x)
    {
        x ^= x >> 16;
        x *= 0x7feb352d;
        x ^= x >> 15;
        x *= 0x846ca68b;
        x ^= x >> 16;
        return x;
    }

    public static string Roman(int n)
    {
        string[] r = { "", "I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX", "X" };
        return n >= 0 && n < r.Length ? r[n] : n.ToString();
    }
}
