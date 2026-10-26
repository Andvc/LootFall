using System;
using System.Collections.Generic;
using Godot;
using LootFall.Progression;

namespace LootFall.View;

/// <summary>
/// One view of the knight drawn through the figure shader (assets/shaders/figure.gdshader): the part of its picture a
/// page shows, where the paper doll puts it, with what's worn painted over each body site and what's lit. The doll fills
/// in <see cref="Lut"/> (a column per site) and calls <see cref="Commit"/>.
/// - Pieces' own pictures (<see cref="SetArt"/>) are drawn over the picture in layer order, and the parts of them that
///   hang behind him under it. On a plate or a close-up they're clipped to the panel; on the whole figure they may
///   reach past it (wings, a sword beside him).
/// - Where a piece still painted by its layer is worn over one with a picture, a pass of the shader at that layer
///   (<see cref="FillPass"/>) paints its sites again among the pictures, over those below it.
/// - The doll draws the ink lines round what's painted (<see cref="DrawInks"/>) with the paint: in <see cref="Inks"/>
///   under the pictures, or with its pass. What's lit goes on top, in <see cref="Lines"/>. Both are clipped to the
///   panel and not shaded.
/// </summary>
public partial class FigurePanel : Control
{
    static Shader _shader;
    readonly MapView _v;
    readonly ShaderMaterial _material;
    /// <summary>The table, a column per site and six rows (see the shader), as RGBA bytes.</summary>
    public const int LutRows = 6;
    public readonly byte[] Lut = new byte[256 * LutRows * 4];
    readonly Image _lutImage = Image.CreateEmpty(256, LutRows, false, Image.Format.Rgba8);
    readonly ImageTexture _lut;
    public readonly Canvas Lines = new() { MouseFilter = MouseFilterEnum.Ignore, ClipContents = true };
    public readonly Canvas Inks = new() { MouseFilter = MouseFilterEnum.Ignore, ClipContents = true };
    /// <summary>Draws the ink lines of what's painted at a layer's pass (-1: under the pictures).</summary>
    public Action<Canvas, int> DrawInks;
    // The pieces' pictures: the parts behind him (under the panel's own picture), and the rest over it.
    readonly Control _behind = new() { MouseFilter = MouseFilterEnum.Ignore, ShowBehindParent = true };
    readonly Control _front = new() { MouseFilter = MouseFilterEnum.Ignore };
    readonly List<ArtItem> _items = new();
    readonly List<FillPass> _passes = new();
    Rect2 _dest, _src;
    float _alpha;

    public FigurePanel(MapView v)
    {
        _v = v;
        _shader ??= GD.Load<Shader>("res://assets/shaders/figure.gdshader");
        _lut = ImageTexture.CreateFromImage(_lutImage);
        _material = NewMaterial(-1);
        Material = _material;
        AddChild(_behind);
        AddChild(_front);
        _front.AddChild(Inks);
        Inks.OnDraw = c => DrawInks?.Invoke(c, -1);
        AddChild(Lines);
    }

    /// <summary>The figure shader with this panel's map and table, for the picture or a layer's pass.</summary>
    ShaderMaterial NewMaterial(int layer)
    {
        var m = new ShaderMaterial { Shader = _shader };
        m.SetShaderParameter("sites", _v.SitesTexture);
        m.SetShaderParameter("lut", _lut);
        m.SetShaderParameter("sites_size", _v.Size);
        m.SetShaderParameter("pass_layer", layer);
        return m;
    }

    public MapView View => _v;
    public Texture2D LutTexture => _lut;
    /// <summary>Pictures are clipped to the panel on a plate or a close-up (what's past its edge isn't drawn there).</summary>
    public bool ClipArt => _v.Closeup || _v.PlateOf != null;
    public Rect2 Dest => _dest;

    /// <summary>One cell of the table.</summary>
    public void Set(int site, int row, Color c)
    {
        int i = (row * 256 + site) * 4;
        Lut[i] = (byte)Mathf.Clamp(Mathf.RoundToInt(c.R * 255), 0, 255);
        Lut[i + 1] = (byte)Mathf.Clamp(Mathf.RoundToInt(c.G * 255), 0, 255);
        Lut[i + 2] = (byte)Mathf.Clamp(Mathf.RoundToInt(c.B * 255), 0, 255);
        Lut[i + 3] = (byte)Mathf.Clamp(Mathf.RoundToInt(c.A * 255), 0, 255);
    }

    /// <summary>Sends the table to the shader.</summary>
    public void Commit()
    {
        _lutImage.SetData(256, LutRows, false, Image.Format.Rgba8, Lut);
        _lut.Update(_lutImage);
    }

