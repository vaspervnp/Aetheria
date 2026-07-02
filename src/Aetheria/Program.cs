using Aetheria.Engine;

// "--smoke" runs the head-less integration simulation (no window/audio) and
// exits non-zero on a crash — used for autonomous verification and CI.
if (args.Contains("--smoke"))
{
    int frames = 4000;
    uint seed = 1337;
    for (int i = 0; i < args.Length - 1; i++)
    {
        if (args[i] == "--frames" && int.TryParse(args[i + 1], out var f)) frames = f;
        if (args[i] == "--seed" && uint.TryParse(args[i + 1], out var s)) seed = s;
    }

    Console.WriteLine($"Aetheria head-less smoke test: {frames} frames, seed {seed}");
    var result = HeadlessSim.Run(frames, seed);
    Console.WriteLine(result);
    return result.Crashed ? 1 : 0;
}

// "--rendertest" boots a hidden window and runs the full render path for N
// frames, then exits — verifies the graphics pipeline without a visible window.
if (args.Contains("--rendertest"))
{
    int frames = 600;
    string? shots = null;
    int room = 0;
    for (int i = 0; i < args.Length - 1; i++)
    {
        if (args[i] == "--frames" && int.TryParse(args[i + 1], out var f)) frames = f;
        if (args[i] == "--shots") shots = args[i + 1];
        if (args[i] == "--room" && int.TryParse(args[i + 1], out var r)) room = r;
    }
    using var probe = new Game(seed: 1337);
    return probe.RunSelfTest(frames, shots, room);
}

Console.WriteLine("Aetheria: The Bio-Mechanical Abyss — launching…");
using var game = new Game(seed: 1337);
game.Run();
return 0;
