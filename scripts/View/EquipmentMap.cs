using System;
using System.Collections.Generic;
using System.Text.Json;
using Godot;
using LootFall.Progression;

namespace LootFall.View;

/// <summary>A body site's area on one picture: its centre (the point deepest inside it), its box and its outline.</summary>
public sealed class MapSite
{
    public int Id;
    public string Key;
    public Vector2 Centre;
    public Rect2 Box;
    /// <summary>Mostly hair (or a beard): its strands don't show through what's worn over it.</summary>
    public bool Busy;
    public Vector2[][] Outline;
}

/// <summary>
/// One picture of the knight (docs/equipment.md, "The figure"): the figure from the front or from behind, or one view
/// of a close-up (the head from either side or the front, a hand's back or palm, a foot's top or sole). In its own
/// pixels: the art, which body site each pixel is (for the mouse, highlights and fills), each site's area, and every
/// slot's point on it.
/// </summary>
public sealed class MapView
{
    public string Name;
    public Vector2 Size;
    public bool Closeup;
    public Texture2D Texture, SitesTexture;
    /// <summary>The site map: each pixel's site, row by row (its size is the picture's, <see cref="Size"/>).</summary>
    public byte[] Ids = Array.Empty<byte>();
    public int Width, Height;
    public readonly List<MapSite> Sites = new() { null };
    public readonly Dictionary<string, int> Index = new();
    /// <summary>Slot points (cell key to pixel): the whole figure's on the front and back, a close-up's on its views.</summary>
    public readonly Dictionary<string, Vector2> Points = new();
    List<Vector2>[] _samples;

    public void Load(string texture, string sites)
    {
        Texture = GD.Load<Texture2D>(texture);
        SitesTexture = GD.Load<Texture2D>(sites);
        var img = SitesTexture?.GetImage();
        if (img == null) return;
        if (img.IsCompressed()) img.Decompress();
        if (img.GetFormat() != Image.Format.L8) img.Convert(Image.Format.L8);
        Ids = img.GetData();
        Width = img.GetWidth();
        Height = img.GetHeight();
    }

    /// <summary>The site under a point (its number in <see cref="Sites"/>, 0 for none).</summary>
    public int SiteIdAt(Vector2 p)
    {
        int x = (int)Mathf.Floor(p.X), y = (int)Mathf.Floor(p.Y);
        if (x < 0 || y < 0 || x >= Width || y >= Height) return 0;
        int id = Ids[y * Width + x];
        return id < Sites.Count ? id : 0;
    }

    public string SiteAt(Vector2 p) => SiteIdAt(p) is var id && id > 0 ? Sites[id].Key : null;

    public bool Has(string site) => Index.ContainsKey(site);

    /// <summary>A loose grid of each site's pixels, for spreading points over an area.</summary>
    public List<Vector2> Samples(int id)
    {
        if (_samples == null)
        {
            _samples = new List<Vector2>[Sites.Count];
            for (int i = 0; i < Sites.Count; i++) _samples[i] = new List<Vector2>();
            for (int y = 2; y < Height; y += 5)
                for (int x = 2; x < Width; x += 5)
                {
                    int s = Ids[y * Width + x];
                    if (s > 0 && s < Sites.Count) _samples[s].Add(new Vector2(x + 0.5f, y + 0.5f));
                }
        }
        return id > 0 && id < _samples.Length ? _samples[id] : new List<Vector2>();
    }
}

/// <summary>A part of a picture a page shows: the view, and the rectangle of it in its pixels.</summary>
public sealed class MapPanel
{
    public string View;
    public Rect2 Rect;
}

/// <summary>
/// The equipment screen's map of the art (assets/equipment/map.json, written by tools/equipment_map.py from the
/// delivered pictures): every view, every body site's area on each, every slot's point, the pages each body region
/// opens (the head, hands and feet on their close-ups, the rest on the figure, both sides where both have slots), and
/// what each site covers (a mask the face's parts, a shirt the torso front and back, a glove the fingers).
/// </summary>
public static class EquipmentMap
{
    /// <summary>The body regions, in the order the screens list them.</summary>
    public static readonly string[] Regions =
    {
        "Head", "Neck", "Torso", "Back", "Waist", "Right arm", "Left arm", "Right hand", "Left hand", "Right leg", "Left leg",
        "Right foot", "Left foot", "Beyond",
    };

    static bool _loaded;
    static readonly Dictionary<string, MapView> _views = new();
    static readonly Dictionary<string, List<MapPanel>> _pages = new();
    static readonly Dictionary<string, List<(string Leaf, int Depth, bool Wrap)>> _covers = new();
    static readonly Dictionary<string, int> _glyph = new();
    static Texture2D _glyphs, _stage, _board;
    static int _glyphCell = 256, _glyphCols = 5;

    public static IReadOnlyDictionary<string, MapView> Views
    {
        get
        {
            Load();
            return _views;
        }
    }

    public static MapView View(string name)
    {
        Load();
        return _views.TryGetValue(name, out var v) ? v : null;
    }

    /// <summary>A region's page: the views it shows, side by side ("Body" for the whole figure, front and back).</summary>
    public static List<MapPanel> Page(string region)
    {
        Load();
        if (region == null)
            return new List<MapPanel>
            {
                new() { View = "front", Rect = new Rect2(Vector2.Zero, View("front").Size) },
                new() { View = "back", Rect = new Rect2(Vector2.Zero, View("back").Size) },
            };
        return _pages.TryGetValue(region, out var p) ? p : Page(null);
    }

