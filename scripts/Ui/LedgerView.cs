using System;
using System.Linq;
using Godot;
using LootFall.Core;

namespace LootFall.Ui;

/// <summary>
/// The shop's ledger. Sounds: one row per kind of thing your shop sees (its sound, how hard you must shake to hear it,
/// how heavy it is, and what one is worth if it's as good as each colour of chest promises; click a row to hear it),
/// and the rules of listening. Regulars: how each seller you've met judges a chest, trades and behaves, and how far
/// their stories hold up, place by place. Places: where chests come from, and what each tends to hold. Collection:
/// everything you've found so far. Synergies: what the shop could be known for, how far along each is, and
/// (at the end of a day) which are active. Songs: the Heartsongs Bram's kin have opened, once there's one to show.
/// </summary>
public partial class LedgerView : Node2D
{
    // Ends above Uncle Bram's box, so his tips stay readable with the ledger open.
    const float PanelX = 40, PanelY = 66, PanelW = 1200, PanelH = 518;
    const float TableX = 64, RowH = 40;

    readonly GameState _state;
    readonly int _firstPage;
    readonly bool _choosing;
    Node2D _page;
    bool _closing;

    public event Action Closed;

    public LedgerView() { }

    /// <param name="choosing">Synergies can be switched on and off (at the end of a day).</param>
    public LedgerView(GameState state, int page = 0, bool choosing = false)
    {
        _state = state;
        _firstPage = page;
        _choosing = choosing;
    }

    static Audio.Sfx Sfx => Main.Instance.Sfx;

    public override void _Ready()
    {
        var blocker = new ColorRect { Color = new Color(0, 0, 0, 0.7f), MouseFilter = Control.MouseFilterEnum.Stop }.Place(-100, -100, 1480, 920);
        blocker.GuiInput += e =>
        {
            if (e is InputEventMouseButton { Pressed: true }) Close();
        };
        AddChild(blocker);
        AddChild(UiKit.MakePanel(Palette.Gold.Darkened(0.3f)).Place(PanelX, PanelY, PanelW, PanelH));
        AddChild(UiKit.MakeLabel("LEDGER", 32, Palette.Gold, outline: 4).Place(PanelX + 24, PanelY + 10, 200, 40));
        AddChild(UiKit.MakeButton("Sounds", Palette.Text, 16, () => ShowPage(0)).Place(PanelX + 220, PanelY + 14, 120, 36));
        AddChild(UiKit.MakeButton("Regulars", Palette.Text, 16, () => ShowPage(1)).Place(PanelX + 348, PanelY + 14, 140, 36));
        AddChild(UiKit.MakeButton("Places", Palette.Text, 16, () => ShowPage(4)).Place(PanelX + 496, PanelY + 14, 120, 36));
        AddChild(UiKit.MakeButton("Collection", Palette.Text, 16, () => ShowPage(2)).Place(PanelX + 624, PanelY + 14, 160, 36));
        // Synergies come with Crownhold: before that, the journal just keeps its tally.
        if (_state.SynergiesOpen)
            AddChild(UiKit.MakeButton("Synergies", Palette.Text, 16, () => ShowPage(3)).Place(PanelX + 792, PanelY + 14, 152, 36));
        if (_state.Songbook.Count > 0)
            AddChild(UiKit.MakeButton("Songs", Palette.Of(Rarity.Mythic), 16, () => ShowPage(5)).Place(PanelX + 952, PanelY + 14, 96, 36));
        AddChild(UiKit.MakeButton("Close [L]", Palette.Text, 16, Close).Place(PanelX + PanelW - 144, PanelY + 12, 120, 40));
        _page = new Node2D();
        AddChild(_page);
        ShowPage(_firstPage);

        Sfx.PlayGroup("book", 1f, -4);
        Modulate = Colors.Transparent;
        CreateTween().TweenProperty(this, "modulate", Colors.White, 0.15f);
    }

