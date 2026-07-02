using System.Numerics;
using Aetheria.Engine.Abilities;
using Aetheria.Engine.Core;
using Aetheria.Engine.Maths;
using Aetheria.Engine.World;
using Xunit;

namespace Aetheria.Tests;

public class RngTests
{
    [Fact]
    public void SameSeedProducesSameSequence()
    {
        var a = new Rng(12345);
        var b = new Rng(12345);
        for (int i = 0; i < 1000; i++)
            Assert.Equal(a.NextULong(), b.NextULong());
    }

    [Fact]
    public void DifferentSeedsDiverge()
    {
        var a = new Rng(1);
        var b = new Rng(2);
        bool anyDifferent = false;
        for (int i = 0; i < 50; i++)
            if (a.NextULong() != b.NextULong()) { anyDifferent = true; break; }
        Assert.True(anyDifferent);
    }

    [Fact]
    public void NextFloatIsInUnitInterval()
    {
        var r = new Rng(99);
        for (int i = 0; i < 10000; i++)
        {
            float f = r.NextFloat();
            Assert.InRange(f, 0f, 0.99999994f);
        }
    }

    [Fact]
    public void RangeIntStaysWithinBounds()
    {
        var r = new Rng(7);
        for (int i = 0; i < 10000; i++)
        {
            int v = r.Range(3, 9);
            Assert.InRange(v, 3, 8);
        }
    }

    [Fact]
    public void RangeIntHandlesDegenerateSpan()
    {
        var r = new Rng(7);
        Assert.Equal(5, r.Range(5, 5));
        Assert.Equal(5, r.Range(5, 4));
    }
}

public class NoiseTests
{
    [Fact]
    public void ValueNoiseIsBounded()
    {
        var n = new Noise(2024);
        for (int i = 0; i < 20000; i++)
        {
            float x = i * 0.13f;
            float y = i * -0.07f + 3.3f;
            float v = n.Value(x, y);
            Assert.InRange(v, 0f, 1f);
        }
    }

    [Fact]
    public void FractalNoiseIsBounded()
    {
        var n = new Noise(555);
        for (int i = 0; i < 20000; i++)
        {
            float v = n.Fractal(i * 0.021f, i * 0.017f, octaves: 5);
            Assert.InRange(v, 0f, 1f);
        }
    }

    [Fact]
    public void SignedFractalIsBounded()
    {
        var n = new Noise(1);
        for (int i = 0; i < 20000; i++)
        {
            float v = n.FractalSigned(i * 0.033f, 10f - i * 0.011f);
            Assert.InRange(v, -1f, 1f);
        }
    }

    [Fact]
    public void NoiseIsDeterministic()
    {
        var a = new Noise(42);
        var b = new Noise(42);
        Assert.Equal(a.Fractal(1.5f, 2.5f), b.Fractal(1.5f, 2.5f));
        Assert.Equal(a.Value(9.1f, -4.2f), b.Value(9.1f, -4.2f));
    }

    [Fact]
    public void NoiseIsContinuous()
    {
        // Value noise should not jump wildly between very close samples.
        var n = new Noise(3);
        float a = n.Value(5.0f, 5.0f);
        float b = n.Value(5.001f, 5.0f);
        Assert.True(MathF.Abs(a - b) < 0.05f);
    }
}

public class AabbTests
{
    [Fact]
    public void IntersectsDetectsOverlap()
    {
        var a = new Aabb(0, 0, 10, 10);
        var b = new Aabb(5, 5, 10, 10);
        Assert.True(a.Intersects(b));
        Assert.True(b.Intersects(a));
    }

    [Fact]
    public void IntersectsExcludesTouchingEdges()
    {
        var a = new Aabb(0, 0, 10, 10);
        var b = new Aabb(10, 0, 10, 10); // shares the right edge
        Assert.False(a.Intersects(b));
        Assert.True(a.Touches(b));
    }

    [Fact]
    public void IntersectsExcludesDisjoint()
    {
        var a = new Aabb(0, 0, 10, 10);
        var b = new Aabb(20, 20, 5, 5);
        Assert.False(a.Intersects(b));
    }

    [Fact]
    public void FromCenterIsCentered()
    {
        var box = Aabb.FromCenter(new Vector2(50, 50), 20, 20);
        Assert.Equal(40, box.X);
        Assert.Equal(40, box.Y);
        Assert.Equal(50, box.CenterX);
        Assert.Equal(50, box.CenterY);
    }

    [Fact]
    public void ContainsPoint()
    {
        var box = new Aabb(0, 0, 10, 10);
        Assert.True(box.Contains(new Vector2(5, 5)));
        Assert.False(box.Contains(new Vector2(11, 5)));
    }

