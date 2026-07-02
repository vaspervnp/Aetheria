using Aetheria.Engine;
using Xunit;

namespace Aetheria.Tests;

/// <summary>
/// Head-less integration smoke tests: run the whole simulation (world + player +
/// combat) for thousands of frames under scripted input across several seeds and
/// assert it never throws. This is the same code path the real game loop drives.
/// </summary>
public class SmokeTests
{
    [Theory]
    [InlineData(1337u)]
    [InlineData(1u)]
    [InlineData(99999u)]
    public void SimulationRunsWithoutCrashing(uint seed)
    {
        var result = HeadlessSim.Run(frames: 5000, seed: seed);
        Assert.False(result.Crashed, result.Error);
        Assert.True(result.Frames > 0);
        Assert.True(result.RoomsVisited >= 1);
    }

    [Fact]
    public void SimulationMakesProgressAndSurvivesRespawns()
    {
        // The scripted bot is deliberately dumb, but it should still collect the
        // first ability and cross at least one room boundary within 5000 frames.
        var result = HeadlessSim.Run(frames: 5000, seed: 1337);
        Assert.False(result.Crashed);
        Assert.True(result.RoomsVisited >= 2, "bot never left the first room");
        Assert.True(result.AbilitiesUnlocked >= 1, "bot never collected an ability");
    }
}
