using System;
using System.Collections.Generic;
using System.Linq;
using ArcherEditorMod.Editor;
using ArcherEditorMod;
using Microsoft.Xna.Framework;
using Monocle;
using TowerFall;
using ImGuiNET;
using Level = TowerFall.Level;

public static partial class ArcherEditorScreen
{
    // ---- validation: runs on setup and on every refresh / hot reload, or on demand ----

    private static List<ValidatorMessage> validation = new();

    public static IReadOnlyList<ValidatorMessage> Validation => validation;

    public static void RunValidation(ArcherData data)
    {
        if (data == null) return;

        try
        {
            validation = ArcherRuntimeValidator.Validate(data);

            // results go to the Console window (opened from the editor window) and to the summary line
            EditorConsole.Info($"Validated {data.Name0} {data.Name1}: {ArcherRuntimeValidator.Summary(validation)}");
            foreach (var message in validation)
                EditorConsole.Log(message.type == ValidatorMessageType.ERROR ? ConsoleLevel.Error : ConsoleLevel.Warn, message.message);
        }
        catch (Exception e)
        {
            validation = new List<ValidatorMessage> { new("Validation crashed: " + e.Message) };
        }
    }

    // ---- frame ----

    // True while an ImGui text field is being typed into: the game must not see those keys (hotkeys, movement).
    private static bool blockGameKeyboard;

    private static void Engine_Draw_Postfix(Engine __instance, GameTime gameTime)
    {
        if (renderer == null) return;
        if (gameTime.ElapsedGameTime == TimeSpan.Zero) return; // prevents crash when moving a window

        if (setupLevel == null || __instance.Scene != setupLevel)
        {
            blockGameKeyboard = false;

            // outside the editor the only things to show are the warning about a crashed last session and
            // the error modal for an editor session that could not start
            if (CrashGuard.Crashed != null)
            {
                try
                {
                    DrawCrashWarning(gameTime);
                }
                catch (Exception e)
                {
                    EditorConsole.Error("The crash warning failed: " + e.Message);
                    CrashGuard.Acknowledge();
                }
            }
            else if (errorException != null)
            {
                try
                {
                    renderer.BeforeLayout(gameTime);
                    ImGui.GetIO().MouseDrawCursor = true;
                    DrawErrorModal();
                    renderer.AfterLayout();
                }
                catch (Exception e)
                {
                    EditorConsole.Error("The error dialog failed: " + e.Message);
                    ClearError();
                }
            }
            return;
        }

        renderer.BeforeLayout(gameTime);

        // The OS cursor is not reliably shown by the game (its window / SDL setup can keep it hidden), so ImGui
        // always draws its own arrow.
        var io = ImGui.GetIO();
        io.MouseDrawCursor = true;
        blockGameKeyboard = io.WantTextInput;

        // one undo history for the data edits; Ctrl+Z undoes, Ctrl+Y / Ctrl+Shift+Z redoes
        EditHistory.Refresh = () => HotRefresh();
        EditHistory.Tick();
        if (io.KeyCtrl && !io.WantTextInput)
        {
            if (ImGui.IsKeyPressed(ImGuiKey.Z, false) && !io.KeyShift) EditHistory.Undo();
            else if (ImGui.IsKeyPressed(ImGuiKey.Y, false) || (ImGui.IsKeyPressed(ImGuiKey.Z, false) && io.KeyShift)) EditHistory.Redo();
        }

        try
        {
            if (editorMode == EditorMode.Unloaded)
                DrawUnloadedUi();
            else
                DrawLiveUi(setupLevel.Session);
        }
        catch (Exception e)
        {
            // ImGui's error recovery closes whatever was left open; the editor itself stays up
            ReportError("The editor UI failed", e);
        }

        try
        {
            DrawErrorModal();
        }
        catch (Exception e)
        {
            EditorConsole.Error("The error dialog failed: " + e.Message);
            ClearError();
        }

        renderer.AfterLayout();
    }

