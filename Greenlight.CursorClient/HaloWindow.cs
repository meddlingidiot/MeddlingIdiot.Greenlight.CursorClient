using System.Diagnostics;
using System.Runtime.Versioning;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;

namespace Greenlight.CursorClient;

/// <summary>
/// A small transparent, always-on-top, click-through window that follows the mouse about, with
/// the halo drawn in the middle of it.
/// </summary>
/// <remarks>
/// <para>
/// A window that follows the cursor rather than one laid over the whole desktop. The overlay
/// would be simpler to move — it never does — but a single window across two monitors at
/// different scales has one <see cref="TopLevel.RenderScaling"/> and two truths about where a
/// pixel is, and the halo would sit a little off the cursor on whichever screen lost. A window
/// that travels with the mouse is re-scaled by Windows as it crosses over, and the maths stays
/// "put the middle on the cursor".
/// </para>
/// <para>
/// The system cursor itself is never touched. Swapping it for a coloured one would need putting
/// back on exit, and a desk toy that crashed and left somebody with a red arrow until they
/// found the setting would be a desk toy they uninstalled.
/// </para>
/// </remarks>
[SupportedOSPlatform("windows")]
public sealed class HaloWindow : Window
{
    /// <summary>Room around the halo at its biggest, so the out-breath is never clipped by the window's edge.</summary>
    private const double Breathing = 6;

    private readonly CursorConfig _config;
    private readonly HaloCanvas _canvas;
    private readonly DispatcherTimer _frames;
    private readonly Stopwatch _clock = Stopwatch.StartNew();

    private TimeSpan _lastFrame;
    private TimeSpan _lastHousekeeping;
    private (int X, int Y)? _cursor;
    private HaloFrame _drawn;

    public HaloWindow(CursorConfig config, HaloScene scene)
    {
        _config = config;
        Scene = scene;

        Title = "Greenlight cursor halo";
        WindowDecorations = WindowDecorations.None;
        CanResize = false;
        ShowInTaskbar = false;
        Topmost = true;
        WindowStartupLocation = WindowStartupLocation.Manual;

        // Never take the caret out of somebody's editor. Paired with WS_EX_NOACTIVATE, which is
        // what makes a click landing here harmless in the first place.
        ShowActivated = false;

        Background = Brushes.Transparent;
        TransparencyLevelHint = [WindowTransparencyLevel.Transparent];

        _canvas = new HaloCanvas(scene, config);
        Content = _canvas;

        // Off screen until the first frame has asked where the mouse is: a window that flashed
        // up in the corner and then jumped to the cursor would be a visible stutter on every
        // start.
        Position = new PixelPoint(-10000, -10000);
        FitToHalo();

        // 60fps. A halo that lags the mouse is the whole effect going wrong, and the frame is a
        // couple of dozen ellipses.
        _frames = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(16) };
        _frames.Tick += OnFrame;
    }

    public HaloScene Scene { get; }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);

        // Needs a real window handle, so it cannot run before the window is shown.
        ClickThroughNative.Apply(TryGetPlatformHandle()?.Handle ?? IntPtr.Zero);

        _lastFrame = _clock.Elapsed;
        _frames.Start();
    }

    protected override void OnClosed(EventArgs e)
    {
        _frames.Stop();
        base.OnClosed(e);
    }

    /// <summary>
    /// Take up a setting changed from the tray while the halo is out. All of them can be
    /// absorbed where it stands: the canvas reads the colours, the style and the size each
    /// frame, and the scene reads the rest.
    /// </summary>
    public void ApplyConfig()
    {
        Scene.ShowWhenOff = _config.ShowWhenOff;
        Scene.PulseSpeed = _config.PulseSpeed;
        Scene.IdleFade = TimeSpan.FromSeconds(_config.IdleFadeSeconds);
        FitToHalo();
        Follow(force: true);

        // The frame loop skips repaints when the scene has not changed, and a new colour or
        // style is not a change the scene knows about.
        _canvas.InvalidateVisual();
    }

    /// <summary>The window is square, big enough for the halo at the top of a breath.</summary>
    private void FitToHalo()
    {
        var side = Math.Ceiling(_config.Size * 1.4 * 2 + Breathing * 2);
        Width = side;
        Height = side;
    }

    private void OnFrame(object? sender, EventArgs e)
    {
        var now = _clock.Elapsed;
        var elapsed = now - _lastFrame;
        _lastFrame = now;

        // Once a second: re-claim the top of the z-order. The taskbar and every other topmost
        // window take turns at it, and a halo that went behind the taskbar whenever the mouse
        // went there would look broken exactly where people look most.
        if (now - _lastHousekeeping >= TimeSpan.FromSeconds(1))
        {
            _lastHousekeeping = now;
            ClickThroughNative.KeepOnTop(TryGetPlatformHandle()?.Handle ?? IntPtr.Zero);
        }

        Follow(force: false);
        Scene.Advance(elapsed);

        // Only redraw when something would look different. With the mouse still and no build
        // running, that is never — and a window repainting sixty times a second to draw the
        // same halo is a laptop fan for nothing.
        var frame = Scene.Frame();
        if (frame != _drawn)
        {
            _drawn = frame;
            _canvas.InvalidateVisual();
        }
    }

    /// <summary>
    /// Put the middle of the window where the halo goes — on the cursor, or the configured
    /// nudge away from it — if the cursor has moved.
    /// </summary>
    private void Follow(bool force)
    {
        var cursor = CursorNative.Position();
        if (cursor is null) return;   // a locked desktop; stay where we were

        if (cursor == _cursor && !force) return;

        if (cursor != _cursor) Scene.Moved();
        _cursor = cursor;

        // Width and the nudge are both logical, Position is physical: both have to be scaled up
        // before they meet the cursor's physical coordinates, or the halo sits a little off the
        // arrow on every scaled display, which is most of them.
        var scaling = RenderScaling <= 0 ? 1 : RenderScaling;
        var half = (int)Math.Round(Width * scaling / 2);
        var (nudgeX, nudgeY) = _config.Nudge();

        Position = new PixelPoint(
            cursor.Value.X - half + (int)Math.Round(nudgeX * scaling),
            cursor.Value.Y - half + (int)Math.Round(nudgeY * scaling));
    }
}
