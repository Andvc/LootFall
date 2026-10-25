namespace LootFall.Game;

/// <summary>
/// The sensible active player, for the fast-forward (main's --hours), the bot's head start and the game check: opens
/// every chest, spends gold and shards where they're worth most every ten seconds, and resets by the simulator's rule
/// once a minute.
/// </summary>
public static class SensiblePlayer
{
    /// <summary>Plays <paramref name="seconds"/> in one-second steps.</summary>
    public static void Play(Session s, double seconds)
    {
        double end = s.Time + seconds;
        while (s.Time < end - 1e-6)
        {
            s.Tick(1.0);
            s.Events.Clear();
            if (s.Pile.Waiting.Count > 0) s.OpenAll();
            if ((int)s.Time % 10 == 0)
            {
                if (s.Unlocked(Feature.Forge)) s.ForgeAll();
                if (s.Unlocked(Feature.Ranks)) s.RankAll();
            }
            if ((int)s.Time % 60 == 0) s.AutoPrestige(transcend: true);
        }
    }
}
