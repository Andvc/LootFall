using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace LootFall.Audio;

/// <summary>
/// Placeholder sound effects synthesized at startup, so the prototype has audio feedback with no asset files.
/// Swap any entry for a real recording later by loading an AudioStream under the same name.
/// </summary>
public partial class Sfx : Node
{
    readonly Dictionary<string, AudioStream> _sounds = new();
    readonly List<AudioStreamPlayer> _voices = new();
    AudioStreamPlayer _hum;
    int _nextVoice;

    public override void _Ready()
    {
        Build();
        for (int i = 0; i < 14; i++)
        {
            var voice = new AudioStreamPlayer();
            AddChild(voice);
            _voices.Add(voice);
        }
        _hum = new AudioStreamPlayer { Stream = _sounds["hum"], VolumeDb = -80 };
        AddChild(_hum);
    }

    public void Play(string name, float pitch = 1f, float volumeDb = 0f)
    {
        if (!_sounds.TryGetValue(name, out var stream))
        {
            GD.PushWarning($"Unknown sound '{name}'");
            return;
        }
        var voice = _voices.FirstOrDefault(v => !v.Playing) ?? _voices[_nextVoice++ % _voices.Count];
        voice.Stream = stream;
        voice.PitchScale = Mathf.Max(0.05f, pitch);
        voice.VolumeDb = volumeDb;
        voice.Play();
    }

    /// <summary>The rising hum while the lid is held. 0 stops it.</summary>
    public void Hum(float intensity)
    {
        if (intensity <= 0.001f)
        {
            _hum.Stop();
            return;
        }
        if (!_hum.Playing) _hum.Play();
        _hum.VolumeDb = Mathf.LinearToDb(0.08f + 0.6f * intensity);
        _hum.PitchScale = 0.7f + intensity * 1.2f;
    }

