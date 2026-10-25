using System;
using System.Collections.Generic;
using LootFall.Progression;

namespace LootFall.Game;

/// <summary>The words for what the rules hold: item names, era materials, stat lines, slot kinds.</summary>
public static class Describe
{
    /// <summary>The material of an item level's codex era (a new one each time depth doubles past 100).</summary>
    public static readonly string[] Eras =
    {
        "Iron", "Steel", "Silvered", "Runed", "Mithril", "Adamant", "Dragonbone", "Starsteel",
        "Voidforged", "Sunforged", "Astral", "Celestium", "Aetherium", "Eternium", "Primeval", "Godforged",
    };

    public static string Era(double ilvl) => Eras[Hero.Era(ilvl)];

    public static string Kind(SlotKind k) => k switch
    {
        SlotKind.Weapon => "Weapons",
        SlotKind.Armor => "Armour",
        SlotKind.Jewelry => "Jewellery",
        SlotKind.Accessory => "Accessories",
        SlotKind.Trinket => "Trinkets",
        SlotKind.Tattoo => "Tattoos",
        SlotKind.Companion => "Companions",
        _ => "Celestial",
    };

    /// <summary>The slot's base noun without its side ("Left Index Ring" reads "Index Ring").</summary>
    public static string Base(string slot) => slot.StartsWith("Left ") ? slot[5..] : slot.StartsWith("Right ") ? slot[6..] : slot;

    /// <summary>
    /// The item family a slot's picture comes from (assets/icons/{family}.png, docs/art-direction.md "Items and icons"):
    /// left and right share one design, the finger rings and the toe rings a family each, each trinket kind one, the
    /// constellations one. About 85 in all.
    /// </summary>
    public static string Family(string slot)
    {
        var b = Base(slot);
        if (b.EndsWith("Toe Ring")) return "toe_ring";
        if (b.EndsWith(" Ring") && b != "Nose Ring") return "ring";
        if (b.StartsWith("Constellation")) return "constellation";
        foreach (var t in new[] { "Charm", "Relic", "Talisman", "Idol" })
            if (b.StartsWith(t + " ")) return t.ToLowerInvariant();
        return b.ToLowerInvariant().Replace("'", "").Replace(' ', '_');
    }

    /// <summary>An item's name: a prefix from its first affix, the era's material and the slot, a suffix from another
    /// affix ("Savage Mithril Helm of the Hawk"). Commons have no affixes and are plain ("Iron Helm").</summary>
    public static string Name(Content c, int slot, Item it)
    {
        string baseName = $"{Era(it.Ilvl)} {Base(c.Slots[slot].Name)}";
        string prefix = null, suffix = null;
        for (int i = 0; i < it.Affixes; i++)
        {
            var names = AffixNames(c, it.Stats[i]);
            if (names == null) continue;
            var adjectives = new List<string>();
            var ofs = new List<string>();
            foreach (var n in names) (n.StartsWith("of ") ? ofs : adjectives).Add(n);
            int pick = (int)(it.Rolls[i] * 997);
            if (prefix == null && adjectives.Count > 0) prefix = adjectives[pick % adjectives.Count];
            else if (suffix == null && ofs.Count > 0) suffix = ofs[pick % ofs.Count];
            if (prefix != null && suffix != null) break;
        }
        return (prefix != null ? prefix + " " : "") + baseName + (suffix != null ? " " + suffix : "");
    }

