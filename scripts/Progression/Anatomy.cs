using System;
using System.Collections.Generic;
using System.Linq;

namespace LootFall.Progression;

/// <summary>How a cell is worn: on the skin, under clothing, as mail, as plate, over everything, as jewellery, held in a
/// hand, grown from the body, around it, or on a piece's own board.</summary>
public enum Layer : byte { Skin, Under, Mail, Armor, Over, Adorn, Hold, Graft, Beyond, Mod }

public enum Side : byte { None, Left, Right }

/// <summary>One slot on a board: a place a piece can fill. A body cell is one layer of one site ("the right index
/// finger's base", "the left forearm's plate"); a board cell is one of a piece's own slots ("the helm's crest").</summary>
public sealed class Cell
{
    public int Id, Board;
    public string Key, Name;
    /// <summary>The site it belongs to (where the screen puts its marker) and the region the site is in.</summary>
    public string Site, SiteName, Region;
    public Side Side;
    public Layer Layer;
    /// <summary>What filling it is worth, relative to one plate of armour (a piece's size is the sum of its cells').</summary>
    public double Worth;
    /// <summary>On the body's back (shown when the figure turns round).</summary>
    public bool Back;
}

/// <summary>
/// An equipment board: cells that pieces fill. The body is board 0 (level 1). Every other board belongs to the pieces
/// that bring it (all the helms bring the helm's board), which never can be worn together, so a board has at most one
/// host at a time; its pieces count only while that host is worn and opens their cells.
/// </summary>
public sealed class Board
{
    public int Id;
    /// <summary>1 for the body; otherwise the level of the pieces that bring it (2 for a piece worn on the body).</summary>
    public int Level;
    public string Key, Name;
    public readonly List<int> Cells = new();
}

/// <summary>One kind of piece, before Content turns it into a slot: where it goes, the cells it fills and keeps free,
/// the boards it brings and which of their cells it opens.</summary>
public sealed class PieceSpec
{
    public string Name, Family;
    public SlotKind Kind;
    public int Board, Level, Tier;
    /// <summary>Opened by the transcendence tree's slot perk rather than by depth.</summary>
    public bool Prestige;
    public Side Side;
    public string Region, Site;
    public int[] Takes, Keeps;
    public (int Board, int[] Opens)[] Brings;
    /// <summary>Its budget share, from the worth of the cells it fills and keeps free (normalised by Content).</summary>
    public double Size;
}

/// <summary>
/// The body and everything that can be worn on it, to the tips of the toes (docs/equipment.md). Cells are generated
/// site by site and layer by layer; pieces name the cells they fill (<see cref="PieceSpec.Takes"/>) and the ones they
/// keep free (<see cref="PieceSpec.Keeps"/>: a hand plate leaves no room for rings), so every rule about what can be
/// worn together is a shared cell. Pieces with slots of their own bring a board, and say which of its cells they open
/// (an open helm opens a visor fitting, a great helm has its own face). Validated as it's built.
/// </summary>
public sealed class Anatomy
{
    public readonly List<Cell> Cells = new();
    public readonly List<Board> Boards = new();
    public readonly List<PieceSpec> Pieces = new();
    readonly Dictionary<string, int> _cell = new(), _board = new();

    public const int Body = 0;
    public const int StartTier = 0, Classic = 1, Detail = 2, Exotic = 3;

    static readonly (Side Side, string P, string Name)[] Sides = { (Side.Right, "R", "Right"), (Side.Left, "L", "Left") };
    static readonly string[] Fingers = { "thumb", "index", "middle", "ring", "little" };
    static readonly string[] FingerNames = { "Thumb", "Index", "Middle", "Ring", "Little" };
    static readonly string[] Toes = { "bigtoe", "toe2", "toe3", "toe4", "littletoe" };
    static readonly string[] ToeNames = { "Big Toe", "Second Toe", "Middle Toe", "Fourth Toe", "Little Toe" };
    static readonly string[] Numerals = { "I", "II", "III", "IV", "V", "VI", "VII", "VIII" };

    public int CellId(string key) => _cell.TryGetValue(key, out int id) ? id : throw new ArgumentException($"No cell {key}");
    public int BoardId(string key) => _board.TryGetValue(key, out int id) ? id : throw new ArgumentException($"No board {key}");
    public bool HasBoard(string key) => _board.ContainsKey(key);

    static double LayerWorth(Layer l) => l switch
    {
        Layer.Skin => 0.35, Layer.Under => 0.5, Layer.Mail => 0.7, Layer.Armor => 1.0, Layer.Over => 0.6,
        Layer.Adorn => 0.45, Layer.Hold => 2.5, Layer.Graft => 1.0, Layer.Beyond => 1.2, _ => 0.35,
    };

    // ---------------------------------------------------------------- building blocks

    int NewBoard(string key, string name, int level)
    {
        var b = new Board { Id = Boards.Count, Key = key, Name = name, Level = level };
        Boards.Add(b);
        _board[key] = b.Id;
        return b.Id;
    }

    void AddCell(int board, string key, string name, string site, string siteName, string region, Side side, Layer layer, double worth, bool back)
    {
        if (_cell.ContainsKey(key)) throw new ArgumentException($"Cell {key} twice");
        var c = new Cell
        {
            Id = Cells.Count, Board = board, Key = key, Name = name, Site = site, SiteName = siteName, Region = region, Side = side,
            Layer = layer, Worth = worth, Back = back,
        };
        Cells.Add(c);
        Boards[board].Cells.Add(c.Id);
        _cell[key] = c.Id;
    }

    /// <summary>A body site and its cells (key, name, layer, worth; worth 0 takes the layer's).</summary>
    void Site(string key, string name, string region, Side side, bool back, params (string Key, string Name, Layer Layer, double Worth)[] cells)
    {
        foreach (var (k, n, l, w) in cells)
            AddCell(Body, $"{key}.{k}", n, key, name, region, side, l, w > 0 ? w : LayerWorth(l), back);
    }

    static (string, string, Layer, double) C(string key, string name, Layer layer, double worth = 0) => (key, name, layer, worth);

    /// <summary>A board brought by pieces, with its cells (key, name).</summary>
    void BoardOf(string key, string name, int level, params (string Key, string Name)[] cells)
    {
        int b = NewBoard(key, name, level);
        foreach (var (k, n) in cells) AddCell(b, $"{key}.{k}", n, key, name, null, Side.None, Layer.Mod, LayerWorth(Layer.Mod), false);
    }

    static (string, string) B(string key, string name) => (key, name);

    /// <summary>Numbered cells: key1..keyN named "Name I".."Name N".</summary>
    static IEnumerable<(string, string)> Many(string key, string name, int n) =>
        Enumerable.Range(1, n).Select(i => ($"{key}{i}", $"{name} {Numerals[i - 1]}"));

    int[] Ids(IEnumerable<string> keys) => keys.Select(CellId).ToArray();

    /// <summary>The cells of a board to open: all of them, or those named (keys within the board).</summary>
    (int, int[]) Opens(string board, params string[] cells)
    {
        int b = BoardId(board);
        return (b, cells.Length == 0 ? Boards[b].Cells.ToArray() : cells.Select(c => CellId($"{board}.{c}")).ToArray());
    }

    PieceSpec Piece(string name, SlotKind kind, int tier, string[] takes, string[] keeps = null, (int, int[])[] brings = null,
        string family = null, Side side = Side.None, bool prestige = false)
    {
        var t = Ids(takes);
        var k = Ids(keeps ?? Array.Empty<string>());
        int board = Cells[t[0]].Board;
        var p = new PieceSpec
        {
            Name = name, Kind = kind, Tier = tier, Takes = t, Keeps = k, Brings = brings ?? Array.Empty<(int, int[])>(),
            Board = board, Level = Boards[board].Level + 1, Side = side, Prestige = prestige,
            Family = family ?? FamilyOf(name),
        };
        var first = Cells[t[0]];
        p.Region = board == Body ? first.Region : null;
        p.Site = first.Site;
        p.Size = t.Sum(c => Cells[c].Worth) + 0.4 * k.Where(c => Cells[c].Board == board).Sum(c => Cells[c].Worth);
        Pieces.Add(p);
        return p;
    }

    /// <summary>The family a piece's picture comes from: its name without the side or a numeral, in snake case.</summary>
    public static string FamilyOf(string name)
    {
        var words = name.Split(' ').ToList();
        if (words[0] is "Left" or "Right") words.RemoveAt(0);
        if (words.Count > 1 && Numerals.Contains(words[^1])) words.RemoveAt(words.Count - 1);
        return string.Join("_", words).ToLowerInvariant().Replace("'", "").Replace("-", "_");
    }

    static string[] S(params string[] keys) => keys;

    // ---------------------------------------------------------------- the body

    public Anatomy()
    {
        NewBoard("body", "The body", 1);
        BuildBody();
        BuildBoards();
        BuildBodyPieces();
        BuildBoardPieces();
        Validate();
    }

