using System;
using System.Collections.Generic;
using System.Linq;

namespace LootFall.Core;

/// <summary>
/// One of the nine Heartsongs: a thing someone loved more than gold, sung into the Locking (docs/world.md).
/// </summary>
/// <param name="Word">What its song pops out of the chest as, while you shake it.</param>
/// <param name="From">Who gave it, and where: the place it belongs to.</param>
/// <param name="Story">Why it was loved, in a sentence or two (the opening and the ledger tell it).</param>
/// <param name="Effect">What it does while it's on display.</param>
/// <param name="Sprite">Sprite under res://assets/sprites/items/.</param>
public sealed record HeartsongDef(string Key, string Name, string Word, string From, string Story, string Effect, string Sprite);

/// <summary>
/// The Heartsongs. A lock of gold could never hold the demon of greed, so the archmages asked the peoples of Aldmark for
/// things they loved more than gold, and sang the Locking into nine of them. Then they hid each one in an ordinary chest
/// among millions, where the Maw would never find them all, and when one is sold it goes back to the Locking, to turn up
/// in some other chest some day. Only a Keywright hears one sing: it sings at the gentlest shake, and nothing drowns it
/// out or stills it. To anyone else it's a trinket, worth what a trinket as fine as its chest fetches (no seller ever
/// prices one, or knows it's there), and the Guild won't take one in pawn: greed can't put a price on it.
/// <para>
/// Every chest a seller brings (not a lesson's, not the first day's) has a small chance of holding one: about one a run.
/// On display (any shop, no shelf needed, no slot taken), each bends one rule of the trade, and like the curios none of
/// them tells you what's in a chest or adds a share to what things sell for. Bram's kin remember every one they've
/// opened (<see cref="Profile.Songs"/>); with all nine, the Crown Chest's ending changes (<see cref="Story.VaultEpilogue"/>).
/// </para>
/// </summary>
public static class Heartsongs
{
    /// <summary>The chance any chest a seller brings holds one (--heartsongs=N sets it, for screenshots).</summary>
    public static float Chance { get; set; } = 0.004f;

    /// <summary>What a merchant pays for one as a trinket, times the quality multiplier of its chest's colour.</summary>
    public const int BaseValue = 10;

    /// <summary>One you've never found is this many times likelier to turn up than one you have.</summary>
    public const float NewWeight = 3f;

    public const string Lullaby = "lullaby", Pipe = "pipe", Chimes = "chimes", Egg = "egg", Lark = "lark", Hearth = "hearth", Bell = "bell",
        Star = "star", Ember = "ember";

    /// <summary>The fey chimes: how likely each thing that came out as promised is to come out a step better (on top of the usual 5%).</summary>
    public const float ChimesLuck = 0.01f;

    public static readonly HeartsongDef[] All =
    {
        new(Lullaby, "The Lullaby Box", "plink", "a farmer's wife at Brindle",
            "She gave the music box that sang her children to sleep. It still plays her lullaby, for a Keywright.",
            "Nobody hurries you: impatient sellers wait as long as you like.", "lullaby_box"),
        new(Pipe, "The Shepherd's Pipe", "toodle", "a shepherd of the Brindle hills",
            "He gave the pipe whose tune could call any flock home. It still calls.",
            "Its tune calls them home: anyone who storms off or walks out comes back later that day, calmer, at their first price.", "shepherds_pipe"),
        new(Chimes, "The Fey Chimes", "ting", "the fair folk of the Old Wood",
            "They gave the chimes from over their door between the worlds. Where they ring, luck turns.",
            "Fey luck: things come out better than their chest promised a little more often.", "fey_chimes"),
        new(Egg, "The Dragon's Egg", "thump", "Ysolt, the last dragon of the Ember Peaks",
            "She gave her egg, still warm. Three hundred years on, its heart still beats.",
            "A dragon guards its hoard: in the first chest you open each day, nothing comes out worse than its chest promised.", "dragons_egg"),
        new(Lark, "The Clockwork Lark", "tweet", "a tinker of Crownhold",
            "He gave the bird he built for a princess who never woke. It sings at dawn, and carries word.",
            "It carries word for you: a regular's commission stays open a week longer.", "clockwork_lark"),
        new(Hearth, "The Hearthstone", "hummm", "the dwarves of Ironroot",
            "They gave a stone from their first hearth. It hums with three thousand years of warm evenings.",
            "Folk linger by its warmth: your exhibition counts half as much again.", "hearthstone"),
        new(Bell, "The Drowned Bell", "dong", "old Aldwater, before the river took it",
            "The town gave its chapel bell. It still tolls for every lie told near it.",
            "It tolls when a seller lies about where their chest came from.", "drowned_bell"),
        new(Star, "The Bottled Star", "twinkle", "the archmages of the Keyhold",
            "They gave a star they caught as students and kept in a bottle all their lives. Things from the Locking gather round it.",
            "Room for one more curio.", "bottled_star"),
        new(Ember, "The Imp's Ember", "crackle", "Pib, an imp who loved a miller's daughter",
            "He gave his own heart, a coal that never goes out, so the Maw could never have her.",
            "Its warmth tempers glass: nothing made of glass ever breaks, dropped or forced.", "imps_ember"),
    };

    public static HeartsongDef Get(string key) => All.FirstOrDefault(h => h.Key == key) ?? All[0];

    public static bool Exists(string key) => All.Any(h => h.Key == key);

    /// <summary>What a Heartsong is, for the ledger and the first one you open.</summary>
    public const string Lore =
        "A lock of gold could never hold the demon of greed. So at the Locking the archmages asked the peoples of Aldmark " +
        "for things they loved more than gold, and sang the Locking into nine of them: the Heartsongs. Then they hid each in " +
        "an ordinary chest, among millions. Only a Keywright hears one sing. To anyone else it's a trinket, and the Guild " +
        "won't touch one: greed can't put a price on it.";

    /// <summary>
    /// Whether this chest holds a Heartsong, and which: one you've never found is likelier (<see cref="NewWeight"/>), and
    /// never one already found or waiting in a chest this run (each is only in one chest at a time).
    /// </summary>
    /// <param name="known">The ones Bram's kin have ever opened.</param>
    /// <param name="taken">The ones found (or on the counter) this run.</param>
    public static HeartsongDef Roll(Random rng, ICollection<string> known, ICollection<string> taken)
    {
        if (rng.NextFloat() >= Chance) return null;
        var free = All.Where(h => !taken.Contains(h.Key)).ToArray();
        return free.Length == 0 ? null : rng.PickWeighted(free, h => known.Contains(h.Key) ? 1f : NewWeight);
    }

    /// <summary>A Heartsong in a chest of this colour (priced as a trinket that fine).</summary>
    public static Item Make(HeartsongDef song, Rarity chest) => new(Category.Heartsong, chest, Song: song.Key);
}