    // One window that throws is closed and reported instead of taking the frame (and the game) down with it.
    private static void Safe(string window, Action draw, Action close)
    {
        try
        {
            draw();
        }
        catch (Exception e)
        {
            ReportError($"The {window} window failed and was closed", e);
            try { close(); } catch { /* nothing more to do */ }
        }
    }

    private static void DrawLiveUi(Session session)
    {
        ImGui.SetNextWindowPos(new System.Numerics.Vector2(10, 10), ImGuiCond.FirstUseEver);
        ImGui.Begin("Archer Editor", ImGuiWindowFlags.AlwaysAutoResize);
        try
        {
            DrawLiveMainWindow(session);
        }
        catch (Exception e)
        {
            ReportError("The main editor window failed", e);
        }
        finally
        {
            ImGui.End();
        }

        var player = EditorPlayer(setupLevel);
        if (player != null)
        {
            var live = allCurrentMocks.Cast<Player>().Append(player);
            Safe("Animation", () => FramesWindow.Draw(renderer, player, editorHat, live, () => HotRefresh()), () => FramesWindow.Open = false);
            Safe("Inspector", () => InspectorWindow.Draw(renderer, player), () => InspectorWindow.Open = false);
            Safe("Archer Data", () => ArcherDataWindow.Draw(player, () => HotRefresh()), () => ArcherDataWindow.Open = false);
            Safe("Hair",() => HairWindow.Draw(renderer, player, () => HotRefresh()), () => HairWindow.Open = false);
            Safe("Particles", () => ParticlesWindow.Draw(renderer, player, () => HotRefresh()), () => ParticlesWindow.Open = false);
            Safe("Archer Tools", () => ToolsWindow.Draw(renderer, player, () => HotRefresh()), () => ToolsWindow.Open = false);
            Safe("Wings & Ghost", () => WingsGhostWindow.Draw(renderer, player, () => HotRefresh(), () => editorWings = true),
                () => WingsGhostWindow.Open = false);
            Safe("Taunt", () => TauntWindow.Draw(renderer, player, () => HotRefresh(), ShowTauntSection), () => TauntWindow.Open = false);
            Safe("Archer Atlas", () => AtlasWindow.DrawArcher(renderer, player.ArcherData, () => HotRefresh()), () => AtlasWindow.ArcherOpen = false);
            Safe("Atlas", () => AtlasWindow.Draw(renderer, player.ArcherData, () => HotRefresh()), () => AtlasWindow.Open = false);
            Safe("Save", () => SaveWindow.Draw(player.ArcherData), () => SaveWindow.Open = false);
        }

        Safe("Archers", () => ArcherPickerWindow.Draw(renderer, EditorPlayerIndex), () => ArcherPickerWindow.Open = false);
        Safe("Base archer picker", () => ArcherPickerWindow.DrawBasePicker(renderer), () => ArcherPickerWindow.BaseOpen = false);
        Safe("Console", EditorConsole.Draw, () => EditorConsole.Open = false);
    }

