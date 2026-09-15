# MeddlingIdiot.Greenlight.CursorClient

A halo round your mouse cursor that turns the colour of your pipeline, and breathes while a
build is running.

A runnable reference consumer of the [Greenlight](https://github.com/meddlingidiot/MeddlingIdiot.Greenlight)
SDK, and a demonstration of how little an app needs to do to use it. The halo is **green** while
everything passes and **amber** while a pull request wants you. When a build breaks it turns
**red** and stays that way for as long as the pipeline is broken. While a build is running it
breathes — growing a third bigger and dimming back on a slow cycle — so the one thing on the
screen you are already looking at tells you the build is still going. With no Greenlight on the
machine at all it goes a cold grey: a green halo on a four-minute-old snapshot would be lying to
you.

The system cursor itself is never touched. The halo is a small transparent window that travels
with the mouse and lets every click fall straight through it — so there is nothing to put back
on exit, and a crash cannot leave you with a red arrow until you find the setting.

The point of it is what it does **not** have. No Azure DevOps client, no GitHub client, no
token, no polling loop — everything it knows arrives through the SDK, from the Greenlight
already running on the machine. Strip out the drawing and the tray icon and the integration is
about twenty lines, all of them in [`App.cs`](Greenlight.CursorClient/App.cs).

## Running it

```bash
dotnet run --project Greenlight.CursorClient
```

Windows only: the click-through window and asking where the cursor is are Win32. The SDK itself
is not — it is plain .NET, and the same twenty lines work anywhere.

## The tray

The halo ignores the mouse by design — it sits under the pointer all day, and a window there
that answered clicks would be a mouse that could not click anything else. So everything lives
on the mascot in the notification area:

- **Halo on the cursor** — take it away and bring it back. Clicking the icon does the same.
- **What it looks like** — a soft glow, a crisp ring, or both.
- **Where it sits** — which way the halo sits from the tip of the cursor. Behind the arrow by
  default: down and a little to the right, tucked behind the arrow's own body and off whatever
  it is pointing at. Below, either corner, the sides and above are all there too, and **Under
  the cursor** puts it back in the middle with the arrow inside it.
- **How far off the tip** — how far that nudge goes.
- **How big**, **How solid** — the ordinary sizes, and how much the halo shows through.
- **How fast it breathes** — the pace of the pulse while a build is running. The only setting
  here that means nothing at all about the build: the pace that reads as "working" to one person
  reads as "alarm" to the next.
- **Fade when the mouse is still** — let the halo fade after the mouse has sat still for a
  while, and come straight back on the first movement. Off by default: it is a status light,
  and a status light that switches itself off is only sometimes what somebody wants.
- **Grey halo when Greenlight is away** — a grey halo rather than none at all. On by default,
  because with it off "Greenlight has stopped" and "the halo has stopped" look identical.
- **Start with Windows** — read from the registry every time it is shown, so it agrees with
  Task Manager's Startup tab rather than with what we last wrote there.
- **Edit the colours…** — opens `cursor.json`. **Reload the file** picks up hand edits without
  a restart.

Every setting is written straight back to the file, so the menu and the JSON are never two
different sets of settings.

## The file

`%AppData%\Greenlight.CursorHalo\cursor.json`, written with the defaults on first run. One
colour per thing Greenlight can say, in any hex Avalonia can parse — a colour with its own
alpha (`#804BFF86`) is somebody asking for a quieter green, and they get it:

```json
{
  "WhenOff":   "#6A7079",
  "WhenGreen": "#4BFF86",
  "WhenAmber": "#FFCE42",
  "WhenRed":   "#FF4E3C",
  "Style": "Glow",
  "Placement": "BehindArrow",
  "PlacementDistance": 16,
  "Size": 36,
  "Opacity": 0.85,
  "PulseSpeed": 1.0,
  "IdleFadeSeconds": 0,
  "ShowWhenOff": true
}
```

`Size` is the halo's reach from the tip of the cursor, in logical pixels, so the same number is
the same apparent size on a scaled display. `Placement` is one of `BehindArrow`, `Below`,
`BelowLeft`, `BelowRight`, `Above`, `Left`, `Right` or `UnderCursor`, and `PlacementDistance`
is how far that way the middle of the halo goes — also in logical pixels, and ignored by
`UnderCursor`, which has nowhere to move to. A file that cannot be parsed falls back to the
defaults rather than refusing to start: it is a desk toy, and a stray comma should not cost you
the whole thing.

## How it is put together

| | |
|---|---|
| [`App.cs`](Greenlight.CursorClient/App.cs) | The whole Greenlight integration |
| [`HaloScene.cs`](Greenlight.CursorClient/HaloScene.cs) | Which colour is showing, how far through a breath it is, whether the mouse has been still. No Avalonia, so it is testable |
| [`HaloCanvas.cs`](Greenlight.CursorClient/HaloCanvas.cs) | The drawing: the glow and the ring |
| [`HaloWindow.cs`](Greenlight.CursorClient/HaloWindow.cs) | The click-through window that follows the mouse |
| [`HaloTray.cs`](Greenlight.CursorClient/HaloTray.cs) | The tray icon and its menu |
| [`CursorConfig.cs`](Greenlight.CursorClient/CursorConfig.cs) | The file, and the clamping that keeps a hand edit from producing something you cannot find |
| [`ClickThroughNative.cs`](Greenlight.CursorClient/ClickThroughNative.cs) | The four window styles that make it furniture |
| [`CursorNative.cs`](Greenlight.CursorClient/CursorNative.cs) | One `GetCursorPos` a frame |

The halo is a window that follows the cursor rather than one laid over the whole desktop, and
that is a deliberate choice: a single window across two monitors at different scales has one
render scaling and two truths about where a pixel is, and the halo would sit a little off the
cursor on whichever screen lost. A window that travels with the mouse is re-scaled by Windows as
it crosses over, and the maths stays "put the middle on the cursor". It repaints only when
something would look different — with the mouse still and no build running, that is never.

The scene is free of Avalonia so the changeover, the breath and the idle fade can be tested
without a window — and so the thing that would make the toy look broken, a breath that never
quite settles back to steady after a build ends, is a test rather than something you would have
to catch by staring at your own cursor.

```bash
dotnet test
```

## Building on it

```bash
dotnet add package MeddlingIdiot.Greenlight.Sdk
```

That is exactly what this repository does — the SDK comes from nuget.org like any other
dependency. Greenlight itself is a separate product and is not open source — this sample is.
The two things it wants you to notice: the client sits in a disabled state and reconnects on
its own when Greenlight is not running, so there is nothing to guard; and the events arrive on
a background thread, so anything touching your UI has to get itself back onto the UI thread.
Both are worked through in `App.cs`.
