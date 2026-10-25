using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using LootFall.Game;
using LootFall.Progression;

/// <summary>
/// progsim --manifest=PATH: the content the art pipeline needs, from the game's own definitions so the manifest never
/// falls behind them: every realm with its monsters (key, name, body plan, size) and guardian, every item family with the
/// slots it covers, and the codex eras (tools/art.py builds art/manifest.json from it).
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
            families = c.Slots.GroupBy(s => Describe.Family(s.Name)).Select(g => new { family = g.Key, kind = g.First().Kind.ToString(), slots = g.Select(s => s.Name) }),
            eras = Describe.Eras,
        };
        File.WriteAllText(path, JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"Wrote {path}: {Bestiary.Realms.Length} realms, {c.Slots.Select(s => Describe.Family(s.Name)).Distinct().Count()} item families");
        return 0;
    }
}