    /// <summary>Where on the body (or the camp) a slot sits, for the armoury's grouping: head to toe, then the hands and
    /// feet with their ten rings each.</summary>
    public static string Region(SlotDef s)
    {
        string n = s.Name;
        bool Has(params string[] words)
        {
            foreach (var w in words)
                if (n.Contains(w)) return true;
            return false;
        }
        switch (s.Kind)
        {
            case SlotKind.Weapon:
                return "In hand and at the belt";
            case SlotKind.Armor:
                if (Has("Helm", "Visor", "Hood", "Gorget")) return "Head";
                if (Has("Breastplate", "Tunic", "Tabard", "Cloak", "Mantle")) return "Body";
                if (Has("Pauldron", "Vambrace", "Elbow", "Gauntlet")) return "Arms";
                if (Has("Belt", "Tassets", "Faulds")) return "Waist";
                if (Has("Boot")) return "Feet";
                return "Legs";
            case SlotKind.Jewelry:
                if (n.EndsWith("Toe Ring")) return n.StartsWith("Left") ? "Left foot" : "Right foot";
                if (n.EndsWith(" Ring") && n != "Nose Ring") return n.StartsWith("Left") ? "Left hand" : "Right hand";
                if (Has("Amulet", "Necklace", "Choker")) return "Neck";
                if (Has("Bracelet", "Armlet")) return "Arms";
                if (Has("Anklet")) return "Ankles";
                return "Head";
            case SlotKind.Trinket:
                return n.Split(' ')[0] + "s";
            case SlotKind.Tattoo:
                if (Has("Brow", "Neck")) return "Head and neck";
                if (Has("Chest", "Back")) return "Body";
                if (Has("Shoulder", "Arm", "Hand")) return "Arms";
                return "Legs";
            case SlotKind.Companion:
                if (Has("Familiar")) return "The familiar";
                if (Has("Mount", "Saddle", "Barding", "Reins")) return "The mount";
                return "The camp";
            case SlotKind.Celestial:
                return n.StartsWith("Constellation") ? "Constellations" : "The heavens";
            default:
                return "Carried";
        }
    }

    static string[] AffixNames(Content c, Stat s)
    {
        foreach (var a in c.Affixes)
            if (a.Stat == s) return a.Names;
        return null;
    }

    static readonly Dictionary<Stat, string> Label = new()
    {
        [Stat.FlatDamage] = "Damage", [Stat.FlatHp] = "Health", [Stat.Armor] = "Armour", [Stat.Dodge] = "Dodge rating",
        [Stat.Block] = "Block rating", [Stat.CritRating] = "Crit rating", [Stat.MultiHit] = "Multi-hit rating",
        [Stat.IncDamage] = "increased damage", [Stat.IncHp] = "increased health", [Stat.CritDamage] = "crit damage",
        [Stat.AttackSpeed] = "attack speed", [Stat.Regen] = "health regenerated a second", [Stat.Leech] = "damage leeched as health",
        [Stat.Might] = "might", [Stat.Fortitude] = "fortitude", [Stat.IncArmor] = "increased armour", [Stat.IncDodge] = "increased dodge",
        [Stat.IncBlock] = "increased block", [Stat.IncCrit] = "increased crit chance", [Stat.IncMultiHit] = "increased multi-hit",
        [Stat.GoldFind] = "gold found", [Stat.ItemFind] = "item rarity", [Stat.XpGain] = "experience", [Stat.MaterialFind] = "materials",
        [Stat.Quantity] = "items dropped", [Stat.MoreDamage] = "more damage", [Stat.MoreHp] = "more health",
    };

    public static string StatName(Stat s) => Label[s];

    /// <summary>One stat as a line: flat stats as absolute amounts (they're kept as LE), percents as percents, "more" as
    /// the multiplier it is.</summary>
    public static string Line(Stat s, double value, Scale sc)
    {
        if (Stats.IsFlat(s)) return $"+{Format.Num(Big.Pow10(value * Math.Log10(sc.R)))} {Label[s]}";
        if (Stats.IsMore(s)) return $"{Format.Percent(sc.Pow(value) - 1, sign: true)} {Label[s]}";
        if (s == Stat.Regen || s == Stat.Leech) return $"{Format.Percent(value, sign: true)} {Label[s]}";
        return $"{Format.Percent(value, sign: true)} {Label[s]}";
    }

    /// <summary>What an item gives in its slot, as lines (main stats first, then each affix).</summary>
    public static List<string> Lines(Hero h, int slot, Item it)
    {
        var sheet = new Sheet();
        h.Contribute(slot, it, sheet, slotBonus: false);
        var lines = new List<string>();
        for (int i = 0; i < Stats.Count; i++)
        {
            var s = (Stat)i;
            double v = sheet.V[i];
            if (Stats.IsFlat(s) ? v <= Scale.None : Math.Abs(v) < 1e-12) continue;
            lines.Add(Line(s, v, h.S));
        }
        return lines;
    }

    /// <summary>A big absolute amount given as LE (a monster's health, a hit).</summary>
    public static string Amount(double le, Scale sc) => Format.Num(Big.Pow10(le * Math.Log10(sc.R)));
}
