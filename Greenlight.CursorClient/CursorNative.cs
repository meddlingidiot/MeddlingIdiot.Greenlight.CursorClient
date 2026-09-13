using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Greenlight.CursorClient;

/// <summary>Where the mouse is, in physical pixels across the whole virtual screen.</summary>
/// <remarks>
/// Asked every frame rather than listened for. The halo is a click-through window, so it never
/// receives a mouse event of its own, and a low-level mouse hook to get them anyway would be a
/// global hook installed by a desk toy — which is the kind of thing antivirus products have
/// opinions about. One <c>GetCursorPos</c> a frame costs nothing.
/// </remarks>
[SupportedOSPlatform("windows")]
internal static class CursorNative
{
    [StructLayout(LayoutKind.Sequential)]
    private struct Point
    {
        public int X, Y;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetCursorPos(out Point point);

    /// <summary>The cursor's position, or <c>null</c> when Windows would not say — a locked desktop, typically.</summary>
    public static (int X, int Y)? Position() =>
        GetCursorPos(out var point) ? (point.X, point.Y) : null;
}
