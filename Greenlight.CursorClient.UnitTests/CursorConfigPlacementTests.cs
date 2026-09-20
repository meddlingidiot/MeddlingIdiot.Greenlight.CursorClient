using Greenlight.CursorClient;

namespace Greenlight.CursorClient.UnitTests;

/// <summary>
/// Where the halo sits relative to the tip of the cursor. Arithmetic rather than anything on
/// screen, because "is it a little to the right?" is a question nobody can answer by looking at
/// their own cursor — which is the one thing on the screen they are never looking at.
/// </summary>
public class CursorConfigPlacementTests
{
    [Fact]
    public void OutOfTheBoxTheHaloSitsBehindTheArrow()
    {
        var config = new CursorConfig();

        Assert.Equal(HaloPlacement.BehindArrow, config.Placement);

        // And well clear of it: the furthest the tray offers, so the halo is off the arrow
        // altogether rather than tucked under its body.
        Assert.Equal(48, config.PlacementDistance);

        var (x, y) = config.Nudge();

        // Down, and to the right by rather less: tucked behind the body of the arrow rather than
        // out on a diagonal from it.
        Assert.True(y > 0);
        Assert.True(x > 0);
        Assert.True(x < y);
    }

    [Fact]
    public void UnderTheCursorIsTheCursor()
    {
        var config = new CursorConfig { Placement = HaloPlacement.UnderCursor, PlacementDistance = 48 };

        // Even with a distance set. There is nowhere for "centred on the tip" to move to, and a
        // distance left over from another placement should not quietly push it off.
        Assert.Equal((0.0, 0.0), config.Nudge());
    }

    [Theory]
    [InlineData(HaloPlacement.Below, 0, 1)]
    [InlineData(HaloPlacement.Above, 0, -1)]
    [InlineData(HaloPlacement.Left, -1, 0)]
    [InlineData(HaloPlacement.Right, 1, 0)]
    [InlineData(HaloPlacement.BelowLeft, -1, 1)]
    [InlineData(HaloPlacement.BelowRight, 1, 1)]
    public void EachPlacementPointsTheWayItIsNamed(HaloPlacement placement, int x, int y)
    {
        var (nudgeX, nudgeY) = new CursorConfig { Placement = placement }.Nudge();

        Assert.Equal(Math.Sign(x), Math.Sign(nudgeX));
        Assert.Equal(Math.Sign(y), Math.Sign(nudgeY));
    }

    [Theory]
    [InlineData(HaloPlacement.BehindArrow)]
    [InlineData(HaloPlacement.Below)]
    [InlineData(HaloPlacement.BelowLeft)]
    [InlineData(HaloPlacement.BelowRight)]
    [InlineData(HaloPlacement.Above)]
    [InlineData(HaloPlacement.Left)]
    [InlineData(HaloPlacement.Right)]
    public void ChangingTheDirectionDoesNotChangeTheDistance(HaloPlacement placement)
    {
        var config = new CursorConfig { Placement = placement, PlacementDistance = 20 };

        var (x, y) = config.Nudge();

        // The diagonals are unit vectors, not (distance, distance) — picking a corner off the
        // menu should move the halo round the cursor, not further away from it.
        Assert.Equal(20, Math.Sqrt(x * x + y * y), 6);
    }

    [Fact]
    public void ADistanceTypedIntoTheFileIsBroughtBackIntoRange()
    {
        var file = Path.Combine(Path.GetTempPath(), $"cursor-placement-{Guid.NewGuid():N}.json");

        try
        {
            new CursorConfig { PlacementDistance = 5000 }.Save(file);

            Assert.Equal(120, CursorConfig.Load(file).PlacementDistance);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public void APlacementSurvivesTheRoundTripThroughTheFile()
    {
        var file = Path.Combine(Path.GetTempPath(), $"cursor-placement-{Guid.NewGuid():N}.json");

        try
        {
            new CursorConfig { Placement = HaloPlacement.BelowLeft, PlacementDistance = 24 }.Save(file);

            var loaded = CursorConfig.Load(file);

            Assert.Equal(HaloPlacement.BelowLeft, loaded.Placement);
            Assert.Equal(24, loaded.PlacementDistance);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public void AReloadedFileBringsItsPlacementWithIt()
    {
        // What the tray's "Reload the file" does: the config object is the one the tray is
        // holding, so a new placement has to arrive in place rather than as a new object.
        var live = new CursorConfig();

        live.CopyFrom(new CursorConfig { Placement = HaloPlacement.Below, PlacementDistance = 30 });

        Assert.Equal(HaloPlacement.Below, live.Placement);
        Assert.Equal(30, live.PlacementDistance);
    }
}
