using System.Text.Json;
using System.Text.Json.Serialization;

namespace Greenlight.CursorClient;

/// <summary>What the halo is wearing. One of these per thing Greenlight can say.</summary>
public enum HaloState
{
    /// <summary>No Greenlight to ask. Grey, or nothing at all — see <see cref="CursorConfig.ShowWhenOff"/>.</summary>
    Off,
    Green,
    Amber,
    Red,
}

/// <summary>How the halo is drawn round the cursor.</summary>
public enum HaloStyle
{
    /// <summary>A soft pool of light, brightest at the cursor and feathering out to nothing.</summary>
    Glow,

    /// <summary>A crisp ring around the cursor, and nothing inside it.</summary>
    Ring,

    /// <summary>Both: the ring, with the glow behind it.</summary>
    Both,
}

/// <summary>Where the halo sits relative to the tip of the cursor.</summary>
/// <remarks>
/// The arrow's hotspot is its tip, and the body of the arrow hangs down and a little to the
/// right of it. A halo centred on the hotspot is therefore a halo centred on the one pixel the
/// cursor is pointing at, which is usually the pixel somebody is trying to look at. Everything
/// here except <see cref="UnderCursor"/> moves the halo off that point by
/// <see cref="CursorConfig.PlacementDistance"/>.
/// </remarks>
public enum HaloPlacement
{
    /// <summary>
    /// Down and a little to the right: tucked behind the body of the arrow, off whatever it is
    /// pointing at. The default.
    /// </summary>
    BehindArrow,

    /// <summary>Straight down from the tip.</summary>
    Below,

    /// <summary>Down and to the left, at a full diagonal.</summary>
    BelowLeft,

    /// <summary>Down and to the right, at a full diagonal — a longer reach than <see cref="BehindArrow"/>.</summary>
    BelowRight,

    /// <summary>Straight up from the tip.</summary>
    Above,

    Left,

    Right,

    /// <summary>Centred on the tip itself, with the cursor in the middle of the halo.</summary>
    UnderCursor,
}

/// <summary>
/// The halo, read from a JSON file the user can edit. Written out with the defaults the first
/// time it is missing, so "where do I change the green" has an answer that does not involve
/// rebuilding anything.
/// </summary>
/// <remarks>
/// Kept in AppData rather than beside the executable: the executable lives under <c>bin</c>,
/// which a rebuild is entitled to delete, and losing somebody's colours to a rebuild would be
/// its own small betrayal.
/// </remarks>
public sealed class CursorConfig
{
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static string DefaultPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Greenlight.CursorHalo", "cursor.json");

    // ── colours ───────────────────────────────────────────────────────────────
    // Any hex Avalonia can parse — "#4BFF86", or "#CC4BFF86" with its own alpha. The alpha is
    // multiplied by Opacity below, so a translucent colour and a low opacity stack.

    /// <summary>No Greenlight to ask: a cold grey, so the halo is visibly saying nothing.</summary>
    public string WhenOff { get; set; } = "#6A7079";

    /// <summary>Everything passing.</summary>
    public string WhenGreen { get; set; } = "#4BFF86";

    /// <summary>A pull request waiting on you.</summary>
    public string WhenAmber { get; set; } = "#FFCE42";

    /// <summary>A broken pipeline.</summary>
    public string WhenRed { get; set; } = "#FF4E3C";

    /// <summary>A pool of light, a ring, or both.</summary>
    public HaloStyle Style { get; set; } = HaloStyle.Glow;

    /// <summary>Where the halo sits relative to the tip of the cursor.</summary>
    public HaloPlacement Placement { get; set; } = HaloPlacement.BehindArrow;

    /// <summary>
    /// How far off the tip the halo sits, in logical pixels — so the nudge keeps pace with the
    /// cursor itself on a scaled display. Ignored by <see cref="HaloPlacement.UnderCursor"/>,
    /// which has nowhere to move to.
    /// </summary>
    /// <remarks>
    /// Sixteen, which on a cursor of the ordinary size puts the halo about where the middle of
    /// the arrow is: far enough to leave the tip clear, close enough that it still reads as the
    /// cursor's own halo rather than as a second thing following the cursor about.
    /// </remarks>
    public double PlacementDistance { get; set; } = 16;

    /// <summary>
    /// How far the halo reaches from the tip of the cursor, in logical pixels — so the same
    /// number is the same apparent size on a scaled display. This is the resting size; a build
    /// under way breathes it out to about a third bigger.
    /// </summary>
    public double Size { get; set; } = 36;

    /// <summary>Overall opacity, for when the halo is louder than you want it to be.</summary>
    public double Opacity { get; set; } = 0.85;

    /// <summary>
    /// How fast the halo breathes while a build is running, as a multiple of its ordinary
    /// pace. Nothing here means anything about the build; it is a setting because the pace
    /// that reads as "working" to one person reads as "alarm" to the next.
    /// </summary>
    public double PulseSpeed { get; set; } = 1.0;

    /// <summary>
    /// How long the mouse has to sit still before the halo fades away, in seconds. Zero or
    /// less means never — the halo is a status light, and a status light that switches
    /// itself off is only sometimes what somebody wants. It comes straight back on the first
    /// movement.
    /// </summary>
    public double IdleFadeSeconds { get; set; } = 0;

