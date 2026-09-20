using System;
using ArcherEditorMod.Editor;
using ImGuiNET;

/// <summary>How the editor session is used.</summary>
public enum EditorMode
{
    /// <summary>The archer is loaded and previewed with mocks, a playground and all the live windows.</summary>
    Live,

    /// <summary>An archer mod that is not loaded is edited in its files: no mocks, no playable player, no preview.</summary>
    Unloaded
}

public static partial class ArcherEditorScreen
{
    private static EditorMode editorMode = EditorMode.Live;
    private static EditorMode? pendingMode;

    // Only what does not depend on the preview: the file editor, the data / atlas viewers, the console.
    private static void DrawUnloadedUi()
    {
        ImGui.SetNextWindowPos(new System.Numerics.Vector2(10, 10), ImGuiCond.FirstUseEver);
        ImGui.Begin("Archer Editor (unloaded archer mode)", ImGuiWindowFlags.AlwaysAutoResize);
        try
        {
            ImGui.TextWrapped("No live preview: nothing is spawned here, the archer is edited in its files.");
            ImGui.Separator();
            ImGui.Checkbox("Unloaded Archer", ref UnloadedEditorWindow.Open);
            ImGui.Checkbox("Data Inspector", ref DataInspectorWindow.Open);
            ImGui.Checkbox("Atlas", ref AtlasWindow.Open);
            ImGui.Checkbox("Console", ref EditorConsole.Open);
            ImGui.Separator();
            if (ImGui.Button("Back to the live editor"))
            {
                pendingMode = EditorMode.Live;
                pendingReopen = true;
            }
        }
        catch (Exception e)
        {
            EditorConsole.Error("The main editor window failed: " + e.Message);
        }
        finally
        {
            ImGui.End();
        }

        Safe("Unloaded Archer", () => UnloadedEditorWindow.Draw(renderer), () => UnloadedEditorWindow.Open = false);
        Safe("Data Inspector", () => DataInspectorWindow.Draw(renderer, null), () => DataInspectorWindow.Open = false);
        Safe("Atlas", () => AtlasWindow.Draw(renderer, null), () => AtlasWindow.Open = false);
        Safe("Base archer picker", () => ArcherPickerWindow.DrawBasePicker(renderer), () => ArcherPickerWindow.BaseOpen = false);
        Safe("Console", EditorConsole.Draw, () => EditorConsole.Open = false);
    }

    // settings menu button
    public static void OpenUnloadedEditor() => OpenEditor(EditorMode.Unloaded);
}
