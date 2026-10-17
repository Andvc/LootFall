using System.Collections.Generic;
using System.Linq;

namespace LootFall.Core;

/// <param name="MinTier">The shop tier needed before it's on offer.</param>
/// <param name="Requires">Another upgrade it builds on, or null.</param>
/// <param name="SetsTier">For renovations: the shop tier it moves you to.</param>
/// <param name="Icon">Icon under res://assets/ui/icons/.</param>
public sealed record Upgrade(string Key, string Name, string Blurb, int Cost, int MinTier, string Icon,
    string Requires = null, int SetsTier = 0)
{
    public bool IsRenovation => SetsTier > 0;
}

/// <summary>What you can spend your earnings on: a bigger shop (new customers and things to learn) and tools.</summary>
public static class Upgrades
{
    public static readonly Upgrade[] All =
    {
        new("scale", "Shop Scale", "Set a chest on it to weigh what's inside. A wool coat outweighs a rag.", 25, 1, "scale"),
        new("peephole", "Keyhole Lens", "Peek at the one thing nearest the keyhole. 2 peeks a day.", 40, 1, "lens"),
        new("shop2", "Village Shop", "Patch the roof, hang a door. 5 customers a day: soldiers and merchants with silver, steel and books.",
            120, 1, "shop", SetsTier: 2),

        new("trumpet", "Ear Trumpet", "Hear the small differences within a sound: copper or silver, iron or steel.", 60, 2, "trumpet"),
        new("lantern", "Lantern Lens", "See the two things nearest the keyhole. 3 peeks a day.", 120, 2, "lens", Requires: "peephole"),
        new("picks", "Fine Lockpicks", "A wider sweet spot when you pick a lock.", 80, 2, "picks"),
        new("sign", "Painted Sign", "One more customer every day.", 100, 2, "sign"),
        new("shop3", "Town Shop", "A real storefront. 6 customers a day: mages with potions and scrolls, and jewelry.",
            300, 2, "shop", SetsTier: 3),

        new("compass", "Mana Compass", "The needle swings toward magic, hard for strong magic.", 150, 3, "compass"),
        new("periscope", "Brass Periscope", "See everything inside. 4 peeks a day.", 300, 3, "lens", Requires: "lantern"),
        new("shop4", "The Emporium", "The finest shop in town. 7 customers a day: nobles, adventurers and crooks.",
            900, 3, "shop", SetsTier: 4),

        new("lodestone", "Lodestone", "Iron and steel tug toward it. Gold, silver and copper don't.", 150, 4, "magnet"),
        new("hammer", "Tapping Hammer", "Knock on a chest: a false bottom sounds hollow.", 120, 4, "hammer"),
        new("warrant", "Royal Warrant", "Appraiser to the Crown. The end of the road, for now.", 2000, 4, "crown"),
    };

    public static Upgrade Get(string key) => All.First(u => u.Key == key);

    /// <summary>What's on offer now: the right tier, prerequisites met, not owned yet.</summary>
    public static IEnumerable<Upgrade> Available(GameState state) =>
        All.Where(u => u.MinTier <= state.Tier && !state.Has(u.Key) && (u.Requires == null || state.Has(u.Requires))
                       && (!u.IsRenovation || u.SetsTier == state.Tier + 1));
}