    void BuildBody()
    {
        const Layer Sk = Layer.Skin, Un = Layer.Under, Ml = Layer.Mail, Ar = Layer.Armor, Ov = Layer.Over, Ad = Layer.Adorn,
            Ho = Layer.Hold, Gr = Layer.Graft, Be = Layer.Beyond;
        // Head.
        Site("scalp", "Scalp", "Head", Side.None, false, C("skin", "Scalp ink", Sk), C("under", "Cap", Un), C("armor", "Helm", Ar), C("over", "Hood or hat", Ov));
        Site("hair", "Hair", "Head", Side.None, false, C("pin1", "Hair pin", Ad, 0.3), C("pin2", "Second hair pin", Ad, 0.3), C("ribbon", "Ribbon", Ad, 0.3), C("beads", "Braid beads", Ad, 0.3));
        Site("brow", "Brow", "Head", Side.None, false, C("skin", "Brow ink", Sk), C("armor", "Brow guard", Ar, 0.6), C("adorn", "Circlet", Ad), C("jewel", "Forehead jewel", Ad, 0.4));
        foreach (var (side, p, n) in Sides)
        {
            Site($"{p}.eye", $"{n} eye", "Head", side, false, C("skin", "Kohl", Sk, 0.25), C("adorn", "Lens", Ad, 0.35));
            Site($"{p}.ear", $"{n} ear", "Head", side, false, C("lobe", "Lobe", Ad, 0.3), C("helix", "Helix", Ad, 0.3), C("cuff", "Cuff", Ad, 0.3), C("tip", "Ear tip", Ad, 0.3));
            Site($"{p}.cheek", $"{n} cheek", "Head", side, false, C("skin", "Cheek ink", Sk, 0.25), C("armor", "Cheek guard", Ar, 0.5));
        }
        Site("face", "Face", "Head", Side.None, false, C("armor", "Face plate", Ar, 0.8), C("over", "Mask", Ov, 0.6));
        Site("nose", "Nose", "Head", Side.None, false, C("armor", "Nasal guard", Ar, 0.4), C("septum", "Septum", Ad, 0.3), C("nostrilR", "Right nostril", Ad, 0.25), C("nostrilL", "Left nostril", Ad, 0.25));
        Site("mouth", "Mouth", "Head", Side.None, false, C("armor", "Mouth guard", Ar, 0.5), C("lip", "Lip", Ad, 0.3), C("teeth", "Teeth", Ad, 0.4), C("tongue", "Tongue", Ad, 0.25));
        Site("chin", "Chin", "Head", Side.None, false, C("skin", "Chin ink", Sk, 0.25), C("armor", "Chin guard", Ar, 0.6), C("adorn", "Beard rings", Ad, 0.3));
        Site("nape", "Nape", "Head", Side.None, true, C("skin", "Nape ink", Sk, 0.25), C("under", "Neck cloth", Un, 0.3), C("armor", "Nape guard", Ar, 0.5));
        Site("horns", "Horns", "Head", Side.None, false, C("graft", "Horns", Gr));
        Site("halo", "Above the head", "Head", Side.None, false, C("halo", "Halo", Be, 1.5));
        // Neck.
        Site("neck", "Neck", "Neck", Side.None, false, C("skin", "Neck ink", Sk), C("under", "Scarf", Un, 0.4), C("armor", "Gorget", Ar, 0.8),
            C("choker", "Choker", Ad, 0.4), C("chain", "Necklace", Ad, 0.5), C("pendant", "Pendant", Ad, 0.7), C("torc", "Torc", Ad, 0.5));
        // Torso, front and back.
        Site("chest", "Chest", "Torso", Side.None, false, C("skin", "Chest ink", Sk), C("under", "Shirt", Un), C("mail", "Mail", Ml), C("armor", "Breastplate", Ar, 1.6),
            C("over", "Tabard", Ov), C("pin1", "Brooch", Ad, 0.3), C("pin2", "Medal", Ad, 0.3), C("pin3", "Second medal", Ad, 0.3), C("pin4", "Third medal", Ad, 0.3));
        Site("heart", "Heart", "Torso", Side.None, false, C("graft", "Heart", Gr, 0.8));
        Site("belly", "Belly", "Torso", Side.None, false, C("skin", "Belly ink", Sk), C("under", "Shirt", Un, 0.4), C("mail", "Mail", Ml, 0.5), C("armor", "Plackart", Ar, 0.8),
            C("over", "Tabard", Ov, 0.4), C("navel", "Navel", Ad, 0.25));
        Site("back", "Back", "Back", Side.None, true, C("skin", "Back ink", Sk, 0.5), C("under", "Shirt", Un, 0.4), C("mail", "Mail", Ml, 0.6), C("armor", "Backplate", Ar, 1.2),
            C("over", "Cloak", Ov, 0.8), C("mount", "Carried on the back", Ho, 1.2), C("wings", "Wings", Gr, 1.6));
        Site("tail", "Tail", "Back", Side.None, true, C("graft", "Tail", Gr, 1.0));
        // Waist.
        Site("waist", "Waist", "Waist", Side.None, false, C("under", "Sash", Un, 0.4), C("armor", "Fauld", Ar, 0.6), C("belt", "Belt", Ad, 0.6));
        foreach (var (side, p, n) in Sides)
            Site($"{p}.hip", $"{n} hip", "Waist", side, false, C("armor", "Tasset", Ar, 0.5), C("hang", "Hanging sheath", Ho, 0.8));
        Site("groin", "Groin", "Waist", Side.None, false, C("armor", "Codpiece", Ar, 0.4), C("over", "Loincloth", Ov, 0.4));
        foreach (var (side, p, n) in Sides)
        {
            string arm = $"{n} arm", hand = $"{n} hand", leg = $"{n} leg", foot = $"{n} foot";
            // Arms.
            Site($"{p}.shoulder", $"{n} shoulder", arm, side, false, C("skin", "Shoulder ink", Sk, 0.3), C("armor", "Pauldron", Ar, 0.9), C("over", "Cloak", Ov, 0.3), C("perch", "Perch", Ad, 0.6));
            Site($"{p}.armpit", $"{n} armpit", arm, side, false, C("armor", "Besagew", Ar, 0.3));
            Site($"{p}.upperarm", $"{n} upper arm", arm, side, false, C("skin", "Upper arm ink", Sk, 0.3), C("under", "Sleeve", Un, 0.3), C("mail", "Mail sleeve", Ml, 0.4),
                C("armor", "Rerebrace", Ar, 0.6), C("adorn", "Armlet", Ad, 0.45));
            Site($"{p}.elbow", $"{n} elbow", arm, side, false, C("skin", "Elbow ink", Sk, 0.2), C("armor", "Couter", Ar, 0.4));
            Site($"{p}.forearm", $"{n} forearm", arm, side, false, C("skin", "Forearm ink", Sk, 0.3), C("under", "Wraps", Un, 0.3), C("armor", "Vambrace", Ar, 0.6), C("adorn", "Hidden blade", Ho, 0.5));
            Site($"{p}.wrist", $"{n} wrist", arm, side, false, C("skin", "Wrist ink", Sk, 0.2), C("armor", "Bracer", Ar, 0.4), C("bangle1", "Bracelet", Ad, 0.35), C("bangle2", "Second bracelet", Ad, 0.35));
            // Hands.
            Site($"{p}.hand", $"Back of the {n.ToLowerInvariant()} hand", hand, side, false, C("skin", "Hand ink", Sk, 0.25), C("under", "Glove", Un, 0.5), C("armor", "Hand plate", Ar, 0.6), C("adorn", "Hand chain", Ad, 0.35));
            Site($"{p}.palm", $"{n} palm", hand, side, false, C("skin", "Palm sigil", Sk, 0.25), C("adorn", "Palm crystal", Ad, 0.4));
            Site($"{p}.grip", $"{n} grip", hand, side, false, C("hold", side == Side.Right ? "Main hand" : "Off hand", Ho));
            for (int f = 0; f < 5; f++)
                Site($"{p}.{Fingers[f]}", $"{n} {FingerNames[f].ToLowerInvariant()}{(f == 0 ? "" : " finger")}", hand, side, false,
                    C("skin", "Finger ink", Sk, 0.1), C("band", "Ring", Ad, 0.4), C("knuckle", "Knuckle ring", Ad, 0.3), C("tip", "Fingertip", Ad, 0.2));
            // Legs.
            Site($"{p}.thigh", $"{n} thigh", leg, side, false, C("skin", "Thigh ink", Sk, 0.3), C("under", "Breeches", Un, 0.4), C("mail", "Chausses", Ml, 0.5),
                C("armor", "Cuisse", Ar, 0.8), C("adorn", "Garter", Ad, 0.4), C("over", "Skirt", Ov, 0.3));
            Site($"{p}.knee", $"{n} knee", leg, side, false, C("skin", "Knee ink", Sk, 0.2), C("under", "Knee wrap", Un, 0.2), C("armor", "Poleyn", Ar, 0.5));
            Site($"{p}.shin", $"{n} shin", leg, side, false, C("skin", "Calf ink", Sk, 0.3), C("under", "Leg wraps", Un, 0.3), C("mail", "Chausses", Ml, 0.4), C("armor", "Greave", Ar, 0.7));
            Site($"{p}.ankle", $"{n} ankle", leg, side, false, C("skin", "Ankle ink", Sk, 0.2), C("under", "Boot shaft", Un, 0.25), C("armor", "Ankle guard", Ar, 0.3),
                C("anklet1", "Anklet", Ad, 0.35), C("anklet2", "Second anklet", Ad, 0.35));
            // Feet.
            Site($"{p}.foot", $"Top of the {n.ToLowerInvariant()} foot", foot, side, false, C("skin", "Foot ink", Sk, 0.25), C("under", "Sock", Un, 0.3), C("armor", "Sabaton", Ar, 0.6), C("adorn", "Foot chain", Ad, 0.3));
            Site($"{p}.sole", $"{n} sole", foot, side, false, C("sole", "Sole", Un, 0.6));
            Site($"{p}.toetips", $"{n} toenails", foot, side, false, C("tips", "Toenails", Ad, 0.4));
            for (int t = 0; t < 5; t++)
                Site($"{p}.{Toes[t]}", $"{n} {ToeNames[t].ToLowerInvariant()}", foot, side, false, C("band", "Toe ring", Ad, 0.3));
        }
        // Around the body.
        Site("aura", "Aura", "Beyond", Side.None, false, C("aura", "Aura", Be, 1.5));
        Site("orbit", "Orbit", "Beyond", Side.None, false, Enumerable.Range(1, 6).Select(i => C($"stone{i}", $"Orbit {Numerals[i - 1]}", Be, 0.6)).ToArray());
        Site("shadow", "Shadow", "Beyond", Side.None, false, C("shadow", "Shadow", Be, 0.8));
        Site("companion", "At your side", "Beyond", Side.None, false, C("familiar", "Familiar", Be, 1.0), C("steed", "Steed", Be, 1.0));
        Site("heavens", "The heavens", "Beyond", Side.None, false,
            Enumerable.Range(1, 8).Select(i => C($"star{i}", $"Constellation {Numerals[i - 1]}", Be, 1.0)).Append(C("comet", "Comet", Be, 1.0)).Append(C("eclipse", "Eclipse", Be, 1.0)).ToArray());
    }

