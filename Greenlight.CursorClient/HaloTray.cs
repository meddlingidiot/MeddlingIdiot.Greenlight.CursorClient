using System.Diagnostics;
using System.Runtime.Versioning;
using Avalonia.Controls;
using Avalonia.Platform;

namespace Greenlight.CursorClient;

/// <summary>
/// The mascot in the notification area, and the menu hanging off him: the only part of this toy
/// a person can click.
/// </summary>
/// <remarks>
/// <para>
/// The halo is click-through by design — it sits under the mouse all day, and a window there
/// that answered clicks would be a mouse that could not click anything else. That leaves the
/// tray for everything: every setting in <see cref="CursorConfig"/> that can be changed while
/// the thing is running is reachable from here, and each change is written straight back to the
/// file, so the menu and the JSON are always the same settings.
/// </para>
/// <para>
/// Avalonia's own <see cref="TrayIcon"/> rather than a tray library, because the sample is meant
/// to be readable — and because a sample that drags in a dependency to draw one icon is making a
/// point nobody asked for.
/// </para>
/// </remarks>
[SupportedOSPlatform("windows")]
public sealed class HaloTray : IDisposable
{
    private static readonly Uri IconUri = new("avares://Greenlight.CursorClient/Assets/MeddlingIdiot.ico");

    private readonly CursorConfig _config;
    private readonly TrayIcon _tray;
    private readonly NativeMenuItem _status;
    private readonly NativeMenuItem _running;
    private readonly NativeMenuItem _startup;

    public HaloTray(CursorConfig config)
    {
        _config = config;

        _status = new NativeMenuItem { Header = "Waiting for Greenlight…", IsEnabled = false };

        _running = new NativeMenuItem
        {
            Header = "Halo on the cursor",
            ToggleType = MenuItemToggleType.CheckBox,
            IsChecked = true,
        };
        _running.Click += (_, _) => SetRunning(!IsRunning?.Invoke() ?? true);

        // Read from the registry rather than from a setting of ours, every time it is shown: the
        // user can turn this off in Task Manager's Startup tab, and a tick remembering what we
        // last wrote would then be telling them the opposite of the truth.
        _startup = Check("Start with Windows", WindowsStartup.IsEnabled, value => WindowsStartup.Set(value));

        var menu = BuildMenu();

        // The top-level items are not inside a submenu, so nothing else re-ticks them. Only the
        // startup one can actually change behind our back, but it can, and this is the moment to
        // notice.
        menu.Opening += (_, _) => _startup.IsChecked = WindowsStartup.IsEnabled();

        _tray = new TrayIcon
        {
            Icon = new WindowIcon(AssetLoader.Open(IconUri)),
            ToolTipText = "Greenlight cursor halo",
            Menu = menu,
            IsVisible = true,
        };

        // The one thing a left click can mean here. There is no main window to open, and a tray
        // icon that does nothing at all when clicked reads as a hung one.
        _tray.Clicked += (_, _) => SetRunning(!IsRunning?.Invoke() ?? true);
    }

    /// <summary>Whether the halo is currently on the cursor.</summary>
    public Func<bool>? IsRunning { get; set; }

    /// <summary>Put the halo on the cursor, or take it off.</summary>
    public Action<bool>? OnSetRunning { get; set; }

    /// <summary>
    /// A setting changed that the halo can absorb where it stands — which is all of them,
    /// because it is drawn from the config each frame.
    /// </summary>
    public Action? OnConfigChanged { get; set; }

    /// <summary>Re-read the file, for colours changed by hand.</summary>
    public Action? OnReloadConfig { get; set; }

    public Action? OnQuit { get; set; }

    /// <summary>Say what the halo is doing, in the tooltip and at the top of the menu.</summary>
    public void ShowState(HaloState state, bool building)
    {
        var running = IsRunning?.Invoke() ?? true;

        _status.Header = state switch
        {
            HaloState.Green => building ? "Greenlight: green — building" : "Greenlight: green — all passing",
            HaloState.Amber => building
                ? "Greenlight: yellow — building"
                : "Greenlight: yellow — a pull request wants you",
            HaloState.Red => building ? "Greenlight: red — rebuilding" : "Greenlight: red — a pipeline is broken",
            _ => "Greenlight not running — nothing claimed",
        };

        _running.IsChecked = running;

        _tray.ToolTipText = running
            ? $"Greenlight cursor halo — {Short(state)}{(building ? ", building" : string.Empty)}"
            : "Greenlight cursor halo — off";
    }

    private static string Short(HaloState state) => state switch
    {
        HaloState.Green => "green",
        HaloState.Amber => "yellow",
        HaloState.Red => "red",
        _ => "not connected",
    };

    public void Dispose()
    {
        _tray.IsVisible = false;
        _tray.Dispose();
    }

    private void SetRunning(bool running)
    {
        OnSetRunning?.Invoke(running);
        _running.IsChecked = running;
        _tray.ToolTipText = running ? "Greenlight cursor halo" : "Greenlight cursor halo — off";
    }

