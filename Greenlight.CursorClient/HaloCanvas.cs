using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Greenlight.CursorClient;

/// <summary>Draws the halo, centred on the control — the window puts the control on the cursor.</summary>
public sealed class HaloCanvas : Control
{
    /// <summary>How many rings the glow is built from.</summary>
    /// <remarks>
    /// Rather more than it looks like it needs. Each ring is a hard-edged ellipse, so a handful
    /// of them at a workable alpha reads as a target painted round the cursor rather than as
    /// light — the fix is many rings, each almost invisible on its own.
    /// </remarks>
    private const int GlowRings = 18;

    /// <summary>
    /// Where the glow starts, as a fraction of its reach. Not at the very tip: a pool of light
    /// that is brightest exactly under the arrow is a pool of light over whatever the arrow is
    /// pointing at, and the point of the cursor is to point at things.
    /// </summary>
    private const double GlowHole = 0.22;

    private static readonly Color Fallback = Color.FromRgb(0x6A, 0x70, 0x79);

    private readonly HaloScene _scene;
    private readonly CursorConfig _config;

    private string? _lastHex;
    private Color _lastColour;

    public HaloCanvas(HaloScene scene, CursorConfig config)
    {
        _scene = scene;
        _config = config;

        // Belt and braces with the Win32 click-through: nothing in this window should ever be
        // a thing the mouse can land on.
        IsHitTestVisible = false;
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var frame = _scene.Frame();
        var colour = Colour(_config.ColourFor(frame.Shown));

        // The colour's own alpha, the config's opacity and the scene's fade all stack. A colour
        // written as "#804BFF86" is somebody asking for a quieter green, and they should get it.
        var alpha = frame.Alpha * _config.Opacity * (colour.A / 255.0);
        if (alpha < 0.003) return;

        var opaque = Color.FromRgb(colour.R, colour.G, colour.B);
        var centre = new Point(Bounds.Width / 2, Bounds.Height / 2);
        var reach = _config.Size * frame.Scale;

        if (_config.Style is HaloStyle.Glow or HaloStyle.Both) DrawGlow(context, opaque, alpha, centre, reach);
        if (_config.Style is HaloStyle.Ring or HaloStyle.Both) DrawRing(context, opaque, alpha, centre, reach);
    }

    private static void DrawGlow(DrawingContext context, Color colour, double alpha, Point centre, double reach)
    {
        for (var i = GlowRings; i >= 1; i--)
        {
            var t = (double)i / GlowRings;
            var radius = reach * (GlowHole + (1 - GlowHole) * t);

            // A falloff between linear and squared, and an alpha low enough that no single ring
            // has a visible edge — what is seen is the pile of them towards the middle. Squared
            // was tried first and left the outer half of the reach doing nothing.
            var ring = 0.12 * Math.Pow(1 - t, 1.5) * alpha;
            if (ring < 0.002) continue;

            context.DrawEllipse(new SolidColorBrush(colour, ring), null, centre, radius, radius);
        }
    }

    private static void DrawRing(DrawingContext context, Color colour, double alpha, Point centre, double reach)
    {
        var radius = reach * 0.86;
        var thickness = Math.Max(2, reach * 0.09);

        // A wider, fainter ring under the crisp one. Without it the ring is a hairline that
        // disappears into a busy page; with it, it has a little light of its own.
        context.DrawEllipse(null, new Pen(new SolidColorBrush(colour, alpha * 0.28), thickness * 2.6), centre, radius, radius);
        context.DrawEllipse(null, new Pen(new SolidColorBrush(colour, alpha), thickness), centre, radius, radius);
    }

    /// <summary>Parse a hex from the file, remembering the last one: it is the same string sixty times a second.</summary>
    private Color Colour(string hex)
    {
        if (ReferenceEquals(hex, _lastHex) || hex == _lastHex) return _lastColour;

        _lastHex = hex;
        _lastColour = Color.TryParse(hex, out var parsed) ? parsed : Fallback;
        return _lastColour;
    }
}