    /// <summary>0: sounds, 1: regulars, 2: collection, 3: synergies, 4: places, 5: Heartsongs.</summary>
    public void ShowPage(int page)
    {
        foreach (var child in _page.GetChildren()) child.QueueFree();
        if (page == 5 && _state.Songbook.Count > 0) BuildSongs();
        else if (page == 1) BuildRegulars(Spread(_state.Tier));
        else if (page == 2) BuildCollection();
        else if (page == 4) BuildPlaces();
        else if (page == 3 && _state.SynergiesOpen) BuildSynergies();
        else BuildSounds();
    }

    /// <summary>
    /// Each kind of synergy on a row: what it's about, its level (five pips), what its level does (or would do), what
    /// the next level needs, and whether it's active. At the end of a day, a button switches it on or off.
    /// </summary>
    void BuildSynergies()
    {
        int slots = _state.SynergySlots, active = _state.ActiveSynergies.Count;
        string intro = _choosing
            ? $"Active: {active} of {slots}. Switch one on to put it to work; more slots come with a Crownhold Charter and a Royal Seal."
            : $"Active: {active} of {slots}. You choose at the end of a day.";
        _page.AddChild(UiKit.MakeLabel(intro, 16, Palette.Muted).Place(TableX, PanelY + 60, PanelW - 48, 20));
        const float rowH = 58;
        for (int i = 0; i < Synergies.All.Length; i++)
        {
            var synergy = Synergies.All[i];
            float y = PanelY + 88 + i * rowH;
            int level = synergy.Level(_state);
            bool on = _state.ActiveSynergies.Contains(synergy.Key);
            bool later = synergy.Tier > _state.Tier && level == 0;
            var row = new Panel { MouseFilter = Control.MouseFilterEnum.Ignore }.Place(TableX - 8, y, PanelW - 32, rowH - 4);
            row.AddThemeStyleboxOverride("panel", UiKit.PanelBox(4));
            if (on) row.AddChild(UiKit.MakeFrame(Palette.Magic));
            _page.AddChild(row);
            var nameColor = later ? Palette.Muted.Darkened(0.2f) : on ? Palette.Magic : level > 0 ? Palette.Gold : Palette.Text;
            row.AddChild(UiKit.MakeLabel(synergy.Name, 16, nameColor).Place(12, 6, 220, 20));
            row.AddChild(UiKit.MakeLabel(later ? $"{synergy.Blurb} (later)" : synergy.Blurb, 16, Palette.Muted).Place(12, 28, 220, 20));
            var pips = new Painter(p =>
            {
                for (int l = 0; l < SynergyDef.MaxLevel; l++)
                {
                    var c = l < level ? Palette.Of((Rarity)l) : Palette.Muted.Darkened(0.4f);
                    p.DrawRect(new Rect2(l * 22, 0, 16, 16), new Color(0, 0, 0, 0.5f));
                    if (l < level) p.DrawRect(new Rect2(l * 22 + 3, 3, 10, 10), c);
                    p.DrawRect(new Rect2(l * 22, 0, 16, 16), c, false, 2);
                }
            }) { Position = new Vector2(240, 18) };
            row.AddChild(pips);
            string effect = level > 0 ? synergy.Effect(level) : $"Level 1: {synergy.Effect(1)}";
            row.AddChild(UiKit.MakeLabel(effect, 16, level > 0 ? Palette.Text : Palette.Muted).Wrapped().Place(360, 6, 480, 46));
            var progress = synergy.Progress(_state).ToList();
            string next = progress.Count == 0 ? "Highest level!" : $"Next: {string.Join("\n", progress)}";
            row.AddChild(UiKit.MakeLabel(next, 16, Palette.Muted).Wrapped().Place(852, 6, 196, 46));
            if (_choosing && level > 0)
            {
                bool canTurnOn = on || active < slots;
                var toggle = UiKit.MakeButton(on ? "Active" : "Use", on ? Palette.Magic : Palette.Good, 16, () =>
                {
                    bool now = _state.ToggleSynergy(synergy.Key);
                    Sfx.Play(now ? "sting" : "tick", now ? 1.3f : 1f, -4);
                    ShowPage(3);
                }).Place(1060, 10, 100, 34);
                toggle.Disabled = !canTurnOn;
                if (!canTurnOn) toggle.TooltipText = "No free slot: switch another one off first.";
                row.AddChild(toggle);
            }
            else if (on) row.AddChild(UiKit.MakeLabel("Active", 16, Palette.Magic, HorizontalAlignment.Center).Place(1060, 18, 100, 20));
        }
    }

