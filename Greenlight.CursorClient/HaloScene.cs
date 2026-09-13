namespace Greenlight.CursorClient;

/// <summary>Everything the canvas needs to draw one frame of the halo.</summary>
/// <param name="Shown">Which colour is on screen right now. Lags <see cref="HaloScene.State"/> by a changeover.</param>
/// <param name="Alpha">How strongly to draw it, 0–1. Folds together the changeover, the idle fade and the breath.</param>
/// <param name="Scale">How big to draw it, as a multiple of the resting size. Above 1 on the out-breath.</param>
public readonly record struct HaloFrame(HaloState Shown, double Alpha, double Scale);

/// <summary>
/// The halo's behaviour, with no drawing in it: which colour is showing, how far through a
/// breath it is, and whether the mouse has been still for long enough to let it fade.
/// </summary>
/// <remarks>
/// Deliberately free of Avalonia so that it can be tested — a breath that never quite settles
/// back to steady after a build ends is not something anyone is going to catch by staring at
/// their own cursor.
/// </remarks>
public sealed class HaloScene
{
    /// <summary>How long a colour takes to fade out, and the next to fade in.</summary>
    private static readonly TimeSpan Changeover = TimeSpan.FromSeconds(0.25);

    /// <summary>One full breath — out and back — at <see cref="PulseSpeed"/> 1.</summary>
    private static readonly TimeSpan Breath = TimeSpan.FromSeconds(1.6);

    /// <summary>How long the breath takes to come up when a build starts, and to settle when it ends.</summary>
    private static readonly TimeSpan BreathEase = TimeSpan.FromSeconds(0.6);

    /// <summary>How long the halo takes to fade once the mouse has been still for long enough.</summary>
    private static readonly TimeSpan IdleFadeOut = TimeSpan.FromSeconds(1.0);

    /// <summary>How long it takes to come back on the first movement. Quick, so it never reads as lagging.</summary>
    private static readonly TimeSpan IdleFadeIn = TimeSpan.FromSeconds(0.15);

    /// <summary>How much bigger the halo is at the top of a breath.</summary>
    private const double BreathGrowth = 0.35;

    /// <summary>How much dimmer it is at the bottom of one.</summary>
    private const double BreathDip = 0.35;

    private HaloState _target = HaloState.Off;

    private double _phase;       // where in the breath, in turns
    private double _breath;      // 0..1, how much of the breath is applied
    private TimeSpan _stillFor;  // how long since the mouse last moved

    /// <summary>What Greenlight said. Setting it starts a changeover; <see cref="Shown"/> catches up.</summary>
    public HaloState State
    {
        get => _target;
        set
        {
            if (_target == value) return;
            _target = value;

            // Nothing on screen to put away, so the first colour after a cold start — or after
            // the halo has faded out — is simply worn rather than waited for.
            if (Fade <= 0) Shown = value;
        }
    }

    /// <summary>Whether a build is running. The breath comes up while it is and settles when it is not.</summary>
    public bool IsBuilding { get; set; }

    /// <summary>Whether <see cref="HaloState.Off"/> is drawn grey, or not drawn at all.</summary>
    public bool ShowWhenOff { get; set; } = true;

    /// <summary>A multiple of the ordinary breathing pace.</summary>
    public double PulseSpeed { get; set; } = 1.0;

    /// <summary>How long the mouse must be still before the halo fades. <see cref="TimeSpan.Zero"/> means never.</summary>
    public TimeSpan IdleFade { get; set; } = TimeSpan.Zero;

    /// <summary>The colour currently on screen.</summary>
    public HaloState Shown { get; private set; } = HaloState.Off;

    /// <summary>How much of <see cref="Shown"/> is on screen, 0–1. Below 1 during a changeover, or when Off is not drawn.</summary>
    public double Fade { get; private set; }

    /// <summary>Whether a colour is on its way out so the next can come in.</summary>
    public bool IsChangingOver => Shown != _target;

    /// <summary>Where in the breath the halo is, 0 at rest and 1 fully out. Zero for as long as no build is running.</summary>
    public double Pulse { get; private set; }

    /// <summary>How awake the halo is, 1 with the mouse moving and 0 once it has faded for stillness.</summary>
    public double Awake { get; private set; } = 1;

    /// <summary>Tell the scene the mouse moved. What holds off the idle fade.</summary>
    public void Moved() => _stillFor = TimeSpan.Zero;

    /// <summary>Advance by one frame's worth of time.</summary>
    public void Advance(TimeSpan elapsed)
    {
        var dt = elapsed.TotalSeconds;
        if (dt <= 0) return;

        AdvanceChangeover(dt);
        AdvanceBreath(dt);
        AdvanceIdle(elapsed);
    }

    /// <summary>What to draw right now.</summary>
    public HaloFrame Frame()
    {
        // The dip is scaled by how much of the breath is applied, so a build that has just
        // ended eases back to full brightness rather than snapping there.
        var brightness = 1 - BreathDip * (_breath - Pulse);
        return new HaloFrame(Shown, Fade * Awake * brightness, 1 + BreathGrowth * Pulse);
    }

    private void AdvanceChangeover(double dt)
    {
        var step = dt / Changeover.TotalSeconds;

        if (IsChangingOver)
        {
            // Out first, all the way, and only then the new colour: a halo that blended from
            // green to red where it stood would spend a few frames being a colour that means
            // nothing.
            Fade = Math.Max(0, Fade - step);
            if (Fade <= 0) Shown = _target;
            return;
        }

        var wanted = Shown == HaloState.Off && !ShowWhenOff ? 0 : 1;
        Fade = Fade < wanted ? Math.Min(wanted, Fade + step) : Math.Max(wanted, Fade - step);
    }

    private void AdvanceBreath(double dt)
    {
        var ease = dt / BreathEase.TotalSeconds;
        _breath = IsBuilding ? Math.Min(1, _breath + ease) : Math.Max(0, _breath - ease);

        if (_breath <= 0)
        {
            // Parked at rest rather than wherever it happened to be, so the next build starts
            // from a halo at its resting size and breathes out — not from the middle of an
            // in-breath.
            _phase = 0;
            Pulse = 0;
            return;
        }

        _phase = (_phase + dt * PulseSpeed / Breath.TotalSeconds) % 1;

        // A cosine rather than a triangle: it lingers at the ends and hurries through the
        // middle, which is what breathing looks like and what a sawtooth does not.
        var wave = 0.5 - 0.5 * Math.Cos(_phase * 2 * Math.PI);
        Pulse = _breath * wave;
    }

    private void AdvanceIdle(TimeSpan elapsed)
    {
        if (IdleFade <= TimeSpan.Zero)
        {
            Awake = 1;
            return;
        }

        _stillFor += elapsed;

        if (_stillFor >= IdleFade)
            Awake = Math.Max(0, Awake - elapsed.TotalSeconds / IdleFadeOut.TotalSeconds);
        else
            Awake = Math.Min(1, Awake + elapsed.TotalSeconds / IdleFadeIn.TotalSeconds);
    }
}