    /// <summary>A rectangle in the view's pixels on screen, where the panel shows the view now.</summary>
    public Rect2 MapRect(Rect2 r)
    {
        if (_src.Size.X <= 0) return new Rect2();
        var k = _dest.Size / _src.Size;
        return new Rect2(_dest.Position + (r.Position - _src.Position) * k, r.Size * k);
    }

    /// <summary>Where to draw: the screen rectangle, and the part of the picture in it (its pixels); whether its lines
    /// need drawing again.</summary>
    public void Show(Rect2 dest, Vector2 srcPos, Vector2 srcSize, float alpha, float time, bool lines)
    {
        bool moved = dest != _dest || _src.Position != srcPos || _src.Size != srcSize;
        _dest = dest;
        _src = new Rect2(srcPos, srcSize);
        _alpha = alpha;
        _material.SetShaderParameter("fade", alpha);
        _material.SetShaderParameter("time", time);
        _behind.Size = _front.Size = Size;
        Place(Lines, lines);
        Place(Inks, lines);
        foreach (var item in _items)
        {
            item.PanelAlpha = alpha;
            if (moved) item.QueueRedraw();
        }
        foreach (var pass in _passes)
        {
            if (!pass.Visible) continue;
            pass.Sync(alpha, time);
            Place(pass.Inks, lines);
            if (moved) pass.QueueRedraw();
        }
        QueueRedraw();
    }

    void Place(Canvas c, bool redraw)
    {
        c.Position = _dest.Position;
        c.Size = _dest.Size;
        c.Visible = _alpha > 0.02f;
        if (redraw) c.QueueRedraw();
    }

    /// <summary>What to draw over the picture, in order (inner layers first): the pieces' pictures, each with how it
    /// looks now, and the layers' passes among them (<see cref="ArtDraw.Pass"/>).</summary>
    public void SetArt(IReadOnlyList<ArtDraw> draws)
    {
        int items = 0, passes = 0;
        foreach (var d in draws)
        {
            Control node;
            if (d.Art == null)
            {
                if (passes == _passes.Count) _passes.Add(new FillPass(this, NewMaterial(d.Pass)) { MouseFilter = MouseFilterEnum.Ignore });
                var pass = _passes[passes++];
                pass.Layer = d.Pass;
                pass.Sync(_alpha, 0);
                node = pass;
            }
            else
            {
                if (items == _items.Count) _items.Add(new ArtItem(this) { MouseFilter = MouseFilterEnum.Ignore });
                var item = _items[items++];
                item.PanelAlpha = _alpha;
                item.Apply(d);
                node = item;
            }
            var parent = d.Behind ? _behind : _front;
            if (node.GetParent() != parent)
            {
                node.GetParent()?.RemoveChild(node);
                parent.AddChild(node);
            }
            parent.MoveChild(node, -1);
            node.Size = Size;
            node.Visible = true;
        }
        // What's left over is kept (hidden) in the panel, so it's freed with it.
        for (int i = items; i < _items.Count; i++)
        {
            _items[i].Visible = false;
            _items[i].Tex = null;
        }
        for (int i = passes; i < _passes.Count; i++) _passes[i].Visible = false;
        Inks.QueueRedraw();
        foreach (var pass in _passes)
            if (pass.Visible) Place(pass.Inks, true);
    }

    /// <summary>The part of the picture the panel shows, where it shows it (on this panel or one of its passes).</summary>
    public void DrawPicture(CanvasItem on)
    {
        if (_v.Texture == null || _alpha <= 0.01f || _dest.Size.X < 1) return;
        // The picture's own pixels may be fewer than its map's (the map is the picture's size): scale the source.
        var k = _v.Texture.GetSize() / _v.Size;
        on.DrawTextureRectRegion(_v.Texture, _dest, new Rect2(_src.Position * k, _src.Size * k));
    }

    public override void _Draw() => DrawPicture(this);

    /// <summary>Lets go of the shaders (before quitting).</summary>
    public static void Release()
    {
        _shader = null;
        ArtItem.Release();
    }
}

/// <summary>A pass of the figure shader at one layer, among the pieces' pictures: the sites painted by that layer, over
/// the pictures below it, with their ink lines.</summary>
public partial class FillPass : Control
{
    readonly FigurePanel _panel;
    readonly ShaderMaterial _mat;
    int _layer = -2;
    public readonly Canvas Inks = new() { MouseFilter = MouseFilterEnum.Ignore, ClipContents = true };

    public FillPass(FigurePanel panel, ShaderMaterial mat)
    {
        _panel = panel;
        _mat = mat;
        Material = mat;
        AddChild(Inks);
        Inks.OnDraw = c => panel.DrawInks?.Invoke(c, _layer);
    }

    public int Layer
    {
        get => _layer;
        set
        {
            if (value == _layer) return;
            _layer = value;
            _mat.SetShaderParameter("pass_layer", value);
            QueueRedraw();
        }
    }

