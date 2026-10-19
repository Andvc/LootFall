using System.Collections.Generic;

namespace LootFall.Core;

/// <summary>
/// Uncle Bram's tips. Each one shows the first time its moment comes up (a sound, a customer, a tool, a renovation),
/// so the game teaches one thing at a time, when it's in front of you.
/// </summary>
public static class Hints
{
    public const string Mentor = "Uncle Bram";

    static readonly Dictionary<string, string> Text = new()
    {
        // The first day: one lesson per customer.
        ["intro"] = "Morning! The stall's yours now. Tam never shakes anything, so he can't know what's inside. " +
                    "Drag the chest slowly: a gentle shake.",
        ["heard_Coins"] = "A jingle: coins. Light things rattle at the gentlest shake. Tick Coins on your slip, on the right.",
        ["slip"] = "The slip adds up what you've ticked, as good as the chest promises. The bar under it is your odds it's " +
                   "worth more than the price: the greener, the safer.",
        ["first_penn"] = "Penn only shook it gently and heard nothing. Try every strength yourself: gentle, firm, hard.",
        ["heard_Cloth"] = "Flump: cloth. Soft things only make a sound when you shake hard.",
        ["first_hob"] = "Hob shook it hard and heard a clank. Shake it yourself, at every strength.",
        ["heard_Metal"] = "Clank: metal. It only moves at a firm shake, and while it clanks you can't hear anything delicate. " +
                          "Shake gently to hear past it.",
        ["first_mags"] = "Mags counts the rattles at a firm shake and charges the same for each. If you hear nothing she " +
                         "didn't, pass: sellers always ask a bit more than it's worth.",
        ["passed"] = "When you pass, they take it away unopened: you'll never know for sure. But your gut usually has an opinion.",
        ["opened"] = "A tick is a good read. A cross is something to listen for next time.",
        ["dayend"] = $"That's a day's trading. Every coin goes toward fixing up the stall: once it's earned {GameState.UpgradesAt}g, " +
                     "all told, we'll talk renovations.",

        // From the second day: chest colours and the ways people trade.
        ["colours"] = "Look at the chest's colour: wood, green, blue, purple. It tells you how good things inside tend to be. " +
                      "Only tends to: you'll get surprises both ways.",
        ["quality"] = "See? Not everything matches its chest. Mostly it does; sometimes it's a little worse; now and then it's much better.",
        ["fixed"] = "This one names a price. Take it or leave it.",
        ["haggle"] = "This one haggles: they ask high. Use the slider to make one offer. They'll take it, name a final price, " +
                     "or storm off if you insult them.",
        ["offer"] = "This one wants you to name a price. Offer what it's worth to you, less a little. You get two tries.",
        ["try_again"] = "Too low. One more try, then they'll go.",
        ["keep"] = "Something grand? You can keep it for display instead of selling it. A good exhibition brings more customers, " +
                   "and better ones. Enchanted things count double.",
        ["kept"] = "On display. You'll see it on the shelf behind the counter.",
        ["exhibition"] = "Your exhibition. Click anything in it to sell it if you need the money.",

        // The shop opens up a step at a time.
        ["upgrades"] = $"{GameState.UpgradesAt}g earned: the stall pays its way! A Village Shop brings more customers, richer chests " +
                       "and a shelf. But village chests cost more: keep enough for tomorrow's. An orange price means wait a day.",
        ["stash"] = "Nearly broke? I keep a little put by for days like this. Don't tell the Guild. It won't last: three times, no more.",
        ["display"] = "A shelf behind the counter, with room for one thing! Keep something grand for display instead of selling " +
                      "it: an exhibition draws more customers, and better chests. Each cabinet adds room for one more.",
        ["synergy"] = "Town folk know a shop by what it collects. Your journal has tallied everything you've acquired: ten of a " +
                      "kind earns a synergy. Open Synergies, bottom right, to put one to work or see how close you are.",
        ["slots"] = "Another synergy slot: open Synergies and put one more to work.",
        ["emporium"] = "The Emporium! As grand as shops get. Bigger things to grow into now: a night market, a patrons' book, " +
                       "a velvet case for curios. Just keep enough back for Vane.",
        ["ledger"] = "Forgotten a sound? Press L for your ledger: the sounds, the regulars you've met, and everything you've found.",
        ["lockpick"] = "The pick sweeps round the lock. Press Space, or click, while it's in the green to set a pin; outside it, " +
                       "it slips. Set every pin cleanly for a masterful pick: everything inside is worth more.",

        // The run: the Guild's dues, curios, commissions, events.
        ["dues"] = "Paid. Next week the Guild wants more, and more the week after. Whatever you spend on the shop has to " +
                   "earn its keep before Vane comes back.",
        ["peddler"] = "Nim sells curios: trinkets that each bend one rule of the trade. You've room for three. His prices climb " +
                      "each week, like the Guild's, so a good one is cheapest now.",
        ["commissions"] = "Regulars want things now: see the board on the left. When something they want comes out of a chest, " +
                          "hand it over instead of selling it. A commission pays far more than a sale.",
        ["deliver"] = "Someone wants this! Deliver it for the commission, or sell it as usual.",
        ["event"] = "Something's happening in town today: one day a week is different. It's up in the top bar: click it to read it again.",
        ["stranger"] = "The Stranger. Packed tight in straw: nothing inside can move, so there's nothing to hear. Drop it to feel " +
                       "how heavy it is. Heavy ones hold more.",
        ["gilded"] = "Gold! One of the old royal chests. Its seller thinks the gold is paint, and priced it as the colour underneath.",
        ["payoff"] = "Look at that purse: enough to pay off the whole debt, every week of it, right now! Do it and the stall is " +
                     "ours, early. Or keep trading first: the Guild only ever asks for this week's due.",
        ["pawn"] = "Short? Pawn what's on display, or your curios, at the Guild's price. If even that isn't enough, the shop is theirs.",
        ["hood"] = "The Hooded Woman's commission pays in map pieces. If you miss it, she'll ask again next week.",

        // Each renovation brings one new thing.
        ["tier2"] = "Village folk bring two new sounds. Bottles slosh at a gentle shake, like coins. " +
                    "Paper rustles only at a hard shake, like cloth. And some chests are locked, or have a false bottom.",
        ["locked"] = "Locked. Buy it and you'll pick the lock before you open it. A crowbar's quicker, but it breaks glass.",
        ["dropped"] = "Dropped! A solid thud is a plain chest. A hollow boom means a false bottom: something hidden nobody's priced.",
        ["hollow"] = "Hear that boom? A false bottom. Tick it on your slip: whatever's in there, nobody's priced it.",
        ["broke"] = "Glass breaks when you drop it. If you heard a slosh, think twice before dropping.",
        ["heard_Bottles"] = "Slosh: a bottle. Delicate, like coins: a clank drowns it out.",
        ["heard_Paper"] = "Rustle: paper. Soft, like cloth: it only sounds at a hard shake.",
        ["tier3"] = "The town shop: jewelry, which tinkles at a gentle shake; mages, whose things are often enchanted (a witchlight " +
                    "would show it); and lots with two or three of a kind. Count the rattles: click a row again for each.",
        ["heard_Jewels"] = "Tinkle: jewelry. Delicate, and worth the most.",
        ["lamp"] = "The witchlight's lit, and it stays lit: anything enchanted glows when it makes a sound. Click the star on a row " +
                   "to mark one enchanted.",
        ["counted"] = "Two of a kind: each one adds its value. Sellers who don't count only price one.",
        ["glow"] = "It glowed! That one's enchanted: worth twice as much. Click the star on its row to mark it.",
        ["tier4"] = "The Emporium. Nobles, adventurers and a crook. Mind Silas: he sells gilded iron as gold and jewels, " +
                    "and it sounds just like the real thing. A magnet would catch it.",
        ["magnet"] = "Magnet's on: iron can't move. Metal goes quiet, so nothing drowns out the delicate things. " +
                     "And gilded fakes go quiet too.",

        // A regular with nothing new to teach gets a tip the first time they come in after the first day; the rest have
        // a lesson (Lessons.cs).
        ["seller_penn"] = "Penn only shakes gently. Heavy things and soft things make no sound for her.",
    };

    public static string Get(string key) => Text.TryGetValue(key, out var text) ? text : null;
}
