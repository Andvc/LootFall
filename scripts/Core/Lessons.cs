using System;

namespace LootFall.Core;

/// <summary>Something the player just did at the counter. The guided first day reacts to these.</summary>
public enum Cue { Arrive, HeardAll, LedgerOpened, Weighed, KnockedSolid, KnockedHollow, SawItem }

/// <param name="After">Shown once this happens.</param>
/// <param name="Key">For <see cref="Cue.SawItem"/>: the item that has to be seen.</param>
public sealed record Hint(Cue After, string Text, string Key = null);

/// <summary>One customer on the guided first day: a chosen chest and price, and Uncle Bram's commentary.</summary>
public sealed record Lesson(string Seller, string Claimed, Func<Chest> Chest, int Ask, int Reservation, string Pitch, Hint[] Hints);

/// <summary>
/// Day one is scripted. Each customer brings a chest built to teach one clue, in an order where each builds on the
/// last: listen and look it up, meet a sound you don't know, weigh a sound-alike, look at what you can't tell apart
/// by ear, catch a con by its silent weight, and knock for a false bottom.
/// </summary>
public static class Lessons
{
    public const string Mentor = "Uncle Bram";

    public static readonly Lesson[] FirstDay =
    {
        new("hob", "Farmhouse Cellar", () => Core.Chest.Of("Farmhouse Cellar", 0, new[] { "silver_crowns", "rusty_dagger" }),
            35, 25, "Found this in the barn. No idea what's in it. 35g?", new Hint[]
            {
                new(Cue.Arrive, "First customer! Grab the chest and shake it: drag it around with the mouse, and listen."),
                new(Cue.HeardAll, "Two sounds: 'tiktik' and 'tank'. Each kind of thing always makes its own sound. " +
                                  "Open my ledger (button on the right, or L) and find them."),
                new(Cue.LedgerOpened, "Silver crowns, 45g. A rusty dagger, 10g. So 55g inside, and Hob wants 35. " +
                                      "Offer him less: slide, then Offer. Sellers always leave room."),
            }),
        new("wren", "Wizard's Tower", () => Core.Chest.Of("Wizard's Tower", 1, new[] { "hand_mirror", "moonstone_pendant" }),
            40, 25, "Master said to sell this. Um. Is 40g okay?", new Hint[]
            {
                new(Cue.Arrive, "Wren, the wizard's apprentice. Give it a shake."),
                new(Cue.HeardAll, "'krik' is a cracked mirror, 8g. But 'tsilin'? Not in my ledger. " +
                                  "You only learn a sound by buying the thing and opening it. Cheap ones are worth a gamble."),
            }),
        new("penn", "Noble Estate", () => Core.Chest.Of("Noble Estate", 1, new[] { "gold_purse", "love_letters" }),
            50, 35, "It was my husband's. Would you give 50g for it?", new Hint[]
            {
                new(Cue.Arrive, "Widow Penn. Shake it."),
                new(Cue.HeardAll, "'chink' and 'shff': a purse of copper and old letters, by the sound. 17g at most. " +
                                  "Still, drag the chest onto the scale and see."),
                new(Cue.Weighed, "{weight} inside. Copper and letters weigh 1.2 lb together, so that purse is too heavy for copper. " +
                                 "Gold makes the same chink, and a purse of gold sells for 320g."),
            }),
        new("marta", "Noble Estate", () => Core.Chest.Of("Noble Estate", 0, new[] { "treasure_map", "rusted_lantern" }),
            55, 40, "Only a padlock on it, dear, so I won't ask much. 55g.", new Hint[]
            {
                new(Cue.Arrive, "Old Marta. She prices by the lock and nothing else. Shake it."),
                new(Cue.HeardAll, "'klunk' is a rusted lantern. 'shff' is loose paper, and loose paper all sounds alike: letters, maps. " +
                                  "You'll have to look. Click the keyhole to hold a lantern up to it."),
                new(Cue.SawItem, "There! That's no love letter, it's a map. Maps sell for hundreds. " +
                                 "Look away when you're done: the clock runs while you look.", "treasure_map"),
            }),
        new("silas", "Noble Estate", () => Core.Chest.Of("Farmhouse Cellar", 1, new[] { "copper_purse", "hand_mirror" },
                stones: new[] { 1.5f }),
            170, 45, "A purse of GOLD in there, friend. Hear it chink? 170g.", new Hint[]
            {
                new(Cue.Arrive, "Silas. Says it's from a noble estate, but noble chests are red. That one's farmhouse green."),
                new(Cue.HeardAll, "A purse and a cracked mirror. Silas swears the purse is gold. Weigh it."),
                new(Cue.Weighed, "{weight}: just right for a gold purse and a mirror. Hm. But it's Silas, with a farmhouse chest. " +
                                 "Take a look inside before you trust the scale."),
                new(Cue.SawItem, "Rags wrapped round a stone! He's weighted a copper purse so it passes for gold. " +
                                 "The scale tells you the weight, never what makes it. Send him off.", "stone"),
            }),
        new("pip", "Noble Estate", () => Core.Chest.Of("Noble Estate", 2, new[] { "signet_ring", "old_brandy" }, falseBottom: "doubloons"),
            80, 65, "A ring and a good brandy. 80g, and it's fair.", new Hint[]
            {
                new(Cue.Arrive, "Pip the Sharp. He knows what's in his chests and prices them right. " +
                                "But he never knocks on them. Click the chest to knock."),
                new(Cue.KnockedHollow, "Hear that boom? Hollow! A false bottom Pip doesn't know about. " +
                                       "Pick the lock cleanly tonight and it's yours. Don't haggle too hard: Pip walks."),
            }),
    };

    public const string BackRoom = "Open them one at a time. Pick the locks gently: " +
                                   "a crowbar breaks glass, and never finds a false bottom.";

    public static Visit ToVisit(Lesson lesson, Random rng) =>
        new(Sellers.Get(lesson.Seller), lesson.Chest(), Provenances.Get(lesson.Claimed), lesson.Ask, lesson.Reservation, rng, lesson.Pitch)
        {
            Lesson = lesson,
        };
}

/// <summary>Walks through a lesson's hints as the player does things. Hints only move forward.</summary>
public sealed class Tutor
{
    readonly Lesson _lesson;
    int _index = -1;

    public Tutor(Lesson lesson)
    {
        _lesson = lesson;
    }

    public Hint Current => _index >= 0 ? _lesson.Hints[_index] : null;

    /// <summary>Returns true if this moved on to a new hint.</summary>
    public bool Notify(Cue cue, string key = null)
    {
        for (int i = _index + 1; i < _lesson.Hints.Length; i++)
        {
            var hint = _lesson.Hints[i];
            if (hint.After != cue || (hint.Key != null && hint.Key != key)) continue;
            _index = i;
            return true;
        }
        return false;
    }

    public bool Expects(Cue cue) => Array.Exists(_lesson.Hints, h => h.After == cue);
}
