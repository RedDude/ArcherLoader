using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Xml;
using ArcherEditorMod.Editor.ImGuiSupport;
using ArcherEditorMod.Editor.Tools;
using ArcherEditorMod.Source.Features;
using FortRise;
using ImGuiNET;
using TowerFall;
using Color = Microsoft.Xna.Framework.Color;

namespace ArcherEditorMod.Editor;

/// <summary>
/// Archer tools: safe data (names, colors, mod meta), archer type, validate an unloaded mod, and copy the archer
/// into a new mod. The type, validate and copy tabs are experimental.
/// </summary>
public static class ToolsWindow
{
    public static bool Open;

    private static readonly Vector4 Red = new(1f, 0.4f, 0.4f, 1);
    private static readonly Vector4 Yellow = new(1f, 0.85f, 0.3f, 1);
    private static readonly Vector4 Green = new(0.5f, 1f, 0.5f, 1);

    public static void Draw(ImGuiRenderer renderer, Player player, Action refreshEditor)
    {
        if (!Open) return;

        ImGui.SetNextWindowSize(new Vector2(560, 520), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowPos(new Vector2(300, 120), ImGuiCond.FirstUseEver);
        if (!ImGui.Begin("Archer Tools", ref Open))
        {
            ImGui.End();
            return;
        }

        var data = player.ArcherData;
        if (ImGui.BeginTabBar("tools_tabs"))
        {
            if (ImGui.BeginTabItem("Type (experimental)"))
            {
                DrawTypeTab(data);
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Validate unloaded (experimental)"))
            {
                DrawValidateTab();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Create copy (experimental)"))
            {
                DrawCopyTab(data);
                ImGui.EndTabItem();
            }

            ImGui.EndTabBar();
        }

        ImGui.End();
    }

    // ================================================================ data

    private static ArcherData? loaded;
    private static string name0 = "", name1 = "";
    private static Vector3 colorA, colorB, colorLight;
    private static int gender;
    private static bool startNoHat;
    private static bool writeFile = true;
    private static string dataStatus = "";

    private static string metaAuthor = "", metaDescription = "", metaVersion = "";
    private static string metaPath = "";
    private static string metaStatus = "";

    private static readonly string[] genders = Enum.GetNames<TFGame.Genders>();

    private static Vector3 ToVector(Color c) => new(c.R / 255f, c.G / 255f, c.B / 255f);
    private static Color ToColor(Vector3 v) => new(v.X, v.Y, v.Z);

    private readonly record struct FormState(string Name0, string Name1, Vector3 A, Vector3 B, Vector3 L, int Gender, bool NoHat);

    private static void LoadData(ArcherData data, ArcherDecorationRegistry.ArcherSource? source)
    {
        loaded = data;
        EditHistory.Forget(("archerdata-form", data));
        name0 = data.Name0 ?? "";
        name1 = data.Name1 ?? "";
        colorA = ToVector(data.ColorA);
        colorB = ToVector(data.ColorB);
        colorLight = ToVector(data.LightbarColor);
        gender = (int)data.Gender;
        startNoHat = data.StartNoHat;
        dataStatus = "";
        metaStatus = "";

        metaPath = source?.ModDirectory != null ? Path.Combine(source.ModDirectory, "meta.json") : "";
        metaAuthor = metaDescription = metaVersion = "";
        if (File.Exists(metaPath))
        {
            try
            {
                var meta = JsonNode.Parse(File.ReadAllText(metaPath))!.AsObject();
                metaAuthor = ReadJson(meta, "author");
                metaDescription = ReadJson(meta, "description");
                metaVersion = ReadJson(meta, "version");
            }
            catch
            {
                metaPath = "";
            }
        }
        else
        {
            metaPath = "";
        }
    }

    private static string ReadJson(JsonObject obj, string key) =>
        obj.FirstOrDefault(p => p.Key.Equals(key, StringComparison.OrdinalIgnoreCase)).Value?.ToString() ?? "";

    private static void WriteJson(JsonObject obj, string key, string value)
    {
        var existing = obj.Select(p => p.Key).FirstOrDefault(k => k.Equals(key, StringComparison.OrdinalIgnoreCase));
        obj[existing ?? key] = value;
    }

    // resolving reads the mod's xml, so keep the answer per archer instead of asking every frame
    private static ArcherData? sourceKey;
    private static ArcherDecorationRegistry.ArcherSource? sourceValue;

    private static ArcherDecorationRegistry.ArcherSource? SourceOf(ArcherData data)
    {
        if (!ReferenceEquals(sourceKey, data))
        {
            sourceKey = data;
            sourceValue = ArcherDecorationRegistry.FindArcherSource(data);
        }
        return sourceValue;
    }

    internal static void DrawDataTab(ArcherData data, Action refresh)
    {
        var source = SourceOf(data);
        if (!ReferenceEquals(loaded, data))
            LoadData(data, source);

        ImGui.TextDisabled(source == null
            ? "Base game archer: changes stay in memory only."
            : $"{source.EntryName}  ({source.Type})");

        ImGui.SeparatorText("Names and colors");
        ImGui.SetNextItemWidth(220);
        ImGui.InputText("Name", ref name0, 32);
        ImGui.SetNextItemWidth(220);
        ImGui.InputText("Subname", ref name1, 32);
        ImGui.ColorEdit3("ColorA", ref colorA);
        ImGui.ColorEdit3("ColorB", ref colorB);
        ImGui.ColorEdit3("LightbarColor", ref colorLight);
        ImGui.SetNextItemWidth(140);
        ImGui.Combo("Gender", ref gender, genders, genders.Length);
        ImGui.Checkbox("Starts without a hat", ref startNoHat);

        // typing in the form is undoable too, before it is applied
        EditHistory.Observe(("archerdata-form", data), "Edit archer data",
            new FormState(name0, name1, colorA, colorB, colorLight, gender, startNoHat),
            s => { name0 = s.Name0; name1 = s.Name1; colorA = s.A; colorB = s.B; colorLight = s.L; gender = s.Gender; startNoHat = s.NoHat; });

        // empty names and clashes with another archer of the same kind are not allowed
        var problems = new List<string>();
        if (string.IsNullOrWhiteSpace(name0)) problems.Add("Name is empty.");
        if (string.IsNullOrWhiteSpace(name1)) problems.Add("Subname is empty.");
        if (problems.Count == 0)
        {
            var clash = ArcherNames.FindCollision(ArcherNames.GroupOf(data), data, name0, name1);
            if (clash != null)
                problems.Add($"'{ArcherNames.Full(name0, name1)}' is already used by another archer.");
        }

        foreach (var problem in problems)
            ImGui.TextColored(Red, problem);

        var fileAvailable = source?.ArcherDataFile != null;
        if (fileAvailable)
            ImGui.Checkbox("Also write archerData.xml", ref writeFile);
        else
            ImGui.TextDisabled("No archerData.xml on disk for this archer (base game or zipped mod).");

        ImGui.BeginDisabled(problems.Count > 0);
        if (ImGui.Button("Apply"))
            ApplyData(data, source, fileAvailable && writeFile);
        ImGui.EndDisabled();
        if (dataStatus.Length > 0)
            ImGui.TextWrapped(dataStatus);

        // ---- mod meta ----
        ImGui.SeparatorText("Mod meta (meta.json)");
        if (metaPath.Length == 0)
        {
            ImGui.TextDisabled("This archer's mod has no meta.json on disk.");
            return;
        }

        ImGui.SetNextItemWidth(260);
        ImGui.InputText("Author", ref metaAuthor, 64);
        ImGui.SetNextItemWidth(260);
        ImGui.InputText("Description", ref metaDescription, 200);
        ImGui.SetNextItemWidth(140);
        ImGui.InputText("Version", ref metaVersion, 24);

        var metaProblems = new List<string>();
        if (string.IsNullOrWhiteSpace(metaAuthor)) metaProblems.Add("Author is empty.");
        if (string.IsNullOrWhiteSpace(metaDescription)) metaProblems.Add("Description is empty.");
        if (!Regex.IsMatch(metaVersion.Trim(), @"^\d+\.\d+\.\d+([-+][0-9A-Za-z.-]+)?$"))
            metaProblems.Add("Version must look like 1.2.3.");
        foreach (var problem in metaProblems)
            ImGui.TextColored(Red, problem);

        ImGui.BeginDisabled(metaProblems.Count > 0);
        if (ImGui.Button("Save meta.json"))
        {
            try
            {
                var meta = JsonNode.Parse(File.ReadAllText(metaPath))!.AsObject();
                WriteJson(meta, "author", metaAuthor.Trim());
                WriteJson(meta, "description", metaDescription.Trim());
                WriteJson(meta, "version", metaVersion.Trim());
                File.WriteAllText(metaPath, meta.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
                metaStatus = "Saved " + metaPath + " (applies on the next start)";
                EditorConsole.Info(metaStatus);
            }
            catch (Exception e)
            {
                metaStatus = "Save failed: " + e.Message;
                EditorConsole.Error(metaStatus);
            }
        }
        ImGui.EndDisabled();
        if (metaStatus.Length > 0)
            ImGui.TextWrapped(metaStatus);
    }

    private readonly record struct DataState(string Name0, string Name1, Color ColorA, Color ColorB, Color Light, TFGame.Genders Gender, bool StartNoHat);

    private static DataState StateOf(ArcherData data) =>
        new(data.Name0, data.Name1, data.ColorA, data.ColorB, data.LightbarColor, data.Gender, data.StartNoHat);

    private static void PutState(ArcherData data, DataState state)
    {
        data.Name0 = state.Name0;
        data.Name1 = state.Name1;
        data.ColorA = state.ColorA;
        data.ColorB = state.ColorB;
        data.LightbarColor = state.Light;
        data.Gender = state.Gender;
        data.StartNoHat = state.StartNoHat;
        loaded = null; // the form reads the archer again
    }

    private static void ApplyData(ArcherData data, ArcherDecorationRegistry.ArcherSource? source, bool toFile)
    {
        var before = StateOf(data);
        data.Name0 = name0.Trim();
        data.Name1 = name1.Trim();
        data.ColorA = ToColor(colorA);
        data.ColorB = ToColor(colorB);
        data.LightbarColor = ToColor(colorLight);
        data.Gender = (TFGame.Genders)gender;
        data.StartNoHat = startNoHat;
        dataStatus = "Applied in memory.";
        var after = StateOf(data);
        EditHistory.Push("Apply archer data (memory only)", () => PutState(data, before), () => PutState(data, after));

        if (toFile)
        {
            try
            {
                var doc = ArcherXml.Load(source!.ArcherDataFile!);
                var element = ArcherXml.FindById(doc, source.LocalId)
                              ?? throw new Exception($"'{source.LocalId}' not found in {source.ArcherDataFile}");

                ArcherXml.SetChild(element, "Name0", data.Name0);
                ArcherXml.SetChild(element, "Name1", data.Name1);
                ArcherXml.SetChild(element, "ColorA", ArcherXml.Hex(data.ColorA));
                ArcherXml.SetChild(element, "ColorB", ArcherXml.Hex(data.ColorB));
                ArcherXml.SetChild(element, "LightbarColor", ArcherXml.Hex(data.LightbarColor));
                ArcherXml.SetChild(element, "Genders", data.Gender.ToString());
                ArcherXml.SetChild(element, "StartNoHat", data.StartNoHat.ToString().ToLowerInvariant());
                ArcherXml.Save(doc, source.ArcherDataFile!);
                dataStatus = "Applied and saved " + source.ArcherDataFile;
            }
            catch (Exception e)
            {
                dataStatus = "Applied in memory, but writing the file failed: " + e.Message;
                EditorConsole.Error(dataStatus);
                return;
            }
        }

        EditorConsole.Info($"Archer data: {dataStatus}");
    }

    // ================================================================ type

    private static ArcherEntryType? pendingType;
    private static ArcherData? pendingBase;
    private static string typeStatus = "";

    private static readonly string[] typeNames = { "Normal", "Alt", "Secret" };

    private static void DrawTypeTab(ArcherData data)
    {
        ImGui.TextWrapped("Experimental: rewrites the archer's element in archerData.xml (and its archerCustomData.xml entry) " +
                          "as a normal, alt or secret archer. The game has to be restarted to see it.");

        var source = SourceOf(data);
        if (source == null || source.ArcherDataFile == null)
        {
            ImGui.TextColored(Yellow, "This archer can't be changed: it needs to come from a mod folder on disk.");
            return;
        }

        ImGui.Text($"{source.EntryName} is currently: {source.Type}" +
                   (source.BaseName != null ? $" (of {source.BaseName})" : ""));

        // group buttons
        for (var i = 0; i < typeNames.Length; i++)
        {
            if (i > 0) ImGui.SameLine(0, 2);
            var active = (int)source.Type == i;
            if (active) ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.2f, 0.5f, 0.8f, 1));
            if (ImGui.Button(typeNames[i]) && !active)
            {
                var wanted = (ArcherEntryType)i;
                pendingBase = null;
                typeStatus = "";
                if (wanted == ArcherEntryType.Normal)
                    pendingType = wanted;
                else
                {
                    pendingType = null;
                    ArcherPickerWindow.OpenBasePicker(i, data, chosen =>
                    {
                        pendingType = wanted;
                        pendingBase = chosen;
                    });
                }
            }
            if (active) ImGui.PopStyleColor();
        }

        if (pendingType.HasValue)
        {
            ImGui.Separator();
            ImGui.TextWrapped(pendingType == ArcherEntryType.Normal
                ? $"Make '{source.LocalId}' a normal archer?"
                : $"Make '{source.LocalId}' the {pendingType.ToString()!.ToLowerInvariant()} of " +
                  $"{ArcherNames.Full(pendingBase!.Name0, pendingBase.Name1)}?");

            if (ImGui.Button("Apply"))
            {
                try
                {
                    ChangeType(source, data, pendingType.Value, pendingBase);
                    typeStatus = "Done. Restart the game to load the new type.";
                    EditorConsole.Info($"Archer type: {source.LocalId} changed to {pendingType}");
                }
                catch (Exception e)
                {
                    typeStatus = "Failed: " + e.Message;
                    EditorConsole.Error("Archer type: " + e.Message);
                }
                pendingType = null;
                pendingBase = null;
            }

            ImGui.SameLine();
            if (ImGui.Button("Cancel"))
            {
                pendingType = null;
                pendingBase = null;
            }
        }

        if (typeStatus.Length > 0)
            ImGui.TextWrapped(typeStatus);
    }

    private static void ChangeType(ArcherDecorationRegistry.ArcherSource source, ArcherData data, ArcherEntryType type, ArcherData? baseArcher)
    {
        var decoration = ArcherDecorationRegistry.Decorations.FirstOrDefault(d => d.ArcherData == data);
        var baseReference = baseArcher != null
            ? ArcherDecorationRegistry.ArcherReferenceName(baseArcher) ?? throw new Exception("Can't tell how to reference that archer.")
            : null;

        ArcherTypeChange.Apply(source.ArcherDataFile!, source.LocalId, type, baseReference,
            decoration?.EditablePath, decoration?.Xml.GetAttribute("id"));
    }

    // ================================================================ validate unloaded

    private static string manifestPath = "";
    private static UnloadedValidator.Report? report;

    private static void DrawValidateTab()
    {
        ImGui.TextWrapped("Experimental: pick the meta.json of an archer mod that is not loaded. Its archerData.xml, " +
                          "atlases and sprite data are read from the folder and checked with the archer editor's validator. " +
                          "Unzip zipped mods first.");

        ImGui.SetNextItemWidth(360);
        ImGui.InputText("##manifest", ref manifestPath, 512);
        ImGui.SameLine();
        if (ImGui.Button("Browse..."))
        {
            var picked = NativeFileDialog.Open("Select the archer mod's meta.json", "meta.json\0meta.json\0All files\0*.*\0");
            if (picked != null) manifestPath = picked;
        }

        ImGui.BeginDisabled(!File.Exists(manifestPath));
        if (ImGui.Button("Validate"))
        {
            report = UnloadedValidator.Run(manifestPath);
            LogReport(report);
        }
        ImGui.EndDisabled();

        if (report == null) return;

        ImGui.Separator();
        ImGui.Text($"{report.ModName}  ({report.Directory})");
        foreach (var problem in report.Problems)
            ImGui.TextColored(Yellow, problem);

        foreach (var archer in report.Archers)
        {
            var errors = archer.Messages.Count(m => m.type == ValidatorMessageType.ERROR);
            ImGui.PushStyleColor(ImGuiCol.Text, errors > 0 ? Red : archer.Messages.Count > 0 ? Yellow : Green);
            var open = ImGui.TreeNodeEx($"{archer.Id}  [{archer.Type}]  {ArcherRuntimeValidator.Summary(archer.Messages)}###{archer.Id}",
                ImGuiTreeNodeFlags.DefaultOpen);
            ImGui.PopStyleColor();
            if (!open) continue;

            if (archer.Messages.Count == 0)
                ImGui.TextColored(Green, "No problems found.");
            foreach (var message in archer.Messages)
            {
                var isError = message.type == ValidatorMessageType.ERROR;
                ImGui.PushStyleColor(ImGuiCol.Text, isError ? Red : Yellow);
                ImGui.TextWrapped((isError ? "ERROR " : "WARN  ") + message.message.Trim());
                ImGui.PopStyleColor();
            }
            ImGui.TreePop();
        }
    }

    private static void LogReport(UnloadedValidator.Report result)
    {
        EditorConsole.Info($"Validating unloaded mod {result.ModName} ({result.Archers.Count} archers)");
        foreach (var problem in result.Problems)
            EditorConsole.Warn(problem);
        foreach (var archer in result.Archers)
        {
            EditorConsole.Info($"{archer.Id}: {ArcherRuntimeValidator.Summary(archer.Messages)}");
            foreach (var message in archer.Messages)
                EditorConsole.Log(message.type == ValidatorMessageType.ERROR ? ConsoleLevel.Error : ConsoleLevel.Warn,
                    $"{archer.Id}: {message.message.Trim()}");
        }
    }

    // ================================================================ create copy

    private static string copyId = "", copyName0 = "", copyName1 = "";
    private static ArcherData? copyFor;
    private static List<string> copyErrors = new();
    private static List<string> copyMessages = new();
    private static bool copyOk;
    private static bool copyDirty = true;

    private static void DrawCopyTab(ArcherData data)
    {
        ImGui.TextWrapped("Experimental: creates a new mod in the game's Mods folder with a copy of this archer. " +
                          "An archer from a mod folder gets the whole folder copied (assets, xml, atlases, images); " +
                          "a zipped mod or base game archer is generated from memory (png files and xml).");

        if (!ReferenceEquals(copyFor, data))
        {
            copyFor = data;
            copyId = "";
            copyName0 = data.Name0 ?? "";
            copyName1 = data.Name1 ?? "";
            copyDirty = true;
            copyMessages.Clear();
        }

        var edited = false;
        ImGui.SetNextItemWidth(220);
        edited |= ImGui.InputText("New id (mod name)", ref copyId, 32);
        ImGui.SetNextItemWidth(220);
        edited |= ImGui.InputText("Name", ref copyName0, 32);
        ImGui.SetNextItemWidth(220);
        edited |= ImGui.InputText("Subname", ref copyName1, 32);

        // the checks touch the disk, so only rerun them after an edit
        if (edited || copyDirty)
        {
            copyDirty = false;
            copyErrors = ArcherCopier.Check(data, copyId.Trim(), copyName0, copyName1);
        }

        foreach (var error in copyErrors)
            ImGui.TextColored(Red, error);

        ImGui.BeginDisabled(copyErrors.Count > 0);
        if (ImGui.Button("Create copy"))
        {
            var result = ArcherCopier.Create(data, copyId.Trim(), copyName0, copyName1);
            copyMessages = result.Messages;
            copyOk = result.Ok;
            copyDirty = true;
            foreach (var message in copyMessages)
                EditorConsole.Log(result.Ok ? ConsoleLevel.Info : ConsoleLevel.Error, "Create copy: " + message);
        }
        ImGui.EndDisabled();

        foreach (var message in copyMessages)
            ImGui.TextColored(copyOk ? Green : Red, message);
    }
}