    // ---------------------------------------------------------------- the boards pieces bring

    void BuildBoards()
    {
        BoardOf("helm", "Helm", 2, B("crest", "Crest"), B("plume", "Plume holder"), B("visor", "Visor"), B("cheekL", "Left cheek fitting"), B("cheekR", "Right cheek fitting"),
            B("aventail", "Aventail"), B("gem1", "Brow socket I"), B("gem2", "Brow socket II"), B("gem3", "Brow socket III"), B("crowngem", "Crown socket"),
            B("lining", "Lining"), B("paint", "Paint"), B("enchant", "Enchantment"), B("engraving", "Engraving"));
        BoardOf("hood", "Hood", 2, B("clasp", "Clasp"), B("lining", "Lining"), B("embroidery", "Embroidery"), B("enchant", "Enchantment"));
        BoardOf("hat", "Hat", 2, B("band", "Hatband"), B("feather", "Feather"), B("pin", "Hat pin"), B("buckle", "Buckle"), B("star1", "Star charm I"), B("star2", "Star charm II"),
            B("star3", "Star charm III"), B("enchant", "Enchantment"));
        BoardOf("crown", "Crown", 2, Many("point", "Crown point", 5).Concat(Many("band", "Band setting", 3)).Append(B("enchant", "Enchantment")).ToArray());
        BoardOf("circlet", "Circlet", 2, B("centre", "Centre stone"), B("sideL", "Left stone"), B("sideR", "Right stone"), B("filigree", "Filigree"));
        BoardOf("lens", "Lenses", 2, B("tint", "Tint"), B("enchant", "Enchantment"), B("chain", "Chain"));
        BoardOf("mask", "Mask", 2, B("paint", "Paint"), B("gemL", "Left socket"), B("gemR", "Right socket"), B("enchant", "Enchantment"));
        BoardOf("horns", "Horns", 2, B("ringL1", "Left horn ring I"), B("ringL2", "Left horn ring II"), B("ringR1", "Right horn ring I"), B("ringR2", "Right horn ring II"),
            B("tipL", "Left horn tip"), B("tipR", "Right horn tip"), B("paint", "Paint"), B("engraving", "Engraving"));
        BoardOf("halo", "Halo", 2, Many("gem", "Halo socket", 6).Append(B("light", "Light")).ToArray());
        BoardOf("thirdeye", "Third eye", 2, B("iris", "Iris"), B("enchant", "Enchantment"));
        BoardOf("necklace", "Necklace", 2, B("charm1", "Charm I"), B("charm2", "Charm II"), B("charm3", "Charm III"), B("clasp", "Clasp"));
        BoardOf("amulet", "Amulet", 2, B("stone", "Stone"), B("runeL", "Left rune"), B("runeR", "Right rune"), B("enchant", "Enchantment"));
        BoardOf("torc", "Torc", 2, B("terminalL", "Left terminal"), B("terminalR", "Right terminal"));
        BoardOf("cuirass", "Breastplate", 2, B("heraldry", "Heraldry"), B("centre", "Centre socket"), B("sideL", "Left socket"), B("sideR", "Right socket"), B("runeL", "Left rune"),
            B("runeR", "Right rune"), B("lancerest", "Lance rest"), B("lining", "Lining"), B("enchant", "Enchantment"), B("engraving", "Engraving"));
        BoardOf("backplate", "Backplate", 2, B("paint", "Paint"), B("rune", "Rune"), B("enchant", "Enchantment"));
        BoardOf("cloak", "Cloak", 2, B("clasp", "Clasp"), B("lining", "Lining"), B("hem1", "Hem rune I"), B("hem2", "Hem rune II"), B("hem3", "Hem rune III"),
            B("embroidery", "Embroidery"), B("enchant", "Enchantment"));
        BoardOf("tabard", "Tabard", 2, B("heraldry", "Heraldry"), B("embroidery", "Embroidery"), B("enchant", "Enchantment"));
        BoardOf("wings", "Wings", 2, Many("feather", "Feather charm", 4).Append(B("bladeL", "Left wing blade")).Append(B("bladeR", "Right wing blade"))
            .Append(B("gemL", "Left wing socket")).Append(B("gemR", "Right wing socket")).Append(B("enchant", "Enchantment")).ToArray());
        BoardOf("tail", "Tail", 2, B("ring1", "Tail ring I"), B("ring2", "Tail ring II"), B("ring3", "Tail ring III"), B("tip", "Tail tip"), B("enchant", "Enchantment"));
        BoardOf("belt", "Belt", 2, new[] { B("buckle", "Buckle") }.Concat(Many("pouch", "Pouch", 4)).Concat(Many("loop", "Potion loop", 4)).Concat(Many("charm", "Belt charm", 3))
            .Append(B("enchant", "Enchantment")).ToArray());
        for (int i = 1; i <= 4; i++) BoardOf($"pouch{i}", $"Pouch {Numerals[i - 1]}", 3, B("a", "Keepsake"), B("b", "Second keepsake"));
        BoardOf("quiver", "Quiver", 2, B("fletching", "Fletching"), B("heads", "Arrowheads"), B("enchant", "Enchantment"));
        BoardOf("banner", "Banner", 2, B("sigil", "Sigil"), B("tasselL", "Left tassel"), B("tasselR", "Right tassel"), B("enchant", "Enchantment"));
        BoardOf("backpack", "Backpack", 2, Many("pocket", "Pocket", 4).Append(B("bedroll", "Bedroll")).Append(B("lantern", "Lantern hook")).ToArray());
        foreach (var (_, p, n) in Sides)
        {
            BoardOf($"{p}.pauldron", $"{n} pauldron", 2, B("spikeA", "Spike"), B("spikeB", "Second spike"), B("crest", "Crest"), B("gem", "Socket"), B("paint", "Paint"), B("enchant", "Enchantment"));
            BoardOf($"{p}.vambrace", $"{n} vambrace", 2, B("runeA", "Rune"), B("runeB", "Second rune"), B("gem", "Socket"), B("paint", "Paint"), B("enchant", "Enchantment"));
            BoardOf($"{p}.gauntlet", $"{n} gauntlet", 2, Many("spike", "Knuckle spike", 4).Append(B("cuff", "Cuff socket")).Append(B("palmrune", "Palm rune"))
                .Append(B("paint", "Paint")).Append(B("enchant", "Enchantment")).ToArray());
            BoardOf($"{p}.glove", $"{n} glove", 2, B("embroidery", "Embroidery"), B("enchant", "Enchantment"));
            BoardOf($"{p}.bangles", $"{n} bangles", 2, Many("charm", "Bangle charm", 3).ToArray());
            for (int f = 0; f < 5; f++) BoardOf($"{p}.{Fingers[f]}.setting", $"{n} {FingerNames[f].ToLowerInvariant()} ring", 2, B("stone", "Stone"));
            BoardOf($"{p}.boot", $"{n} boot", 2, B("spur", "Spur"), B("buckleA", "Buckle"), B("buckleB", "Second buckle"), B("toecap", "Toe cap"), B("laces", "Laces"),
                B("sole", "Sole enchantment"), B("paint", "Paint"));
            BoardOf($"{p}.sabaton", $"{n} sabaton", 2, B("spike", "Toe spike"), B("paint", "Paint"), B("enchant", "Enchantment"));
            BoardOf($"{p}.leg", $"{n} leg harness", 2, B("rune", "Rune"), B("kneespike", "Knee spike"), B("paint", "Paint"), B("enchant", "Enchantment"));
        }
        BoardOf("blade", "Main-hand weapon", 2, B("pommel", "Pommel"), B("guard", "Crossguard"), B("grip", "Grip"), B("rune1", "Blade rune I"), B("rune2", "Blade rune II"),
            B("rune3", "Blade rune III"), B("rune4", "Blade rune IV"), B("rune5", "Blade rune V"), B("gemA", "Blade socket"), B("gemB", "Second blade socket"),
            B("enchant", "Enchantment"), B("oil", "Blade oil"), B("tassel", "Tassel"));
        BoardOf("pommel", "Pommel", 3, B("stone", "Pommel stone"));
        BoardOf("crest", "Crest", 3, B("jewel", "Crest jewel"));
        BoardOf("offblade", "Off-hand weapon", 2, B("pommel", "Pommel"), B("guard", "Guard"), B("rune1", "Rune I"), B("rune2", "Rune II"), B("gem", "Socket"),
            B("enchant", "Enchantment"), B("oil", "Blade oil"));
        BoardOf("shield", "Shield", 2, B("boss", "Boss"), B("rim", "Rim"), B("heraldry", "Heraldry"), B("spikes", "Spikes"), B("gemA", "Socket"), B("gemB", "Second socket"),
            B("straps", "Straps"), B("enchant", "Enchantment"));
        BoardOf("focus", "Focus", 2, B("core", "Core"), B("rune1", "Rune I"), B("rune2", "Rune II"), B("rune3", "Rune III"), B("enchant", "Enchantment"));
        BoardOf("familiar", "Familiar", 2, B("collar", "Collar"), B("charm", "Charm"), B("barding", "Barding"), B("bell", "Bell"), B("enchant", "Enchantment"));
        BoardOf("perch", "Shoulder familiar", 2, B("jesses", "Jesses"), B("hood", "Falcon hood"), B("bell", "Bell"));
        BoardOf("steed", "Steed", 2, B("saddle", "Saddle"), B("barding", "Barding"), B("reins", "Reins"), B("shoe1", "Horseshoe I"), B("shoe2", "Horseshoe II"),
            B("shoe3", "Horseshoe III"), B("shoe4", "Horseshoe IV"), B("plume", "Plume"), B("bags", "Saddlebags"));
        BoardOf("saddlebags", "Saddlebags", 3, B("a", "Keepsake"), B("b", "Second keepsake"));
        BoardOf("aura", "Aura", 2, B("mote1", "Mote I"), B("mote2", "Mote II"), B("mote3", "Mote III"));
    }

