using System;
using System.IO;
using System.Text.Json;

namespace ArcherEditorMod.Editor;

/// <summary>
/// Crash detector. While an editor session is running a marker file sits in the mod's storage folder; a clean exit
/// (back to the main menu, or the game closing) removes it. If the marker is still there when the game starts, the
/// last session ended abnormally, and the editor offers to reset the selection or reopen the same archer.
/// </summary>
public static class CrashGuard
{
    public sealed class Marker
    {
        public int PlayerIndex { get; set; }
        public int ArcherIndex { get; set; }
        public int ArcherType { get; set; }
        public string ArcherName { get; set; } = "";
        public string Started { get; set; } = "";
        public bool QuickStart { get; set; }
    }

    /// <summary>The session that did not end cleanly last time, until the user decides what to do.</summary>
    public static Marker? Crashed { get; private set; }

    private static bool active;
    private static bool levelSeen;
    private static bool exitHooked;

    private static string MarkerPath =>
        Path.Combine(FortEntrance.Instance.Context.Storage.StoragePath, "ArcherEditor.session");

    /// <summary>Call once at startup, before anything can open the editor.</summary>
    public static void Check()
    {
        if (!exitHooked)
        {
            exitHooked = true;
            AppDomain.CurrentDomain.ProcessExit += (_, _) => MarkClean();
        }

        try
        {
            if (File.Exists(MarkerPath))
                Crashed = JsonSerializer.Deserialize<Marker>(File.ReadAllText(MarkerPath));
        }
        catch
        {
            // an unreadable marker still means the last session did not end cleanly
            Crashed = new Marker { ArcherName = "unknown" };
        }
    }

    public static void MarkStarted(int playerIndex, int archerIndex, int archerType, string archerName)
    {
        try
        {
            var marker = new Marker
            {
                PlayerIndex = playerIndex,
                ArcherIndex = archerIndex,
                ArcherType = archerType,
                ArcherName = archerName,
                Started = DateTime.Now.ToString("g"),
                QuickStart = FortEntrance.Instance.Settings.QuickStart
            };

            File.WriteAllText(MarkerPath, JsonSerializer.Serialize(marker));
            active = true;
            levelSeen = false;
        }
        catch
        {
            // the guard must never be the thing that breaks the editor
        }
    }

    /// <summary>The editor level is up: from now on leaving to the main menu counts as a clean exit.</summary>
    public static void EditorLevelSeen() => levelSeen = true;

    /// <summary>True once the editor session is over and the game is back on the main menu.</summary>
    public static bool ShouldCloseOnMenu => active && levelSeen;

    public static void MarkClean()
    {
        if (!active) return;
        active = false;

        try
        {
            if (File.Exists(MarkerPath))
                File.Delete(MarkerPath);
        }
        catch
        {
            // ignored
        }
    }

    /// <summary>The user dealt with the warning (or ignored it): forget the old crash.</summary>
    public static void Acknowledge()
    {
        Crashed = null;
        if (active) return; // a new session already owns the marker

        try
        {
            if (File.Exists(MarkerPath))
                File.Delete(MarkerPath);
        }
        catch
        {
            // ignored
        }
    }
}
