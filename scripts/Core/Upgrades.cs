using System.Collections.Generic;
using System.Linq;

namespace LootFall.Core;

/// <param name="MinTier">The shop tier needed before it's on offer.</param>
/// <param name="SetsTier">For renovations: the shop tier it moves you to.</param>
/// <param name="Slots">For display cabinets: how many things it shows.</param>
/// <param name="Icon">Icon under res://assets/ui/icons/.</param>
/// <param name="SynergySlots">How many more synergies it lets you have active.</param>
/// <param name="CurioSlots">How many more curios it gives you room for.</param>
/// <param name="Customers">How many more customers come every day.</param>
/// <param name="Commissions">How many more commissions the regulars post each week.</param>
/// <param name="Requires">An upgrade that has to be bought first (the cabinet before it, the charter before the seal).</param>
public sealed record Upgrade(string Key, string Name, string Blurb, int Cost, int MinTier, string Icon, int SetsTier = 0, int Slots = 0,
    int SynergySlots = 0, int CurioSlots = 0, int Customers = 0, int Commissions = 0, string Requires = null)
{
    public bool IsRenovation => SetsTier > 0;
}

/// <summary>
/// What you can spend your earnings on. Moves (renovations) bring richer customers and new things to learn; tools each answer
/// one question your ears can't; cabinets show off what you keep, which draws more and better customers.
/// </summary>
public static class Upgrades
{
    public static readonly Upgrade[] All =
    {
        new("shop2", "Move to Thornwick", "A shop at Thornwick Market: 5 customers a day, better chests, locks, and two new sounds: potions and spells.",
            60, 1, "shop", SetsTier: 2),

        new("sign", "Painted Sign", "One more customer every day.", 40, 2, "sign", Customers: 1),
        new("cabinet1", "Oak Cabinet", "Room to display one more thing. Displays draw customers.", 60, 2, "cabinet", Slots: 1),
        new("picks", "Fine Lockpicks", "A wider sweet spot when you pick a lock.", 50, 2, "picks"),
        new("cabinet2", "Oak Dresser", "Room to display one more thing.", 120, 2, "cabinet", Slots: 1, Requires: "cabinet1"),
        new("shop3", "Move to Crownhold", "A shop in the capital: 6 customers a day, archmages with enchanted things, a dwarf, and gems.",
            300, 2, "shop", SetsTier: 3),

        new("lamp", "Witchlight", "Always lit: anything enchanted glows when it makes a sound. Nobody else can see it.", 200, 3, "lamp"),
        new("cabinet3", "Glass Cabinet", "Room to display one more thing.", 250, 3, "cabinet", Slots: 1, Requires: "cabinet2"),
        new("charter", "Crownhold Charter", "Two synergies at once.", 300, 3, "charter", SynergySlots: 1),
        new("case", "Curio Case", "Room for one more curio.", 250, 3, "case", CurioSlots: 1),
        new("cabinet4", "Glass Case", "Room to display one more thing.", 400, 3, "cabinet", Slots: 1, Requires: "cabinet3"),
        new("shop4", "The Royal Emporium", "By the Gilded Gate: 7 customers a day, the court, demons, and a fence for the Maw.", 2000, 3, "shop",
            SetsTier: 4),

        new("magnet", "Lodestone", "Iron can't move near it: no clanking, and demon gold goes quiet.", 400, 4, "magnet"),
        new("cabinet5", "Gilded Cabinet", "Room to display one more thing.", 700, 4, "cabinet", Slots: 1, Requires: "cabinet4"),
        new("seal", "Royal Seal", "Three synergies at once.", 900, 4, "seal", SynergySlots: 1, Requires: "charter"),
        new("cabinet6", "Gilded Case", "Room to display one more thing.", 1100, 4, "cabinet", Slots: 1, Requires: "cabinet5"),

        // The Royal Emporium's own: big sums to grow into while the Guild's dues climb.
        new("night", "Night Market", "Open after dark: two more customers every day.", 2500, 4, "night", Customers: 2),
        new("patrons", "Patrons' Book", "The regulars post four commissions a week, not three.", 4000, 4, "patrons", Commissions: 1),
        new("velvet", "Velvet Case", "Room for one more curio.", 6000, 4, "case", CurioSlots: 1, Requires: "case"),
    };

    public static Upgrade Get(string key) => All.First(u => u.Key == key);

    /// <summary>
    /// What's on offer now: nothing until the stall has earned its keep; then the right tier, not owned yet, only the
    /// next renovation, and only once what it builds on is bought (one cabinet after another, the charter before the seal).
    /// </summary>
    public static IEnumerable<Upgrade> Available(GameState state) => !state.UpgradesOpen ? Enumerable.Empty<Upgrade>() :
        All.Where(u => u.MinTier <= state.Tier && !state.Has(u.Key) && (!u.IsRenovation || u.SetsTier == state.Tier + 1) &&
                       (u.Requires == null || state.Has(u.Requires)));
}