    /// <summary>
    /// The songbook: the nine Heartsongs, three by three. One the family has opened shows its picture, who loved it and
    /// what it does on display (its story on hover), and whether it's singing in the shop now; the rest are "?".
    /// </summary>
    void BuildSongs()
    {
        int known = Heartsongs.All.Count(h => _state.Songbook.Contains(h.Key));
        _page.AddChild(UiKit.MakeLabel("Nine things loved more than gold, sung into the Locking and hidden in chests among millions. Only a " +
                                       "Keywright hears one sing; to anyone else it's a trinket. Hover a card for its story. " +
                                       $"Bram's kin know {known} of the nine" +
                                       (known == Heartsongs.All.Length ? ": the Crown Chest can be sung whole." : ": with all nine, the Crown Chest's ending changes."),
            16, Palette.Muted).Wrapped().Place(TableX, PanelY + 56, PanelW - 48, 60));
        const float cardW = 376, cardH = 128, gap = 8;
        for (int i = 0; i < Heartsongs.All.Length; i++)
        {
            var song = Heartsongs.All[i];
            bool found = _state.Songbook.Contains(song.Key);
            bool shown = _state.Shows(song.Key);
            float x = TableX - 8 + i % 3 * (cardW + gap), y = PanelY + 118 + i / 3 * (cardH + 4);
            var card = new Panel { MouseFilter = Control.MouseFilterEnum.Pass }.Place(x, y, cardW, cardH);
            card.AddThemeStyleboxOverride("panel", UiKit.PanelBox(4));
            if (shown) card.AddChild(UiKit.MakeFrame(Palette.Of(Rarity.Mythic)));
            _page.AddChild(card);
            if (!found)
            {
                card.AddChild(UiKit.MakeLabel("?", 32, Palette.Muted, HorizontalAlignment.Center).Place(0, 44, cardW, 40));
                card.TooltipText = "Not heard yet.";
                continue;
            }
            card.TooltipText = $"{song.Name}, from {song.From}. {song.Story}";
            card.AddChild(UiKit.MakeItemIcon(Heartsongs.Make(song, Rarity.Common), new Vector2(32, 40), 48));
            card.AddChild(UiKit.MakeLabel(song.Name, 16, Palette.Of(Rarity.Mythic)).Place(64, 6, cardW - 72, 20));
            card.AddChild(UiKit.MakeLabel($"From {song.From}.", 16, Palette.Muted).Wrapped().Place(64, 28, cardW - 72, 40));
            card.AddChild(UiKit.MakeLabel(song.Effect, 16, Palette.Text).Wrapped().Place(10, 66, cardW - 18, 60));
            if (shown) card.AddChild(UiKit.MakeLabel("singing", 16, Palette.Gold, HorizontalAlignment.Right).Place(cardW - 100, 6, 92, 20));
        }
    }