    [Fact]
    public void OverlapDepthIsPositiveWhenOverlapping()
    {
        var a = new Aabb(0, 0, 10, 10);
        var b = new Aabb(8, 0, 10, 10);
        Assert.Equal(2f, a.OverlapX(b), 3);
    }
}

public class TileMapTests
{
    [Fact]
    public void OutOfBoundsIsSolid()
    {
        var m = new TileMap(10, 10);
        Assert.True(m.IsSolidTile(-1, 5));
        Assert.True(m.IsSolidTile(5, -1));
        Assert.True(m.IsSolidTile(10, 5));
        Assert.True(m.IsSolidTile(5, 10));
    }

    [Fact]
    public void EmptyTileIsNotSolid()
    {
        var m = new TileMap(10, 10);
        Assert.False(m.IsSolidTile(5, 5));
    }

    [Fact]
    public void PhaseTileSolidUnlessPhasing()
    {
        var m = new TileMap(10, 10);
        m.Set(4, 4, TileType.Phase);
        Assert.True(m.IsSolidTile(4, 4, phasing: false));
        Assert.False(m.IsSolidTile(4, 4, phasing: true));
    }

    [Fact]
    public void FillClipsToBounds()
    {
        var m = new TileMap(8, 8);
        m.Fill(-5, -5, 3, 3, TileType.Solid);
        Assert.True(m.IsSolidTile(0, 0));
        Assert.True(m.IsSolidTile(3, 3));
        Assert.False(m.IsSolidTile(4, 4));
    }

    [Fact]
    public void BorderSealsEdges()
    {
        var m = new TileMap(6, 6);
        m.Border(TileType.Solid);
        Assert.True(m.IsSolidTile(0, 3));
        Assert.True(m.IsSolidTile(5, 3));
        Assert.True(m.IsSolidTile(3, 0));
        Assert.True(m.IsSolidTile(3, 5));
        Assert.False(m.IsSolidTile(3, 3));
    }

    [Fact]
    public void WorldTileConversionRoundTrips()
    {
        var m = new TileMap(10, 10, 16);
        Assert.Equal(2, m.WorldToTileX(40f));
        Assert.Equal(2, m.WorldToTileX(47.9f));
        Assert.Equal(3, m.WorldToTileX(48f));
        Assert.Equal(32f, m.TileToWorldX(2));
    }

    [Fact]
    public void OverlapsSolidDetectsSolidTiles()
    {
        var m = new TileMap(10, 10, 16);
        m.Set(2, 2, TileType.Solid);
        var over = new Aabb(2 * 16 + 4, 2 * 16 + 4, 8, 8);
        var clear = new Aabb(5 * 16, 5 * 16, 8, 8);
        Assert.True(m.OverlapsSolid(over));
        Assert.False(m.OverlapsSolid(clear));
    }
}

public class AbilityTests
{
    [Fact]
    public void StartsWithNoAbilities()
    {
        var set = new AbilitySet();
        Assert.Equal(0, set.Count);
        Assert.False(set.Has(AbilityType.Dash));
        Assert.False(set.HasAll);
    }

    [Fact]
    public void UnlockGrantsAbility()
    {
        var set = new AbilitySet();
        Assert.True(set.Unlock(AbilityType.Dash));
        Assert.True(set.Has(AbilityType.Dash));
        Assert.Equal(1, set.Count);
    }

    [Fact]
    public void UnlockIsIdempotent()
    {
        var set = new AbilitySet();
        Assert.True(set.Unlock(AbilityType.Phase));
        Assert.False(set.Unlock(AbilityType.Phase));
        Assert.Equal(1, set.Count);
    }

    [Fact]
    public void HasAllWhenEveryAbilityUnlocked()
    {
        var set = new AbilitySet();
        foreach (var a in AbilitySet.All) set.Unlock(a);
        Assert.True(set.HasAll);
        Assert.Equal(AbilitySet.All.Count, set.Count);
    }
}

public class InputTests
{
    [Fact]
    public void ScriptedSourceReplaysThenReturnsNone()
    {
        var src = new ScriptedInputSource()
            .Add(InputState.Jump())
            .Hold(InputState.Move(1f), 3);
        Assert.True(src.Poll().JumpPressed);
        Assert.Equal(1f, src.Poll().MoveX);
        src.Poll();
        src.Poll();
        // exhausted
        Assert.Equal(InputState.None.MoveX, src.Poll().MoveX);
        Assert.True(src.Exhausted);
    }
}