    public void Sync(float alpha, float time)
    {
        _mat.SetShaderParameter("fade", alpha);
        _mat.SetShaderParameter("time", time);
    }

    public override void _Draw() => _panel.DrawPicture(this);
}

/// <summary>How to draw one piece's picture on a panel: the picture, whether it's the part behind him, whether his hair
/// lies over it, where it lies in the panel's view pixels, the era's metal, and its dimming, light and faintness.</summary>
public struct ArtDraw
{
    public PieceArt Art;
    public bool Behind, UnderHair;
    public Rect2 ViewRect;
    public Ink.Ramp Era;
    public float Dim, Fade;
    public Color Glow;
    /// <summary>Without a picture: the layer whose painted sites are drawn again here.</summary>
    public int Pass;

    public static ArtDraw FillPass(Layer layer) => new() { Pass = (int)layer };
}

/// <summary>One piece's picture over a panel, through assets/shaders/piece.gdshader.</summary>
public partial class ArtItem : Control
{
    static Shader _shader;
    readonly FigurePanel _panel;
    readonly ShaderMaterial _mat;
    public Texture2D Tex;
    Rect2 _viewRect;
    float _fade = 1, _panelAlpha = 1, _dim = -1, _metal = -1;
    int _hair = -1;
    Color _glow = new(0, 0, 0, -1), _era;

    public ArtItem(FigurePanel panel)
    {
        _panel = panel;
        _shader ??= GD.Load<Shader>("res://assets/shaders/piece.gdshader");
        _mat = new ShaderMaterial { Shader = _shader };
        _mat.SetShaderParameter("sites", panel.View.SitesTexture);
        _mat.SetShaderParameter("lut", panel.LutTexture);
        _mat.SetShaderParameter("sites_size", panel.View.Size);
        _mat.SetShaderParameter("body", panel.View.Texture);
        Material = _mat;
    }

    public float PanelAlpha
    {
        set
        {
            if (Mathf.IsEqualApprox(value, _panelAlpha)) return;
            _panelAlpha = value;
            _mat.SetShaderParameter("fade", _fade * _panelAlpha);
        }
    }

    public void Apply(ArtDraw d)
    {
        var tex = d.Behind ? d.Art.Behind : d.Art.Texture;
        bool redraw = tex != Tex || d.ViewRect != _viewRect;
        Tex = tex;
        if (d.ViewRect != _viewRect)
        {
            _viewRect = d.ViewRect;
            _mat.SetShaderParameter("view_rect", new Vector4(_viewRect.Position.X, _viewRect.Position.Y, _viewRect.Size.X, _viewRect.Size.Y));
        }
        float metal = d.Art.Metal ? 1 : 0;
        if (!Mathf.IsEqualApprox(metal, _metal))
        {
            _metal = metal;
            _mat.SetShaderParameter("metal", metal);
        }
        int hair = d.UnderHair && _panel.View.Index.TryGetValue("hair", out int id) ? id : 0;
        if (hair != _hair)
        {
            _hair = hair;
            _mat.SetShaderParameter("hair", hair);
        }
        if (d.Era.Mid != _era)
        {
            _era = d.Era.Mid;
            _mat.SetShaderParameter("era_shadow", d.Era.Shadow);
            _mat.SetShaderParameter("era_mid", d.Era.Mid);
            _mat.SetShaderParameter("era_light", d.Era.Light);
            _mat.SetShaderParameter("era_hi", d.Era.Hi);
        }
        if (!Mathf.IsEqualApprox(d.Dim, _dim))
        {
            _dim = d.Dim;
            _mat.SetShaderParameter("dim", d.Dim);
        }
        if (d.Glow != _glow)
        {
            _glow = d.Glow;
            _mat.SetShaderParameter("glow", d.Glow);
        }
        if (!Mathf.IsEqualApprox(d.Fade, _fade))
        {
            _fade = d.Fade;
            _mat.SetShaderParameter("fade", _fade * _panelAlpha);
        }
        if (redraw) QueueRedraw();
    }

    public override void _Draw()
    {
        if (Tex == null) return;
        var r = _panel.MapRect(_viewRect);
        if (r.Size.X < 0.5f) return;
        if (!_panel.ClipArt)
        {
            DrawTextureRect(Tex, r, false);
            return;
        }
        var clip = r.Intersection(_panel.Dest);
        if (clip.Size.X < 0.5f || clip.Size.Y < 0.5f) return;
        var ts = Tex.GetSize();
        var src = new Rect2((clip.Position - r.Position) / r.Size * ts, clip.Size / r.Size * ts);
        DrawTextureRectRegion(Tex, clip, src);
    }

    public static void Release() => _shader = null;
}
