using System;
using Godot;

namespace LootFall.Ui;

/// <summary>A Node2D whose drawing is supplied as a callback, so visuals can be built without a class per shape.</summary>
public partial class Painter : Node2D
{
    public Action<Painter> OnDraw;

    public Painter() { }

    public Painter(Action<Painter> onDraw)
    {
        OnDraw = onDraw;
    }

    public override void _Draw() => OnDraw?.Invoke(this);
}