    /// <summary>
    /// A grid: one row per kind, one column per quality, then the fake. Found things show as they came out of the chest;
    /// the rest are "?". A star means you've found one enchanted too.
    /// </summary>
    void BuildCollection()
    {
        const float cell = 64, gridX = TableX + 140, gridY = PanelY + 92, step = cell + 76;
        string[] columns = { "Common", "Uncommon", "Rare", "Epic", "Legendary", "Fake" };
        for (int c = 0; c < columns.Length; c++)
            _page.AddChild(UiKit.MakeLabel(columns[c], 16, c < 5 ? Palette.Of((Rarity)c) : Palette.Muted, HorizontalAlignment.Center)
                .Place(gridX + c * step - 38, gridY - 26, cell + 66, 20));
        var entries = Collection.Entries.ToList();
        int row = 0;
        foreach (var kind in Categories.Kinds)
        {
            float y = gridY + row * cell;
            _page.AddChild(UiKit.MakeKindIcon(kind.Category, new Vector2(TableX + 16, y + cell / 2 - 6)));
            _page.AddChild(UiKit.MakeLabel(kind.Name, 16, Palette.Text).Place(TableX + 40, y + cell / 2 - 16, 100, 20));
            foreach (var entry in entries.Where(e => e.Category == kind.Category))
            {
                int column = entry.Fake ? 5 : (int)entry.Quality;
                float x = gridX + column * step;
                bool found = _state.Found.Contains(entry.Entry);
                bool enchanted = _state.FoundEnchanted.Contains(entry.Entry);
                var slot = UiKit.MakePanel(found ? Palette.Of(entry.Rarity) : Palette.PanelBorder).Place(x, y, cell - 10, cell - 10);
                slot.TooltipText = !found ? "Not found yet"
                    : $"{entry.Entry}: {entry.Value}g" + (enchanted ? $" (found enchanted too: {entry.Value * Categories.MagicMultiplier}g)" : "");
                slot.MouseFilter = Control.MouseFilterEnum.Pass;
                _page.AddChild(slot);
                if (found)
                {
                    slot.AddChild(UiKit.MakeItemIcon(entry, new Vector2((cell - 10) / 2, (cell - 10) / 2), 48));
                    if (enchanted)
                        slot.AddChild(UiKit.MakeLabel("*", 24, Palette.Magic, outline: 4).Place(cell - 26, -8, 24, 30));
                }
                else
                {
                    slot.AddChild(UiKit.MakeLabel("?", 24, Palette.Muted, HorizontalAlignment.Center).Place(0, 16, cell - 10, 30));
                }
            }
            row++;
        }
        _page.AddChild(UiKit.MakeLabel($"Found {_state.Found.Count} of {Collection.Size}. A star: found enchanted too. Hover one to see its " +
                                       "name and price.", 16, Palette.Muted).Place(TableX, PanelY + PanelH - 34, PanelW - 48, 20));
    }

    void BuildSounds()
    {
        int tier = _state.Tier;
        float y = PanelY + 60;
        Header("Kind", TableX + 48, y);
        Header("Sound", TableX + 220, y);
        Header("Shake", TableX + 330, y);
        Header("Weight", TableX + 420, y);
        Header("Note", TableX + 520, y);
        Header("If it's as good as the chest:", TableX + 760, y - 20);
        for (int c = 0; c < 4; c++) Header(Categories.ChestSkin((Rarity)c), TableX + 760 + c * 104, y);

        var kinds = Categories.UpTo(tier).ToList();
        for (int i = 0; i < kinds.Count; i++) _page.AddChild(MakeRow(kinds[i], y + 24 + i * RowH, tier));

        var rules = new[]
        {
            "Light things rattle at a gentle shake, weapons at a firm one, soft things only at a hard one. Click a row to hear it.",
            "While weapons clank, delicate things go unheard: shake gently to hear past it.",
            tier >= 2 ? "Dropped, a chest with a false bottom booms hollow. Glass may break." : null,
            tier >= 3 ? "Two or three of a kind each sound from their own spot: count them. Sand rustles like a scroll, but pours at a gentle shake." : null,
            tier >= 3 ? $"Anything can be enchanted: it sounds the same, but glows under a witchlight. Worth x{Categories.MagicMultiplier}." +
                        (tier >= 4 ? " Demon gold is iron under the gilt: a lodestone silences it." : "") : null,
        }.Where(r => r != null).ToArray();
        _page.AddChild(UiKit.MakeLabel(string.Join("\n", rules.Select(r => "- " + r)), 16, Palette.Text)
            .Place(TableX, y + 34 + kinds.Count * RowH, PanelW - 48, 140));
    }

    void Header(string text, float x, float y) => _page.AddChild(UiKit.MakeLabel(text, 16, Palette.Gold.Darkened(0.2f)).Place(x, y, 200, 20));

