using Aetheria.Engine.Core;

namespace Aetheria.Engine.Audio;

public enum Sfx
{
    Jump, DoubleJump, Dash, Land, Pulse, EnemyHit, EnemyDead,
    Pickup, Unlock, Hurt, Death, Transition, Victory,
}

public enum Waveform { Sine, Square, Saw, Triangle, Noise }

/// <summary>
/// Synthesizes retro/synth sound effects mathematically into 16-bit PCM WAV byte
/// buffers held entirely in memory — no audio files on disk. The buffers are fed
/// straight to Raylib's <c>LoadWaveFromMemory</c> at runtime. Fully deterministic
/// so the output is unit-testable.
/// </summary>
public static class WavSynth
{
    public const int SampleRate = 22050;

    public static byte[] Generate(Sfx sfx) => sfx switch
    {
        Sfx.Jump      => Tone(0.14f, 300, 620, Waveform.Square, 0.35f, seed: 1),
        Sfx.DoubleJump=> Tone(0.14f, 520, 900, Waveform.Square, 0.32f, seed: 2),
        Sfx.Dash      => Layer(
                            Tone(0.18f, 760, 180, Waveform.Saw, 0.30f, seed: 3),
                            Tone(0.12f, 0, 0, Waveform.Noise, 0.18f, seed: 4)),
        Sfx.Land      => Tone(0.09f, 150, 70, Waveform.Triangle, 0.4f, seed: 5),
        Sfx.Pulse     => Tone(0.11f, 680, 300, Waveform.Square, 0.30f, seed: 6),
        Sfx.EnemyHit  => Tone(0.07f, 0, 0, Waveform.Noise, 0.30f, seed: 7),
        Sfx.EnemyDead => Layer(
                            Tone(0.26f, 420, 60, Waveform.Saw, 0.30f, seed: 8),
                            Tone(0.2f, 0, 0, Waveform.Noise, 0.22f, seed: 9)),
        Sfx.Pickup    => Arpeggio(new[] { 660f, 990f }, 0.07f, Waveform.Sine, 0.34f, seed: 10),
        Sfx.Unlock    => Arpeggio(new[] { 440f, 660f, 880f, 1320f }, 0.10f, Waveform.Sine, 0.34f, seed: 11),
        Sfx.Hurt      => Tone(0.2f, 420, 120, Waveform.Square, 0.32f, seed: 12),
        Sfx.Death     => Layer(
                            Tone(0.6f, 320, 50, Waveform.Saw, 0.34f, seed: 13),
                            Tone(0.5f, 0, 0, Waveform.Noise, 0.16f, seed: 14)),
        Sfx.Transition=> Tone(0.3f, 220, 520, Waveform.Sine, 0.26f, seed: 15),
        Sfx.Victory   => Arpeggio(new[] { 523f, 659f, 784f, 1046f, 1319f }, 0.12f, Waveform.Sine, 0.34f, seed: 16),
        _             => Tone(0.1f, 440, 440, Waveform.Sine, 0.3f, seed: 17),
    };

    /// <summary>A single tone with a linear pitch glide start→end and exp decay.</summary>
    public static byte[] Tone(float seconds, float freqStart, float freqEnd, Waveform shape, float amp, int seed)
    {
        int n = Math.Max(1, (int)(seconds * SampleRate));
        var samples = new float[n];
        var rng = new Rng((ulong)seed * 2654435761UL);
        double phase = 0;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)n;
            float freq = freqStart + (freqEnd - freqStart) * t;
            phase += 2.0 * Math.PI * freq / SampleRate;
            float s = Sample(shape, phase, rng);
            float env = Envelope(t, n, i);
            samples[i] = s * env * amp;
        }
        return Encode(samples);
    }

    /// <summary>A rising sequence of equal-length notes.</summary>
    public static byte[] Arpeggio(float[] freqs, float noteSeconds, Waveform shape, float amp, int seed)
    {
        int noteN = Math.Max(1, (int)(noteSeconds * SampleRate));
        var samples = new float[noteN * freqs.Length];
        var rng = new Rng((ulong)seed * 40503UL + 7);
        for (int k = 0; k < freqs.Length; k++)
        {
            double phase = 0;
            for (int i = 0; i < noteN; i++)
            {
                float t = i / (float)noteN;
                phase += 2.0 * Math.PI * freqs[k] / SampleRate;
                float s = Sample(shape, phase, rng);
                samples[k * noteN + i] = s * Envelope(t, noteN, i) * amp;
            }
        }
        return Encode(samples);
    }

    /// <summary>Mix two rendered clips sample-by-sample (length = max of the two).</summary>
    public static byte[] Layer(byte[] a, byte[] b)
    {
        float[] sa = Decode(a);
        float[] sb = Decode(b);
        int n = Math.Max(sa.Length, sb.Length);
        var mix = new float[n];
        for (int i = 0; i < n; i++)
        {
            float va = i < sa.Length ? sa[i] : 0f;
            float vb = i < sb.Length ? sb[i] : 0f;
            mix[i] = va + vb;
        }
        return Encode(mix);
    }

    private static float Sample(Waveform shape, double phase, Rng rng)
    {
        double p = phase % (2.0 * Math.PI);
        if (p < 0) p += 2.0 * Math.PI;
        double norm = p / (2.0 * Math.PI); // 0..1
        return shape switch
        {
            Waveform.Sine => (float)Math.Sin(phase),
            Waveform.Square => norm < 0.5 ? 1f : -1f,
            Waveform.Saw => (float)(2.0 * norm - 1.0),
            Waveform.Triangle => (float)(4.0 * Math.Abs(norm - 0.5) - 1.0),
            Waveform.Noise => rng.NextFloat() * 2f - 1f,
            _ => 0f,
        };
    }

    /// <summary>Short attack ramp + exponential decay to avoid clicks and feel punchy.</summary>
    private static float Envelope(float t, int n, int i)
    {
        const int attack = 48; // ~2ms
        float a = i < attack ? i / (float)attack : 1f;
        float decay = MathF.Exp(-4.5f * t);
        return a * decay;
    }

    // ---- WAV (PCM 16-bit mono) encode / decode -----------------------------
    public static byte[] Encode(float[] samples)
    {
        int dataSize = samples.Length * 2;
        using var ms = new MemoryStream(44 + dataSize);
        using var w = new BinaryWriter(ms);
        w.Write(new[] { 'R', 'I', 'F', 'F' });
        w.Write(36 + dataSize);
        w.Write(new[] { 'W', 'A', 'V', 'E' });
        w.Write(new[] { 'f', 'm', 't', ' ' });
        w.Write(16);                       // fmt chunk size
        w.Write((short)1);                 // PCM
        w.Write((short)1);                 // mono
        w.Write(SampleRate);
        w.Write(SampleRate * 2);           // byte rate
        w.Write((short)2);                 // block align
        w.Write((short)16);                // bits per sample
        w.Write(new[] { 'd', 'a', 't', 'a' });
        w.Write(dataSize);
        for (int i = 0; i < samples.Length; i++)
        {
            float v = Math.Clamp(samples[i], -1f, 1f);
            w.Write((short)(v * 32767f));
        }
        w.Flush();
        return ms.ToArray();
    }

    public static float[] Decode(byte[] wav)
    {
        int dataSize = BitConverter.ToInt32(wav, 40);
        int n = dataSize / 2;
        var samples = new float[n];
        for (int i = 0; i < n; i++)
        {
            short s = BitConverter.ToInt16(wav, 44 + i * 2);
            samples[i] = s / 32767f;
        }
        return samples;
    }
}
