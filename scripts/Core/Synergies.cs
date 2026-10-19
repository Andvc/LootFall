using System;
using System.Collections.Generic;
using System.Linq;

namespace LootFall.Core;

/// <summary>
/// A synergy in the collection journal: something the shop gets known for once you've acquired enough of something
/// (ten of a kind at a quality or better), or done something often enough (perfect reads in a row). Each has five
/// levels; only active synergies do anything, and the shop starts with room for one.
/// </summary>
public sealed class SynergyDef
{
    public string Key { get; init; }
    public string Name { get; init; }

    /// <summary>What it's about, in a few words.</summary>
    public string Blurb { get; init; }

    /// <summary>The kinds to acquire ten of (empty for achievement synergies).</summary>
    public Category[] Kinds { get; init; } = Array.Empty<Category>();

    /// <summary>For achievement synergies: what's needed for each level, and how far you've got.</summary>
    public int[] Steps { get; init; }
    public Func<GameState, int> Achieved { get; init; }
    public string StepWord { get; init; }

    /// <summary>What it does at a level (1 to 5).</summary>
    public Func<int, string> Effect { get; init; }

    /// <summary>The shop tier from which you could make progress (for the ledger: later ones are shown greyed).</summary>
    public int Tier { get; init; } = 1;

    public const int MaxLevel = 5;

    /// <summary>Of each kind, how many you need at the level's quality or better.</summary>
    public const int PerKind = 10;

    /// <summary>The level the shop has reached: 0 until the first is.</summary>
    public int Level(GameState state)
    {
        if (Kinds.Length > 0)
        {
            for (int level = MaxLevel; level >= 1; level--)
                if (Kinds.All(k => state.Acquired(k, (Rarity)(level - 1)) >= PerKind)) return level;
            return 0;
        }
        int have = Achieved(state);
        return Steps.Count(step => have >= step);
    }

    /// <summary>What the next level needs, and how far along you are: one line per kind, or one for an achievement.</summary>
    public IEnumerable<string> Progress(GameState state)
    {
        int next = Level(state) + 1;
        if (next > MaxLevel) yield break;
        if (Kinds.Length > 0)
        {
            var quality = (Rarity)(next - 1);
            foreach (var kind in Kinds)
                yield return $"{Math.Min(PerKind, state.Acquired(kind, quality))}/{PerKind} {Categories.Get(kind).Name.ToLowerInvariant()}" +
                             (quality == Rarity.Common ? "" : $", {Categories.QualityName(quality)} or better");
        }
        else yield return $"{Achieved(state)}/{Steps[next - 1]} {StepWord}";
    }
}

/// <summary>
/// What the shop gets known for. A name brings people and chances your way (more of a kind in the chests, smiths at the
/// door, false bottoms, enchanted things), and spares you some breakage; it never adds a share to what things sell for,
/// which would grow the shop faster than your judgment does.
/// </summary>
public static class Synergies
{
    public static readonly SynergyDef[] All =
    {
        new()
        {
            Key = "jewelry", Name = "Jewelry Tycoon", Blurb = "Coins and jewelry", Kinds = new[] { Category.Coins, Category.Jewels }, Tier = 3,
            Effect = l => $"Moneychangers and jewellers come your way: coins and jewelry turn up in chests {Pct(MoneyKinds(l))} more often.",
        },
        new()
        {
            Key = "smith", Name = "Smithing Tycoon", Blurb = "Metal", Kinds = new[] { Category.Metal },
            Effect = l => $"Smiths and soldiers, who bring plenty of metal, come {Pct(SmithVisits(l) - 1)} more often.",
        },
        new()
        {
            Key = "vintner", Name = "Vintner", Blurb = "Bottles", Kinds = new[] { Category.Bottles }, Tier = 2,
            Effect = l => l >= SynergyDef.MaxLevel ? "Packed like a vintner: glass never breaks, dropped or forced."
                : $"Packed like a vintner: glass is {Pct(Sturdier(l))} less likely to break, dropped or forced.",
        },
        new()
        {
            Key = "archivist", Name = "Archivist", Blurb = "Paper", Kinds = new[] { Category.Paper }, Tier = 2,
            Effect = l => $"People trust you with their secrets: chests are {Pct(HiddenBonus(l))} likelier to have a false bottom.",
        },
        new()
        {
            Key = "tailor", Name = "Tailor", Blurb = "Cloth", Kinds = new[] { Category.Cloth },
            Effect = l => $"Fine drapery: your exhibition counts +{Pct(ShowBonus(l))}.",
        },
        new()
        {
            Key = "keen", Name = "Keen Eye", Blurb = "Perfect reads in a row", Steps = new[] { 3, 6, 10, 15, 25 }, StepWord = "perfect reads in a row",
            Achieved = s => s.BestStreak,
            Effect = l => $"A perfect read earns you a valuer's fee: {KeenFee(l)}g per shop tier.",
        },
        new()
        {
            Key = "arcane", Name = "Arcanist", Blurb = "Enchanted things found", Steps = new[] { 3, 8, 15, 25, 40 }, StepWord = "enchanted things", Tier = 3,
            Achieved = s => s.MagicFound,
            Effect = l => $"Mages seek you out: each thing is {Pct(MagicBonus(l))} likelier to be enchanted.",
        },
    };

    public static SynergyDef Get(string key) => All.First(s => s.Key == key);

    /// <summary>"12%", the same in every locale.</summary>
    public static string Pct(float fraction) => $"{MathF.Round(fraction * 100)}%";

    public static float MoneyKinds(int level) => 0.1f * level;
    public static float Sturdier(int level) => Math.Min(1f, 0.2f * level);
    public static float HiddenBonus(int level) => 0.02f * level;
    public static float ShowBonus(int level) => 0.1f * level;
    public static int KeenFee(int level) => level;
    public static float MagicBonus(int level) => 0.01f * level;

    /// <summary>How much more often smiths and soldiers (who bring a lot of metal) come in.</summary>
    public static float SmithVisits(int level) => 1f + 0.2f * level;
}
