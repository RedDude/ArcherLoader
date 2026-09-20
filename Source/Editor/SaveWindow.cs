using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using ArcherEditorMod.Editor.Tools;
using ImGuiNET;
using TowerFall;

namespace ArcherEditorMod.Editor;

/// <summary>
/// Saves the archer: all its own data (names, colors, gender, sprites, sound, gems...) into the mod's archerData.xml,
/// the head / bow offsets edited in the Animation window into its spriteData.xml, and what the
/// editor added (hair, particles, wings, ghost, taunt) as an archerCustomData.xml entry, in the archer's own mod or
/// in a separate one, with the ArcherEditor dependency added or removed to match.
/// A base game archer's own data is never written (copy it first); its editor data can still go to a separate mod.
/// </summary>
public static class SaveWindow
{
    public static bool Open;

    private static ArcherData? lastArcher;
    private static bool saveArcher = true;
    private static bool saveEditor = true;
    private static bool saveOffsets;
    private static bool saveSprites = true;
    private static List<string> blocking = new();
    private static int validatedAt = -1000;
    private static int frameCounter;
    private static bool sameMod = true;
    private static string separateName = "";
    private static List<string> messages = new();
    private static bool ok;

    private static readonly Vector4 Red = new(1f, 0.4f, 0.4f, 1);
    private static readonly Vector4 Yellow = new(1f, 0.85f, 0.3f, 1);
    private static readonly Vector4 Green = new(0.5f, 1f, 0.5f, 1);