    // ---------------------------------------------------------------- pieces on the body (level 2)

    void BuildBodyPieces()
    {
        const SlotKind W = SlotKind.Weapon, A = SlotKind.Armor, J = SlotKind.Jewelry, X = SlotKind.Accessory, K = SlotKind.Trinket,
            T = SlotKind.Tattoo, P = SlotKind.Companion, Cel = SlotKind.Celestial;
        string[] Ear(string s) => S($"{s}.ear.lobe", $"{s}.ear.helix", $"{s}.ear.cuff", $"{s}.ear.tip");
        string[] BothEars() => Ear("L").Concat(Ear("R")).ToArray();
        string[] Hair = S("hair.pin1", "hair.pin2", "hair.ribbon", "hair.beads");
        var faceOpen = S("L.eye.adorn", "R.eye.adorn", "nose.septum", "nose.nostrilL", "nose.nostrilR", "mouth.lip", "mouth.tongue", "mouth.teeth", "chin.adorn");

        // The head: helms, and what goes on a head without one.
        Piece("Great Helm", A, Classic, S("scalp.armor", "brow.armor", "face.armor", "L.cheek.armor", "R.cheek.armor", "nape.armor"),
            Hair.Concat(S("brow.adorn", "horns.graft", "face.over", "scalp.over")).Concat(BothEars()).Concat(faceOpen).ToArray(),
            new[] { Opens("helm", "crest", "plume", "aventail", "gem1", "gem2", "gem3", "crowngem", "lining", "paint", "enchant", "engraving") });
        Piece("Sallet", A, StartTier, S("scalp.armor", "brow.armor", "L.cheek.armor", "R.cheek.armor", "nape.armor"),
            Hair.Concat(S("brow.adorn", "horns.graft", "scalp.over")).Concat(BothEars()).ToArray(),
            new[] { Opens("helm", "crest", "plume", "visor", "cheekL", "cheekR", "gem1", "gem2", "lining", "paint", "enchant") });
        Piece("Nasal Helm", A, Detail, S("scalp.armor", "brow.armor", "nose.armor"), Hair.Concat(S("brow.adorn", "horns.graft", "nose.septum", "scalp.over")).ToArray(),
            new[] { Opens("helm", "crest", "aventail", "gem1", "lining", "paint", "enchant") });
        Piece("Horned Helm", A, Exotic, S("scalp.armor", "brow.armor", "face.armor", "L.cheek.armor", "R.cheek.armor", "nape.armor", "horns.graft"),
            Hair.Concat(S("brow.adorn", "face.over", "scalp.over")).Concat(BothEars()).Concat(faceOpen).ToArray(),
            new[] { Opens("helm", "gem1", "gem2", "gem3", "crowngem", "lining", "paint", "enchant", "engraving"), Opens("horns") });
        Piece("Coif", A, Detail, S("scalp.under", "nape.under"), Hair.Concat(BothEars()).ToArray());
        Piece("Arming Cap", X, Detail, S("scalp.under"), S("hair.pin1", "hair.pin2", "hair.ribbon"));
        Piece("Hood", X, Classic, S("scalp.over"), Hair.Concat(S("L.ear.helix", "L.ear.cuff", "L.ear.tip", "R.ear.helix", "R.ear.cuff", "R.ear.tip")).ToArray(),
            new[] { Opens("hood") });
        Piece("Wizard Hat", X, Detail, S("scalp.over"), S("hair.pin1", "hair.pin2", "hair.ribbon"), new[] { Opens("hat") });
        Piece("Crown", J, Exotic, S("scalp.over"), S("hair.pin1", "hair.pin2"), new[] { Opens("crown") });
        Piece("Grafted Horns", A, Exotic, S("horns.graft"), S("scalp.armor", "scalp.over"), new[] { Opens("horns") }, family: "horns");
        Piece("Circlet", J, Classic, S("brow.adorn"), brings: new[] { Opens("circlet", "centre", "filigree") });
        Piece("Diadem", J, Detail, S("brow.adorn"), S("hair.pin1"), new[] { Opens("circlet") });
        Piece("Forehead Jewel", J, Detail, S("brow.jewel"));
        Piece("Third Eye", K, Exotic, S("brow.jewel"), S("brow.skin"), new[] { Opens("thirdeye") });
        Piece("Brow Tattoo", T, Detail, S("brow.skin"));
        Piece("Scalp Tattoo", T, Detail, S("scalp.skin"));
        Piece("Hair Pin", J, Classic, S("hair.pin1"));
        Piece("Second Hair Pin", J, Detail, S("hair.pin2"), family: "hair_pin");
        Piece("Hair Ribbon", X, Detail, S("hair.ribbon"));
        Piece("Braid Beads", J, Detail, S("hair.beads"));
        Piece("Spectacles", X, Detail, S("L.eye.adorn", "R.eye.adorn"), brings: new[] { Opens("lens") });
        Piece("Goggles", X, Exotic, S("L.eye.adorn", "R.eye.adorn"), S("brow.adorn"), new[] { Opens("lens", "tint", "enchant") });
        Piece("Monocle", X, Classic, S("R.eye.adorn"), brings: new[] { Opens("lens") });
        Piece("Eyepatch", X, Detail, S("L.eye.adorn"));
        foreach (var (side, p, n) in Sides) Piece($"{n} Eye Kohl", T, Detail, S($"{p}.eye.skin"), side: side, family: "kohl");
        Piece("War Paint", T, Detail, S("brow.skin", "L.cheek.skin", "R.cheek.skin", "chin.skin"));
        foreach (var (side, p, n) in Sides) Piece($"{n} Cheek Tattoo", T, Detail, S($"{p}.cheek.skin"), side: side, family: "cheek_tattoo");
        Piece("Chin Tattoo", T, Detail, S("chin.skin"));
        Piece("Mask", X, Detail, S("face.over"), S("L.eye.adorn", "R.eye.adorn", "nose.septum", "nose.nostrilL", "nose.nostrilR", "mouth.lip", "mouth.tongue"), new[] { Opens("mask") });
        foreach (var (side, p, n) in Sides)
        {
            Piece($"{n} Earring", J, side == Side.Right ? Classic : Detail, S($"{p}.ear.lobe"), side: side, family: "earring");
            Piece($"{n} Helix Ring", J, Detail, S($"{p}.ear.helix"), side: side, family: "helix_ring");
            Piece($"{n} Ear Cuff", J, Detail, S($"{p}.ear.cuff"), S($"{p}.ear.helix"), side: side, family: "ear_cuff");
            Piece($"{n} Ear Chain", J, Detail, S($"{p}.ear.lobe", $"{p}.ear.helix"), side: side, family: "ear_chain");
            Piece($"{n} Elven Ear Tip", J, Exotic, S($"{p}.ear.tip"), side: side, family: "ear_tip");
        }
        Piece("Septum Ring", J, Detail, S("nose.septum"));
        foreach (var (side, p, n) in Sides) Piece($"{n} Nose Stud", J, Detail, S($"nose.nostril{p}"), side: side, family: "nose_stud");
        Piece("Nose Chain", J, Exotic, S("nose.nostrilR", "R.ear.lobe"));
        Piece("Lip Ring", J, Detail, S("mouth.lip"));
        Piece("Gold Teeth", J, Detail, S("mouth.teeth"));
        Piece("Fang Caps", W, Exotic, S("mouth.teeth"));
        Piece("Tongue Stud", J, Exotic, S("mouth.tongue"));
        Piece("Mouth Guard", A, Detail, S("mouth.armor"), S("mouth.lip", "mouth.tongue"));
        Piece("Bevor", A, Detail, S("chin.armor", "neck.armor"), S("chin.adorn", "neck.choker"));
        Piece("Beard Rings", J, Detail, S("chin.adorn"));
        Piece("Nape Tattoo", T, Detail, S("nape.skin"));
        Piece("Neck Cloth", X, Detail, S("nape.under"));
        Piece("Halo", Cel, Exotic, S("halo.halo"), brings: new[] { Opens("halo") }, prestige: true);
        Piece("Starcrown", Cel, Exotic, S("halo.halo"), brings: new[] { Opens("halo", "gem1", "gem2", "gem3", "light") }, prestige: true);

        // The neck.
        Piece("Neck Tattoo", T, Detail, S("neck.skin"));
        Piece("Scarf", X, Detail, S("neck.under"));
        Piece("Gorget", A, Classic, S("neck.armor"), S("neck.choker", "neck.torc"));
        Piece("Choker", J, Detail, S("neck.choker"));
        Piece("Necklace", J, Classic, S("neck.chain"), brings: new[] { Opens("necklace") });
        Piece("Amulet", J, Classic, S("neck.pendant"), brings: new[] { Opens("amulet") });
        Piece("Torc", J, Detail, S("neck.torc"), S("neck.choker"), new[] { Opens("torc") });
        Piece("Chain of Office", J, Exotic, S("neck.chain", "neck.pendant"), brings: new[] { Opens("necklace"), Opens("amulet", "stone", "enchant") });

        // The torso, in layers.
        Piece("Chest Tattoo", T, Detail, S("chest.skin"));
        Piece("Belly Tattoo", T, Detail, S("belly.skin"));
        Piece("Back Tattoo", T, Detail, S("back.skin"));
        Piece("Torso Tattoo", T, Exotic, S("chest.skin", "belly.skin", "back.skin"));
        Piece("Shirt", X, Classic, S("chest.under", "belly.under", "back.under"));
        Piece("Gambeson", A, Detail, S("chest.under", "belly.under", "back.under", "L.upperarm.under", "R.upperarm.under"));
        Piece("Mail Shirt", A, Detail, S("chest.mail", "belly.mail", "back.mail"));
        Piece("Hauberk", A, Exotic, S("chest.mail", "belly.mail", "back.mail", "L.upperarm.mail", "R.upperarm.mail", "L.thigh.mail", "R.thigh.mail"));
        Piece("Breastplate", A, StartTier, S("chest.armor"), brings: new[] { Opens("cuirass", "heraldry", "centre", "lancerest", "lining", "enchant", "engraving") });
        Piece("Plackart", A, Detail, S("belly.armor"));
        Piece("Backplate", A, Classic, S("back.armor"), brings: new[] { Opens("backplate") });
        Piece("Cuirass", A, Detail, S("chest.armor", "belly.armor", "back.armor"), brings: new[] { Opens("cuirass"), Opens("backplate") });
        Piece("Brigandine", A, Detail, S("chest.armor", "belly.armor", "back.armor"), brings: new[] { Opens("cuirass", "centre", "sideL", "sideR", "runeL", "runeR", "lining", "enchant") });
        Piece("Full Plate Harness", A, Exotic, S("chest.armor", "belly.armor", "back.armor", "L.shoulder.armor", "R.shoulder.armor", "L.armpit.armor", "R.armpit.armor",
            "L.upperarm.armor", "R.upperarm.armor", "waist.armor", "L.hip.armor", "R.hip.armor"),
            brings: new[] { Opens("cuirass"), Opens("backplate"), Opens("L.pauldron"), Opens("R.pauldron") });
        Piece("Tabard", X, Classic, S("chest.over", "belly.over"), brings: new[] { Opens("tabard") });
        Piece("Surcoat", X, Detail, S("chest.over", "belly.over", "groin.over"), brings: new[] { Opens("tabard") });
        Piece("Robe", X, Detail, S("chest.over", "belly.over", "back.over", "groin.over", "L.thigh.over", "R.thigh.over"), brings: new[] { Opens("tabard", "embroidery", "enchant") });
        Piece("Cloak", X, Classic, S("back.over", "L.shoulder.over", "R.shoulder.over"), brings: new[] { Opens("cloak") });
        Piece("Cape", X, Detail, S("back.over"), brings: new[] { Opens("cloak", "clasp", "lining", "embroidery", "enchant") });
        Piece("Mantle", X, Detail, S("L.shoulder.over", "R.shoulder.over"));
        Piece("Brooch", J, Classic, S("chest.pin1"));
        for (int i = 2; i <= 4; i++) Piece($"Medal {Numerals[i - 2]}", K, Detail, S($"chest.pin{i}"), family: "medal");
        Piece("Navel Ring", J, Detail, S("belly.navel"));
        Piece("Heartstone", K, Exotic, S("heart.graft"));
        Piece("Quiver", W, Detail, S("back.mount"), brings: new[] { Opens("quiver") });
        Piece("Back Scabbard", W, Detail, S("back.mount"));
        Piece("War Banner", X, Exotic, S("back.mount"), brings: new[] { Opens("banner") });
        Piece("Backpack", K, Detail, S("back.mount"), brings: new[] { Opens("backpack") });
        Piece("Wings", Cel, Exotic, S("back.wings"), S("back.over", "back.mount"), new[] { Opens("wings") });
        Piece("Tail", X, Exotic, S("tail.graft"), brings: new[] { Opens("tail") });

        // The waist.
        Piece("Sash", X, Detail, S("waist.under"));
        Piece("Belt", X, Classic, S("waist.belt"), brings: new[] { Opens("belt") });
        Piece("Girdle", A, Exotic, S("waist.belt", "waist.armor"), brings: new[] { Opens("belt", "buckle", "pouch1", "pouch2", "loop1", "loop2", "enchant") });
        Piece("Fauld", A, Detail, S("waist.armor"));
        Piece("Faulds and Tassets", A, Detail, S("waist.armor", "L.hip.armor", "R.hip.armor"));
        foreach (var (side, p, n) in Sides)
        {
            Piece($"{n} Tasset", A, Classic, S($"{p}.hip.armor"), side: side, family: "tasset");
            Piece($"{n} Hip Scabbard", W, Detail, S($"{p}.hip.hang"), side: side, family: "scabbard");
        }
        Piece("Codpiece", A, Exotic, S("groin.armor"));
        Piece("Loincloth", X, Detail, S("groin.over"));

        // Both arms and hands, both legs and feet.
        foreach (var (side, p, n) in Sides)
        {
            PieceSpec Sided(string name, SlotKind kind, int tier, string[] takes, string[] keeps = null, (int, int[])[] brings = null, string family = null) =>
                Piece($"{n} {name}", kind, tier, takes.Select(k => $"{p}.{k}").ToArray(), keeps?.Select(k => $"{p}.{k}").ToArray(), brings, family ?? FamilyOf(name), side);
            string[] fingerCells(string cell) => Fingers.Select(f => $"{f}.{cell}").ToArray();

            // Arms.
            Sided("Shoulder Tattoo", T, Detail, S("shoulder.skin"));
            Sided("Upper Arm Tattoo", T, Detail, S("upperarm.skin"));
            Sided("Elbow Tattoo", T, Detail, S("elbow.skin"));
            Sided("Forearm Tattoo", T, Detail, S("forearm.skin"));
            Sided("Wrist Tattoo", T, Detail, S("wrist.skin"));
            Sided("Sleeve Tattoo", T, Exotic, S("shoulder.skin", "upperarm.skin", "elbow.skin", "forearm.skin", "wrist.skin"));
            Sided("Spaulder", A, Classic, S("shoulder.armor"), brings: new[] { Opens($"{p}.pauldron", "spikeA", "gem", "paint", "enchant") });
            Sided("Pauldron", A, Classic, S("shoulder.armor", "armpit.armor"), brings: new[] { Opens($"{p}.pauldron") });
            Sided("Besagew", A, Detail, S("armpit.armor"));
            Sided("Mail Sleeve", A, Detail, S("upperarm.mail"));
            Sided("Rerebrace", A, Classic, S("upperarm.armor"));
            Sided("Couter", A, Classic, S("elbow.armor"));
            Sided("Vambrace", A, Classic, S("forearm.armor"), brings: new[] { Opens($"{p}.vambrace", "runeA", "gem", "paint", "enchant") });
            Sided("Arm Harness", A, Detail, S("upperarm.armor", "elbow.armor", "forearm.armor"), brings: new[] { Opens($"{p}.vambrace") });
            Sided("Full Arm Harness", A, Exotic, S("shoulder.armor", "armpit.armor", "upperarm.armor", "elbow.armor", "forearm.armor"),
                brings: new[] { Opens($"{p}.pauldron"), Opens($"{p}.vambrace") });
            Sided("Arm Wraps", X, Detail, S("forearm.under"));
            Sided("Armlet", J, Classic, S("upperarm.adorn"));
            Sided("Wrist Blade", W, Exotic, S("forearm.adorn"));
            Sided("Bracer", A, Classic, S("wrist.armor"), S("wrist.bangle1", "wrist.bangle2"));
            Sided("Bracelet", J, Classic, S("wrist.bangle1"));
            Sided("Second Bracelet", J, Detail, S("wrist.bangle2"), family: "bracelet");
            Sided("Bangle Stack", J, Detail, S("wrist.bangle1", "wrist.bangle2"), brings: new[] { Opens($"{p}.bangles") });
            if (side == Side.Right) Sided("Shoulder Familiar", P, Detail, S("shoulder.perch"), brings: new[] { Opens("perch") });
            else Sided("Shoulder Lantern", K, Detail, S("shoulder.perch"));

            // Hands.
            Sided("Hand Tattoo", T, Detail, S("hand.skin"));
            Sided("Palm Sigil", T, Detail, S("palm.skin"));
            Sided("Knuckle Letters", T, Exotic, S("index.skin", "middle.skin", "ring.skin", "little.skin"));
            Sided("Ring Tattoo", T, Detail, S("ring.skin"));
            Sided("Thumb Tattoo", T, Detail, S("thumb.skin"));
            Sided("Glove", X, Classic, S("hand.under"), fingerCells("tip"), new[] { Opens($"{p}.glove") });
            Sided("Fingerless Glove", X, Detail, S("hand.under"), brings: new[] { Opens($"{p}.glove") });
            Sided("Hand Plate", A, Detail, S("hand.armor"), fingerCells("band").Concat(fingerCells("knuckle")).ToArray());
            Sided("Gauntlet", A, Classic, S("hand.under", "hand.armor", "wrist.armor"),
                fingerCells("band").Concat(fingerCells("knuckle")).Concat(fingerCells("tip")).Concat(S("wrist.bangle1", "wrist.bangle2", "palm.adorn", "hand.adorn")).ToArray(),
                new[] { Opens($"{p}.gauntlet") });
            Sided("Claw Gauntlet", W, Exotic, S("hand.under", "hand.armor").Concat(fingerCells("tip")).ToArray(),
                fingerCells("band").Concat(fingerCells("knuckle")).Concat(S("palm.adorn", "hand.adorn")).ToArray(),
                new[] { Opens($"{p}.gauntlet", "cuff", "palmrune", "paint", "enchant") });
            for (int f = 0; f < 5; f++)
            {
                string fk = Fingers[f], fn = FingerNames[f];
                Sided($"{fn} Ring", J, f == 1 ? Classic : Detail, S($"{fk}.band"), brings: new[] { Opens($"{p}.{fk}.setting") }, family: "ring");
                Sided($"{fn} Knuckle Ring", J, Detail, S($"{fk}.knuckle"), family: "knuckle_ring");
            }
            Sided("Talons", W, Exotic, fingerCells("tip"));
            Sided("Nail Lacquer", T, Detail, fingerCells("tip"));
            Sided("Thimble", X, Detail, S("middle.tip"));
            Sided("Knuckleduster", W, Detail, S("index.band", "middle.band", "ring.band", "little.band"));
            Sided("Hand Chain", J, Detail, S("hand.adorn", "middle.band", "wrist.bangle1"));
            Sided("Palm Crystal", K, Exotic, S("palm.adorn"));

            // Legs.
            Sided("Thigh Tattoo", T, Detail, S("thigh.skin"));
            Sided("Knee Tattoo", T, Detail, S("knee.skin"));
            Sided("Calf Tattoo", T, Detail, S("shin.skin"));
            Sided("Ankle Tattoo", T, Detail, S("ankle.skin"));
            Sided("Leg Sleeve Tattoo", T, Exotic, S("thigh.skin", "knee.skin", "shin.skin", "ankle.skin"));
            Sided("Breeches", X, Detail, S("thigh.under"));
            Sided("Knee Wrap", X, Detail, S("knee.under"));
            Sided("Leg Wraps", X, Detail, S("shin.under"));
            Sided("Chausses", A, Detail, S("thigh.mail", "shin.mail"));
            Sided("Cuisse", A, Classic, S("thigh.armor"));
            Sided("Poleyn", A, Classic, S("knee.armor"));
            Sided("Greave", A, Classic, S("shin.armor"), brings: new[] { Opens($"{p}.leg", "rune", "paint", "enchant") });
            Sided("Leg Harness", A, Detail, S("thigh.armor", "knee.armor", "shin.armor"), brings: new[] { Opens($"{p}.leg") });
            Sided("Ankle Guard", A, Detail, S("ankle.armor"), S("ankle.anklet1", "ankle.anklet2"));
            Sided("Garter", J, Detail, S("thigh.adorn"));
            Sided("Thigh Sheath", W, Detail, S("thigh.adorn"));
            Sided("Anklet", J, Classic, S("ankle.anklet1"));
            Sided("Second Anklet", J, Detail, S("ankle.anklet2"), family: "anklet");

            // Feet.
            string[] toes = Toes.Select(t => $"{t}.band").Append("toetips.tips").Append("foot.adorn").ToArray();
            Sided("Foot Tattoo", T, Detail, S("foot.skin"));
            Sided("Sock", X, Detail, S("foot.under"));
            Sided("Sandal", X, Classic, S("sole.sole"));
            Sided("Boot", A, side == Side.Right ? StartTier : Classic, S("sole.sole", "foot.under", "ankle.under"), toes.Concat(S("ankle.anklet1", "ankle.anklet2")).ToArray(),
                new[] { Opens($"{p}.boot", "spur", "buckleA", "laces", "sole", "paint") });
            Sided("Sabaton", A, Detail, S("foot.armor"), toes, new[] { Opens($"{p}.sabaton") });
            Sided("Armoured Boot", A, Exotic, S("sole.sole", "foot.under", "foot.armor", "ankle.under", "ankle.armor"), toes.Concat(S("ankle.anklet1", "ankle.anklet2")).ToArray(),
                new[] { Opens($"{p}.boot"), Opens($"{p}.sabaton") });
            for (int t = 0; t < 5; t++) Sided($"{ToeNames[t]} Ring", J, t == 0 ? Classic : Detail, S($"{Toes[t]}.band"), family: "toe_ring");
            Sided("Toe Talons", W, Exotic, S("toetips.tips"));
            Sided("Toenail Paint", T, Detail, S("toetips.tips"));
            Sided("Foot Chain", J, Detail, S("foot.adorn", "toe2.band"));
        }
        Piece("Trousers", X, Classic, S("L.thigh.under", "R.thigh.under", "L.knee.under", "R.knee.under", "L.shin.under", "R.shin.under"));
        Piece("Mail Leggings", A, Exotic, S("L.thigh.mail", "R.thigh.mail", "L.shin.mail", "R.shin.mail"));

        // In the hands.
        Piece("Main Hand", W, StartTier, S("R.grip.hold"), brings: new[] { Opens("blade", "pommel", "guard", "grip", "rune1", "rune2", "rune3", "gemA", "gemB", "enchant", "oil", "tassel") }, family: "main_hand");
        Piece("Great Weapon", W, Detail, S("R.grip.hold", "L.grip.hold"), brings: new[] { Opens("blade") });
        Piece("Off-Hand Blade", W, Classic, S("L.grip.hold"), brings: new[] { Opens("offblade") }, family: "off_hand");
        Piece("Shield", A, Classic, S("L.grip.hold"), brings: new[] { Opens("shield") });
        Piece("Focus Orb", J, Detail, S("L.grip.hold"), brings: new[] { Opens("focus") });
        Piece("Everburning Torch", K, Exotic, S("L.grip.hold"), family: "torch");

        // Around the body.
        Piece("Aura", Cel, Exotic, S("aura.aura"), brings: new[] { Opens("aura") }, prestige: true);
        for (int i = 1; i <= 6; i++) Piece($"Orbiting Stone {Numerals[i - 1]}", J, Exotic, S($"orbit.stone{i}"), family: "ioun_stone");
        Piece("Shadow Cloak", X, Exotic, S("shadow.shadow"));
        Piece("Familiar", P, Detail, S("companion.familiar"), brings: new[] { Opens("familiar") });
        Piece("Steed", P, Exotic, S("companion.steed"), brings: new[] { Opens("steed") });
        string[] signs = { "Ram", "Bull", "Twins", "Crab", "Lion", "Maiden", "Scales", "Archer" };
        for (int i = 0; i < 8; i++) Piece($"Constellation of the {signs[i]}", Cel, Exotic, S($"heavens.star{i + 1}"), family: "constellation", prestige: true);
        Piece("Comet", Cel, Exotic, S("heavens.comet"), prestige: true);
        Piece("Eclipse", Cel, Exotic, S("heavens.eclipse"), prestige: true);
    }

