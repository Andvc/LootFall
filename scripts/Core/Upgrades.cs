using System.Collections.Generic;
using System.Linq;

namespace LootFall.Core;

/// <param name="MinTier">The shop tier needed before it's on offer.</param>
/// <param name="SetsTier">For renovations: the shop tier it moves you to.</param>
/// <param name="Icon">Icon under res://assets/ui/icons/.</param>
public sealed record Upgrade(string Key, string Name, string Blurb, int Cost, int MinTier, string Icon, int SetsTier = 0)
{
    public bool IsRenovation => SetsTier > 0;
}

/// <summary>
/// What you can spend your earnings on. Renovations bring richer customers and one new thing to learn each; the two
/// tools each answer one question your ears can't: what's magic, and what's iron.
/// </summary>
public static class Upgrades
{
    public static readonly Upgrade[] All =
    {
        new("shop2", "Village Shop", "5 customers a day, things worth 4x, and two new sounds: slosh and rustle.", 60, 1, "shop", SetsTier: 2),

        new("sign", "Painted Sign", "One more customer every day.", 40, 2, "sign"),
        new("shop3", "Town Shop", "6 customers a day: mages with magic things, and jewelry that tinkles.",
            100, 2, "shop", SetsTier: 3),

        new("lamp", "Witchlight", "Dims the room: magic glows when it makes a sound. Nobody else can see it.", 150, 3, "lamp"),
        new("shop4", "The Emporium", "7 customers a day: nobles, adventurers and a crook.",
            600, 3, "shop", SetsTier: 4),

        new("magnet", "Magnet", "Iron can't move: no clanking, and gilded fakes go quiet.", 300, 4, "magnet"),
        new("warrant", "Royal Warrant", "Appraiser to the Crown. The end of the road, for now.", 3500, 4, "crown"),
    };

    public static Upgrade Get(string key) => All.First(u => u.Key == key);

    /// <summary>What's on offer now: the right tier, not owned yet, and only the next renovation.</summary>
    public static IEnumerable<Upgrade> Available(GameState state) =>
        All.Where(u => u.MinTier <= state.Tier && !state.Has(u.Key) && (!u.IsRenovation || u.SetsTier == state.Tier + 1));
}