    void Build()
    {
        var rng = new Random(7);
        float R(float a, float b) => a + (b - a) * (float)rng.NextDouble();

        Add("blip", new Synth(0.35f).Tone(0, 0.35f, 880, 0.5f, 12).Tone(0, 0.35f, 1320, 0.22f, 18));
        Add("tick", new Synth(0.05f).Tone(0, 0.05f, 1500, 0.25f, 80));
        Add("bid", new Synth(0.15f).Tone(0, 0.15f, 620, 0.4f, 30).Tone(0, 0.15f, 930, 0.18f, 40));
        Add("fold", new Synth(0.35f).Tone(0, 0.35f, 330, 0.35f, 9, freqEnd: 240));
        Add("gavel", new Synth(0.45f)
            .Tone(0, 0.15f, 190, 0.8f, 35).Noise(0, 0.05f, 0.5f, 90, 0.6f)
            .Tone(0.2f, 0.2f, 170, 0.9f, 30).Noise(0.2f, 0.06f, 0.6f, 80, 0.6f));
        Add("coin", new Synth(0.5f)
            .Tone(0, 0.4f, 2350, 0.3f, 11).Tone(0, 0.4f, 3520, 0.2f, 14).Tone(0, 0.4f, 4980, 0.1f, 18)
            .Tone(0.07f, 0.4f, 2500, 0.18f, 12).Tone(0.07f, 0.4f, 3750, 0.12f, 15));
        Add("pin", new Synth(0.15f).Noise(0, 0.05f, 0.6f, 90, 0.9f).Tone(0, 0.15f, 1900, 0.45f, 40).Tone(0, 0.15f, 2850, 0.2f, 55));
        Add("pin_perfect", new Synth(0.5f).Noise(0, 0.05f, 0.6f, 90, 0.9f).Tone(0, 0.15f, 1900, 0.45f, 40)
            .Tone(0.03f, 0.45f, 1568, 0.3f, 8).Tone(0.09f, 0.4f, 2093, 0.25f, 9));
        Add("miss", new Synth(0.25f).Tone(0, 0.25f, 160, 0.7f, 22, freqEnd: 100).Noise(0, 0.1f, 0.35f, 50, 0.3f));
        Add("snap", new Synth(0.45f).Noise(0, 0.4f, 0.9f, 14, 0.8f).Tone(0, 0.3f, 420, 0.4f, 16, freqEnd: 160).Noise(0.05f, 0.25f, 0.5f, 25, 0.4f));
        Add("crowbar", new Synth(0.7f).Tone(0, 0.5f, 95, 0.9f, 9, freqEnd: 60).Noise(0, 0.25f, 0.7f, 18, 0.35f).Noise(0.18f, 0.4f, 0.5f, 10, 0.15f));
        Add("sting", new Synth(0.6f).Tone(0, 0.6f, 784, 0.32f, 6).Tone(0.08f, 0.55f, 1175, 0.32f, 6).Tone(0, 0.6f, 1568, 0.1f, 9));
        Add("burst", new Synth(1.3f).Noise(0, 1.2f, 0.9f, 4.5f, 0.25f).Tone(0, 1.1f, 70, 0.9f, 3.5f, freqEnd: 38).Noise(0, 0.15f, 0.5f, 30, 1f));
        Add("whoosh", new Synth(0.6f).Noise(0, 0.6f, 0.6f, 6, 0.18f, attack: 0.16f));
        Add("fanfare", new Synth(1.6f)
            .Tone(0, 1.4f, 523, 0.25f, 2.6f).Tone(0.08f, 1.4f, 659, 0.25f, 2.6f)
            .Tone(0.16f, 1.4f, 784, 0.25f, 2.6f).Tone(0.24f, 1.3f, 1047, 0.3f, 2.4f).Tone(0.24f, 1.3f, 2093, 0.08f, 3f));
        Add("reveal_big", new Synth(1.3f).Tone(0, 1.2f, 523, 0.2f, 2.5f).Tone(0, 1.2f, 659, 0.2f, 2.5f)
            .Tone(0, 1.2f, 784, 0.2f, 2.5f).Tone(0.05f, 1.0f, 2093, 0.1f, 4f));
        Add("win", new Synth(0.8f).Tone(0, 0.6f, 659, 0.3f, 5).Tone(0.1f, 0.6f, 784, 0.3f, 5).Tone(0.2f, 0.6f, 1047, 0.35f, 4));
        Add("lose", new Synth(0.7f).Tone(0, 0.35f, 392, 0.35f, 6).Tone(0.22f, 0.45f, 330, 0.35f, 5, freqEnd: 300));
        Add("scale", new Synth(0.7f).Tone(0, 0.2f, 140, 0.6f, 20).Noise(0, 0.05f, 0.4f, 60, 0.5f).Tone(0.06f, 0.6f, 900, 0.15f, 7, freqEnd: 760));
        Add("inspect_lock", new Synth(0.4f).Tone(0, 0.12f, 2400, 0.22f, 45).Tone(0.14f, 0.12f, 2650, 0.22f, 45).Noise(0, 0.03f, 0.2f, 100, 0.8f));

        var creak = new Synth(0.8f);
        for (float t = 0; t < 0.7f; t += 1f / Mathf.Lerp(30, 70, t / 0.7f))
            creak.Tone(t, 0.04f, R(260, 420), R(0.15f, 0.35f) * MathF.Sin(MathF.PI * t / 0.7f), 60);
        Add("creak", creak);

        var shatter = new Synth(0.6f).Noise(0, 0.45f, 0.55f, 11, 0.9f, highpass: true);
        for (int i = 0; i < 12; i++) shatter.Tone(R(0, 0.3f), 0.2f, R(2800, 5600), R(0.08f, 0.18f), R(25, 45));
        Add("shatter", shatter);

        var jingle = new Synth(0.7f);
        for (int i = 0; i < 10; i++)
        {
            float t = R(0, 0.5f), f = R(2000, 3300);
            jingle.Tone(t, 0.25f, f, 0.18f, 20).Tone(t, 0.25f, f * 1.5f, 0.08f, 28);
        }
        Add("shake_jingle", jingle);

        var clink = new Synth(0.7f);
        for (int i = 0; i < 7; i++) clink.Tone(R(0, 0.5f), 0.2f, R(3200, 5000), 0.2f, 40);
        Add("shake_clink", clink);

        var clank = new Synth(0.8f);
        for (int i = 0; i < 4; i++)
        {
            float t = R(0, 0.5f), f = R(380, 700);
            clank.Tone(t, 0.3f, f, 0.35f, 16).Tone(t, 0.3f, f * 2.76f, 0.12f, 24).Noise(t, 0.03f, 0.3f, 80, 0.6f);
        }
        Add("shake_clank", clank);

        var thud = new Synth(0.8f);
        foreach (float t in new[] { 0f, 0.22f, 0.45f })
            thud.Tone(t, 0.3f, R(75, 95), 0.8f, 14, freqEnd: 50).Noise(t, 0.08f, 0.4f, 40, 0.08f);
        Add("shake_thud", thud);

        var rustle = new Synth(0.7f);
        for (int i = 0; i < 8; i++) rustle.Noise(R(0, 0.5f), 0.15f, 0.25f, 12, 0.6f, highpass: true, attack: 0.02f);
        Add("shake_rustle", rustle);

        // Integer frequencies over exactly one second loop seamlessly; the 110/111 pair gives a slow shimmer.
        var hum = new Synth(1f).Tone(0, 1f, 110, 0.3f, 0, attack: 0).Tone(0, 1f, 111, 0.18f, 0, attack: 0)
            .Tone(0, 1f, 220, 0.16f, 0, attack: 0).Tone(0, 1f, 330, 0.07f, 0, attack: 0);
        _sounds["hum"] = hum.ToWav(loop: true);
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

        public Synth Tone(float start, float duration, float freq, float amp, float decay,
            float freqEnd = -1, float attack = 0.003f)
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

        public Synth Noise(float start, float duration, float amp, float decay, float lowpass,
            bool highpass = false, float attack = 0.001f)
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

        public AudioStreamWav ToWav(bool loop = false)
        {
            var data = new byte[_buffer.Length * 2];
            for (int i = 0; i < _buffer.Length; i++)
            {
                short v = (short)(MathF.Tanh(_buffer[i]) * 32000f);
                data[i * 2] = (byte)(v & 0xff);
                data[i * 2 + 1] = (byte)((v >> 8) & 0xff);
            }
            var wav = new AudioStreamWav
            {
                Format = AudioStreamWav.FormatEnum.Format16Bits,
                MixRate = Rate,
                Stereo = false,
                Data = data,
            };
            if (loop)
            {
                wav.LoopMode = AudioStreamWav.LoopModeEnum.Forward;
                wav.LoopBegin = 0;
                wav.LoopEnd = _buffer.Length;
            }
            return wav;
        }
    }
}
