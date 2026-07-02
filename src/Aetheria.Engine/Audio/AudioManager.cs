using Raylib_cs;

namespace Aetheria.Engine.Audio;

/// <summary>
/// Loads every procedurally-synthesized SFX into Raylib sounds at startup and
/// plays them on demand. Degrades gracefully to silence if no audio device is
/// available (e.g. head-less CI).
/// </summary>
public sealed class AudioManager : IDisposable
{
    private readonly Dictionary<Sfx, Sound> _sounds = new();
    private readonly List<Wave> _waves = new();
    private bool _ready;
    private float _volume = 0.55f;

    public bool Ready => _ready;

    public AudioManager()
    {
        try
        {
            Raylib.InitAudioDevice();
            _ready = Raylib.IsAudioDeviceReady();
        }
        catch { _ready = false; }
        if (!_ready) return;

        foreach (Sfx sfx in Enum.GetValues<Sfx>())
        {
            byte[] wav = WavSynth.Generate(sfx);
            Wave wave = Raylib.LoadWaveFromMemory(".wav", wav);
            Sound sound = Raylib.LoadSoundFromWave(wave);
            Raylib.SetSoundVolume(sound, _volume);
            _sounds[sfx] = sound;
            _waves.Add(wave);
        }
    }

    public void SetVolume(float v)
    {
        _volume = Math.Clamp(v, 0f, 1f);
        if (!_ready) return;
        foreach (var s in _sounds.Values) Raylib.SetSoundVolume(s, _volume);
    }

    public void Play(Sfx sfx)
    {
        if (_ready && _sounds.TryGetValue(sfx, out var s))
            Raylib.PlaySound(s);
    }

    public void Dispose()
    {
        if (!_ready) return;
        foreach (var s in _sounds.Values) Raylib.UnloadSound(s);
        foreach (var w in _waves) Raylib.UnloadWave(w);
        _sounds.Clear();
        _waves.Clear();
        Raylib.CloseAudioDevice();
        _ready = false;
    }
}