    // ---------------------------------------------------------------- pieces on the boards (level 3 and 4)

    void BuildBoardPieces()
    {
        const SlotKind W = SlotKind.Weapon, A = SlotKind.Armor, J = SlotKind.Jewelry, X = SlotKind.Accessory, K = SlotKind.Trinket,
            T = SlotKind.Tattoo, Cel = SlotKind.Celestial;
        // A piece on a board: its cells by key within the board.
        PieceSpec Mod(string board, string name, SlotKind kind, int tier, string[] takes, string[] keeps = null, (int, int[])[] brings = null, string family = null,
            Side side = Side.None, bool prestige = false) =>
            Piece(name, kind, tier, takes.Select(k => $"{board}.{k}").ToArray(), keeps?.Select(k => $"{board}.{k}").ToArray(), brings, family, side, prestige);

        // Helms.
        Mod("helm", "Helm Crest", A, Detail, S("crest"), brings: new[] { Opens("crest") }, family: "crest");
        Mod("helm", "Helm Plume", X, Detail, S("plume"), family: "plume");
        Mod("helm", "Dragon Crest", A, Exotic, S("crest", "plume"), brings: new[] { Opens("crest") });
        Mod("helm", "Visor", A, Detail, S("visor"));
        Mod("helm", "Left Cheek Fitting", A, Detail, S("cheekL"), family: "cheek_fitting");
        Mod("helm", "Right Cheek Fitting", A, Detail, S("cheekR"), family: "cheek_fitting");
        Mod("helm", "Aventail", A, Detail, S("aventail"));
        for (int i = 1; i <= 3; i++) Mod("helm", $"Brow Gem {Numerals[i - 1]}", J, Detail, S($"gem{i}"), family: "gem");
        Mod("helm", "Crown Gem", J, Exotic, S("crowngem"), family: "gem");
        Mod("helm", "Helm Lining", X, Detail, S("lining"), family: "lining");
        Mod("helm", "Helm Paint", X, Detail, S("paint"), family: "paint");
        Mod("helm", "Helm Enchantment", T, Detail, S("enchant"), family: "enchantment");
        Mod("helm", "Helm Engraving", T, Exotic, S("engraving"), family: "engraving");
        Mod("crest", "Crest Jewel", J, Exotic, S("jewel"), family: "gem");
        // Hoods, hats, crowns, circlets, lenses, masks.
        Mod("hood", "Hood Clasp", J, Detail, S("clasp"), family: "clasp");
        Mod("hood", "Hood Lining", X, Detail, S("lining"), family: "lining");
        Mod("hood", "Hood Embroidery", X, Detail, S("embroidery"), family: "embroidery");
        Mod("hood", "Hood Enchantment", T, Detail, S("enchant"), family: "enchantment");
        Mod("hat", "Hatband", X, Detail, S("band"));
        Mod("hat", "Hat Feather", X, Detail, S("feather"), family: "feather");
        Mod("hat", "Hat Pin", J, Detail, S("pin"), family: "hair_pin");
        Mod("hat", "Hat Buckle", J, Detail, S("buckle"), family: "buckle");
        for (int i = 1; i <= 3; i++) Mod("hat", $"Star Charm {Numerals[i - 1]}", K, Exotic, S($"star{i}"), family: "star_charm");
        Mod("hat", "Hat Enchantment", T, Detail, S("enchant"), family: "enchantment");
        for (int i = 1; i <= 5; i++) Mod("crown", $"Crown Point Gem {Numerals[i - 1]}", J, Exotic, S($"point{i}"), family: "gem");
        for (int i = 1; i <= 3; i++) Mod("crown", $"Crown Band Gem {Numerals[i - 1]}", J, Exotic, S($"band{i}"), family: "gem");
        Mod("crown", "Crown Enchantment", T, Exotic, S("enchant"), family: "enchantment");
        Mod("circlet", "Circlet Stone", J, Detail, S("centre"), family: "gem");
        Mod("circlet", "Left Circlet Stone", J, Detail, S("sideL"), family: "gem");
        Mod("circlet", "Right Circlet Stone", J, Detail, S("sideR"), family: "gem");
        Mod("circlet", "Circlet Filigree", T, Detail, S("filigree"), family: "engraving");
        Mod("lens", "Lens Tint", X, Detail, S("tint"));
        Mod("lens", "Lens Enchantment", T, Detail, S("enchant"), family: "enchantment");
        Mod("lens", "Lens Chain", J, Detail, S("chain"), family: "chain");
        Mod("mask", "Mask Paint", X, Detail, S("paint"), family: "paint");
        Mod("mask", "Left Mask Gem", J, Detail, S("gemL"), family: "gem");
        Mod("mask", "Right Mask Gem", J, Detail, S("gemR"), family: "gem");
        Mod("mask", "Mask Enchantment", T, Detail, S("enchant"), family: "enchantment");
        // Horns, halo, third eye.
        foreach (var (k, n) in new[] { ("ringL1", "Left Horn Ring I"), ("ringL2", "Left Horn Ring II"), ("ringR1", "Right Horn Ring I"), ("ringR2", "Right Horn Ring II") })
            Mod("horns", n, J, Exotic, S(k), family: "horn_ring");
        Mod("horns", "Left Horn Tip", W, Exotic, S("tipL"), family: "horn_tip");
        Mod("horns", "Right Horn Tip", W, Exotic, S("tipR"), family: "horn_tip");
        Mod("horns", "Horn Paint", X, Exotic, S("paint"), family: "paint");
        Mod("horns", "Horn Engraving", T, Exotic, S("engraving"), family: "engraving");
        for (int i = 1; i <= 6; i++) Mod("halo", $"Halo Gem {Numerals[i - 1]}", Cel, Exotic, S($"gem{i}"), family: "gem", prestige: true);
        Mod("halo", "Halo Light", Cel, Exotic, S("light"), prestige: true);
        Mod("thirdeye", "Iris Gem", J, Exotic, S("iris"), family: "gem");
        Mod("thirdeye", "Third Eye Enchantment", T, Exotic, S("enchant"), family: "enchantment");
        // The neck's jewellery.
        for (int i = 1; i <= 3; i++) Mod("necklace", $"Necklace Charm {Numerals[i - 1]}", K, Detail, S($"charm{i}"), family: "charm");
        Mod("necklace", "Necklace Clasp", J, Detail, S("clasp"), family: "clasp");
        Mod("amulet", "Amulet Stone", J, Detail, S("stone"), family: "gem");
        Mod("amulet", "Left Amulet Rune", T, Detail, S("runeL"), family: "rune");
        Mod("amulet", "Right Amulet Rune", T, Detail, S("runeR"), family: "rune");
        Mod("amulet", "Amulet Enchantment", T, Detail, S("enchant"), family: "enchantment");
        Mod("torc", "Left Torc Terminal", J, Detail, S("terminalL"), family: "torc_terminal");
        Mod("torc", "Right Torc Terminal", J, Detail, S("terminalR"), family: "torc_terminal");
        // Body armour and what's over it.
        Mod("cuirass", "Heraldry", X, Detail, S("heraldry"));
        Mod("cuirass", "Breastplate Gem", J, Detail, S("centre"), family: "gem");
        Mod("cuirass", "Left Breastplate Gem", J, Detail, S("sideL"), family: "gem");
        Mod("cuirass", "Right Breastplate Gem", J, Detail, S("sideR"), family: "gem");
        Mod("cuirass", "Left Breastplate Rune", T, Detail, S("runeL"), family: "rune");
        Mod("cuirass", "Right Breastplate Rune", T, Detail, S("runeR"), family: "rune");
        Mod("cuirass", "Lance Rest", W, Detail, S("lancerest"));
        Mod("cuirass", "Breastplate Lining", X, Detail, S("lining"), family: "lining");
        Mod("cuirass", "Breastplate Enchantment", T, Detail, S("enchant"), family: "enchantment");
        Mod("cuirass", "Breastplate Engraving", T, Exotic, S("engraving"), family: "engraving");
        Mod("backplate", "Backplate Paint", X, Detail, S("paint"), family: "paint");
        Mod("backplate", "Backplate Rune", T, Detail, S("rune"), family: "rune");
        Mod("backplate", "Backplate Enchantment", T, Detail, S("enchant"), family: "enchantment");
        Mod("cloak", "Cloak Clasp", J, Detail, S("clasp"), family: "clasp");
        Mod("cloak", "Cloak Lining", X, Detail, S("lining"), family: "lining");
        for (int i = 1; i <= 3; i++) Mod("cloak", $"Hem Rune {Numerals[i - 1]}", T, Detail, S($"hem{i}"), family: "rune");
        Mod("cloak", "Cloak Embroidery", X, Detail, S("embroidery"), family: "embroidery");
        Mod("cloak", "Cloak Enchantment", T, Detail, S("enchant"), family: "enchantment");
        Mod("tabard", "Tabard Heraldry", X, Detail, S("heraldry"), family: "heraldry");
        Mod("tabard", "Tabard Embroidery", X, Detail, S("embroidery"), family: "embroidery");
        Mod("tabard", "Tabard Enchantment", T, Detail, S("enchant"), family: "enchantment");
        // Wings and tail.
        for (int i = 1; i <= 4; i++) Mod("wings", $"Feather Charm {Numerals[i - 1]}", K, Exotic, S($"feather{i}"), family: "feather");
        Mod("wings", "Left Wing Blade", W, Exotic, S("bladeL"), family: "wing_blade");
        Mod("wings", "Right Wing Blade", W, Exotic, S("bladeR"), family: "wing_blade");
        Mod("wings", "Left Wing Gem", J, Exotic, S("gemL"), family: "gem");
        Mod("wings", "Right Wing Gem", J, Exotic, S("gemR"), family: "gem");
        Mod("wings", "Wing Enchantment", T, Exotic, S("enchant"), family: "enchantment");
        for (int i = 1; i <= 3; i++) Mod("tail", $"Tail Ring {Numerals[i - 1]}", J, Exotic, S($"ring{i}"), family: "tail_ring");
        Mod("tail", "Tail Spike", W, Exotic, S("tip"));
        Mod("tail", "Tail Tuft", X, Exotic, S("tip"));
        Mod("tail", "Tail Enchantment", T, Exotic, S("enchant"), family: "enchantment");
        // The belt and what hangs from it, down to what's in the pouches.
        Mod("belt", "Belt Buckle", J, Detail, S("buckle"), family: "buckle");
        for (int i = 1; i <= 4; i++)
        {
            Mod("belt", $"Pouch {Numerals[i - 1]}", K, Detail, S($"pouch{i}"), brings: new[] { Opens($"pouch{i}") }, family: "pouch");
            Mod($"pouch{i}", $"Pouch {Numerals[i - 1]} Keepsake", K, Exotic, S("a"), family: "keepsake");
            Mod($"pouch{i}", $"Pouch {Numerals[i - 1]} Second Keepsake", K, Exotic, S("b"), family: "keepsake");
        }
        for (int i = 1; i <= 4; i++) Mod("belt", $"Potion {Numerals[i - 1]}", K, Detail, S($"loop{i}"), family: "potion");
        for (int i = 1; i <= 3; i++) Mod("belt", $"Belt Charm {Numerals[i - 1]}", K, Detail, S($"charm{i}"), family: "charm");
        Mod("belt", "Belt Enchantment", T, Detail, S("enchant"), family: "enchantment");
        // Things on the back.
        Mod("quiver", "Fletching", X, Detail, S("fletching"));
        Mod("quiver", "Arrowheads", W, Detail, S("heads"));
        Mod("quiver", "Quiver Enchantment", T, Detail, S("enchant"), family: "enchantment");
        Mod("banner", "Banner Sigil", X, Exotic, S("sigil"), family: "heraldry");
        Mod("banner", "Left Banner Tassel", X, Exotic, S("tasselL"), family: "tassel");
        Mod("banner", "Right Banner Tassel", X, Exotic, S("tasselR"), family: "tassel");
        Mod("banner", "Banner Enchantment", T, Exotic, S("enchant"), family: "enchantment");
        for (int i = 1; i <= 4; i++) Mod("backpack", $"Pocket {Numerals[i - 1]}", K, Detail, S($"pocket{i}"), family: "pouch");
        Mod("backpack", "Bedroll", X, Detail, S("bedroll"));
        Mod("backpack", "Hanging Lantern", K, Detail, S("lantern"), family: "lantern");
        // Each side's armour and jewellery.
        foreach (var (side, p, n) in Sides)
        {
            PieceSpec SidedMod(string board, string name, SlotKind kind, int tier, string[] takes, string family) =>
                Mod($"{p}.{board}", $"{n} {name}", kind, tier, takes, family: family, side: side);
            SidedMod("pauldron", "Pauldron Spike", W, Detail, S("spikeA"), "spike");
            SidedMod("pauldron", "Second Pauldron Spike", W, Detail, S("spikeB"), "spike");
            SidedMod("pauldron", "Pauldron Crest", A, Detail, S("crest"), "crest");
            SidedMod("pauldron", "Pauldron Gem", J, Detail, S("gem"), "gem");
            SidedMod("pauldron", "Pauldron Paint", X, Detail, S("paint"), "paint");
            SidedMod("pauldron", "Pauldron Enchantment", T, Detail, S("enchant"), "enchantment");
            SidedMod("vambrace", "Vambrace Rune", T, Detail, S("runeA"), "rune");
            SidedMod("vambrace", "Second Vambrace Rune", T, Detail, S("runeB"), "rune");
            SidedMod("vambrace", "Vambrace Gem", J, Detail, S("gem"), "gem");
            SidedMod("vambrace", "Vambrace Paint", X, Detail, S("paint"), "paint");
            SidedMod("vambrace", "Vambrace Enchantment", T, Detail, S("enchant"), "enchantment");
            for (int i = 1; i <= 4; i++) SidedMod("gauntlet", $"Knuckle Spike {Numerals[i - 1]}", W, Detail, S($"spike{i}"), "spike");
            SidedMod("gauntlet", "Cuff Gem", J, Detail, S("cuff"), "gem");
            SidedMod("gauntlet", "Palm Rune", T, Detail, S("palmrune"), "rune");
            SidedMod("gauntlet", "Gauntlet Paint", X, Detail, S("paint"), "paint");
            SidedMod("gauntlet", "Gauntlet Enchantment", T, Detail, S("enchant"), "enchantment");
            SidedMod("glove", "Glove Embroidery", X, Detail, S("embroidery"), "embroidery");
            SidedMod("glove", "Glove Enchantment", T, Detail, S("enchant"), "enchantment");
            for (int i = 1; i <= 3; i++) SidedMod("bangles", $"Bangle Charm {Numerals[i - 1]}", K, Detail, S($"charm{i}"), "charm");
            for (int f = 0; f < 5; f++) SidedMod($"{Fingers[f]}.setting", $"{FingerNames[f]} Ring Stone", J, Detail, S("stone"), "gem");
            SidedMod("boot", "Spur", W, Detail, S("spur"), "spur");
            SidedMod("boot", "Boot Buckle", J, Detail, S("buckleA"), "buckle");
            SidedMod("boot", "Second Boot Buckle", J, Detail, S("buckleB"), "buckle");
            SidedMod("boot", "Toe Cap", A, Detail, S("toecap"), "toe_cap");
            SidedMod("boot", "Bootlaces", X, Detail, S("laces"), "laces");
            SidedMod("boot", "Sole Enchantment", T, Detail, S("sole"), "enchantment");
            SidedMod("boot", "Boot Paint", X, Detail, S("paint"), "paint");
            SidedMod("sabaton", "Toe Spike", W, Detail, S("spike"), "spike");
            SidedMod("sabaton", "Sabaton Paint", X, Detail, S("paint"), "paint");
            SidedMod("sabaton", "Sabaton Enchantment", T, Detail, S("enchant"), "enchantment");
            SidedMod("leg", "Greave Rune", T, Detail, S("rune"), "rune");
            SidedMod("leg", "Knee Spike", W, Detail, S("kneespike"), "spike");
            SidedMod("leg", "Greave Paint", X, Detail, S("paint"), "paint");
            SidedMod("leg", "Greave Enchantment", T, Detail, S("enchant"), "enchantment");
        }
        // Weapons and what's held.
        Mod("blade", "Pommel", W, Detail, S("pommel"), brings: new[] { Opens("pommel") });
        Mod("pommel", "Pommel Stone", J, Exotic, S("stone"), family: "gem");
        Mod("blade", "Crossguard", W, Detail, S("guard"));
        Mod("blade", "Grip Wrap", X, Detail, S("grip"));
        for (int i = 1; i <= 5; i++) Mod("blade", $"Blade Rune {Numerals[i - 1]}", T, i <= 3 ? Detail : Exotic, S($"rune{i}"), family: "rune");
        Mod("blade", "Blade Gem", J, Detail, S("gemA"), family: "gem");
        Mod("blade", "Second Blade Gem", J, Detail, S("gemB"), family: "gem");
        Mod("blade", "Blade Enchantment", T, Detail, S("enchant"), family: "enchantment");
        Mod("blade", "Blade Oil", K, Detail, S("oil"));
        Mod("blade", "Sword Tassel", X, Detail, S("tassel"), family: "tassel");
        Mod("offblade", "Off-Hand Pommel", W, Detail, S("pommel"), family: "pommel");
        Mod("offblade", "Off-Hand Guard", W, Detail, S("guard"), family: "crossguard");
        Mod("offblade", "Off-Hand Rune I", T, Detail, S("rune1"), family: "rune");
        Mod("offblade", "Off-Hand Rune II", T, Detail, S("rune2"), family: "rune");
        Mod("offblade", "Off-Hand Gem", J, Detail, S("gem"), family: "gem");
        Mod("offblade", "Off-Hand Enchantment", T, Detail, S("enchant"), family: "enchantment");
        Mod("offblade", "Off-Hand Oil", K, Detail, S("oil"), family: "blade_oil");
        Mod("shield", "Shield Boss", A, Detail, S("boss"));
        Mod("shield", "Shield Rim", A, Detail, S("rim"));
        Mod("shield", "Shield Heraldry", X, Detail, S("heraldry"), family: "heraldry");
        Mod("shield", "Shield Spikes", W, Detail, S("spikes"), family: "spike");
        Mod("shield", "Shield Gem", J, Detail, S("gemA"), family: "gem");
        Mod("shield", "Second Shield Gem", J, Detail, S("gemB"), family: "gem");
        Mod("shield", "Shield Straps", X, Detail, S("straps"));
        Mod("shield", "Shield Enchantment", T, Detail, S("enchant"), family: "enchantment");
        Mod("focus", "Focus Core", J, Detail, S("core"), family: "gem");
        for (int i = 1; i <= 3; i++) Mod("focus", $"Focus Rune {Numerals[i - 1]}", T, Detail, S($"rune{i}"), family: "rune");
        Mod("focus", "Focus Enchantment", T, Detail, S("enchant"), family: "enchantment");
        // Companions.
        Mod("familiar", "Familiar Collar", J, Detail, S("collar"), family: "collar");
        Mod("familiar", "Familiar Charm", K, Detail, S("charm"), family: "charm");
        Mod("familiar", "Familiar Barding", A, Detail, S("barding"), family: "barding");
        Mod("familiar", "Familiar Bell", K, Detail, S("bell"), family: "bell");
        Mod("familiar", "Familiar Enchantment", T, Detail, S("enchant"), family: "enchantment");
        Mod("perch", "Jesses", X, Detail, S("jesses"));
        Mod("perch", "Falcon Hood", X, Detail, S("hood"));
        Mod("perch", "Falcon Bell", K, Detail, S("bell"), family: "bell");
        Mod("steed", "Saddle", X, Exotic, S("saddle"));
        Mod("steed", "Steed Barding", A, Exotic, S("barding"), family: "barding");
        Mod("steed", "Reins", X, Exotic, S("reins"));
        for (int i = 1; i <= 4; i++) Mod("steed", $"Horseshoe {Numerals[i - 1]}", A, Exotic, S($"shoe{i}"), family: "horseshoe");
        Mod("steed", "Steed Plume", X, Exotic, S("plume"), family: "plume");
        Mod("steed", "Saddlebags", K, Exotic, S("bags"), brings: new[] { Opens("saddlebags") });
        Mod("saddlebags", "Saddlebag Keepsake", K, Exotic, S("a"), family: "keepsake");
        Mod("saddlebags", "Second Saddlebag Keepsake", K, Exotic, S("b"), family: "keepsake");
        for (int i = 1; i <= 3; i++) Mod("aura", $"Aura Mote {Numerals[i - 1]}", Cel, Exotic, S($"mote{i}"), family: "aura_mote", prestige: true);
    }

