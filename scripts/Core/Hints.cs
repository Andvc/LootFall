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
        ["first_mags"] = "Mags shook it firmly. If you hear nothing she didn't, pass: sellers always ask a bit more than it's worth.",
        ["passed"] = "When you pass, they show you what was inside. Worth knowing either way.",
        ["opened"] = "A tick is a good read. A cross is something to listen for next time.",
        ["dayend"] = "Spend what you earn on the shop. A bigger shop brings richer customers, and new sounds.",

        // Each renovation brings one new thing.
        ["tier2"] = "Village folk bring two new sounds. Bottles slosh at a gentle shake, like coins. " +
                    "Paper rustles only at a hard shake, like cloth.",
        ["heard_Bottles"] = "Slosh: a bottle. Delicate, like coins: a clank drowns it out.",
        ["heard_Paper"] = "Rustle: paper. Soft, like cloth: it only sounds at a hard shake.",
        ["tier3"] = "Mages come to the town shop. They bring jewelry, which tinkles at a gentle shake, and magic, " +
                    "which sounds like anything else. A witchlight would show it.",
        ["heard_Jewels"] = "Tinkle: jewelry. Delicate, and worth the most.",
        ["lamp"] = "Witchlight's on: magic glows when it makes a sound. Click a tick again to mark it magic.",
        ["glow"] = "It glowed! That one's enchanted: five times the value. Click its tick again to mark it magic.",
        ["tier4"] = "The Emporium. Mind Silas: he sells gilded iron as gold and jewels, and it sounds just like the real thing. " +
                    "A magnet would catch it.",
        ["magnet"] = "Magnet's on: iron can't move. Metal goes quiet, so nothing drowns out the delicate things. " +
                     "And gilded fakes go quiet too.",

        // Regulars, the first time they come in after the first day.
        ["seller_tam"] = "Tam never shakes anything. Whatever's inside, he doesn't know.",
        ["seller_penn"] = "Penn only shakes gently. Heavy things and soft things make no sound for her.",
        ["seller_hob"] = "Hob shakes hard. The clanking drowns out anything delicate.",
        ["seller_mags"] = "Mags shakes firmly: soft things stay quiet for her, and clanks drown out the delicate ones.",
        ["seller_brask"] = "Brask shakes hard, like Hob. Listen for what's under the clank.",
        ["seller_tobias"] = "Tobias talks things up. He'll claim a sound that isn't there: check every tick on the slip.",
        ["seller_marta"] = "Marta only shakes gently, like Penn. Shake hard yourself.",
        ["seller_wren"] = "Wren shakes gently and never checks for magic.",
        ["seller_orrin"] = "Orrin shakes hard and never checks for magic.",
        ["seller_vex"] = "Vex's butler shakes firmly. Soft things stay quiet for him.",
        ["seller_gull"] = "Gull shakes hard. Listen for what's under the clank.",
        ["seller_pip"] = "Pip shakes gently, then hard. He hears everything. Only magic gets past him.",
        ["seller_silas"] = "Silas. If he says gold, it may be gilded iron. Too cheap to be true, usually.",
    };

    public static string Get(string key) => Text.TryGetValue(key, out var text) ? text : null;
}
