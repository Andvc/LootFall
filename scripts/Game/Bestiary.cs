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
            Monsters = new[] { M("bloblet", "Bloblet", Body.Hopper, 0.62), M("toadstool", "Toadstool Shambler", Body.Walker, 0.9), M("moss_hound", "Moss Hound", Body.Walker, 0.85), M("thicket_glutton", "Thicket Glutton", Body.Walker, 0.85) },
            Guardian = M("old_tusk", "Old Tusk", Body.Heavy, 1.7),
        },
        new()
        {
            Key = "dunes", Name = "Sunscorch Dunes",
            Monsters = new[] { M("dune_scorpion", "Dune Scorpion", Body.Walker, 0.8), M("sand_raptor", "Sand Raptor", Body.Walker, 0.95), M("carrion_harpy", "Carrion Harpy", Body.Flyer, 0.9), M("stitched_dead", "Stitched Dead", Body.Walker, 0.95) },
            Guardian = M("sand_warlord", "The Sand Warlord", Body.Walker, 1.6),
        },
        new()
        {
            Key = "frostpeak", Name = "Frostpeak",
            Monsters = new[] { M("snow_biter", "Snow Biter", Body.Hopper, 0.8), M("rime_wolf", "Rime Wolf", Body.Walker, 0.95), M("frost_siren", "Frost Siren", Body.Flyer, 0.95), M("rime_stalker", "Rime Stalker", Body.Walker, 1.0) },
            Guardian = M("frost_warden", "The Frost Warden", Body.Heavy, 1.7),
        },
        new()
        {
            Key = "mire", Name = "Drowned Mire",
            Monsters = new[] { M("tide_shrimp", "Tide Shrimp", Body.Slither, 1.0), M("drowned_maiden", "Drowned Maiden", Body.Flyer, 1.0), M("bog_gawker", "Bog Gawker", Body.Hopper, 0.85), M("pale_grasper", "Pale Grasper", Body.Walker, 0.95) },
            Guardian = M("deep_one", "The Deep One", Body.Heavy, 1.7),
        },
        new()
        {
            Key = "ashlands", Name = "Ashlands",
            Monsters = new[] { M("cinder_phoenix", "Cinder Phoenix", Body.Flyer, 0.9), M("lavamander", "Lavamander", Body.Slither, 1.0), M("magma_worm", "Magma Worm", Body.Slither, 1.0), M("spined_hellbeast", "Spined Hellbeast", Body.Walker, 1.0) },
            Guardian = M("cinder_colossus", "The Cinder Colossus", Body.Heavy, 1.8),
        },
        new()
        {
            Key = "ruins", Name = "Fallen Ruins",
            Monsters = new[] { M("risen_knight", "Risen Knight", Body.Walker, 1.0), M("restless_shade", "Restless Shade", Body.Flyer, 1.0), M("living_armor", "Living Armour", Body.Heavy, 1.05), M("dark_priest", "Dark Priest", Body.Walker, 1.0) },
            Guardian = M("gilded_maiden", "The Gilded Maiden", Body.Walker, 1.5),
        },
        new()
        {
            Key = "caverns", Name = "Deep Caverns",
            Monsters = new[] { M("batmadillo", "Batmadillo", Body.Walker, 1.0), M("pale_ravager", "Pale Ravager", Body.Walker, 0.95), M("swine_thug", "Swine Thug", Body.Heavy, 1.1), M("chained_horror", "Chained Horror", Body.Walker, 1.0) },
            Guardian = M("vermithrax", "Vermithrax", Body.Walker, 1.8),
        },
        new()
        {
            Key = "starfall", Name = "Starfall Rift",
            Monsters = new[] { M("cycloper", "Cycloper", Body.Walker, 0.8), M("rift_wolf", "Rift Wolf", Body.Walker, 1.0), M("crystalis", "Crystalis", Body.Heavy, 1.1), M("mangler", "Mangler", Body.Flyer, 0.9) },
            Guardian = M("hollow_star", "The Hollow Star", Body.Heavy, 1.9),
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
