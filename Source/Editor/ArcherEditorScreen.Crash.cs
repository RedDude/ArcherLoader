using System;
using System.Collections.Generic;
using System.Linq;
using ArcherEditorMod;
using ArcherEditorMod.Editor;
using ImGuiNET;
using Microsoft.Xna.Framework;
using Monocle;
using TowerFall;

public static partial class ArcherEditorScreen
{
    private static bool pendingReopenAfterCrash;
    private static bool warningShown;
    private static bool disableQuickStartOnReset = true;

    // ImGui warning shown outside the editor when the last editor session ended abnormally
    private static void DrawCrashWarning(GameTime gameTime)
    {
        var crash = CrashGuard.Crashed;
        if (crash == null) return;

        if (!warningShown)
        {
            warningShown = true;
            Engine.Instance.IsMouseVisible = true;
            disableQuickStartOnReset = crash.QuickStart;
        }

        renderer.BeforeLayout(gameTime);

        var io = ImGui.GetIO();
        io.MouseDrawCursor = true;
        ImGui.SetNextWindowPos(io.DisplaySize / 2, ImGuiCond.Always, new System.Numerics.Vector2(0.5f, 0.5f));
        ImGui.SetNextWindowSize(new System.Numerics.Vector2(460, 0));
        ImGui.Begin("Archer Editor", ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoResize);

        ImGui.PushStyleColor(ImGuiCol.Text, new System.Numerics.Vector4(1f, 0.85f, 0.3f, 1));
        ImGui.TextWrapped("The game did not close cleanly while the Archer Editor was open.");
        ImGui.PopStyleColor();

        ImGui.Spacing();
        ImGui.TextWrapped($"Last session: {crash.ArcherName} (player {crash.PlayerIndex + 1}), started {crash.Started}");
        ImGui.TextWrapped("If that archer is what crashes the game, reset the editor to a valid archer. Otherwise " +
                          "you can open the editor again with the same archer.");
        ImGui.Spacing();

        if (crash.QuickStart)
            ImGui.Checkbox("Also turn off quick start", ref disableQuickStartOnReset);

        if (ImGui.Button("Reset archer editor"))
        {
            var settings = FortEntrance.Instance.Settings;
            settings.EditorPlayerIndex = 0;
            settings.EditorArcherIndex = 0; // the first archer is always a valid one
            settings.EditorArcherType = 0;
            if (crash.QuickStart && disableQuickStartOnReset)
                settings.QuickStart = false;
            SaveSettings();
            CloseCrashWarning();
        }

        ImGui.SameLine();
        if (ImGui.Button("Open again with the same archer"))
        {
            // the marker holds what was open when it went wrong; the selection settings follow it
            var settings = FortEntrance.Instance.Settings;
            settings.EditorPlayerIndex = crash.PlayerIndex;
            settings.EditorArcherIndex = crash.ArcherIndex;
            settings.EditorArcherType = crash.ArcherType;
            SaveSettings();
            pendingReopenAfterCrash = true;
            CloseCrashWarning();
        }

        ImGui.SameLine();
        if (ImGui.Button("Ignore"))
            CloseCrashWarning();

        ImGui.End();
        renderer.AfterLayout();
    }

    // ---- caught exceptions: shown in a modal on top of the editor ----

    private static string? errorWhere;
    private static Exception? errorException;
    private static bool errorPopupOpened;

    /// <summary>Logs a caught exception and raises the error modal. Only the first one is kept until dismissed.</summary>
    private static void ReportError(string where, Exception e)
    {
        EditorConsole.Error($"{where}: {e.Message}");
        if (errorException != null) return; // don't pile up while the modal is already up

        errorWhere = where;
        errorException = e;
        errorPopupOpened = false;
    }

    // must be called inside an ImGui frame
    private static void DrawErrorModal()
    {
        if (errorException == null) return;

        const string title = "Archer Editor error";
        if (!errorPopupOpened)
        {
            errorPopupOpened = true;
            ImGui.OpenPopup(title);
        }

        var io = ImGui.GetIO();
        ImGui.SetNextWindowPos(io.DisplaySize / 2, ImGuiCond.Always, new System.Numerics.Vector2(0.5f, 0.5f));
        ImGui.SetNextWindowSize(new System.Numerics.Vector2(560, 0));

        bool open = true;
        if (!ImGui.BeginPopupModal(title, ref open, ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove))
        {
            // closed with the X, or the popup never opened
            if (errorPopupOpened && !ImGui.IsPopupOpen(title)) ClearError();
            return;
        }

        try
        {
            ImGui.PushStyleColor(ImGuiCol.Text, new System.Numerics.Vector4(1f, 0.4f, 0.4f, 1));
            ImGui.TextWrapped(errorWhere ?? "The editor hit an error.");
            ImGui.PopStyleColor();
            ImGui.TextWrapped(errorException.Message);
            ImGui.Spacing();

            ImGui.BeginChild("##errorDetails", new System.Numerics.Vector2(0, 200), ImGuiChildFlags.Borders);
            ImGui.TextUnformatted(errorException.ToString());
            ImGui.EndChild();
            ImGui.Spacing();

            if (ImGui.Button("Copy details"))
                ImGui.SetClipboardText($"{errorWhere}\n{errorException}");

            ImGui.SameLine();
            if (ImGui.Button("Dismiss") || !open)
            {
                ImGui.CloseCurrentPopup();
                ClearError();
            }
        }
        finally
        {
            ImGui.EndPopup();
        }
    }

    private static void ClearError()
    {
        errorException = null;
        errorWhere = null;
        errorPopupOpened = false;
    }

    private static void CloseCrashWarning()
    {
        CrashGuard.Acknowledge();
        warningShown = false;
        if (!pendingReopenAfterCrash)
            Engine.Instance.IsMouseVisible = false;
    }

    // opening a session from inside the ImGui frame is not safe: it happens on the next update
    private static void HandleCrashReopen()
    {
        if (!pendingReopenAfterCrash) return;

        pendingReopenAfterCrash = false;
        OpenEditor();
    }
}