    private NativeMenu BuildMenu() =>
    [
        _status,
        new NativeMenuItemSeparator(),
        _running,
        new NativeMenuItemSeparator(),
        Submenu("What it looks like",
            Style("A glow", HaloStyle.Glow),
            Style("A ring", HaloStyle.Ring),
            Style("Both", HaloStyle.Both)),
        Submenu("How big",
            Size("Small", 22),
            Size("Ordinary", 36),
            Size("Large", 56),
            Size("Hard to miss", 90)),
        Submenu("How solid",
            Opacity("Solid", 1.0),
            Opacity("Nearly solid", 0.85),
            Opacity("Half there", 0.5),
            Opacity("Barely there", 0.3)),
        Submenu("How fast it breathes",
            Pulse("Slow", 0.6),
            Pulse("Ordinary", 1.0),
            Pulse("Quick", 1.6),
            Pulse("Urgent", 2.4)),
        Submenu("Fade when the mouse is still",
            Idle("Never", 0),
            Idle("After 5 seconds", 5),
            Idle("After 30 seconds", 30),
            Idle("After 2 minutes", 120)),
        Check("Grey halo when Greenlight is away",
            () => _config.ShowWhenOff,
            value =>
            {
                _config.ShowWhenOff = value;
                Persist();
                OnConfigChanged?.Invoke();
            }),
        _startup,
        new NativeMenuItemSeparator(),
        Item("Edit the colours…", EditConfig),
        Item("Reload the file", () => OnReloadConfig?.Invoke()),
        new NativeMenuItemSeparator(),
        Item("Quit", () => OnQuit?.Invoke()),
    ];

    // ── the settings ──────────────────────────────────────────────────────────
    // Deliberately not here: the colours. Four hex strings are not something anybody wants to
    // pick off a menu, so the menu's job there is just to make the file findable.

    private NativeMenuItem Style(string header, HaloStyle style) =>
        Choice(header, () => _config.Style == style, () =>
        {
            _config.Style = style;
            Persist();
            OnConfigChanged?.Invoke();
        });

    private NativeMenuItem Size(string header, double size) =>
        Choice(header, () => Math.Abs(_config.Size - size) < 0.001, () =>
        {
            _config.Size = size;
            Persist();
            OnConfigChanged?.Invoke();
        });

    private NativeMenuItem Opacity(string header, double opacity) =>
        Choice(header, () => Math.Abs(_config.Opacity - opacity) < 0.001, () =>
        {
            _config.Opacity = opacity;
            Persist();
            OnConfigChanged?.Invoke();
        });

    private NativeMenuItem Pulse(string header, double speed) =>
        Choice(header, () => Math.Abs(_config.PulseSpeed - speed) < 0.001, () =>
        {
            _config.PulseSpeed = speed;
            Persist();
            OnConfigChanged?.Invoke();
        });

    private NativeMenuItem Idle(string header, double seconds) =>
        Choice(header, () => Math.Abs(_config.IdleFadeSeconds - seconds) < 0.001, () =>
        {
            _config.IdleFadeSeconds = seconds;
            Persist();
            OnConfigChanged?.Invoke();
        });

    // ── Menu plumbing ─────────────────────────────────────────────────────────
    // Each option asks the config what it should look like when the menu opens rather than being
    // ticked once at startup: the file is editable by hand and reloadable from this very menu, so
    // anything remembering its own state would start lying the moment it was.

    private static NativeMenuItem Check(string header, Func<bool> isOn, Action<bool> set)
    {
        var item = new NativeMenuItem
        {
            Header = header,
            ToggleType = MenuItemToggleType.CheckBox,
            IsChecked = isOn(),
        };

        item.Click += (_, _) =>
        {
            set(!isOn());
            item.IsChecked = isOn();
        };

        return item;
    }

    private static NativeMenuItem Item(string header, Action click)
    {
        var item = new NativeMenuItem { Header = header };
        item.Click += (_, _) => click();
        return item;
    }

    private static NativeMenuItem Submenu(string header, params NativeMenuItem[] items)
    {
        var menu = new NativeMenu();
        foreach (var item in items) menu.Add(item);

        void Retick()
        {
            foreach (var item in items)
                if (item.CommandParameter is Func<bool> isChosen)
                    item.IsChecked = isChosen();
        }

        // Twice, because neither moment is reliable on its own: picking an option has to move
        // the tick off the old one straight away, and opening the menu has to account for the
        // file having been edited by hand behind its back.
        foreach (var item in items) item.Click += (_, _) => Retick();
        menu.Opening += (_, _) => Retick();

        return new NativeMenuItem { Header = header, Menu = menu };
    }

    private static NativeMenuItem Choice(string header, Func<bool> isChosen, Action choose)
    {
        var item = new NativeMenuItem
        {
            Header = header,
            ToggleType = MenuItemToggleType.Radio,
            IsChecked = isChosen(),

            // Parked here rather than in a dictionary: the menu owns its items, and a second
            // collection to keep in step with it is a second thing to get wrong.
            CommandParameter = isChosen,
        };

        item.Click += (_, _) => choose();
        return item;
    }

    private void Persist() => _config.Save();

    /// <summary>
    /// Open <c>cursor.json</c> in whatever the machine opens JSON with. The four colours are the
    /// one thing that is not on the menu, so the menu's job there is to make the file findable.
    /// </summary>
    private void EditConfig()
    {
        try
        {
            // It is written out on first run, but a deleted file should still open something
            // rather than nothing.
            if (!File.Exists(CursorConfig.DefaultPath)) _config.Save();

            Process.Start(new ProcessStartInfo(CursorConfig.DefaultPath) { UseShellExecute = true });
        }
        catch
        {
            // No editor associated with .json, or the shell refused. A desk toy does not get to
            // interrupt anyone over it.
        }
    }
}