    public static void Draw(ArcherData data)
    {
        if (!Open) return;

        ImGui.SetNextWindowSize(new Vector2(540, 0), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowPos(new Vector2(300, 200), ImGuiCond.FirstUseEver);
        if (!ImGui.Begin("Save archer data", ref Open))
        {
            ImGui.End();
            return;
        }

        if (!ReferenceEquals(lastArcher, data))
        {
            lastArcher = data;
            separateName = CustomDataSaver.SuggestSeparateName(data);
            // the archer's own mod when it has one on disk, otherwise a separate mod
            sameMod = CustomDataSaver.Validate(data, true, "") == null;
            saveArcher = ArcherDataSaver.CannotSaveReason(data) == null;
            saveEditor = CustomDataSaver.Inspect(data).Any;
            saveOffsets = FramesWindow.OffsetsCannotSaveReason(data) == null && FramesWindow.OffsetsChanged(data);
            saveSprites = true;
            validatedAt = -1000;
            messages.Clear();
        }

        ImGui.Text($"{ArcherNames.Full(data.Name0, data.Name1)}");

        // ---- the archer's own data ----
        ImGui.SeparatorText("Archer data");
        var cannotSave = ArcherDataSaver.CannotSaveReason(data);
        ImGui.BeginDisabled(cannotSave != null);
        ImGui.Checkbox("Save all archer data (names, colors, gender, hat, sprites, sound, gems...)", ref saveArcher);
        ImGui.EndDisabled();

        string? archerProblem = null;
        if (cannotSave != null)
        {
            saveArcher = false;
            ImGui.TextColored(Yellow, cannotSave);
            if (ImGui.SmallButton("Open Archer Tools"))
                ToolsWindow.Open = true;
        }
        else
        {
            archerProblem = ArcherDataSaver.Problem(data);
            if (archerProblem != null)
                ImGui.TextColored(Red, archerProblem);
            else
                ImGui.TextDisabled("Written to the archerData.xml of the archer's mod.");
        }

        // ---- head / bow offsets edited in the Animation window ----
        ImGui.SeparatorText("Frame offsets");
        var offsetsReason = FramesWindow.OffsetsCannotSaveReason(data);
        ImGui.BeginDisabled(offsetsReason != null);
        ImGui.Checkbox("Save head origins, bow offsets and HideBow", ref saveOffsets);
        ImGui.EndDisabled();
        if (offsetsReason != null)
        {
            saveOffsets = false;
            ImGui.TextColored(Yellow, offsetsReason);
        }
        else
        {
            ImGui.TextDisabled(FramesWindow.OffsetsChanged(data)
                ? "Edited in the Animation window. Written to the body sprite's spriteData.xml."
                : "Not edited in the Animation window; saving rewrites the values the file already has.");
        }

        // ---- atlas rectangles and sprite data edited in the Atlas window ----
        ImGui.SeparatorText("Atlas and sprite data");
        ImGui.BeginDisabled(!AtlasWindow.HasSpriteChanges);
        ImGui.Checkbox("Save atlas rectangles and sprite data", ref saveSprites);
        ImGui.EndDisabled();
        ImGui.TextDisabled(AtlasWindow.HasSpriteChanges
            ? "Edited in the Atlas / Animation windows. Written to the archer's mod: atlas xml and sprite data xml."
            : "Nothing edited in the Atlas / Animation windows.");

        // ---- what the editor added ----
        ImGui.SeparatorText("Editor data");
        var summary = CustomDataSaver.Inspect(data);
        ImGui.Checkbox("Save what the editor added", ref saveEditor);
        ImGui.TextDisabled($"Hairs: {summary.Hairs}   Particles: {summary.Particles}   " +
                           $"Wings: {(summary.Wings ? "yes" : "no")}   Ghost: {(summary.Ghost ? "yes" : "no")}   " +
                           $"Taunt: {(summary.Taunt ? "yes" : "no")}");
        if (!summary.Any)
            ImGui.TextWrapped("Nothing added. Saving removes any editor data written for this archer before, " +
                              "and the ArcherEditor dependency if nothing else in the mod needs it.");

        string? editorProblem = null;
        if (saveEditor)
        {
            var sameProblem = CustomDataSaver.Validate(data, true, "");
            ImGui.BeginDisabled(sameProblem != null);
            if (ImGui.RadioButton("In the archer's mod", sameMod && sameProblem == null)) sameMod = true;
            ImGui.EndDisabled();
            if (sameProblem != null)
                ImGui.TextDisabled(sameProblem);
            else if (sameMod)
                ImGui.TextDisabled("Writes its archerCustomData.xml; ArcherEditor becomes an optional dependency of the mod.");

            if (ImGui.RadioButton("In a separate mod", !sameMod || sameProblem != null)) sameMod = false;
            if (!sameMod || sameProblem != null)
            {
                ImGui.SetNextItemWidth(260);
                ImGui.InputText("Mod name", ref separateName, 48);
                ImGui.TextDisabled("Created in the Mods folder (or updated if it exists); it requires ArcherEditor and the archer's mod.");
            }

            editorProblem = CustomDataSaver.Validate(data, sameMod && sameProblem == null, separateName);
            if (editorProblem != null)
                ImGui.TextColored(Red, editorProblem);
        }

        // ---- validation: nothing is written while the archer has errors ----
        if (frameCounter++ - validatedAt >= 30)
        {
            validatedAt = frameCounter;
            blocking = new List<string>();
            try
            {
                blocking.AddRange(ArcherRuntimeValidator.Validate(data)
                    .Where(v => v.type == ValidatorMessageType.ERROR).Select(v => v.message));
            }
            catch (Exception e)
            {
                blocking.Add("Validation crashed: " + e.Message);
            }

            if (saveSprites)
                blocking.AddRange(AtlasWindow.SpriteProblems());
        }

        ImGui.Separator();
        if (blocking.Count > 0)
        {
            ImGui.TextColored(Red, $"Fix {blocking.Count} error(s) before saving:");
            foreach (var problem in blocking.Take(12))
                ImGui.TextColored(Red, "  " + problem);
        }

        // ---- save ----
        var nothing = !saveArcher && !saveEditor && !saveOffsets && !(saveSprites && AtlasWindow.HasSpriteChanges);
        ImGui.BeginDisabled(nothing || blocking.Count > 0 || (saveArcher && archerProblem != null) || (saveEditor && editorProblem != null));
        if (ImGui.Button("Save"))
            Run(data, saveArcher, saveEditor, saveOffsets, saveSprites && AtlasWindow.HasSpriteChanges);
        ImGui.EndDisabled();

        foreach (var message in messages)
            ImGui.TextColored(ok ? Green : Red, message);

        ImGui.End();
    }

    private static void Run(ArcherData data, bool archer, bool editor, bool offsets, bool sprites)
    {
        var results = new List<string>();
        ok = true;

        // each part reports on its own, so one failing does not hide the other
        if (archer)
        {
            try
            {
                results.Add("Saved " + ArcherDataSaver.Save(data));
            }
            catch (Exception e)
            {
                ok = false;
                results.Add("Archer data failed: " + e.Message);
            }
        }

        if (sprites)
        {
            try
            {
                foreach (var file in AtlasWindow.SaveSprites(data))
                    results.Add("Saved " + file);
            }
            catch (Exception e)
            {
                ok = false;
                results.Add("Atlas / sprite data failed: " + e.Message);
            }
        }

        if (offsets)
        {
            try
            {
                results.Add("Saved " + FramesWindow.SaveOffsets(data));
            }
            catch (Exception e)
            {
                ok = false;
                results.Add("Frame offsets failed: " + e.Message);
            }
        }

        if (editor)
        {
            try
            {
                var sameProblem = CustomDataSaver.Validate(data, true, "");
                results.AddRange(CustomDataSaver.Save(data, sameMod && sameProblem == null, separateName));
            }
            catch (Exception e)
            {
                ok = false;
                results.Add("Editor data failed: " + e.Message);
            }
        }

        if (ok) results.Add("Restart the game to load it.");
        foreach (var message in results)
        {
            if (ok) EditorConsole.Info("Save: " + message);
            else EditorConsole.Error("Save: " + message);
        }

        messages = results;
    }
}
