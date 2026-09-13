using Greenlight.CursorClient;

namespace Greenlight.CursorClient.UnitTests;

/// <summary>
/// The scene, without a window. Everything here is arithmetic somebody would otherwise have to
/// check by staring at their own cursor — which is a thing people are bad at noticing, because
/// it is the one thing on the screen they are never looking at.
/// </summary>
public class HaloSceneTests
{
    private const double Step = 1.0 / 60;

    private static HaloScene Lit(HaloState state = HaloState.Green)
    {
        var scene = new HaloScene { State = state };
        Run(scene, 1);
        return scene;
    }

    private static void Run(HaloScene scene, double seconds)
    {
        for (var t = 0.0; t < seconds; t += Step) scene.Advance(TimeSpan.FromSeconds(Step));
    }

    // ── colours and the changeover ────────────────────────────────────────────

    [Fact]
    public void FirstColourArrivesWithoutAChangeover()
    {
        var scene = new HaloScene();

        // Nothing on screen to put away, so the first snapshot after a cold start is simply worn
        // rather than waited for.
        scene.State = HaloState.Green;

        Assert.Equal(HaloState.Green, scene.Shown);
        Assert.False(scene.IsChangingOver);
    }

    [Fact]
    public void AChangeOfColourFadesTheOldOneOutFirst()
    {
        var scene = Lit();

        scene.State = HaloState.Red;

        // Still green on the very next frame: the halo must not change colour where it stands.
        scene.Advance(TimeSpan.FromSeconds(Step));
        Assert.Equal(HaloState.Green, scene.Shown);
        Assert.True(scene.IsChangingOver);

        Run(scene, 1);
        Assert.Equal(HaloState.Red, scene.Shown);
        Assert.False(scene.IsChangingOver);
        Assert.Equal(1, scene.Fade);
    }

    [Fact]
    public void TheNewColourIsNeverDrawnBeforeTheOldOneHasGone()
    {
        var scene = Lit();
        scene.State = HaloState.Red;

        var flipped = false;
        for (var t = 0.0; t < 1 && !flipped; t += Step)
        {
            scene.Advance(TimeSpan.FromSeconds(Step));
            flipped = scene.Shown == HaloState.Red;

            // Red is put on only in the frame green reaches nothing, and at nothing itself.
            if (flipped) Assert.Equal(0, scene.Fade);
        }

        Assert.True(flipped, "never changed over");
    }

    [Fact]
    public void GreenlightGoingAwayShowsGreyByDefault()
    {
        var scene = Lit();

        scene.State = HaloState.Off;
        Run(scene, 1);

        Assert.Equal(HaloState.Off, scene.Shown);
        Assert.Equal(1, scene.Fade);
    }

    [Fact]
    public void GreenlightGoingAwayFadesAllTheWayOutWhenAsked()
    {
        var scene = Lit();
        scene.ShowWhenOff = false;

        scene.State = HaloState.Off;
        Run(scene, 1);

        Assert.Equal(HaloState.Off, scene.Shown);
        Assert.Equal(0, scene.Fade);
        Assert.Equal(0, scene.Frame().Alpha);
    }

    [Fact]
    public void AColourAfterAFadeOutIsWornStraightAway()
    {
        var scene = Lit();
        scene.ShowWhenOff = false;
        scene.State = HaloState.Off;
        Run(scene, 1);

        // Nothing on screen, so there is nothing to wait for — the same rule as the cold start.
        scene.State = HaloState.Amber;
        Assert.Equal(HaloState.Amber, scene.Shown);
        Assert.False(scene.IsChangingOver);
    }

    [Fact]
    public void TurningGreyOffWhileGreyIsShowingFadesItOut()
    {
        var scene = Lit(HaloState.Off);
        Assert.Equal(1, scene.Fade);

        scene.ShowWhenOff = false;
        Run(scene, 1);

        Assert.Equal(0, scene.Fade);
    }

    // ── the breath ────────────────────────────────────────────────────────────

    [Fact]
    public void SteadyWhenNothingIsBuilding()
    {
        var scene = Lit();
        Run(scene, 3);

        var frame = scene.Frame();
        Assert.Equal(0, scene.Pulse);
        Assert.Equal(1, frame.Scale);
        Assert.Equal(1, frame.Alpha);
    }

