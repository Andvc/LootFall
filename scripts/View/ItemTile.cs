using System;
using Godot;
using LootFall.Game;
using LootFall.Progression;

namespace LootFall.View;

/// <summary>
/// An item's icon in three layers (docs/art-direction.md, "Items and icons"): its family's neutral picture, a rarity
/// frame and face drawn by code (plain, ornate, crowned or radiant by band), and state overlays (the tier numeral, a NEW
/// badge for a first find, an upgrade arrow, affix dots, forge, rank and mastery marks).
/// </summary>
public static class ItemTile
{
    /// <summary>The art tier an item's picture is drawn at: its era's (Ink.ArtTier).</summary>
    public static int Tier(Item it) => it == null ? 0 : Ink.ArtTier(Hero.Era(it.Ilvl));

    /// <summary>Draws an item tile in <paramref name="r"/>. <paramref name="rarity"/> -1 draws an empty slot; <paramref name="tier"/>
    /// picks the family's picture for the item's era (<see cref="Tier"/>).</summary>
    public static void Draw(CanvasItem c, Rect2 r, string family, int rarity, int affixes = 0, bool isNew = false, bool upgrade = false,
        int forge = 0, int rank = 0, int mastery = 0, bool locked = false, bool numeral = true, float time = 0, int tier = 0)
    {
        float s = r.Size.X;
        var face = rarity >= 0 ? Look.TierFace(rarity) : new Color("#15111d");
        var edge = rarity >= 0 ? Look.Tier(rarity) : Look.Edge;
        int band = rarity >= 0 ? Look.Band(rarity) : -1;
        float radius = Mathf.Max(4, s * 0.12f);
        // Glow behind band C and D.
        if (band >= 2)
        {
            float pulse = 0.6f + 0.4f * Mathf.Sin(time * 3 + r.Position.X * 0.01f);
            c.DrawStyleBox(Box(new Color(edge, 0.22f * pulse), Colors.Transparent, radius + s * 0.08f, 0), r.Grow(s * 0.08f));
        }
        c.DrawStyleBox(Box(face, Look.Ink, radius, Mathf.Max(2, s * 0.035f)), r);
        // The rarity edge: thicker and doubled as the bands rise.
        float w = band switch { -1 => 1.5f, 0 => s * 0.03f, 1 => s * 0.045f, 2 => s * 0.05f, _ => s * 0.06f };
        c.DrawStyleBox(Box(Colors.Transparent, rarity >= 0 ? edge : Look.Edge, radius - 1, w), r.Grow(-s * 0.03f));
        if (band >= 1) c.DrawStyleBox(Box(Colors.Transparent, edge.Lightened(0.35f), radius - 3, Mathf.Max(1, s * 0.012f)), r.Grow(-s * 0.03f - w - 2));
        if (band >= 2)
        {
            // Crowned (C) and radiant (D): points at the top corners and the middle of the top edge.
            foreach (float fx in new[] { 0.5f, 0.15f, 0.85f })
            {
                var top = new Vector2(r.Position.X + s * fx, r.Position.Y);
                float k = fx == 0.5f ? 1f : 0.6f;
                c.DrawColoredPolygon(new[] { top + new Vector2(-s * 0.07f * k, s * 0.02f), top + new Vector2(0, -s * 0.09f * k), top + new Vector2(s * 0.07f * k, s * 0.02f) }, edge);
            }
        }
        if (band == 3)
        {
            // Radiant: a slowly turning glint.
            var at = r.Position + new Vector2(s * 0.84f, s * 0.16f);
            Ink.Glint(c, at, s * (0.1f + 0.03f * Mathf.Sin(time * 4)), new Color(1, 1, 1, 0.85f));
        }
        // The picture.
        if (family != null)
        {
            var tex = Assets.Icon(family, tier);
            if (tex != null)
            {
                var inner = r.Grow(-s * 0.12f);
                var tint = rarity >= 0 ? Colors.White : new Color(0.45f, 0.42f, 0.5f, 0.7f);
                if (locked) tint = new Color(0.2f, 0.18f, 0.25f, 0.6f);
                c.DrawTextureRect(tex, inner, false, tint);
            }
        }
        if (locked)
        {
            var at = r.GetCenter();
            Ink.Poly(c, new[] { at + new Vector2(-s * 0.16f, -s * 0.02f), at + new Vector2(s * 0.16f, -s * 0.02f), at + new Vector2(s * 0.16f, s * 0.22f), at + new Vector2(-s * 0.16f, s * 0.22f) }, Ink.Iron.Mid, 2);
            c.DrawArc(at + new Vector2(0, -s * 0.04f), s * 0.1f, Mathf.Pi, Mathf.Tau, 12, Look.Ink, Mathf.Max(2, s * 0.04f), true);
        }
        if (s < 40) return;
        // Overlays: affix dots along the bottom, the numeral at the top left, NEW and the upgrade arrow at the top right.
        for (int i = 0; i < Math.Min(affixes, 12); i++)
            c.DrawCircle(r.Position + new Vector2(s * 0.16f + i * s * 0.065f, s * 0.9f), Mathf.Max(1.5f, s * 0.025f), edge.Lightened(0.3f), true, -1, true);
        if (numeral && rarity >= 0 && s >= 56)
            c.DrawString(Look.UiBold, r.Position + new Vector2(s * 0.07f, s * 0.2f), Look.Numeral(rarity), HorizontalAlignment.Left, -1, (int)(s * 0.16f), Look.TierText(rarity));
        if (isNew)
        {
            var at = r.Position + new Vector2(s * 0.62f, s * 0.04f);
            c.DrawStyleBox(Box(Look.Gold, Look.Ink, 4, 2), new Rect2(at, new Vector2(s * 0.36f, s * 0.18f)));
            c.DrawString(Look.Strong, at + new Vector2(s * 0.04f, s * 0.15f), "NEW", HorizontalAlignment.Left, -1, Math.Max(10, (int)(s * 0.14f)), Look.Ink);
        }
        if (upgrade)
        {
            var at = r.Position + new Vector2(s * 0.84f, s * 0.74f);
            c.DrawColoredPolygon(new[] { at + new Vector2(0, -s * 0.12f), at + new Vector2(s * 0.1f, 0), at + new Vector2(s * 0.04f, 0), at + new Vector2(s * 0.04f, s * 0.1f), at + new Vector2(-s * 0.04f, s * 0.1f), at + new Vector2(-s * 0.04f, 0), at + new Vector2(-s * 0.1f, 0) }, Look.Good);
        }
        if (forge > 0 && s >= 56)
            c.DrawString(Look.Numbers, r.Position + new Vector2(s * 0.08f, s * 0.84f), $"+{forge}", HorizontalAlignment.Left, -1, (int)(s * 0.15f), Look.Gold);
        if (rank > 0 && s >= 56)
            for (int i = 0; i < Math.Min(5, rank); i++) Ink.Glint(c, r.Position + new Vector2(s * (0.9f - i * 0.1f), s * 0.8f), s * 0.05f, Look.Shards);
    }

    static StyleBoxFlat Box(Color bg, Color border, float radius, float width)
    {
        var b = new StyleBoxFlat { BgColor = bg, BorderColor = border, AntiAliasing = true, DrawCenter = bg.A > 0 };
        b.SetCornerRadiusAll((int)radius);
        b.SetBorderWidthAll((int)Math.Round(width));
        return b;
    }
}
