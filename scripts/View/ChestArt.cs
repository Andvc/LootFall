using Godot;
using LootFall.Game;

namespace LootFall.View;

/// <summary>
/// The chest, drawn inked (a stand-in for its final art): an oak box with iron bands and a lock. Its kind sets the wood
/// and the bands (a guardian's is gilded); the rarest thing inside shows only as the glow and the lock's colour, never as
/// a painted rarity (docs/art-direction.md: rarity is code's).
/// </summary>
public static class ChestArt
{
    /// <summary>Draws a chest standing at <paramref name="foot"/> (bottom centre), <paramref name="w"/> wide; the lid
    /// opens by <paramref name="lid"/> (0 shut, 1 thrown back).</summary>
    public static void Draw(CanvasItem c, Vector2 foot, float w, ChestKind kind, int rarity, float lid = 0, float glow = 0, float time = 0)
    {
        float h = w * 0.62f;
        var wood = kind == ChestKind.Guardian ? new Ink.Ramp("#5a2a1e", "#8a4430", "#b8664a") : new Ink.Ramp("#4b3220", "#7b5534", "#a77a4e");
        var band = kind == ChestKind.Guardian ? Ink.Gold : kind == ChestKind.Boss ? Ink.Steel : Ink.Iron;
        float x0 = foot.X - w / 2, x1 = foot.X + w / 2, y1 = foot.Y, y0 = foot.Y - h * 0.62f;
        if (glow > 0 && rarity >= 0)
        {
            var g = Look.Tier(rarity);
            for (int i = 3; i >= 1; i--) c.DrawCircle(new Vector2(foot.X, y0), w * (0.5f + 0.25f * i) * (0.9f + 0.1f * Mathf.Sin(time * 4)), new Color(g, 0.06f * glow * (4 - i)), true, -1, true);
        }
        // Lid: a half-round, hinged at the back; opening rotates it up and back, behind the box.
        float lidH = h * 0.42f;
        var hinge = new Vector2(x0 + w * 0.02f, y0);
        var lidPts = new Vector2[12];
        for (int i = 0; i < 12; i++)
        {
            float t = i / 11f;
            float ang = Mathf.Pi + Mathf.Pi * t;
            var p = new Vector2(foot.X + Mathf.Cos(ang) * (w / 2 - w * 0.01f), y0 + Mathf.Sin(ang) * lidH);
            lidPts[i] = hinge + (p - hinge).Rotated(-lid * 0.95f) * (1 - 0.35f * lid);
        }
        if (lid >= 0.5f) Ink.Shaded(c, lidPts, wood, Ink.Line);
        // Box.
        var box = new[] { new Vector2(x0, y0), new Vector2(x1, y0), new Vector2(x1 - w * 0.03f, y1), new Vector2(x0 + w * 0.03f, y1) };
        Ink.Shaded(c, box, wood, Ink.Line);
        if (lid > 0.05f && rarity >= 0)
        {
            // Light spilling out of the open chest.
            var g = Look.Tier(rarity);
            c.DrawColoredPolygon(new[] { new Vector2(x0 + w * 0.08f, y0), new Vector2(x1 - w * 0.08f, y0), new Vector2(x1 + w * 0.4f, y0 - h * 2.4f), new Vector2(x0 - w * 0.4f, y0 - h * 2.4f) },
                new Color(g, 0.18f * lid));
            c.DrawColoredPolygon(new[] { new Vector2(x0 + w * 0.06f, y0 - 3), new Vector2(x1 - w * 0.06f, y0 - 3), new Vector2(x1 - w * 0.06f, y0 + 6), new Vector2(x0 + w * 0.06f, y0 + 6) },
                g.Lightened(0.5f));
        }
        if (lid < 0.5f) Ink.Shaded(c, lidPts, wood, Ink.Line);
        // Bands on the box and the lid, and the lock in the colour of what's inside.
        foreach (float fx in new[] { 0.18f, 0.82f })
        {
            float bx = x0 + w * fx;
            Ink.Poly(c, new[] { new Vector2(bx - w * 0.05f, y0), new Vector2(bx + w * 0.05f, y0), new Vector2(bx + w * 0.05f, y1), new Vector2(bx - w * 0.05f, y1) }, band.Mid, 2.5f);
        }
        var lockC = rarity >= 0 ? Look.Tier(rarity) : band.Light;
        var lockAt = new Vector2(foot.X, y0 + h * 0.05f);
        if (lid < 0.3f)
        {
            Ink.Poly(c, new[] { lockAt + new Vector2(-w * 0.08f, -h * 0.12f), lockAt + new Vector2(w * 0.08f, -h * 0.12f), lockAt + new Vector2(w * 0.08f, h * 0.14f), lockAt + new Vector2(-w * 0.08f, h * 0.14f) }, band.Light, 2.5f);
            c.DrawCircle(lockAt + new Vector2(0, h * 0.01f), w * 0.035f, lockC, true, -1, true);
        }
    }
}
