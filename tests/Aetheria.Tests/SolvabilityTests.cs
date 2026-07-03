using Aetheria.Engine.Abilities;
using Aetheria.Engine.World;
using Xunit;

namespace Aetheria.Tests;

/// <summary>
/// Proves the procedural world is actually completable: <see cref="WorldSolver"/>
/// simulates real progression (Open doors always; AbilityGates once the ability
/// is held; Blast doors once their switch's room is reachable) and must reach the
/// Core, collecting every progression ability along the way.
/// </summary>
public class SolvabilityTests
{
    [Fact]
    public void TheDefaultGameSeedIsBeatable()
    {
        var world = MapGenerator.Generate(1337u);   // the seed Game uses
        var r = WorldSolver.Solve(world);
        Assert.True(r.Beatable, "seed 1337 is not completable");
        Assert.True(r.CoreReachable);
        Assert.Contains(AbilityType.DoubleJump, r.AbilityOrder);
        Assert.Contains(AbilityType.Dash, r.AbilityOrder);
        Assert.Contains(AbilityType.Phase, r.AbilityOrder);
    }

    [Theory]
    [InlineData(0u)]
    [InlineData(1u)]
    [InlineData(2u)]
    [InlineData(42u)]
    [InlineData(777u)]
    [InlineData(999u)]
    [InlineData(1337u)]
    [InlineData(2024u)]
    [InlineData(31337u)]
    [InlineData(555555u)]
    public void EveryGeneratedWorldIsBeatable(uint seed)
    {
        var world = MapGenerator.Generate(seed);
        var r = WorldSolver.Solve(world);
        Assert.True(r.Beatable, $"seed {seed}: core unreachable (reached {r.RoomsEventuallyReachable}/{world.Rooms.Count})");
        Assert.Empty(r.AbilitiesNeverReached);   // all four abilities obtainable
    }

    [Fact]
    public void FirstAbilityIsReachableWithNoAbilities()
    {
        // The very first pickup (Double Jump) must be reachable from the start
        // without needing anything — otherwise the run dead-ends immediately.
        for (uint seed = 100; seed < 130; seed++)
        {
            var world = MapGenerator.Generate(seed);
            var r = WorldSolver.Solve(world);
            Assert.Contains(AbilityType.DoubleJump, r.AbilityOrder);
        }
    }
}
