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
public sealed record Upgrade(string Key, string Name, string Blurb, int Cost, int MinTier, string Icon, int SetsTier = 0, int Slots = 0,
    int SynergySlots = 0, int CurioSlots = 0, int Customers = 0, int Commissions = 0)
{
    public bool IsRenovation => SetsTier > 0;
}

/// <summary>
/// What you can spend your earnings on. Renovations bring richer customers and new things to learn; tools each answer
/// one question your ears can't; cabinets show off what you keep, which draws more and better customers.
/// </summary>
public static class Upgrades
{
    public static readonly Upgrade[] All =
    {
        new("shop2", "Village Shop", "5 customers a day, better chests, locks, and two new sounds: slosh and rustle.", 60, 1, "shop", SetsTier: 2),

        new("sign", "Painted Sign", "One more customer every day.", 40, 2, "sign", Customers: 1),
        new("cabinet1", "Oak Cabinet", "Room to display three more things. Displays draw customers.", 60, 2, "cabinet", Slots: 3),
        new("picks", "Fine Lockpicks", "A wider sweet spot when you pick a lock.", 50, 2, "picks"),
        new("shop3", "Town Shop", "6 customers a day: mages with enchanted things, a miner, and jewelry.", 200, 2, "shop", SetsTier: 3),

        new("lamp", "Witchlight", "Always lit: anything enchanted glows when it makes a sound. Nobody else can see it.", 200, 3, "lamp"),
        new("cabinet2", "Glass Cabinet", "Room to display three more things.", 250, 3, "cabinet", Slots: 3),
        new("charter", "Guild Charter", "Two synergies at once.", 300, 3, "charter", SynergySlots: 1),
        new("case", "Curio Case", "Room for one more curio.", 250, 3, "case", CurioSlots: 1),
        new("shop4", "The Emporium", "7 customers a day: nobles, adventurers and a crook.", 1200, 3, "shop", SetsTier: 4),

        new("magnet", "Magnet", "Iron can't move: no clanking, and gilded fakes go quiet.", 400, 4, "magnet"),
        new("cabinet3", "Gilded Cabinet", "Room to display four more things.", 600, 4, "cabinet", Slots: 4),
        new("seal", "Royal Seal", "Three synergies at once.", 900, 4, "seal", SynergySlots: 1),

        // The Emporium's own: big sums to grow into while the Guild's dues climb.
        new("night", "Night Market", "Open after dark: two more customers every day.", 2500, 4, "night", Customers: 2),
        new("patrons", "Patrons' Book", "The regulars post four commissions a week, not three.", 4000, 4, "patrons", Commissions: 1),
        new("velvet", "Velvet Case", "Room for one more curio.", 6000, 4, "case", CurioSlots: 1),
    };

    public static Upgrade Get(string key) => All.First(u => u.Key == key);

    /// <summary>
    /// What's on offer now: nothing until the stall has earned its keep; then the right tier, not owned yet, and only
    /// the next renovation.
    /// </summary>
    public static IEnumerable<Upgrade> Available(GameState state) => !state.UpgradesOpen ? Enumerable.Empty<Upgrade>() :
        All.Where(u => u.MinTier <= state.Tier && !state.Has(u.Key) && (!u.IsRenovation || u.SetsTier == state.Tier + 1));
}