    // ---------------------------------------------------------------- checks

    /// <summary>Two pieces on a board can't be worn together when one fills a cell the other fills or keeps free.</summary>
    public static bool Conflict(PieceSpec a, PieceSpec b) =>
        a.Board == b.Board && (a.Takes.Any(c => b.Takes.Contains(c) || b.Keeps.Contains(c)) || b.Takes.Any(c => a.Keeps.Contains(c)));

    void Validate()
    {
        var problems = new List<string>();
        foreach (var p in Pieces)
        {
            foreach (int c in p.Takes.Concat(p.Keeps))
                if (Cells[c].Board != p.Board) problems.Add($"{p.Name}: cell {Cells[c].Key} is on another board");
            if (p.Takes.Distinct().Count() != p.Takes.Length) problems.Add($"{p.Name}: a cell taken twice");
            foreach (var (b, opens) in p.Brings)
                foreach (int c in opens)
                    if (Cells[c].Board != b) problems.Add($"{p.Name}: opens {Cells[c].Key}, not on {Boards[b].Key}");
        }
        // Pieces that bring the same board must never be worn together, or the board would have two hosts.
        foreach (var board in Boards.Skip(1))
        {
            var hosts = Pieces.Where(p => p.Brings.Any(x => x.Board == board.Id)).ToList();
            if (hosts.Count == 0) problems.Add($"Board {board.Key} has no host");
            for (int i = 0; i < hosts.Count; i++)
                for (int j = i + 1; j < hosts.Count; j++)
                    if (!Conflict(hosts[i], hosts[j])) problems.Add($"{hosts[i].Name} and {hosts[j].Name} both bring {board.Key} and could be worn together");
        }
        // Every cell can be filled by something.
        var taken = new HashSet<int>(Pieces.SelectMany(p => p.Takes));
        foreach (var c in Cells)
            if (!taken.Contains(c.Id)) problems.Add($"Nothing fills {c.Key}");
        var names = Pieces.GroupBy(p => p.Name).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        foreach (var n in names) problems.Add($"Two pieces named {n}");
        if (problems.Count > 0) throw new InvalidOperationException("Anatomy: " + string.Join("; ", problems.Take(20)));
    }
}
