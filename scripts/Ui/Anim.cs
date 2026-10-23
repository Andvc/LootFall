using System;
using System.Collections.Generic;
using System.Text.Json;
using Godot;

namespace LootFall.Ui;

/// <summary>An actor's frames as tools/import_art.py wrote them (assets/sprites/actors/actors.json).</summary>
public sealed class ActorData
{
    public int w { get; set; }
    public int h { get; set; }
    public int footX { get; set; }
    public int footY { get; set; }
    public int bodyW { get; set; }
    public int bodyH { get; set; }
    public bool facesLeft { get; set; }
    public Dictionary<string, int[]> anims { get; set; } = new();
    public string[] weaponColors { get; set; }
    public string[] armorColors { get; set; }
}

public sealed class FxData
{
    public int w { get; set; }
    public int h { get; set; }
    public int[] durations { get; set; } = Array.Empty<int>();
}

/// <summary>Builds SpriteFrames for actors and effects from their strips, once each.</summary>
public static class Anims
{
    static Dictionary<string, ActorData> _actors;
    static Dictionary<string, FxData> _fx;
    static readonly Dictionary<string, SpriteFrames> Frames = new();

    static T ReadJson<T>(string path)
    {
        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
        return JsonSerializer.Deserialize<T>(file.GetAsText());
    }

    /// <summary>Lets go of every cached animation (before quitting).</summary>
    public static void Release() => Frames.Clear();

    public static ActorData Actor(string key)
    {
        _actors ??= ReadJson<Dictionary<string, ActorData>>("res://assets/sprites/actors/actors.json");
        return _actors[key];
    }

    public static FxData Fx(string name)
    {
        _fx ??= ReadJson<Dictionary<string, FxData>>("res://assets/sprites/fx/fx.json");
        return _fx[name];
    }

    /// <summary>Frames for an actor: every animation it has, the idle one looping, the rest playing once.</summary>
    public static SpriteFrames ForActor(string key)
    {
        if (Frames.TryGetValue("actor:" + key, out var cached)) return cached;
        var data = Actor(key);
        var frames = new SpriteFrames();
        frames.RemoveAnimation("default");
        foreach (var (name, durations) in data.anims)
        {
            var sheet = GD.Load<Texture2D>($"res://assets/sprites/actors/{key}/{name}.png");
            frames.AddAnimation(name);
            frames.SetAnimationLoopMode(name, name == "idle" ? SpriteFrames.LoopMode.Linear : SpriteFrames.LoopMode.None);
            frames.SetAnimationSpeed(name, 10);
            for (int i = 0; i < durations.Length; i++)
                frames.AddFrame(name, new AtlasTexture { Atlas = sheet, Region = new Rect2(i * data.w, 0, data.w, data.h) }, durations[i] / 100f);
        }
        Frames["actor:" + key] = frames;
        return frames;
    }

    /// <summary>Frames for one of the pack's effects, played once.</summary>
    public static SpriteFrames ForFx(string name)
    {
        if (Frames.TryGetValue("fx:" + name, out var cached)) return cached;
        var data = Fx(name);
        var sheet = GD.Load<Texture2D>($"res://assets/sprites/fx/{name}.png");
        var frames = new SpriteFrames();
        frames.RemoveAnimation("default");
        frames.AddAnimation("play");
        frames.SetAnimationLoopMode("play", SpriteFrames.LoopMode.None);
        frames.SetAnimationSpeed("play", 10);
        for (int i = 0; i < data.durations.Length; i++)
            frames.AddFrame("play", new AtlasTexture { Atlas = sheet, Region = new Rect2(i * data.w, 0, data.w, data.h) }, data.durations[i] / 100f);
        Frames["fx:" + name] = frames;
        return frames;
    }

    /// <summary>Total length of an actor's animation in seconds, at normal speed.</summary>
    public static float Length(string key, string anim)
    {
        var data = Actor(key);
        if (!data.anims.TryGetValue(anim, out var d)) return 0;
        int total = 0;
        foreach (var ms in d) total += ms;
        return total / 1000f;
    }

    /// <summary>Plays an effect once at a spot (at 2x, like everything in the battle), then frees it.</summary>
    public static AnimatedSprite2D PlayFx(Node parent, string name, Vector2 at, Color? tint = null, float speed = 1.4f,
        bool flip = false, int scale = 2, float rotation = 0, bool additive = false)
    {
        var fx = new AnimatedSprite2D
        {
            SpriteFrames = ForFx(name), Position = at, Scale = new Vector2(flip ? -scale : scale, scale), Rotation = rotation,
            Modulate = tint ?? Colors.White, SpeedScale = speed, ZIndex = 20,
        };
        if (additive) fx.Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };
        parent.AddChild(fx);
        fx.AnimationFinished += fx.QueueFree;
        fx.Play("play");
        return fx;
    }
}
