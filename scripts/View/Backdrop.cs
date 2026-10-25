using Godot;
using LootFall.Game;

namespace LootFall.View;

/// <summary>
/// The lane's backdrop: the realm's painted far layer (scrolling slowly, mirrored to tile), a ground band and dark
/// foreground tufts drawn in code in the realm's sampled colours (docs/art-direction.md, "Backdrops"). Changing realm
/// crossfades to the next.
/// </summary>
public partial class Backdrop : Node2D
{
    const float FarSpeed = 0.15f, GroundSpeed = 1f, FrontSpeed = 1.3f;
    const float GroundTop = Look.Ground - 26;

    Node2D _far, _oldFar;
    readonly Painter _ground, _front;
    RealmColors _colors;
    string _realm;
    float _scroll;

    public Backdrop()
    {
        _ground = new Painter(DrawGround) { ZIndex = 1 };
        _front = new Painter(DrawFront) { ZIndex = 40 };
        AddChild(_ground);
        AddChild(_front);
    }

    /// <summary>Shows the realm of this depth (a crossfade if it changed).</summary>
    public void SetDepth(int depth)
    {
        var realm = Bestiary.Realm(depth);
        if (realm.Key == _realm) return;
        _realm = realm.Key;
        _colors = Assets.Realm(realm.Key);
        // A realm come round again is darker and colder.
        float darken = Mathf.Min(0.45f, Bestiary.Cycle(depth) * 0.18f);
        _oldFar = _far;
        float k = 0.86f - darken;
        _far = new Node2D { ZIndex = 0, Modulate = new Color(k, k, k + darken * 0.3f, 0) };
        var tex = Assets.Backdrop(realm.Key);
        for (int i = 0; i < 2; i++)
            _far.AddChild(new Sprite2D { Texture = tex, Centered = false, Position = new Vector2(i * 1920 + (i == 1 ? 1920 : 0), 0), Scale = new Vector2(i == 1 ? -1 : 1, 1) });
        AddChild(_far);
        MoveChild(_far, 0);
        var t = CreateTween().SetParallel();
        t.TweenProperty(_far, "modulate:a", 1f, _oldFar == null ? 0.01f : 1.2f);
        if (_oldFar != null)
        {
            var old = _oldFar;
            t.TweenProperty(old, "modulate:a", 0f, 1.2f);
            t.Chain().TweenCallback(Callable.From(old.QueueFree));
        }
        _ground.QueueRedraw();
        _front.QueueRedraw();
    }

    /// <summary>Moves the world past the knight (he walks; the lane comes to him).</summary>
    public void Scroll(float dx)
    {
        _scroll += dx;
        if (_far != null)
        {
            float x = -(_scroll * FarSpeed % 3840);
            _far.Position = new Vector2(x, 0);
            // The mirrored pair repeats every 3840 px; a second copy follows it.
            if (_far.GetChildCount() == 2 && x < -1920) _far.Position = new Vector2(x + 3840, 0);
        }
        _ground.QueueRedraw();
        _front.QueueRedraw();
    }

    void DrawGround(Painter p)
    {
        if (_colors == null) return;
        var g = _colors.Ground;
        var top = g.Darkened(0.25f);
        var bottom = g.Darkened(0.7f);
        p.DrawPolygon(new[] { new Vector2(0, GroundTop), new Vector2(1920, GroundTop), new Vector2(1920, Look.LaneH + 40), new Vector2(0, Look.LaneH + 40) },
            new[] { top, top, bottom, bottom });
        // A worn path line where the feet go, and pebbles that scroll with the lane.
        p.DrawLine(new Vector2(0, GroundTop), new Vector2(1920, GroundTop), g.Lightened(0.08f), 3, true);
        p.DrawRect(new Rect2(0, Look.Ground - 6, 1920, 12), new Color(0, 0, 0, 0.12f));
        float off = -(_scroll * GroundSpeed % 160);
        for (int i = -1; i < 14; i++)
        {
            float x = off + i * 160;
            p.DrawColoredPolygon(Ink.Ellipse(new Vector2(x + 30, Look.Ground + 18), 10, 4, 10), g.Darkened(0.45f));
            p.DrawColoredPolygon(Ink.Ellipse(new Vector2(x + 102, Look.Ground + 40), 16, 5, 10), g.Darkened(0.5f));
            p.DrawColoredPolygon(Ink.Ellipse(new Vector2(x + 70, GroundTop + 8), 6, 2.5f, 8), g.Lightened(0.05f));
        }
    }

    void DrawFront(Painter p)
    {
        if (_colors == null) return;
        // Dark tufts along the bottom edge, faster than the lane: depth without clutter.
        var ink = new Color(Look.Ink, 0.88f);
        float off = -(_scroll * FrontSpeed % 420);
        for (int i = -1; i < 7; i++)
        {
            float x = off + i * 420;
            p.DrawColoredPolygon(new[]
            {
                new Vector2(x, Look.LaneH), new Vector2(x + 16, Look.LaneH - 34), new Vector2(x + 26, Look.LaneH - 8), new Vector2(x + 40, Look.LaneH - 52),
                new Vector2(x + 52, Look.LaneH - 10), new Vector2(x + 66, Look.LaneH - 30), new Vector2(x + 80, Look.LaneH),
            }, ink);
            p.DrawColoredPolygon(new[]
            {
                new Vector2(x + 230, Look.LaneH), new Vector2(x + 250, Look.LaneH - 24), new Vector2(x + 290, Look.LaneH - 30), new Vector2(x + 316, Look.LaneH),
            }, ink);
        }
    }
}