    Control MakeRow(CategoryDef kind, float y, int tier)
    {
        var row = new Panel { MouseFilter = Control.MouseFilterEnum.Stop }.Place(TableX - 8, y, PanelW - 32, RowH - 4);
        row.AddThemeStyleboxOverride("panel", UiKit.PanelBox(4));
        row.AddChild(UiKit.MakeKindIcon(kind.Category, new Vector2(28, (RowH - 4) / 2)));
        row.AddChild(UiKit.MakeLabel(kind.Name, 16, Palette.Text).Place(56, 8, 160, 20));
        row.AddChild(UiKit.MakeLabel(kind.Word, 16, Palette.Word).Place(228, 8, 100, 20));
        row.AddChild(UiKit.MakeLabel(Categories.LevelName(kind.StirsAt), 16, Palette.Level(kind.StirsAt)).Place(338, 8, 100, 20));
        row.AddChild(UiKit.MakeLabel(Categories.HeftWord(kind.Category), 16, Palette.Text).Place(428, 8, 100, 20));
        string note = kind.Loud ? "loud: drowns delicate" : kind.Delicate ? "delicate: clanks drown it" : "soft: hard shake only";
        if (kind.FakeName != null && kind.FakeIron && tier >= 4) note = "delicate; demon gold apes it";
        else if (kind.FakeName != null && !kind.FakeIron && tier >= 3) note = "soft; sand sounds alike";
        row.AddChild(UiKit.MakeLabel(note, 16, Palette.Muted).Place(528, 8, 236, 20));
        for (int c = 0; c < 4; c++)
            row.AddChild(UiKit.MakeLabel($"{Categories.Nominal(kind.Category, (Rarity)c)}g", 16, Palette.Of((Rarity)c)).Place(768 + c * 104, 8, 96, 20));
        row.GuiInput += e =>
        {
            if (e is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }) return;
            Sfx.PlayGroup(Audio.SoundSets.Group(kind.Category), kind.Pitch, 2f);
            row.PivotOffset = row.Size / 2;
            var bump = row.CreateTween();
            bump.TweenProperty(row, "scale", Vector2.One * 1.02f, 0.05f);
            bump.TweenProperty(row, "scale", Vector2.One, 0.12f);
        };
        return row;
    }

    /// <summary>Which spread of the regulars a shop tier's sellers are on: one per place.</summary>
    static int Spread(int tier) => Math.Clamp(tier, 1, ShopTiers.Max) - 1;

    /// <summary>The regulars, a spread per place, with buttons along the bottom.</summary>
    void BuildRegulars(int spread)
    {
        int total = Sellers.All.Count(s => s.Tier <= _state.Tier);
        int metCount = Sellers.All.Count(s => s.Tier <= _state.Tier && _state.Met.Contains(s.Key));
        _page.AddChild(UiKit.MakeLabel($"{metCount} of {total} met", 16, Palette.Muted, HorizontalAlignment.Right)
            .Place(PanelX + PanelW - 224, PanelY + PanelH - 32, 200, 20));
        for (int i = 0; i <= Spread(_state.Tier) && _state.Tier >= 2; i++)
        {
            int which = i;
            _page.AddChild(UiKit.MakeButton(ShopTiers.Name(i + 1), spread == i ? Palette.Gold : Palette.Text, 16, () => ShowRegulars(which))
                .Place(TableX + i * 216, PanelY + PanelH - 44, 208, 34));
        }

        var met = Sellers.All.Where(s => s.Tier <= _state.Tier && Spread(s.Tier) == spread && _state.Met.Contains(s.Key)).ToList();
        if (met.Count == 0)
        {
            _page.AddChild(UiKit.MakeLabel("You haven't met anyone yet.", 24, Palette.Muted, HorizontalAlignment.Center).Place(PanelX, 300, PanelW, 30));
            return;
        }
        for (int i = 0; i < met.Count; i++)
        {
            var seller = met[i];
            float x = TableX + i % 2 * 580, y = PanelY + 58 + i / 2 * 98;
            _page.AddChild(UiKit.MakeSprite($"res://assets/sprites/sellers/{seller.Key}.png", new Vector2(x + 20, y + 24), 1));
            string trade = seller.Trade switch { Trade.Haggle => "haggles", Trade.Offer => "you name the price", _ => "fixed price" };
            var name = UiKit.MakeLabel(seller.Name, 16, Palette.Gold).Place(x + 52, y + 2, 260, 20);
            name.TooltipText = $"{seller.Name} {seller.JudgeWord}; {trade}.";
            name.MouseFilter = Control.MouseFilterEnum.Pass;
            _page.AddChild(name);
            var (story, trusted) = StoryLine(seller);
            _page.AddChild(UiKit.MakeLabel(story, 16, trusted ? Palette.Good : Palette.Warn, HorizontalAlignment.Right).Place(x + 252, y + 2, 300, 20));
            _page.AddChild(UiKit.MakeLabel(seller.Habit, 16, Palette.Text).Wrapped().Place(x + 52, y + 22, 510, 70));
        }
    }

    /// <summary>How far to trust what they say about where a chest came from (Bram's word), and your own tally of it.</summary>
    (string Text, bool Trusted) StoryLine(SellerDef seller)
    {
        if (seller.Stories.Count == 0 && seller.Tales.Length == 0) return ("", true);
        var (told, truths) = _state.StoriesChecked(seller.Key);
        string word = seller.Honesty >= 1 ? "Stories: true" : seller.Honesty <= 0 ? "Stories: lies" : seller.Honesty >= 0.6f ? "Stories: mostly true"
            : "Stories: often tall";
        return (told == 0 ? word : $"{word} ({truths}/{told} true)", seller.Honesty >= 0.6f);
    }

    /// <summary>
    /// Where chests come from, two columns of them: what each place tends to hold, and the kinds you'd mostly hear. Only
    /// places you've heard of (a story told true, or a chest of theirs opened) are filled in.
    /// </summary>
    void BuildPlaces()
    {
        var kinds = Categories.UpTo(_state.Tier).ToList();
        var places = Origins.All.Where(o => _state.Places.Contains(o.Key)).ToList();
        _page.AddChild(UiKit.MakeLabel($"{places.Count} of {Origins.All.Length} places heard of. Where a chest came from says what it " +
                                       "tends to hold; not every seller says so truthfully.", 16, Palette.Muted).Place(TableX, PanelY + 60, PanelW - 48, 20));
        if (places.Count == 0)
        {
            _page.AddChild(UiKit.MakeLabel("Nobody's told you where their chest came from yet.", 24, Palette.Muted, HorizontalAlignment.Center)
                .Place(PanelX, 300, PanelW, 30));
            return;
        }
        const float rowH = 42;
        for (int i = 0; i < places.Count; i++)
        {
            var place = places[i];
            float x = TableX + i / 10 * 580, y = PanelY + 88 + i % 10 * rowH;
            _page.AddChild(UiKit.MakeLabel(Capital(place.Name), 16, Palette.Gold).Place(x, y, 300, 20));
            _page.AddChild(UiKit.MakeLabel(place.Holds, 16, Palette.Text).Place(x, y + 18, 400, 20));
            // The kinds you'd mostly hear in a chest from there (at this shop): the likeliest first.
            float total = kinds.Sum(k => place.Kinds[(int)k.Category]);
            var mostly = kinds.Where(k => place.Kinds[(int)k.Category] / total >= 0.2f).OrderByDescending(k => place.Kinds[(int)k.Category]).Take(3).ToList();
            for (int k = 0; k < mostly.Count; k++)
            {
                var icon = UiKit.MakeKindIcon(mostly[k].Category, new Vector2(x + 440 + k * 36, y + 18));
                _page.AddChild(icon);
            }
        }
    }

    static string Capital(string text) => char.ToUpperInvariant(text[0]) + text[1..];

    void ShowRegulars(int spread)
    {
        foreach (var child in _page.GetChildren()) child.QueueFree();
        BuildRegulars(spread);
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (e is InputEventKey { Pressed: true, Echo: false, Keycode: Key.L or Key.Escape })
        {
            GetViewport().SetInputAsHandled();
            Close();
        }
    }

    public void Close()
    {
        if (_closing || !IsInsideTree()) return;
        _closing = true;
        var tween = CreateTween();
        tween.TweenProperty(this, "modulate", Colors.Transparent, 0.12f);
        tween.TweenCallback(Callable.From(() =>
        {
            Closed?.Invoke();
            QueueFree();
        }));
    }
}
