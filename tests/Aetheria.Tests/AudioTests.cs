using System.Text;
using Aetheria.Engine.Audio;
using Xunit;

namespace Aetheria.Tests;

public class AudioTests
{
    [Fact]
    public void EncodedWavHasValidRiffHeader()
    {
        byte[] wav = WavSynth.Generate(Sfx.Jump);
        Assert.True(wav.Length > 44);
        Assert.Equal("RIFF", Encoding.ASCII.GetString(wav, 0, 4));
        Assert.Equal("WAVE", Encoding.ASCII.GetString(wav, 8, 4));
        Assert.Equal("fmt ", Encoding.ASCII.GetString(wav, 12, 4));
        Assert.Equal("data", Encoding.ASCII.GetString(wav, 36, 4));

        int riffSize = BitConverter.ToInt32(wav, 4);
        int dataSize = BitConverter.ToInt32(wav, 40);
        Assert.Equal(wav.Length - 8, riffSize);
        Assert.Equal(wav.Length - 44, dataSize);

        Assert.Equal(1, BitConverter.ToInt16(wav, 20));   // PCM
        Assert.Equal(1, BitConverter.ToInt16(wav, 22));   // mono
        Assert.Equal(WavSynth.SampleRate, BitConverter.ToInt32(wav, 24));
        Assert.Equal(16, BitConverter.ToInt16(wav, 34));  // bits/sample
    }

    [Theory]
    [InlineData(Sfx.Jump)]
    [InlineData(Sfx.DoubleJump)]
    [InlineData(Sfx.Dash)]
    [InlineData(Sfx.Land)]
    [InlineData(Sfx.Pulse)]
    [InlineData(Sfx.EnemyHit)]
    [InlineData(Sfx.EnemyDead)]
    [InlineData(Sfx.Pickup)]
    [InlineData(Sfx.Unlock)]
    [InlineData(Sfx.Hurt)]
    [InlineData(Sfx.Death)]
    [InlineData(Sfx.Transition)]
    [InlineData(Sfx.Victory)]
    public void EverySfxProducesAudibleSamples(Sfx sfx)
    {
        byte[] wav = WavSynth.Generate(sfx);
        float[] samples = WavSynth.Decode(wav);
        Assert.True(samples.Length > 100);

        float peak = 0f;
        foreach (var s in samples)
        {
            Assert.InRange(s, -1.0001f, 1.0001f);   // never clips past full scale
            peak = MathF.Max(peak, MathF.Abs(s));
        }
        Assert.True(peak > 0.05f, $"{sfx} is silent");
    }

    [Fact]
    public void SynthesisIsDeterministic()
    {
        Assert.Equal(WavSynth.Generate(Sfx.Unlock), WavSynth.Generate(Sfx.Unlock));
        Assert.Equal(WavSynth.Generate(Sfx.Dash), WavSynth.Generate(Sfx.Dash));
    }

    [Fact]
    public void DecodeRoundTripsEncode()
    {
        var original = new float[] { 0f, 0.5f, -0.5f, 1f, -1f, 0.25f };
        byte[] wav = WavSynth.Encode(original);
        float[] back = WavSynth.Decode(wav);
        Assert.Equal(original.Length, back.Length);
        for (int i = 0; i < original.Length; i++)
            Assert.Equal(original[i], back[i], 3);
    }
}
