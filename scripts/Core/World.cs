using System;
using System.Collections.Generic;
using System.Linq;

namespace LootFall.Core;

public enum Size { Small, Medium, Large }

/// <summary>
/// A kind of monster. Sprite is its folder under res://assets/sprites/actors/. Hp and Atk multiply the stage's monster;
/// Interval is seconds between its attacks. Flies hovers above the ground.
/// </summary>
public sealed record MonsterDef(string Key, string Name, string Sprite, double Hp, double Atk, double Interval, Size Size, bool Flies = false);

/// <summary>A zone: ten stages in one place, the last with its boss.</summary>
public sealed record ZoneDef(string Key, string Name, string Background, string Music, string[] Monsters, string Boss, string BossTitle);

public static class World
{
    public static readonly MonsterDef[] Monsters =
    {
        new("slime", "Slime", "slime", 1.0, 0.9, 1.6, Size.Small),
        new("mushroom", "Toadstool", "mushroom", 1.2, 1.0, 1.8, Size.Small),
        new("snake", "Adder", "snake", 0.8, 1.2, 1.3, Size.Small),
        new("viper", "Sand Viper", "snake_viper", 0.85, 1.2, 1.3, Size.Small),
        new("bat", "Cave Bat", "bat", 0.7, 0.9, 1.0, Size.Small, Flies: true),
        new("emberbat", "Ember Bat", "bat_ember", 0.75, 1.0, 1.0, Size.Small, Flies: true),
        new("dino", "Salamander", "dino", 1.1, 1.1, 1.6, Size.Small),
        new("sundino", "Sun Lizard", "dino_sun", 1.1, 1.1, 1.5, Size.Small),
        new("mossslime", "Bog Slime", "slime_moss", 1.05, 0.9, 1.6, Size.Small),
        new("octopus", "Reef Lurker", "octopus", 1.3, 1.05, 1.7, Size.Medium),
        new("ghost", "Wisp", "ghost", 0.9, 1.1, 1.5, Size.Small, Flies: true),
        new("shade", "Shade", "ghost_shade", 0.95, 1.15, 1.5, Size.Small, Flies: true),
        new("frostcap", "Frostcap", "mushroom_frost", 1.2, 1.0, 1.8, Size.Small),
        new("yeti", "Yeti", "yeti", 1.3, 1.15, 2.0, Size.Medium),
        new("boar", "Tusker", "boar", 1.3, 1.15, 2.0, Size.Medium),
        new("greyboar", "Frost Tusker", "boar_tusk", 1.3, 1.15, 2.0, Size.Medium),
        new("reptile", "Lizardman", "reptile", 1.3, 1.15, 1.8, Size.Medium),
        new("bloodreptile", "Blood Lizardman", "reptile_blood", 1.3, 1.15, 1.8, Size.Medium),
        new("mimic", "Mimic", "mimic", 1.0, 1.0, 1.7, Size.Medium),
        new("giant", "Giant", "giant", 1.0, 1.0, 2.4, Size.Large),
        new("dragon", "Dragon", "dragon", 1.0, 1.0, 2.2, Size.Large, Flies: false),
        new("kingmimic", "Mimic King", "mimic_gilded", 1.0, 1.0, 1.7, Size.Medium),
        new("embergiant", "Ember Giant", "giant_ember", 1.0, 1.0, 2.4, Size.Large),
        new("ashdragon", "Ash Dragon", "dragon_ash", 1.0, 1.0, 2.2, Size.Large),
    };

    public static readonly ZoneDef[] Zones =
    {
        new("greenwood", "Greenwood", "greenwood", "battle", new[] { "slime", "mushroom", "snake" }, "boar", "Old Tusk"),
        new("dunes", "Sunscar Dunes", "dunes", "battle2", new[] { "viper", "dino", "emberbat" }, "reptile", "The Sand Warlord"),
        new("coast", "Sapphire Coast", "coast", "battle", new[] { "mossslime", "octopus", "bat" }, "octopus", "The Deep One"),
        new("ruins", "Haunted Ruins", "ruins", "battle2", new[] { "ghost", "shade", "frostcap" }, "kingmimic", "The Mimic King"),
        new("frostpeak", "Frostpeak", "frostpeak", "battle", new[] { "yeti", "bat", "greyboar" }, "giant", "The Frost Giant"),
        new("roost", "Dragon's Roost", "roost", "battle2", new[] { "bloodreptile", "sundino", "emberbat" }, "dragon", "Vermithrax"),
    };

    static readonly Dictionary<string, MonsterDef> ByKey = Monsters.ToDictionary(m => m.Key);

    public static MonsterDef Monster(string key) => ByKey[key];

    /// <summary>Zone index (0-based) for a stage; past the last zone they come round again, harder.</summary>
    public static int ZoneIndex(int stage) => (stage - 1) / Balance.StagesPerZone % Zones.Length;

    public static ZoneDef Zone(int stage) => Zones[ZoneIndex(stage)];

    /// <summary>How many times the zones have come round (0 the first time).</summary>
    public static int Cycle(int stage) => (stage - 1) / (Balance.StagesPerZone * Zones.Length);

    public static int StageInZone(int stage) => (stage - 1) % Balance.StagesPerZone + 1;

    public static bool IsZoneBoss(int stage) => StageInZone(stage) == Balance.StagesPerZone;

    /// <summary>"Greenwood 1-3"; past the last zone, "Greenwood II 1-3".</summary>
    public static string StageName(int stage)
    {
        int cycle = Cycle(stage);
        string roman = cycle switch { 0 => "", 1 => " II", 2 => " III", 3 => " IV", _ => $" {cycle + 1}" };
        return $"{Zone(stage).Name}{roman} {ZoneIndex(stage) + 1}-{StageInZone(stage)}";
    }

    /// <summary>A wave: one to three of the zone's monsters (fewer of the big ones), or a stage's boss.</summary>
    public static List<MonsterDef> Wave(Rng rng, int stage)
    {
        var zone = Zone(stage);
        var wave = new List<MonsterDef>();
        // Early stages ease in with smaller waves.
        int most = stage <= 2 ? 1 : stage <= 10 ? 2 : 3;
        int count = rng.Range(1, most + 1);
        double room = 3;
        for (int i = 0; i < count; i++)
        {
            var m = Monster(rng.Pick(zone.Monsters));
            double size = m.Size == Size.Small ? 1 : 1.5;
            if (size > room) break;
            room -= size;
            wave.Add(m);
        }
        return wave;
    }

    /// <summary>The boss at the end of a stage: a big one of the zone's monsters, or the zone's own boss on its last stage.</summary>
    public static MonsterDef Boss(int stage)
    {
        var zone = Zone(stage);
        return IsZoneBoss(stage) ? Monster(zone.Boss) : Monster(zone.Monsters[(StageInZone(stage) - 1) % zone.Monsters.Length]);
    }

    public static string BossName(int stage) => IsZoneBoss(stage) ? Zone(stage).BossTitle : $"Big {Boss(stage).Name}";
}
