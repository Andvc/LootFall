using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using LootFall.Game;
using LootFall.Progression;

/// <summary>
/// progsim --manifest=PATH: the content the art pipeline needs, from the game's own definitions so the manifest never
/// falls behind them: every realm with its monsters (key, name, body plan, size) and guardian, every item family with the
/// pieces in it, the body's sites with their slots, every piece with the slots it fills and keeps free and the boards it
/// brings, every board with its slots, and the codex eras (tools/art.py builds art/manifest.json from it, and
/// tools/equipment_pieces.py the equipment pieces' art list).
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
            // Every piece: where it goes (the slots it fills and keeps free, by key), the boards it brings and the slots of
            // each it opens.
            pieces = c.Slots.Select(s => new
            {
                name = s.Name, family = Describe.Family(s), kind = s.Kind.ToString(), board = c.Boards[s.Board].Key, level = s.Level,
                side = s.Side.ToString(), region = Describe.Region(c, s), prestige = s.Prestige, tier = s.Tier, depth = s.UnlockDepth,
                takes = s.Takes.Select(x => c.Cells[x].Key), keeps = s.Keeps.Select(x => c.Cells[x].Key),
                brings = s.Brings.Select(b => new { board = c.Boards[b.Board].Key, opens = b.Opens.Select(x => c.Cells[x].Key) }),
            }),
            boards = c.Boards.Select(b => new
            {
                key = b.Key, name = b.Name, level = b.Level, hosts = b.Hosts.Select(h => c.Slots[h].Name),
                slots = b.Cells.Select(x => new { key = c.Cells[x].Key, name = c.Cells[x].Name }),
            }),
            eras = Describe.Eras,
        };
        File.WriteAllText(path, JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"Wrote {path}: {Bestiary.Realms.Length} realms, {c.Slots.Select(s => Describe.Family(s)).Distinct().Count()} item families");
        return 0;
    }
}
