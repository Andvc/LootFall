using Godot;

namespace LootFall.Audio;

/// <summary>One looping track at a time on the "Music" bus, crossfading when it changes (title, the zones, bosses).</summary>
public partial class Music : Node
{
    AudioStreamPlayer _current, _old;
    string _playing;

    public override void _Ready()
    {
        _current = new AudioStreamPlayer { Bus = "Music" };
        _old = new AudioStreamPlayer { Bus = "Music" };
        AddChild(_current);
        AddChild(_old);
    }

    /// <summary>Plays res://assets/music/{name}.ogg, looping, fading over from whatever was on.</summary>
    public void Play(string name, float fade = 1.2f)
    {
        if (name == _playing) return;
        _playing = name;
        (_old, _current) = (_current, _old);
        var stream = GD.Load<AudioStream>($"res://assets/music/{name}.ogg");
        if (stream is AudioStreamOggVorbis ogg) ogg.Loop = true;
        _current.Stream = stream;
        _current.VolumeDb = -40;
        _current.Play();
        var t = CreateTween().SetParallel();
        t.TweenProperty(_current, "volume_db", 0f, fade);
        if (_old.Playing) t.TweenProperty(_old, "volume_db", -60f, fade);
        t.Chain().TweenCallback(Callable.From(() => _old.Stop()));
    }

    public void Stop()
    {
        _playing = null;
        _current.Stop();
        _old.Stop();
        _current.Stream = null;
        _old.Stream = null;
    }
}
