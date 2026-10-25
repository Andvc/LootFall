using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using LootFall.Game;
using LootFall.Progression;

/// <summary>
/// progsim --manifest=PATH: the content the art pipeline needs, from the game's own definitions so the manifest never
/// falls behind them: every realm with its monsters (key, name, body plan, size) and guardian, every item family with the
/// pieces in it, the body's sites with their slots, and the codex eras (tools/art.py builds art/manifest.json from it).
/// </summary>
static class ArtContent
{
    public static int Write(string path)
    {
        var c = new Content(new Params());
        object Monster(MonsterDef m) => new { key = m.Key, name = m.Name, body = m.Body.ToString(), size = m.Size };
        var data = new
        {
            realms = Bestiary.Realms.Select((r, i) => new { key = r.Key, name = r.Name, era = i, monsters = r.Monsters.Select(Monster), guardian = Monster(r.Guardian) }),
            families = c.Slots.GroupBy(s => Describe.Family(s)).Select(g => new { family = g.Key, kind = g.First().Kind.ToString(), slots = g.Select(s => s.Name) }),
            // The body's sites and their slots, by region: what the equipment figure has to show (art/equipment).
            sites = c.Cells.Where(x => x.Board == Anatomy.Body).GroupBy(x => x.Site).Select(g => new
            {
                key = g.Key, name = g.First().SiteName, region = g.First().Region, back = g.First().Back,
                slots = g.Select(x => new { key = x.Key, name = x.Name, layer = x.Layer.ToString() }),
            }),
            eras = Describe.Eras,
        };
        File.WriteAllText(path, JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"Wrote {path}: {Bestiary.Realms.Length} realms, {c.Slots.Select(s => Describe.Family(s)).Distinct().Count()} item families");
        return 0;
    }
}