    /// <summary>Draw a grey halo when Greenlight is away, rather than nothing at all.</summary>
    /// <remarks>
    /// On by default. With it off, "Greenlight has stopped" and "the halo has stopped" look
    /// identical, and the difference is exactly the thing a status light exists to show.
    /// </remarks>
    public bool ShowWhenOff { get; set; } = true;

    /// <summary>
    /// How far to move the middle of the halo off the tip of the cursor, in logical pixels,
    /// with y counting downwards as the screen does.
    /// </summary>
    /// <remarks>
    /// The diagonals are unit vectors rather than <c>(distance, distance)</c>, so that picking a
    /// corner changes which way the halo sits and not how far away it gets.
    /// <see cref="HaloPlacement.BehindArrow"/> leans right by about a third rather than by a
    /// half, which follows the slope of the arrow's own body instead of a true diagonal.
    /// </remarks>
    public (double X, double Y) Nudge()
    {
        var (x, y) = Placement switch
        {
            HaloPlacement.BehindArrow => Unit(0.35, 1),
            HaloPlacement.Below => (0.0, 1.0),
            HaloPlacement.BelowLeft => Unit(-1, 1),
            HaloPlacement.BelowRight => Unit(1, 1),
            HaloPlacement.Above => (0.0, -1.0),
            HaloPlacement.Left => (-1.0, 0.0),
            HaloPlacement.Right => (1.0, 0.0),
            _ => (0.0, 0.0),
        };

        return (x * PlacementDistance, y * PlacementDistance);

        static (double X, double Y) Unit(double x, double y)
        {
            var length = Math.Sqrt(x * x + y * y);
            return (x / length, y / length);
        }
    }

    /// <summary>The colour for a given state. What the canvas asks, every frame.</summary>
    public string ColourFor(HaloState state) => state switch
    {
        HaloState.Green => WhenGreen,
        HaloState.Amber => WhenAmber,
        HaloState.Red => WhenRed,
        _ => WhenOff,
    };

    /// <summary>
    /// Load the file, writing the defaults out first if it is not there. A file that cannot be
    /// read or parsed falls back to the defaults rather than refusing to start: this is a desk
    /// toy, and a stray comma should not cost you the whole thing.
    /// </summary>
    public static CursorConfig Load(string? path = null)
    {
        var file = path ?? DefaultPath;

        try
        {
            if (!File.Exists(file))
            {
                var fresh = new CursorConfig();
                fresh.Save(file);
                return fresh;
            }

            var loaded = JsonSerializer.Deserialize<CursorConfig>(File.ReadAllText(file), Json);
            if (loaded is null) return new CursorConfig();

            // A file written before one of these existed deserializes it as null, and so does a
            // hand edit that deleted a line. Neither should be a crash on the next frame.
            var defaults = new CursorConfig();
            loaded.WhenOff ??= defaults.WhenOff;
            loaded.WhenGreen ??= defaults.WhenGreen;
            loaded.WhenAmber ??= defaults.WhenAmber;
            loaded.WhenRed ??= defaults.WhenRed;

            // Clamped on the way in, not only on the way out. The file is hand-editable, and a
            // size of 4000 should give you a big halo rather than a window the size of the
            // desktop being dragged about at sixty frames a second.
            loaded.Size = Math.Clamp(loaded.Size, 8, 240);
            loaded.Opacity = Math.Clamp(loaded.Opacity, 0.1, 1.0);

            // Not down to zero: a pulse that never moves is not a pulse, and somebody who typed
            // a 0 in here meant "slow", not "broken".
            loaded.PulseSpeed = Math.Clamp(loaded.PulseSpeed, 0.1, 4.0);
            loaded.IdleFadeSeconds = Math.Clamp(loaded.IdleFadeSeconds, 0, 60 * 60);

            // Far enough to put the halo wherever somebody wants it around the arrow, not so far
            // that it stops being the cursor's halo and becomes a thing chasing the cursor about.
            loaded.PlacementDistance = Math.Clamp(loaded.PlacementDistance, 0, 120);

            return loaded;
        }
        catch
        {
            return new CursorConfig();
        }
    }

    public void Save(string? path = null)
    {
        var file = path ?? DefaultPath;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            File.WriteAllText(file, JsonSerializer.Serialize(this, Json));
        }
        catch
        {
            // A toy that cannot write its config still runs perfectly well on the defaults.
        }
    }

    /// <summary>Take on everything from a freshly-read file, in place.</summary>
    /// <remarks>
    /// Copied into this instance rather than swapping it for the new one: the tray is holding
    /// this object, and it is the tray's menu that has to keep agreeing with the file.
    /// </remarks>
    public void CopyFrom(CursorConfig other)
    {
        WhenOff = other.WhenOff;
        WhenGreen = other.WhenGreen;
        WhenAmber = other.WhenAmber;
        WhenRed = other.WhenRed;
        Style = other.Style;
        Placement = other.Placement;
        PlacementDistance = other.PlacementDistance;
        Size = other.Size;
        Opacity = other.Opacity;
        PulseSpeed = other.PulseSpeed;
        IdleFadeSeconds = other.IdleFadeSeconds;
        ShowWhenOff = other.ShowWhenOff;
    }
}