    [Fact]
    public void ABuildMakesItBreathe()
    {
        var scene = Lit();
        scene.IsBuilding = true;

        // Let the breath come up, then watch a whole cycle.
        Run(scene, 1);

        var biggest = 0.0;
        var smallest = double.MaxValue;
        var dimmest = double.MaxValue;
        for (var t = 0.0; t < 1.6; t += Step)
        {
            scene.Advance(TimeSpan.FromSeconds(Step));
            var frame = scene.Frame();
            biggest = Math.Max(biggest, frame.Scale);
            smallest = Math.Min(smallest, frame.Scale);
            dimmest = Math.Min(dimmest, frame.Alpha);
        }

        Assert.True(biggest > 1.3, $"never breathed out (largest {biggest})");
        Assert.True(smallest < 1.02, $"never breathed back in (smallest {smallest})");
        Assert.True(dimmest < 0.7, $"never dimmed on the in-breath (dimmest {dimmest})");
    }

    [Fact]
    public void TheBreathComesUpRatherThanSnapping()
    {
        var scene = Lit();
        scene.IsBuilding = true;

        // The first frame of a build is not the top of a breath. Even at the worst phase, the
        // amplitude has only just started to come up.
        scene.Advance(TimeSpan.FromSeconds(Step));
        Assert.True(scene.Frame().Scale < 1.05);
    }

    [Fact]
    public void ABuildEndingSettlesBackToSteady()
    {
        var scene = Lit();
        scene.IsBuilding = true;
        Run(scene, 2);

        scene.IsBuilding = false;
        Run(scene, 2);

        var frame = scene.Frame();
        Assert.Equal(0, scene.Pulse);
        Assert.Equal(1, frame.Scale);
        Assert.Equal(1, frame.Alpha, 6);
    }

    [Fact]
    public void ABuildEndingNeverLeavesItDimmerThanSteady()
    {
        var scene = Lit();
        scene.IsBuilding = true;
        Run(scene, 2);

        scene.IsBuilding = false;
        for (var t = 0.0; t < 2; t += Step)
        {
            scene.Advance(TimeSpan.FromSeconds(Step));
            Assert.InRange(scene.Frame().Alpha, 0.6, 1.0000001);
        }
    }

    [Fact]
    public void PulseSpeedIsAMultiple()
    {
        var slow = Lit();
        var quick = Lit();
        slow.IsBuilding = quick.IsBuilding = true;
        slow.PulseSpeed = 1.0;
        quick.PulseSpeed = 2.0;
        Run(slow, 1);
        Run(quick, 1);

        Assert.Equal(CountPeaks(slow, 8) * 2, CountPeaks(quick, 8), 1.0);
    }

    private static int CountPeaks(HaloScene scene, double seconds)
    {
        var peaks = 0;
        var previous = scene.Pulse;
        var rising = false;
        for (var t = 0.0; t < seconds; t += Step)
        {
            scene.Advance(TimeSpan.FromSeconds(Step));
            var now = scene.Pulse;
            if (rising && now < previous) peaks++;
            rising = now > previous;
            previous = now;
        }

        return peaks;
    }

    // ── the idle fade ─────────────────────────────────────────────────────────

    [Fact]
    public void NeverFadesForStillnessUnlessAsked()
    {
        var scene = Lit();
        Run(scene, 30);

        Assert.Equal(1, scene.Awake);
    }

    [Fact]
    public void FadesOnceTheMouseHasBeenStillLongEnough()
    {
        var scene = Lit();
        scene.IdleFade = TimeSpan.FromSeconds(2);
        scene.Moved();

        Run(scene, 1.5);
        Assert.Equal(1, scene.Awake);

        Run(scene, 2.5);
        Assert.Equal(0, scene.Awake);
        Assert.Equal(0, scene.Frame().Alpha);
    }

    [Fact]
    public void ComesBackOnTheFirstMovement()
    {
        var scene = Lit();
        scene.IdleFade = TimeSpan.FromSeconds(1);
        Run(scene, 4);
        Assert.Equal(0, scene.Awake);

        scene.Moved();
        Run(scene, 0.3);

        Assert.Equal(1, scene.Awake);
    }

    [Fact]
    public void MovingKeepsItAwake()
    {
        var scene = Lit();
        scene.IdleFade = TimeSpan.FromSeconds(1);

        for (var t = 0.0; t < 5; t += Step)
        {
            if ((int)(t * 2) != (int)((t - Step) * 2)) scene.Moved();   // twice a second
            scene.Advance(TimeSpan.FromSeconds(Step));
            Assert.Equal(1, scene.Awake);
        }
    }

    [Fact]
    public void TurningTheIdleFadeOffWakesItUp()
    {
        var scene = Lit();
        scene.IdleFade = TimeSpan.FromSeconds(1);
        Run(scene, 4);
        Assert.Equal(0, scene.Awake);

        scene.IdleFade = TimeSpan.Zero;
        scene.Advance(TimeSpan.FromSeconds(Step));

        Assert.Equal(1, scene.Awake);
    }
}
