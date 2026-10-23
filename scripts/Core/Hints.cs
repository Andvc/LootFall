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
        ["intro"] = "Morning! The stall's yours now, and the Key-mark's on your hand: any chest you've paid for opens for you. " +
                    "Nobody else can see in, so they shake, and guess. Tam never even shook his. Drag the chest slowly: a gentle shake.",
        ["kin_hesk"] = "Your back won't take a hard shake or a drop, Hesk, but your scale doesn't lie. Weigh what you heard against " +
                       "what's on it: what's left over is what you couldn't hear, or what's under a false bottom.",
        ["kin_rosa"] = "Everybody haggles with you, Rosa, though they ask a bit more, knowing you. Make your offer with the slider. " +
                       "Too low and they'll let you try again, but walk one out of the door and they won't be back this week.",
        ["kin_isolde"] = "Your shelf's up from the first day, Isolde. Keep what you like: visitors pay at the door every night, a coin " +
                         "a point of exhibition. But the Guild won't take your collection on a short night.",
        ["heard_Coins"] = "A jingle: coin. Light things rattle at the gentlest shake. Tick Coin on your slip, on the right.",
        ["slip"] = "The slip adds up what you've ticked, as good as the chest promises. The bar under it is your odds it's " +
                   "worth more than the price: the greener, the safer.",
        ["first_penn"] = "Penn only shook it gently and heard nothing. Try every strength yourself: gentle, firm, hard.",
        ["heard_Garb"] = "Flump: a cloak. Soft things only make a sound when you shake hard.",
        ["first_hob"] = "Hob shook it hard and heard a clank. Shake it yourself, at every strength.",
        ["heard_Arms"] = "Clank: a weapon, an axe or a blade. It only moves at a firm shake, and while it clanks you can't hear anything " +
                         "delicate. Shake gently to hear past it.",
        ["first_mags"] = "Mags counts the rattles at a firm shake and charges the same for each. If you hear nothing she " +
                         "didn't, pass: sellers always ask a bit more than it's worth.",
        ["passed"] = "When you pass, they take it away unopened: you'll never know for sure. But your gut usually has an opinion.",
        ["opened"] = "A tick is a good read. A cross is something to listen for next time.",
        ["dayend"] = $"That's a day's trading. Every coin goes toward the stall: once it's earned {GameState.UpgradesAt}g, all told, " +
                     "we'll talk about Thornwick.",

        // From the second day: chest colours, the ways people trade, and their stories.
        ["colours"] = "Look at the chest's colour: plain wood, green, blue, purple. The Wardens painted them by what went in. It tells " +
                      "you how good things inside tend to be. Only tends to: you'll get surprises both ways.",
        ["quality"] = "See? Not everything matches its chest. Mostly it does; sometimes it's a little worse; now and then it's much better.",
        ["fixed"] = "This one names a price. Take it or leave it.",
        ["haggle"] = "This one haggles: they ask high. Use the slider to make one offer. They'll take it, name a final price, " +
                     "or storm off if you insult them.",
        ["offer"] = "This one wants you to name a price. Offer what it's worth to you, less a little. You get two tries.",
        ["try_again"] = "Too low. One more try, then they'll go.",
        ["story"] = "Listen to where they say it came from: a barrow holds grave goods, a goblin warren holds coin. The ledger's " +
                    "Places page keeps what each place tends to hold.",
        ["keep"] = "Something grand? You can keep it for display instead of selling it. A good exhibition brings more customers, " +
                   "and better ones. Enchanted things count double.",
        ["kept"] = "On display. You'll see it on the shelf behind the counter.",
        ["exhibition"] = "Your exhibition. Click anything in it to sell it if you need the money.",

        // The shop moves inward, a step at a time.
        ["upgrades"] = $"{GameState.UpgradesAt}g earned: the stall pays its way! Thornwick Market has more customers, richer chests " +
                       "and room for a shelf. But Thornwick chests cost more: keep enough for tomorrow's. An orange price means wait a day.",
        ["stash"] = "Nearly broke? I keep a little put by for days like this. Don't tell the Guild. It won't last forever: see the day card for what's left.",
        ["display"] = "A shelf behind the counter, with room for one thing! Keep something grand for display instead of selling " +
                      "it: an exhibition draws more customers, and better chests. Each cabinet adds room for one more.",
        ["synergy"] = "Crownhold folk know a shop by what it collects. Your journal has tallied everything you've acquired: ten of a " +
                      "kind earns a synergy. Open Synergies, bottom right, to put one to work or see how close you are.",
        ["slots"] = "Another synergy slot: open Synergies and put one more to work.",
        ["emporium"] = "The Royal Emporium, in the palace's shadow. The Gate's just down the street: you can feel it hum. Bigger " +
                       "things to grow into now: a night market, a patrons' book, a velvet case for curios. Keep enough back for Vane.",
        ["ledger"] = "Forgotten a sound, or someone's habits? Press L for your ledger: the sounds, the places chests come from, the regulars " +
                     "you've met, and everything you've found.",
        ["lockpick"] = "The ward's let go: the chest is yours. Now the old lock under it. The pick sweeps round: press Space, or " +
                       "click, while it's in the green to set a pin. Set every pin cleanly and nothing inside gets scratched.",

        // The run: the Guild's dues, curios, commissions, events.
        ["dues"] = "Paid. Next week the Guild wants more, and more the week after. Whatever you spend on the shop has to " +
                   "earn its keep before Vane comes back.",
        ["peddler"] = "Nim sells curios: oddments from the Locking that each bend one rule of the trade. You've room for three. " +
                      "Nim's prices climb each week, like the Guild's, so a good one is cheapest now. Don't ask what Nim is.",
        ["commissions"] = "Regulars want things now: see the board on the left. When something they want comes out of a chest, " +
                          "hand it over instead of selling it. A commission pays far more than a sale.",
        ["deliver"] = "Someone wants this! Deliver it for the commission, or sell it as usual.",
        ["event"] = "Something's happening in town today: one day a week is different. It's up in the top bar: click it to read it again.",
        ["stranger"] = "The Stranger. That chest's wrapped in iron chains: nothing inside can move, so there's nothing to hear. " +
                       "Drop it to feel how heavy it is. Heavy ones hold more.",
        ["gilded"] = "A royal seal! One of King Aldric's own chests, gilded. Its seller thinks the gold is paint, and priced it as " +
                     "the colour underneath.",
        ["payoff"] = "Look at that purse: enough to pay off the whole back-tithe, every week of it, right now! Do it and the " +
                     "stall is ours, early. Or keep trading first: the Guild only ever asks for this week's.",
        ["pawn"] = "Short? Pawn what's on display, or your curios, at the Guild's price. If even that isn't enough, the Guild seals the shop.",
        ["hood"] = "The Hooded Woman's commission pays in shards of the Aldric Key. If you miss it, she'll ask again next week.",

        // Each move brings one new thing.
        ["tier2"] = "Thornwick folk bring two new sounds. Potions slosh at a gentle shake, like coin. Spells, scrolls and " +
                    "spellbooks, rustle only at a hard shake, like cloaks. And some chests are locked, or have a false bottom.",
        ["locked"] = "Locked. Buy it and you'll pick the lock before you open it. A crowbar's quicker, but it breaks glass.",
        ["dropped"] = "Dropped! A solid thud is a plain chest. A hollow boom means a false bottom: something hidden nobody's priced.",
        ["hollow"] = "Hear that boom? A false bottom. Tick it on your slip: whatever's in there, nobody's priced it.",
        ["broke"] = "Glass breaks when you drop it. If you heard a slosh, think twice before dropping.",
        ["heard_Potions"] = "Slosh: a potion. Delicate, like coin: a clank drowns it out.",
        ["heard_Scrolls"] = "Rustle: a spell, a scroll or a spellbook. Soft, like a cloak: it only sounds at a hard shake.",
        ["tier3"] = "Crownhold: gems, which tinkle at a gentle shake; archmages, whose things are often enchanted (a witchlight " +
                    "would show it); and lots with two or three of a kind. Count the rattles: click a row again for each.",
        ["heard_Gems"] = "Tinkle: a gem. Delicate, and worth the most.",
        ["lamp"] = "The witchlight's lit, and it stays lit: anything enchanted glows when it makes a sound. Click the star on a row " +
                   "to mark one enchanted.",
        ["counted"] = "Two of a kind: each one adds its value. Sellers who don't count only price one.",
        ["glow"] = "It glowed! That one's enchanted: worth twice as much. Click the star on its row to mark it.",
        ["tier4"] = "The Royal Emporium. Court folk, the Maw's creatures up through the cracks in the Gate, and Silas, who sells " +
                    "demon gold: gilded iron that jingles just like the real thing. A lodestone would catch it.",
        ["magnet"] = "Lodestone's on: iron can't move. Weapons go quiet, so nothing drowns out the delicate things. And demon gold, " +
                     "which is iron under the gilt, goes quiet too.",
        ["shake_limit"] = "That's all the shaking this one stands: it's coming apart. Go by what you heard.",

        // Heartsongs (Heartsongs.cs): about one a run, in any chest.
        ["heard_Heartsong"] = "That sound... I've heard it once in forty years. A Heartsong! Only a Keywright hears one sing: the " +
                              "seller has no idea it's there. Tick it on your slip, and buy that chest.",
        ["song_found"] = "A Heartsong. Keep it on display: it needs no shelf, and it's worth more singing in the shop than any " +
                         "merchant will pay. Each one does something different.",
        ["song_shown"] = "Hear that? The whole stall's different with it singing. It'll sing as long as it's ours.",

        // A regular with nothing new to teach gets a tip the first time they come in after the first day; the rest have
        // a lesson (Lessons.cs). Bram's kin who come knowing the trade still get his word on each seller's terms.
        ["seller_penn"] = "Penn only shakes gently. Heavy things and soft things make no sound for her.",
        ["seller_dorrin"] = "Dorrin's Barrow chests are rotten: about two seconds of shaking, all told. One strength, then: pick it well.",
        ["seller_snikkit"] = "Snikkit puts his price up a tenth for every strength you try after the first. Pick two: gentle and hard.",
        ["seller_bramble"] = "Mother Bramble dreams her price, but she always dreams true of the dearest thing inside. If it never sounds, it's under a false bottom.",
        ["engine"] = "You could pay off the Guild now. Or buy its Bookkeeping Engine for the same sum: every chest read for you, to finish the run fast.",
        ["seller_wenna"] = "Wenna's scale says what her chest weighs. Take away what you heard: what's left never stirred (cloak 3 lb, spell 1).",
        ["seller_pell"] = "Pell's ticket counts every article, false bottom and all. Count what you hear: one short means one under the floor.",
        ["seller_ansel"] = "Brother Ansel won't have his chests shaken hard: half again if you do. Crypt chests are full of holy cloaks.",
        ["seller_fennick"] = "Fennick packs in straw: everything stirs a strength later, cloaks and spells never. Firm, hard, then drop it.",
        ["seller_orrin"] = "Orrin wagers: read his chest exactly, every kind and every count, and he gives back a third of the price.",
        ["seller_corisande"] = "Lady Corisande's trick boxes often hide a false bottom, and she'll have half again if you drop one.",
        ["seller_aldous"] = "Sir Aldous takes the best weapon in his chest, for the fee he names. Value what's left.",
        ["seller_vexis"] = "Vexis gives you twelve seconds. Gently, then hard, and decide.",
        ["seller_malbranche"] = "Malbranche is cheap, but the dearest thing inside is his. Many middling things suit his bargain.",
        ["seller_skrae"] = "Skrae hears everything but magic and the floor. Look for a glow, and drop it.",
        ["seller_ermine"] = "Lady Ermine prices only her gems; the rest comes free. Some are paste, and she won't have a lodestone near.",
        ["seller_aldo"] = "Old Aldo's price is your slip. Exactly right: a quarter off. Wrong: what it holds, and a quarter more.",
        ["seller_maw"] = "The Maw sells for one coin, but everyone after him today asks twice as much. Count who's still waiting.",
    };

    public static string Get(string key) => Text.TryGetValue(key, out var text) ? text : null;
}
