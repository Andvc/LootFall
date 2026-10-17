using System.Collections.Generic;

namespace LootFall.Core;

/// <summary>
/// Uncle Bram's tips. Each one shows the first time its moment comes up (a new tool, a new kind of customer, a new
/// sound), so the game teaches one thing at a time, when it's in front of you.
/// </summary>
public static class Hints
{
    public const string Mentor = "Uncle Bram";

    static readonly Dictionary<string, string> Text = new()
    {
        ["intro"] = "Morning! The stall's yours now. Grab Hob's chest and shake it: drag it around with the mouse, and listen.",
        ["heard"] = "Everything inside rattles on its own. Coins jingle, metal clanks, bottles slosh, cloth just flumps. " +
                    "What you've heard is listed on the right.",
        ["deal"] = "Worth more than he's asking? Copper's about 12g, a bottle 8 to 16, a good coat 20. " +
                   "If it's worth more, buy it. If not, pass. You open what you buy straight away.",
        ["opened"] = "See? Same sounds as everything of its kind, every time. It's all in the ledger now (L).",
        ["dayend"] = "Not bad for a first day. Spend what you earn on the shop. A scale's a good start: " +
                     "a heavy flump is a good coat, a light one is rags.",
        ["haggle"] = "You can haggle now: make one offer below the asking price. If it's enough, it's yours. " +
                     "If not, they'll name a final price.",
        ["scale"] = "Drop the chest on the scale. It reads what's inside: a wool coat weighs three rags, a pouch of copper about a pound.",
        ["peephole"] = "Click the keyhole to peek. You'll only see the one thing nearest it. " +
                       "Shake the chest and peek again to see something else. Two peeks a day.",
        ["lantern"] = "The lantern lens shows the two things nearest the keyhole. Three peeks a day.",
        ["periscope"] = "The periscope shows everything inside. Four peeks a day.",
        ["trumpet"] = "With the ear trumpet you'll hear the differences: a dull jingle is copper, a bright one silver. " +
                      "Iron clanks; steel rings on.",
        ["compass"] = "The mana compass. Click it with a chest on the counter: the needle swings toward magic.",
        ["lodestone"] = "The lodestone pulls at iron and steel. Gold doesn't stick to it. Iron painted gold does.",
        ["hammer"] = "Knock with the hammer. A false bottom sounds hollow: there's something hidden underneath.",
        ["locked"] = "This one's locked. Buy it and you'll pick the lock before you open it. Gently: a crowbar breaks glass.",
        ["tier2"] = "A proper village shop! Soldiers and merchants now, with locked chests and new sounds: " +
                    "silver jingles brighter, steel rings, paper rustles.",
        ["tier3"] = "A town shop! Mages now: bottles that fizz, magic paper and cloth, and jewelry. Listen for the tinkle.",
        ["tier4"] = "The Emporium. Nobles, adventurers and crooks: gold, gems, false bottoms and fakes. Check everything twice.",
        ["family_Paper"] = "That rustle is paper. Letters are worth nothing, a ledger something. Books thump as well.",
        ["family_Jewels"] = "Hear that tiny tinkle? Jewelry. Light as a feather and often worth a fortune.",

        ["seller_hob"] = "Hob prices on a hunch. Sometimes he asks too much, sometimes far too little.",
        ["seller_penn"] = "Widow Penn needs the money. She asks little and takes less.",
        ["seller_mags"] = "Mags prices by how many things rattle. Four rags cost the same as four coats to her.",
        ["seller_tam"] = "Little Tam asks the same few coins for anything. Usually junk. Usually.",
        ["seller_brask"] = "Sergeant Brask prices by weight. Heavy junk is treasure to him, and light silver is nothing. Weigh it.",
        ["seller_pip"] = "Pip knows what's inside and prices it fair. You won't get rich off Pip, unless there's something he can't see.",
        ["seller_marta"] = "Old Marta prices by the lock. A fancy lock on junk, a padlock on treasure: that's your chance.",
        ["seller_wren"] = "Wren thinks magic is priceless and the rest is junk. Plain good things come cheap from her.",
        ["seller_orrin"] = "Magister Orrin prices magic right and the rest as junk. He never haggles.",
        ["seller_vex"] = "Baron Vex asks double. Offer low and he'll come down a long way. Too low and he storms off.",
        ["seller_gull"] = "Captain Gull names one price. Sea salvage: gold and jewels, or barnacles.",
        ["seller_silas"] = "Silas. If he says gold, it's probably iron. If it feels full, it's probably stones. Check.",
    };

    public static string Get(string key) => Text.TryGetValue(key, out var text) ? text : null;
}
