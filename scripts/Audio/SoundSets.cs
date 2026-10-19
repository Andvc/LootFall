using System;
using System.Collections.Generic;
using LootFall.Core;

namespace LootFall.Audio;

/// <summary>
/// The sound each kind of thing makes, with alternatives to choose from in Settings > Item sounds. Each option is a
/// sound group: recordings under res://assets/audio/{group}/ (from <c>tools/import_art.py</c>) or one synthesized in
/// <see cref="Sfx"/>. The first option of each kind is the default.
/// </summary>
public static class SoundSets
{
    public sealed record Option(string Name, string Group);

    static readonly Dictionary<Category, Option[]> All = new()
    {
        [Category.Coins] = new Option[]
        {
            new("A: a purse of coins", "jingle"), new("B: clinking chips", "jingle_chips"), new("C: a handful of chips", "jingle_handle"),
            new("D: bright coins (synth)", "jingle_bright"),
        },
        [Category.Metal] = new Option[]
        {
            new("A: tin", "clank"), new("B: light metal", "clank_light"), new("C: pots and pans", "clank_pot"),
            new("D: metal plate", "clank_plate"),
        },
        [Category.Cloth] = new Option[]
        {
            new("A: cloth", "flump"), new("B: soft thump", "flump_soft"), new("C: heavy thump", "flump_heavy"),
            new("D: leather", "flump_leather"),
        },
        [Category.Bottles] = new Option[]
        {
            new("A: slosh (synth)", "slosh"), new("B: glug (synth)", "slosh_glug"), new("C: glass clink", "slosh_clink"),
        },
        [Category.Paper] = new Option[]
        {
            new("A: shoved cards", "rustle"), new("B: book pages", "rustle_book"), new("C: sliding cards", "rustle_slide"),
            new("D: shuffled cards", "rustle_shuffle"),
        },
        [Category.Jewels] = new Option[]
        {
            new("A: glass tinkle", "tinkle"), new("B: other glass", "tinkle_glass"), new("C: little bells (synth)", "tinkle_bell"),
            new("D: light plate", "tinkle_plate"),
        },
    };

    public static IReadOnlyList<Option> For(Category kind) => All[kind];

    /// <summary>The chosen option for a kind.</summary>
    public static Option Chosen(Category kind)
    {
        var options = All[kind];
        return options[Math.Clamp(Settings.SoundSet(kind), 0, options.Length - 1)];
    }

    /// <summary>The sound group to play for a thing of this kind.</summary>
    public static string Group(Category kind) => Chosen(kind).Group;
}
