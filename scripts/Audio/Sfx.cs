using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace LootFall.Audio;

/// <summary>
/// Two kinds of sound: recorded CC0 samples in groups under res://assets/audio/ (each item's own sound when a chest
/// is shaken, plus handling noises), and placeholder effects synthesized at startup for everything else.
/// </summary>
public partial class Sfx : Node
{
    readonly Dictionary<string, AudioStream> _sounds = new();
    readonly Dictionary<string, AudioStream[]> _groups = new();
    readonly List<AudioStreamPlayer> _voices = new();
    readonly Random _rng = new();
    AudioStreamPlayer _hum;
    int _nextVoice;

    public override void _Ready()
    {
        Build();
        for (int i = 0; i < 24; i++)
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
        PlayStream(stream, pitch, volumeDb);
    }

    /// <summary>Plays a random recording from res://assets/audio/{group}/ (files 0.ogg, 1.ogg, ...).</summary>
    public void PlayGroup(string group, float pitch = 1f, float volumeDb = 0f)
    {
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
        if (streams.Length > 0) PlayStream(streams[_rng.Next(streams.Length)], pitch, volumeDb);
    }

    /// <summary>An item's own sound, exactly as it rattles in a chest.</summary>
    public void Play(Core.Signature sound, float volumeDb = 0f) => PlayGroup(sound.Group, sound.Pitch, sound.VolumeDb + volumeDb);

    void PlayStream(AudioStream stream, float pitch, float volumeDb)
    {
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
        // A sealed hollow under the floor rings on like a drum; a solid chest has nothing like it.
        Add("hollow", new Synth(1.4f)
            .Tone(0, 1.4f, 92, 0.9f, 2.6f, freqEnd: 86).Tone(0, 1.2f, 139, 0.5f, 3.4f).Tone(0, 0.9f, 211, 0.25f, 5f)
            .Noise(0, 0.06f, 0.5f, 40, 0.15f));
        Add("lantern", new Synth(0.5f).Noise(0, 0.45f, 0.3f, 7, 0.08f, attack: 0.05f).Tone(0.02f, 0.3f, 1300, 0.08f, 12));

        var creak = new Synth(0.8f);
        for (float t = 0; t < 0.7f; t += 1f / Mathf.Lerp(30, 70, t / 0.7f))
            creak.Tone(t, 0.04f, R(260, 420), R(0.15f, 0.35f) * MathF.Sin(MathF.PI * t / 0.7f), 60);
        Add("creak", creak);

        var shatter = new Synth(0.6f).Noise(0, 0.45f, 0.55f, 11, 0.9f, highpass: true);
        for (int i = 0; i < 12; i++) shatter.Tone(R(0, 0.3f), 0.2f, R(2800, 5600), R(0.08f, 0.18f), R(25, 45));
        Add("shatter", shatter);

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