    private static void DrawLiveMainWindow(Session session)
    {
        // ---- which archer ----
        var shownIndex = EditorPlayerIndex + 1; // players are numbered 1-4 on screen
        ImGui.SetNextItemWidth(90);
        ImGui.SliderInt("Player", ref shownIndex, 1, 4);
        if (ImGui.IsItemDeactivatedAfterEdit() && shownIndex - 1 != EditorPlayerIndex)
        {
            FortEntrance.Instance.Settings.EditorPlayerIndex = shownIndex - 1;
            SaveSettings();
            pendingReopen = true;
        }

        ImGui.Checkbox("Archers", ref ArcherPickerWindow.Open);

        // ---- mock section ----
        ImGui.Separator();
        DrawSectionSelector();
        var sectionArcher = EditorPlayer(setupLevel)?.ArcherData;
        if (sectionArcher != null)
            DrawCustomMocks(sectionArcher);

        // ---- preview ----
        ImGui.Separator();
        if (ImGui.Button("Allegiance: " + editorAllegiance))
        {
            editorAllegiance = editorAllegiance == Allegiance.Neutral
                ? Allegiance.Blue : editorAllegiance == Allegiance.Blue
                ? Allegiance.Red : Allegiance.Neutral;
            HotRefresh();
        }

        if (ImGui.Button("Playground (Space): " + (editorPlayground ? "ON" : "OFF")))
        {
            editorPlayground = !editorPlayground;
            HotRefresh();
        }

        if (ImGui.Button("Hat: " + editorHat))
        {
            editorHat = editorHat == Player.HatStates.NoHat ? Player.HatStates.Normal
                : editorHat == Player.HatStates.Normal ? Player.HatStates.Crown
                : Player.HatStates.NoHat;
            HotRefresh();
        }

        if (ImGui.Button("Wings: " + (editorWings ? "ON" : "OFF")))
        {
            editorWings = !editorWings;
            HotRefresh();
        }

        if (ImGui.Button("Flip: " + editorFacing))
        {
            editorFacing = editorFacing == Facing.Left ? Facing.Right : Facing.Left;
            HotRefresh();
        }

        if (ImGui.Button(rollcallPreview != null ? "Hide Portraits (F)" : "Show Portraits (F)"))
            TogglePortraits(session);

        if (ImGui.Button("Rollcall (T)"))
            ToggleRollcall(session);

        if (ImGui.Button("Reload (R)"))
            HotRefresh();

        // the shared undo history of the data edits (Ctrl+Z / Ctrl+Y)
        ImGui.BeginDisabled(!EditHistory.CanUndo);
        if (ImGui.Button("Undo" + (EditHistory.NextUndo is { } undoLabel ? ": " + undoLabel : "")))
            EditHistory.Undo();
        ImGui.EndDisabled();
        ImGui.BeginDisabled(!EditHistory.CanRedo);
        if (ImGui.Button("Redo" + (EditHistory.NextRedo is { } redoLabel ? ": " + redoLabel : "")))
            EditHistory.Redo();
        ImGui.EndDisabled();

        // ---- tools ----
        ImGui.Separator();
        ImGui.Checkbox("Archer Data", ref ArcherDataWindow.Open);
        ImGui.Checkbox("Archer Atlas (sprite data)", ref AtlasWindow.ArcherOpen);
        ImGui.Checkbox("Atlas", ref AtlasWindow.Open);
        ImGui.Checkbox("Animation", ref FramesWindow.Open);
        ImGui.Checkbox("Hair", ref HairWindow.Open);
        ImGui.Checkbox("Particles", ref ParticlesWindow.Open);
        ImGui.Checkbox("Wings & Ghost", ref WingsGhostWindow.Open);
        ImGui.Checkbox("Taunt", ref TauntWindow.Open);

        if (ImGui.CollapsingHeader("Advanced"))
        {
            ImGui.Checkbox("Inspector (archer, data, xml)", ref InspectorWindow.Open);
            ImGui.Checkbox("Console", ref EditorConsole.Open);
            ImGui.Checkbox("Archer Tools (experimental)", ref ToolsWindow.Open);
        }

        var hasErrors = validation.Any(v => v.type == ValidatorMessageType.ERROR);
        ImGui.PushStyleColor(ImGuiCol.Text, hasErrors
            ? new System.Numerics.Vector4(1f, 0.4f, 0.4f, 1)
            : validation.Count > 0 ? new System.Numerics.Vector4(1f, 0.85f, 0.3f, 1) : new System.Numerics.Vector4(0.5f, 1f, 0.5f, 1));
        ImGui.Text("Validation: " + ArcherRuntimeValidator.Summary(validation));
        ImGui.PopStyleColor();
        ImGui.SameLine();
        if (ImGui.SmallButton("Validate"))
            RunValidation(EditorPlayer(setupLevel)?.ArcherData);

        // ---- save / other modes ----
        ImGui.Separator();
        if (ImGui.Button("Save archer data..."))
            SaveWindow.Open = true;

        if (ImGui.Button("Edit unloaded archer (experimental)..."))
        {
            pendingMode = EditorMode.Unloaded;
            pendingReopen = true;
        }
    }
}
