namespace LootFall.Core;

/// <summary>Player-facing text for clues.</summary>
public static class Describe
{
    public static string Shake(Chest chest)
    {
        string main = (chest.DominantSound, chest.SoundsFine) switch
        {
            (Sound.Jingle, true) => "A rich, heavy jingle. Gold, maybe?",
            (Sound.Jingle, false) => "A tinny jingle. Copper, probably.",
            (Sound.Clink, true) => "A pure, bell-like ring of fine crystal. Fragile.",
            (Sound.Clink, false) => "Cheap glassy clinking. Fragile.",
            (Sound.Clank, true) => "A deep, ringing clank. Good steel.",
            (Sound.Clank, false) => "A rattly clank, like old scrap.",
            (Sound.Thud, true) => "A dense, solid thud. Something carved, or ancient.",
            (Sound.Thud, false) => "A hollow thud. Wood, or plain iron.",
            (Sound.Rustle, true) => "The crisp rustle of fine paper or silk.",
            _ => "A limp rustle of old cloth.",
        };
        if (chest.HasFragile && chest.DominantSound != Sound.Clink)
            main += " Beneath it, a faint glassy clink.";
        main += chest.ManyItems ? " Lots of things shift around." : " Only a couple of things slide around.";
        return main;
    }

    public static string Weigh(Chest chest) =>
        $"The scale settles at {chest.Weight:0.0} kg. (Empty, a chest like this weighs " +
        $"{ChestGenerator.EmptyWeightMin:0.0}-{ChestGenerator.EmptyWeightMax:0.0} kg.)";

    public static string Lock(Chest chest) =>
        $"{chest.Lock.Name}: {chest.Lock.Description} ({chest.Lock.Pins} pins)";
}
