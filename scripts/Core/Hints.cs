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
        ["slip"] = "The slip adds up what you've ticked. If it's worth more than they ask, buy it.",
        ["first_penn"] = "Penn only shook it gently and heard nothing. Try every strength yourself: gentle, firm, hard.",
        ["heard_Cloth"] = "Flump: cloth. Soft things only make a sound when you shake hard.",
        ["first_hob"] = "Hob shook it hard and heard a clank. Shake it yourself, at every strength.",
        ["heard_Metal"] = "Clank: metal. It only moves at a firm shake, and while it clanks you can't hear anything delicate. " +
                          "Shake gently to hear past it.",
        ["first_mags"] = "Mags counts the rattles at a firm shake and charges the same for each. If you hear nothing she " +
                         "didn't, pass: sellers always ask a bit more than it's worth.",
        ["passed"] = "When you pass, they show you what was inside. Worth knowing either way.",
        ["opened"] = "A tick is a good read. A cross is something to listen for next time.",
        ["dayend"] = "Spend what you earn on the shop. A bigger shop brings richer customers, and new sounds.",

        // From the second day: chest colours and the ways people trade.
        ["colours"] = "Look at the chest's colour: wood, green, blue, purple. It tells you how good things inside tend to be. " +
                      "Only tends to: you'll get surprises both ways.",
        ["quality"] = "See? Not everything matches its chest. Some come out better, some worse. On average, the slip's about right.",
        ["fixed"] = "This one names a price. Take it or leave it.",
        ["haggle"] = "This one haggles: they ask high. Use the slider to make one offer. They'll take it, name a final price, " +
                     "or storm off if you insult them.",
        ["offer"] = "This one wants you to name a price. Offer what it's worth to you, less a little. You get two tries.",
        ["try_again"] = "Too low. One more try, then they'll go.",
        ["keep"] = "Something grand? You can keep it for display instead of selling it. A good exhibition brings more customers, " +
                   "and better ones. Magic things count double.",
        ["kept"] = "On display. You'll see it on the shelf behind the counter.",
        ["exhibition"] = "Your exhibition. Click anything in it to sell it if you need the money.",

        // Each renovation brings one new thing.
        ["tier2"] = "Village folk bring two new sounds. Bottles slosh at a gentle shake, like coins. " +
                    "Paper rustles only at a hard shake, like cloth. And some chests are locked, or have a false bottom.",
        ["locked"] = "Locked. Buy it and you'll pick the lock before you open it. A crowbar's quicker, but it breaks glass.",
        ["dropped"] = "Dropped! A solid thud is a plain chest. A hollow boom means a false bottom: something hidden nobody's priced.",
        ["hollow"] = "Hear that boom? A false bottom. Tick it on your slip: whatever's in there, nobody's priced it.",
        ["broke"] = "Glass breaks when you drop it. If you heard a slosh, think twice before dropping.",
        ["heard_Bottles"] = "Slosh: a bottle. Delicate, like coins: a clank drowns it out.",
        ["heard_Paper"] = "Rustle: paper. Soft, like cloth: it only sounds at a hard shake.",
        ["tier3"] = "The town shop: jewelry, which tinkles at a gentle shake, and mages, whose things are often magic. " +
                    "Magic sounds like anything else. A witchlight would show it.",
        ["heard_Jewels"] = "Tinkle: jewelry. Delicate, and worth the most.",
        ["lamp"] = "The witchlight's lit, and it stays lit: magic glows when it makes a sound. Click a tick again to mark it magic.",
        ["glow"] = "It glowed! That one's enchanted: four times the value. Click its tick again to mark it magic.",
        ["tier4"] = "The Emporium. Nobles, adventurers and a crook. Mind Silas: he sells gilded iron as gold and jewels, " +
                    "and it sounds just like the real thing. A magnet would catch it.",
        ["magnet"] = "Magnet's on: iron can't move. Metal goes quiet, so nothing drowns out the delicate things. " +
                     "And gilded fakes go quiet too.",

        // Regulars, the first time they come in after the first day: one habit each, and what it's good for.
        ["seller_tam"] = "Tam never shakes anything and has no idea what it's worth. Name a few coins. Shake it first, mind: " +
                         "his chests are sometimes empty.",
        ["seller_penn"] = "Penn only shakes gently. Heavy things and soft things make no sound for her.",
        ["seller_hob"] = "Hob shakes hard, and always asks about double. Offer him about half.",
        ["seller_mags"] = "Mags counts the rattles and charges the same for each, whatever's rattling. A clank costs what a jingle " +
                          "does: buy when the rattles are good ones.",
        ["seller_brask"] = "Brask goes by weight: heavy means valuable to him. Metal's heavy and cheap; bottles and paper are light. " +
                           "A light chest of good things is a bargain. And his lockboxes are nearly always locked.",
        ["seller_tobias"] = "Tobias talks things up. He'll claim a sound that isn't there: check every tick on the slip.",
        ["seller_marta"] = "Marta's chests are old family ones: false bottoms are common. But she hates you dropping them. " +
                           "Drop one and it's half again.",
        ["seller_wick"] = "Granny Wick sews. She shakes firmly, never hard, and won't have you do it either: half again if you do. " +
                          "Her chests are full of cloth she never hears. Trust it, or pay to find out.",
        ["seller_wren"] = "Wren can't tell what's in a chest, but she feels magic. Name a low price and she'll take it... " +
                          "unless something's enchanted. Then she wants more, and that tells you something.",
        ["seller_orrin"] = "Orrin's prices come from his dreams, not his chests. Listen, add it up, and buy when the dream's cheap.",
        ["seller_grit"] = "Grit's a miner. He knows metal. Everything else he prices at half... except jewelry, which he " +
                          "thinks is all diamonds. Haggle him down, but mind the tinkles.",
        ["seller_zara"] = "Madame Zara prices by the chest's colour, and feels magic, so that's priced in. Her trick boxes often " +
                          "hide a false bottom she doesn't price: drop it and listen. But don't shake it hard.",
        ["seller_vex"] = "Baron Vex never looks inside: he prices by the colour, and gives you twelve seconds. His things are " +
                         "often better than their colour. Shake gently, then hard, and decide.",
        ["seller_gull"] = "Gull always swears there's a smuggler's false bottom, and prices it in. Drop it: no boom, no false " +
                          "bottom. She haggles.",
        ["seller_pip"] = "Pip listens gently, then hard: she hears everything. Only magic and false bottoms get past her, and " +
                         "she has plenty of both.",
        ["seller_ermine"] = "Lady Ermine knows only her jewelry, at what she paid for it. Whatever else the maid packed comes free. " +
                            "Some of her jewels are paste, and a magnet would show it... but use one and it's half again.",
        ["seller_silas"] = "Silas. If he says gold, it may be gilded iron. Too cheap to be true, usually.",
    };

    public static string Get(string key) => Text.TryGetValue(key, out var text) ? text : null;
}
