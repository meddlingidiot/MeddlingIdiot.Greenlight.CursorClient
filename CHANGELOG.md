# Changelog

All notable changes to this project are documented here.

## [Unreleased]

### Added

- First cut: a halo round the mouse cursor, coloured from the Greenlight running on the machine.
  Green while everything passes, amber while a pull request wants you, red when a pipeline
  breaks, and a cold grey when there is no Greenlight to ask.
- A build pulse. While a build is running the halo breathes — a third bigger and a third dimmer
  on a slow cosine — eased in over half a second when the build starts and eased back to steady
  when it ends, never snapping and never left dimmer than steady.
- A changeover. A new colour fades the old one all the way out first rather than blending where
  it stands, so the halo never spends a few frames being a colour that means nothing.
- Three looks - a soft glow with a hole at the tip so it never covers what the cursor is pointing
  at, a crisp ring, or both.
- A window that follows the cursor rather than an overlay across the desktop, so a second monitor
  at a different scale does not put the halo a little off the arrow. Click-through, never
  activated, out of Alt+Tab, and repainted only when something would look different.
- An idle fade, off by default: let the halo fade after the mouse has sat still for a while, and
  bring it straight back on the first movement.
- A tray menu for everything - style, size, opacity, how fast it breathes, the idle fade, whether
  Greenlight being away shows grey or nothing - each written straight back to `cursor.json`.
- "Start with Windows" in the tray menu, registering the stable shim beside the install rather
  than the versioned copy an update would move.