    /// <summary>The leaf sites (those with an area on some view) a site covers, how far down, and whether only what's
    /// wrapped round (cloth, mail, plate, over) reaches it.</summary>
    public static List<(string Leaf, int Depth, bool Wrap)> Covers(string site)
    {
        Load();
        return _covers.TryGetValue(site, out var c) ? c : new List<(string, int, bool)>();
    }

    public static Texture2D Stage
    {
        get
        {
            Load();
            return _stage;
        }
    }

    public static Texture2D Board
    {
        get
        {
            Load();
            return _board;
        }
    }

    public static Texture2D Glyphs
    {
        get
        {
            Load();
            return _glyphs;
        }
    }

    /// <summary>A slot kind's glyph in the atlas (skin, under, mail, armor, over, adorn, hold, graft, beyond, mod).</summary>
    public static Rect2 GlyphRect(string name)
    {
        Load();
        int i = _glyph.TryGetValue(name, out var g) ? g : _glyph.GetValueOrDefault("mod");
        return new Rect2(i % _glyphCols * _glyphCell, i / _glyphCols * _glyphCell, _glyphCell, _glyphCell);
    }

    public static string GlyphOf(Layer l) => l switch
    {
        Layer.Skin => "skin", Layer.Under => "under", Layer.Mail => "mail", Layer.Armor => "armor", Layer.Over => "over",
        Layer.Adorn => "adorn", Layer.Hold => "hold", Layer.Graft => "graft", Layer.Beyond => "beyond", _ => "mod",
    };

    static Vector2 V(JsonElement e) => new((float)e[0].GetDouble(), (float)e[1].GetDouble());

    static void Load()
    {
        if (_loaded) return;
        _loaded = true;
        const string path = "res://assets/equipment/map.json";
        if (!FileAccess.FileExists(path))
        {
            GD.PushError("No assets/equipment/map.json: run tools/equipment_map.py");
            return;
        }
        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
        using var doc = JsonDocument.Parse(file.GetAsText());
        var root = doc.RootElement;
        foreach (var vp in root.GetProperty("views").EnumerateObject())
        {
            var j = vp.Value;
            var v = new MapView { Name = vp.Name, Size = V(j.GetProperty("size")), Closeup = j.GetProperty("closeup").GetBoolean() };
            v.Load(j.GetProperty("texture").GetString(), j.GetProperty("sites_texture").GetString());
            foreach (var sj in j.GetProperty("sites").EnumerateArray())
            {
                var box = sj.GetProperty("box");
                var outline = new List<Vector2[]>();
                foreach (var ring in sj.GetProperty("outline").EnumerateArray())
                {
                    var pts = new List<Vector2>();
                    foreach (var p in ring.EnumerateArray()) pts.Add(V(p));
                    outline.Add(pts.ToArray());
                }
                var site = new MapSite
                {
                    Id = v.Sites.Count, Key = sj.GetProperty("key").GetString(), Centre = V(sj.GetProperty("centre")),
                    Box = new Rect2(box[0].GetSingle(), box[1].GetSingle(), box[2].GetSingle() - box[0].GetSingle(), box[3].GetSingle() - box[1].GetSingle()),
                    Outline = outline.ToArray(),
                };
                if (sj.TryGetProperty("busy", out var busy)) site.Busy = busy.GetBoolean();
                v.Index[site.Key] = site.Id;
                v.Sites.Add(site);
            }
            _views[v.Name] = v;
        }
        foreach (var group in new[] { "overview", "closeups" })
            foreach (var vp in root.GetProperty(group).EnumerateObject())
                if (_views.TryGetValue(vp.Name, out var v))
                    foreach (var cp in vp.Value.EnumerateObject()) v.Points[cp.Name] = V(cp.Value);
        foreach (var pp in root.GetProperty("pages").EnumerateObject())
        {
            var list = new List<MapPanel>();
            foreach (var pj in pp.Value.EnumerateArray())
            {
                var r = pj.GetProperty("rect");
                list.Add(new MapPanel
                {
                    View = pj.GetProperty("view").GetString(),
                    Rect = new Rect2(r[0].GetSingle(), r[1].GetSingle(), r[2].GetSingle() - r[0].GetSingle(), r[3].GetSingle() - r[1].GetSingle()),
                });
            }
            _pages[pp.Name] = list;
        }
        foreach (var cp in root.GetProperty("covers").EnumerateObject())
        {
            var list = new List<(string, int, bool)>();
            foreach (var e in cp.Value.EnumerateArray()) list.Add((e[0].GetString(), e[1].GetInt32(), e[2].GetBoolean()));
            _covers[cp.Name] = list;
        }
        var glyphs = root.GetProperty("glyphs");
        _glyphs = GD.Load<Texture2D>(glyphs.GetProperty("file").GetString());
        _glyphCell = glyphs.GetProperty("cell").GetInt32();
        _glyphCols = glyphs.GetProperty("grid")[0].GetInt32();
        int gi = 0;
        foreach (var g in glyphs.GetProperty("order").EnumerateArray()) _glyph[g.GetString()] = gi++;
        var backdrops = root.GetProperty("backdrops");
        _stage = GD.Load<Texture2D>(backdrops.GetProperty("stage").GetString());
        _board = GD.Load<Texture2D>(backdrops.GetProperty("board").GetString());
    }

    /// <summary>Lets go of the pictures (before quitting).</summary>
    public static void Release()
    {
        Outlines.Release();
        _views.Clear();
        _pages.Clear();
        _covers.Clear();
        _glyph.Clear();
        _glyphs = _stage = _board = null;
        _loaded = false;
    }
}
