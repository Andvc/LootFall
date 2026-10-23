using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace LootFall.Audio;

/// <summary>
/// Two kinds of sound: recorded CC0 samples in groups under res://assets/audio/{group}/ (hits, coins, the chest's
/// creak and latch), and tones synthesized at startup for the chest's rising build-up, sparkles and stings. Everything
/// plays on the "Sfx" bus; music has its own player and bus (see <see cref="Music"/>).
/// </summary>
public partial class Sfx : Node
{
    readonly Dictionary<string, AudioStream> _sounds = new();
    readonly Dictionary<string, AudioStream[]> _groups = new();
    readonly List<AudioStreamPlayer> _voices = new();
    readonly Random _rng = new();
    int _nextVoice;
    bool _released;

    /// <summary>When each group last played, so a burst of hits doesn't stack twenty copies.</summary>
    readonly Dictionary<string, ulong> _lastPlayed = new();

    public override void _Ready()
    {
        Build();
        for (int i = 0; i < 28; i++)
        {
            var voice = new AudioStreamPlayer { Bus = "Sfx" };
            AddChild(voice);
            _voices.Add(voice);
        }
    }

    /// <summary>Stops every sound and lets go of them all, so quitting leaves nothing playing (or leaked) behind.</summary>
    public void Release()
    {
        _released = true;
        foreach (var voice in _voices)
        {
            voice.Stop();
            voice.Stream = null;
        }
        var streams = _sounds.Values.Concat(_groups.Values.SelectMany(s => s)).Distinct().ToList();
        _sounds.Clear();
        _groups.Clear();
        foreach (var stream in streams) stream.Dispose();
    }

    /// <summary>A synthesized sound by name.</summary>
    public void Play(string name, float pitch = 1f, float volumeDb = 0f)
    {
        if (_released) return;
        if (!_sounds.TryGetValue(name, out var stream))
        {
            GD.PushWarning($"Unknown sound '{name}'");
            return;
        }
        PlayStream(stream, pitch, volumeDb);
    }

    /// <summary>
    /// A random recording from res://assets/audio/{group}/ (0.ogg, 1.ogg...), or the synthesized sound of that name.
    /// <paramref name="minGap"/> drops a play that comes too soon after the last of the same group.
    /// </summary>
    public void PlayGroup(string group, float pitch = 1f, float volumeDb = 0f, float minGap = 0.03f)
    {
        if (_released) return;
        ulong now = Time.GetTicksMsec();
        if (_lastPlayed.TryGetValue(group, out var last) && now - last < minGap * 1000) return;
        _lastPlayed[group] = now;
        if (_sounds.TryGetValue(group, out var synth))
        {
            PlayStream(synth, pitch, volumeDb);
            return;
        }
        if (!_groups.TryGetValue(group, out var streams))
        {
            var list = new List<AudioStream>();
            for (int i = 0; ; i++)
            {
                string path = $"res://assets/audio/{group}/{i}.ogg";
                if (!ResourceLoader.Exists(path)) break;
                list.Add(GD.Load<AudioStream>(path));
            }
            _groups[group] = streams = list.ToArray();
            if (streams.Length == 0) GD.PushWarning($"No recordings in sound group '{group}'");
        }
        if (streams.Length > 0) PlayStream(streams[_rng.Next(streams.Length)], pitch * (0.94f + 0.12f * (float)_rng.NextDouble()), volumeDb);
    }

    void PlayStream(AudioStream stream, float pitch, float volumeDb)
    {
        if (_released) return;
        var voice = _voices.FirstOrDefault(v => !v.Playing) ?? _voices[_nextVoice++ % _voices.Count];
        voice.Stream = stream;
        voice.PitchScale = Mathf.Max(0.05f, pitch);
        voice.VolumeDb = volumeDb;
        voice.Play();
    }

    /// <summary>Writes every synthesized sound to WAV files, so they can be checked outside the game.</summary>
    public void Export(string directory)
    {
        DirAccess.MakeDirRecursiveAbsolute(directory);
        foreach (var (name, stream) in _sounds)
            if (stream is AudioStreamWav wav) wav.SaveToWav($"{directory}/{name}.wav");
    }

