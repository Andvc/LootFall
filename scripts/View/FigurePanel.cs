using Godot;

namespace LootFall.View;

/// <summary>
/// One view of the knight drawn through the figure shader (assets/shaders/figure.gdshader): the part of its picture a
/// page shows, where the paper doll puts it, with what's worn painted over each body site and what's lit. The doll fills
/// in <see cref="Lut"/> (a column per site) and calls <see cref="Commit"/>; it draws the lines round what's worn and
/// lit in <see cref="Lines"/>, which is clipped to the panel and not shaded.
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
    Rect2 _dest, _src;
    float _alpha;

    public FigurePanel(MapView v)
    {
        _v = v;
        _shader ??= GD.Load<Shader>("res://assets/shaders/figure.gdshader");
        _lut = ImageTexture.CreateFromImage(_lutImage);
        _material = new ShaderMaterial { Shader = _shader };
        _material.SetShaderParameter("sites", v.SitesTexture);
        _material.SetShaderParameter("lut", _lut);
        _material.SetShaderParameter("sites_size", v.Size);
        Material = _material;
        AddChild(Lines);
    }

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

    /// <summary>Where to draw: the screen rectangle, and the part of the picture in it (its pixels); whether its lines
    /// need drawing again.</summary>
    public void Show(Rect2 dest, Vector2 srcPos, Vector2 srcSize, float alpha, float time, bool lines)
    {
        _dest = dest;
        _src = new Rect2(srcPos, srcSize);
        _alpha = alpha;
        _material.SetShaderParameter("fade", alpha);
        _material.SetShaderParameter("time", time);
        Lines.Position = dest.Position;
        Lines.Size = dest.Size;
        Lines.Visible = alpha > 0.02f;
        QueueRedraw();
        if (lines) Lines.QueueRedraw();
    }

    public override void _Draw()
    {
        if (_v.Texture == null || _alpha <= 0.01f || _dest.Size.X < 1) return;
        // The picture's own pixels may be fewer than its map's (the map is the picture's size): scale the source.
        var k = _v.Texture.GetSize() / _v.Size;
        DrawTextureRectRegion(_v.Texture, _dest, new Rect2(_src.Position * k, _src.Size * k));
    }

    /// <summary>Lets go of the shader (before quitting).</summary>
    public static void Release() => _shader = null;
}
