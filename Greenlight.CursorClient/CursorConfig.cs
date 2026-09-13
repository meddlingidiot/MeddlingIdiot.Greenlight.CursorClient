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
        Size = other.Size;
        Opacity = other.Opacity;
        PulseSpeed = other.PulseSpeed;
        IdleFadeSeconds = other.IdleFadeSeconds;
        ShowWhenOff = other.ShowWhenOff;
    }
}