    void Build()
    {
        var rng = new Random(7);
        float R(float a, float b) => a + (b - a) * (float)rng.NextDouble();

        Add("tick", new Synth(0.05f).Tone(0, 0.05f, 1500, 0.25f, 80));
        // The chest's build-up: one bright ting per rarity step, each a little higher (played at rising pitch).
        Add("ting", new Synth(0.7f).Tone(0, 0.7f, 1320, 0.28f, 6).Tone(0, 0.5f, 2640, 0.12f, 9).Tone(0, 0.35f, 3960, 0.05f, 14));
        // The low hum under a chest about to burst.
        Add("rumble", new Synth(0.6f).Tone(0, 0.6f, 60, 0.6f, 4, freqEnd: 45).Noise(0, 0.5f, 0.35f, 6, 0.06f, attack: 0.05f));
        Add("burst", new Synth(1.1f).Noise(0, 1.0f, 0.8f, 5f, 0.3f).Tone(0, 0.9f, 80, 0.8f, 4f, freqEnd: 40).Noise(0, 0.12f, 0.5f, 30, 1f));
        Add("whoosh", new Synth(0.45f).Noise(0, 0.45f, 0.5f, 7, 0.2f, attack: 0.12f));
        Add("sparkle", new Synth(0.8f).Tone(0, 0.6f, 2093, 0.16f, 6).Tone(0.06f, 0.6f, 2637, 0.14f, 6).Tone(0.12f, 0.6f, 3136, 0.12f, 6)
            .Tone(0.18f, 0.6f, 4186, 0.1f, 6));
        Add("chime", new Synth(1.6f).Tone(0, 1.6f, 1047, 0.3f, 2.2f).Tone(0, 1.2f, 2890, 0.12f, 3.5f).Tone(0, 0.8f, 5650, 0.06f, 6f));
        Add("fanfare", new Synth(1.8f)
            .Tone(0, 1.4f, 523, 0.25f, 2.4f).Tone(0.09f, 1.4f, 659, 0.25f, 2.4f).Tone(0.18f, 1.4f, 784, 0.25f, 2.4f)
            .Tone(0.27f, 1.5f, 1047, 0.3f, 2.0f).Tone(0.27f, 1.5f, 2093, 0.08f, 3f));
        Add("mythic", new Synth(2.4f)
            .Tone(0, 2.2f, 392, 0.22f, 1.5f).Tone(0.1f, 2.1f, 494, 0.2f, 1.5f).Tone(0.2f, 2.0f, 587, 0.2f, 1.5f)
            .Tone(0.3f, 1.9f, 784, 0.25f, 1.4f).Tone(0.42f, 1.8f, 988, 0.25f, 1.4f).Tone(0.54f, 1.8f, 1175, 0.28f, 1.3f)
            .Tone(0.54f, 1.8f, 2349, 0.08f, 2f).Noise(0, 0.4f, 0.25f, 8, 0.1f));
        Add("upgrade", new Synth(0.7f).Tone(0, 0.25f, 784, 0.3f, 8).Tone(0.07f, 0.25f, 988, 0.3f, 8).Tone(0.14f, 0.25f, 1175, 0.3f, 8)
            .Tone(0.21f, 0.45f, 1568, 0.35f, 5));
        Add("down", new Synth(0.5f).Tone(0, 0.45f, 520, 0.3f, 5, freqEnd: 300).Tone(0, 0.4f, 780, 0.12f, 6, freqEnd: 450));
        Add("thunder", new Synth(0.7f).Noise(0, 0.6f, 0.8f, 7, 0.9f, highpass: true).Tone(0, 0.5f, 90, 0.6f, 6, freqEnd: 50));
        Add("fire", new Synth(0.5f).Noise(0, 0.5f, 0.5f, 6, 0.15f, attack: 0.04f).Noise(0, 0.3f, 0.2f, 12, 0.8f, highpass: true));
        Add("heal", new Synth(0.7f).Tone(0, 0.6f, 880, 0.18f, 5, freqEnd: 1320).Tone(0.05f, 0.6f, 1320, 0.12f, 5, freqEnd: 1760));
        Add("barrier", new Synth(0.5f).Tone(0, 0.45f, 440, 0.25f, 6, freqEnd: 660).Tone(0, 0.45f, 660, 0.12f, 6, freqEnd: 990));
        Add("meteor", new Synth(1.0f).Tone(0, 0.5f, 1400, 0.15f, 2, freqEnd: 300).Noise(0.45f, 0.5f, 0.9f, 6, 0.25f)
            .Tone(0.45f, 0.5f, 60, 0.9f, 5, freqEnd: 35));
        Add("dodge", new Synth(0.25f).Noise(0, 0.25f, 0.35f, 12, 0.5f, highpass: true, attack: 0.03f));
        var drop = new Synth(0.35f);
        for (int i = 0; i < 3; i++) drop.Tone(i * 0.05f, 0.2f, R(1700, 2600), 0.15f, 20);
        Add("pop", drop);
    }

    void Add(string name, Synth synth) => _sounds[name] = synth.ToWav();

    /// <summary>A tiny additive synth that renders into a float buffer.</summary>
    sealed class Synth
    {
        const int Rate = 44100;
        readonly float[] _buffer;
        readonly Random _rng = new(1234);

        public Synth(float seconds)
        {
            _buffer = new float[(int)(seconds * Rate)];
        }

        public Synth Tone(float start, float duration, float freq, float amp, float decay, float freqEnd = -1, float attack = 0.003f)
        {
            int s0 = (int)(start * Rate);
            int n = (int)(duration * Rate);
            double phase = 0;
            for (int i = 0; i < n && s0 + i < _buffer.Length; i++)
            {
                float t = i / (float)Rate;
                float f = freqEnd < 0 ? freq : Mathf.Lerp(freq, freqEnd, t / duration);
                phase += f / Rate;
                float env = MathF.Exp(-decay * t) * (attack > 0 ? Math.Min(1f, t / attack) : 1f);
                _buffer[s0 + i] += (float)Math.Sin(phase * Math.Tau) * amp * env;
            }
            return this;
        }

        public Synth Noise(float start, float duration, float amp, float decay, float lowpass, bool highpass = false, float attack = 0.001f)
        {
            int s0 = (int)(start * Rate);
            int n = (int)(duration * Rate);
            float smooth = 0;
            for (int i = 0; i < n && s0 + i < _buffer.Length; i++)
            {
                float t = i / (float)Rate;
                float x = (float)(_rng.NextDouble() * 2 - 1);
                smooth += lowpass * (x - smooth);
                float sample = highpass ? x - smooth : smooth;
                float env = Math.Min(1f, t / attack) * MathF.Exp(-decay * Math.Max(0, t - attack));
                _buffer[s0 + i] += sample * amp * env;
            }
            return this;
        }

        public AudioStreamWav ToWav()
        {
            var data = new byte[_buffer.Length * 2];
            for (int i = 0; i < _buffer.Length; i++)
            {
                short v = (short)(MathF.Tanh(_buffer[i]) * 32000f);
                data[i * 2] = (byte)(v & 0xff);
                data[i * 2 + 1] = (byte)((v >> 8) & 0xff);
            }
            return new AudioStreamWav { Format = AudioStreamWav.FormatEnum.Format16Bits, MixRate = Rate, Stereo = false, Data = data };
        }
    }
}
