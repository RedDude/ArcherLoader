using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
using ArcherEditorMod.Editor.ImGuiSupport;
using ArcherEditorMod.Editor.Tools;
using ArcherEditorMod.Source.Features;
using FortRise;
using ImGuiNET;
using TowerFall;

namespace ArcherEditorMod.Editor;

/// <summary>
/// Edits an archer mod that is not loaded, straight in its files: names and colors, meta.json, type, head / bow
/// offsets, and the raw xml of its entries. There is no live preview. Everything that touches a file goes through
/// Try(), so a bad file or a failed write ends up as a message, never as an exception in the ImGui frame.
/// </summary>
public static class UnloadedEditorWindow
{
    public static bool Open;

    private static readonly Vector4 Red = new(1f, 0.4f, 0.4f, 1);
    private static readonly Vector4 Yellow = new(1f, 0.85f, 0.3f, 1);
    private static readonly Vector4 Green = new(0.5f, 1f, 0.5f, 1);

    // ---- state ----
    private static string metaPath = "";
    private static UnloadedMod? mod;
    private static UnloadedArcher? archer;
    private static string status = "";
    private static bool statusIsError;

    // data tab
    private static UnloadedEdits.DataForm form = new();
    private static List<string> nameProblems = new();
    private static readonly string[] genders = Enum.GetNames<TFGame.Genders>();

    // meta tab
    private static string author = "", description = "", version = "";

    // type tab
    private static readonly string[] typeNames = { "Normal", "Alt", "Secret" };
    private static ArcherEntryType? pendingType;
    private static string? pendingBaseName;
    private static string? pendingBaseLabel;

    // offsets tab
    private static string? bodySpriteId;
    private static string? spriteFile;
    private static readonly string[] tableNames = { "HeadYOrigins", "HeadXOrigins", "BowXOffsets", "BowYOffsets" };
    private static Dictionary<string, int[]?> tables = new();
    private static bool hideBow, hadHideBow;
    private static int newTableSize = 32;

    // xml tab
    private static int xmlKind;
    private static readonly string[] xmlKinds = { "archerData entry", "archerCustomData entry", "body sprite data" };
    private static string xmlText = "";
    private static string xmlFile = "";
    private static string xmlId = "";
    private static string[] xmlRoots = Array.Empty<string>();
    private static string? xmlAddUnder;
    private static bool xmlAvailable;
    private static string xmlWhy = "";

    // validate tab
    private static UnloadedValidator.Report? report;

    // ---- helpers ----

    private static void Try(string what, Action action)
    {
        try
        {
            action();
        }
        catch (Exception e)
        {
            Say($"{what} failed: {e.Message}", true);
        }
    }

    private static void Say(string message, bool error)
    {
        status = message;
        statusIsError = error;
        if (error) EditorConsole.Error("Unloaded editor: " + message);
        else EditorConsole.Info("Unloaded editor: " + message);
    }

    private static void LoadMod(string path)
    {
        var loadedMod = UnloadedMod.Load(path); // throws before anything changes
        mod = loadedMod;
        archer = null;
        report = null;
        Select(loadedMod.Archers[0]);
        Say($"Loaded {loadedMod.Name}: {loadedMod.Archers.Count} archers.", false);
    }

    private static void ReloadMod()
    {
        if (mod == null) return;
        var id = archer?.Id;
        var reloaded = UnloadedMod.Load(mod.MetaPath);
        mod = reloaded;
        Select(reloaded.Archers.FirstOrDefault(a => a.Id == id) ?? reloaded.Archers[0]);
    }

    // reads every form of the selected archer; a failure leaves the previous buffers untouched
    private static void Select(UnloadedArcher selected)
    {
        var newForm = UnloadedEdits.ReadForm(selected);
        var (newAuthor, newDescription, newVersion) = UnloadedEdits.ReadMeta(mod!.MetaPath);

        var newBodyId = UnloadedEdits.BodySpriteId(selected);
        var newSpriteFile = newBodyId != null ? UnloadedEdits.FindSpriteFile(mod, newBodyId) : null;
        var newTables = new Dictionary<string, int[]?>();
        var newHadHide = false;
        var newHide = false;
        if (newBodyId != null && newSpriteFile != null)
        {
            foreach (var name in tableNames)
                newTables[name] = UnloadedEdits.ReadTable(newSpriteFile, newBodyId, name);
            newHide = UnloadedEdits.ReadBool(newSpriteFile, newBodyId, "HideBow");
            newHadHide = newHide || HasElement(newSpriteFile, newBodyId, "HideBow");
        }

        archer = selected;
        form = newForm;
        author = newAuthor;
        description = newDescription;
        version = newVersion;
        bodySpriteId = newBodyId;
        spriteFile = newSpriteFile;
        tables = newTables;
        hideBow = newHide;
        hadHideBow = newHadHide;
        pendingType = null;
        pendingBaseName = null;
        nameProblems = UnloadedEdits.CheckNames(mod, selected, form.Name0, form.Name1);
        LoadXml();
    }

    private static bool HasElement(string file, string spriteId, string name) =>
        ArcherXml.FindById(ArcherXml.Load(file), spriteId)?[name] != null;

    private static void LoadXml()
    {
        xmlAvailable = false;
        xmlWhy = "";
        xmlText = "";
        if (mod == null || archer == null) return;

        try
        {
            switch (xmlKind)
            {
                case 0:
                    xmlFile = archer.ArcherDataFile;
                    xmlId = archer.Id;
                    xmlRoots = new[] { "Archer", "AltArcher", "SecretArcher" };
                    xmlAddUnder = null;
                    xmlText = UnloadedEdits.ReadEntryXml(xmlFile, xmlId);
                    xmlAvailable = true;
                    break;

                case 1:
                    var custom = UnloadedEdits.FindCustomEntry(mod, archer);
                    xmlRoots = new[] { "Archer", "AltArcher", "SecretArcher" };
                    xmlAddUnder = "Archers";
                    if (custom != null)
                    {
                        xmlFile = custom.Value.File;
                        xmlId = custom.Value.Id;
                        xmlText = UnloadedEdits.ReadEntryXml(xmlFile, xmlId);
                    }
                    else
                    {
                        // no entry yet: offer an empty one in the mod's first archerCustomData.xml
                        xmlFile = mod.CustomDataFiles.FirstOrDefault() ??
                                  Path.Combine(mod.Directory, "ArcherCustomData", "archerCustomData.xml");
                        xmlId = archer.Id;
                        xmlText = $"<{archer.Element} id=\"{archer.Id}\">\n</{archer.Element}>";
                        xmlWhy = "This archer has no archerCustomData.xml entry yet; saving creates it.";
                    }
                    xmlAvailable = true;
                    break;

                default:
                    if (bodySpriteId == null || spriteFile == null)
                    {
                        xmlWhy = "The body sprite was not found in this mod's spriteData.xml.";
                        break;
                    }
                    xmlFile = spriteFile;
                    xmlId = bodySpriteId;
                    xmlRoots = Array.Empty<string>();
                    xmlAddUnder = null;
                    xmlText = UnloadedEdits.ReadEntryXml(xmlFile, xmlId);
                    xmlAvailable = true;
                    break;
            }
        }
        catch (Exception e)
        {
            xmlWhy = "Could not read it: " + e.Message;
        }
    }

    // ---- drawing ----

    public static void Draw(ImGuiRenderer renderer)
    {
        if (!Open) return;

        ImGui.SetNextWindowSize(new Vector2(720, 600), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowPos(new Vector2(20, 60), ImGuiCond.FirstUseEver);
        if (!ImGui.Begin("Unloaded Archer (experimental, no live preview)", ref Open))
        {
            ImGui.End();
            return;
        }

        DrawLoader();

        if (mod != null && archer != null)
        {
            DrawArcherPicker();
            ImGui.Separator();
            DrawTabs();
        }

        ImGui.End();
    }

    private static void DrawLoader()
    {
        ImGui.TextWrapped("Edit an archer mod straight in its files, without loading it. Pick its meta.json (folder mods only). " +
                          "Changes are written to the files at once; the game has to be restarted to see them.");

        ImGui.SetNextItemWidth(430);
        ImGui.InputText("##meta", ref metaPath, 512);
        ImGui.SameLine();
        if (ImGui.Button("Browse..."))
        {
            var picked = NativeFileDialog.Open("Select the archer mod's meta.json", "meta.json\0meta.json\0All files\0*.*\0");
            if (picked != null) metaPath = picked;
        }

        ImGui.SameLine();
        ImGui.BeginDisabled(!File.Exists(metaPath));
        if (ImGui.Button("Load"))
            Try("Load", () => LoadMod(metaPath.Trim().Trim('"')));
        ImGui.EndDisabled();

        if (status.Length > 0)
            ImGui.TextColored(statusIsError ? Red : Green, status);
    }

    private static void DrawArcherPicker()
    {
        ImGui.Text($"{mod!.Name}  ({mod.Directory})");

        UnloadedArcher? choose = null;
        ImGui.SetNextItemWidth(320);
        if (ImGui.BeginCombo("Archer", $"{archer!.Id}  [{archer.Element}]  {ArcherNames.Full(archer.Name0, archer.Name1)}"))
        {
            foreach (var candidate in mod.Archers)
            {
                if (ImGui.Selectable($"{candidate.Id}  [{candidate.Element}]  {ArcherNames.Full(candidate.Name0, candidate.Name1)}",
                        candidate == archer))
                    choose = candidate;
            }
            ImGui.EndCombo();
        }

        // switch after the combo is closed
        if (choose != null && choose != archer)
            Try("Select", () => Select(choose));
    }

    private static void DrawTabs()
    {
        if (!ImGui.BeginTabBar("unloaded_tabs"))
            return;

        if (ImGui.BeginTabItem("Data")) { DrawData(); ImGui.EndTabItem(); }
        if (ImGui.BeginTabItem("Meta")) { DrawMeta(); ImGui.EndTabItem(); }
        if (ImGui.BeginTabItem("Type")) { DrawType(); ImGui.EndTabItem(); }
        if (ImGui.BeginTabItem("Offsets")) { DrawOffsets(); ImGui.EndTabItem(); }
        if (ImGui.BeginTabItem("XML")) { DrawXml(); ImGui.EndTabItem(); }
        if (ImGui.BeginTabItem("Validate")) { DrawValidate(); ImGui.EndTabItem(); }

        ImGui.EndTabBar();
    }

    // ---- data ----

    private static void DrawData()
    {
        var edited = false;
        ImGui.SetNextItemWidth(220);
        edited |= ImGui.InputText("Name", ref form.Name0, 32);
        ImGui.SetNextItemWidth(220);
        edited |= ImGui.InputText("Subname", ref form.Name1, 32);
        ImGui.ColorEdit3("ColorA", ref form.ColorA);
        ImGui.ColorEdit3("ColorB", ref form.ColorB);
        ImGui.ColorEdit3("LightbarColor", ref form.Light);
        ImGui.SetNextItemWidth(140);
        ImGui.Combo("Gender", ref form.Gender, genders, genders.Length);
        ImGui.Checkbox("Starts without a hat", ref form.StartNoHat);

        if (edited)
            nameProblems = UnloadedEdits.CheckNames(mod!, archer!, form.Name0, form.Name1);

        foreach (var problem in nameProblems)
            ImGui.TextColored(Red, problem);

        ImGui.BeginDisabled(nameProblems.Count > 0);
        if (ImGui.Button("Save"))
        {
            Try("Save data", () =>
            {
                UnloadedEdits.WriteForm(archer!, form);
                Say($"Saved {archer!.ArcherDataFile}", false);
                ReloadMod();
            });
        }
        ImGui.EndDisabled();
    }

    // ---- meta ----

    private static void DrawMeta()
    {
        ImGui.SetNextItemWidth(260);
        ImGui.InputText("Author", ref author, 64);
        ImGui.SetNextItemWidth(260);
        ImGui.InputText("Description", ref description, 200);
        ImGui.SetNextItemWidth(140);
        ImGui.InputText("Version", ref version, 24);

        var problems = new List<string>();
        if (string.IsNullOrWhiteSpace(author)) problems.Add("Author is empty.");
        if (string.IsNullOrWhiteSpace(description)) problems.Add("Description is empty.");
        if (!Regex.IsMatch(version.Trim(), @"^\d+\.\d+\.\d+([-+][0-9A-Za-z.-]+)?$")) problems.Add("Version must look like 1.2.3.");
        foreach (var problem in problems)
            ImGui.TextColored(Red, problem);

        ImGui.BeginDisabled(problems.Count > 0);
        if (ImGui.Button("Save meta.json"))
        {
            Try("Save meta", () =>
            {
                UnloadedEdits.WriteMeta(mod!.MetaPath, author.Trim(), description.Trim(), version.Trim());
                Say($"Saved {mod.MetaPath}", false);
            });
        }
        ImGui.EndDisabled();
    }

    // ---- type ----

    private static void DrawType()
    {
        ImGui.TextWrapped("Rewrites the archer's element (and its archerCustomData.xml entry) as a normal, alt or secret archer.");
        var currentType = archer!.Element switch { "AltArcher" => 1, "SecretArcher" => 2, _ => 0 };
        ImGui.Text($"Currently: {typeNames[currentType]}");

        for (var i = 0; i < typeNames.Length; i++)
        {
            if (i > 0) ImGui.SameLine(0, 2);
            var active = currentType == i;
            if (active) ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.2f, 0.5f, 0.8f, 1));
            var clicked = ImGui.Button(typeNames[i]);
            if (active) ImGui.PopStyleColor();

            if (!clicked || active) continue;

            var wanted = (ArcherEntryType)i;
            pendingBaseName = pendingBaseLabel = null;
            if (wanted == ArcherEntryType.Normal)
            {
                pendingType = wanted;
            }
            else
            {
                pendingType = null;
                // the base is a loaded archer: same picker as the live editor, only archers still free for it
                ArcherPickerWindow.OpenBasePicker(i, null, chosen =>
                {
                    pendingBaseName = ArcherDecorationRegistry.ArcherReferenceName(chosen);
                    pendingBaseLabel = ArcherNames.Full(chosen.Name0, chosen.Name1);
                    pendingType = pendingBaseName != null ? wanted : null;
                    if (pendingBaseName == null)
                        Say("Can't tell how to reference that archer.", true);
                });
            }
        }

        if (!pendingType.HasValue) return;

        ImGui.Separator();
        ImGui.TextWrapped(pendingType == ArcherEntryType.Normal
            ? $"Make '{archer.Id}' a normal archer?"
            : $"Make '{archer.Id}' the {pendingType.ToString()!.ToLowerInvariant()} of {pendingBaseLabel} ({pendingBaseName})?");

        if (ImGui.Button("Apply"))
        {
            var type = pendingType.Value;
            var reference = pendingBaseName;
            pendingType = null;
            Try("Change type", () =>
            {
                var custom = UnloadedEdits.FindCustomEntry(mod!, archer!);
                ArcherTypeChange.Apply(archer!.ArcherDataFile, archer.Id, type, reference, custom?.File, custom?.Id);
                Say($"{archer.Id} is now {type}.", false);
                ReloadMod();
            });
        }

        ImGui.SameLine();
        if (ImGui.Button("Cancel"))
            pendingType = null;
    }

    // ---- offsets ----

    private static void DrawOffsets()
    {
        if (bodySpriteId == null || spriteFile == null)
        {
            ImGui.TextColored(Yellow, "This archer's body sprite was not found in the mod's spriteData.xml, so there are no offsets to edit.");
            return;
        }

        ImGui.TextDisabled($"{bodySpriteId}  in  {Path.GetFileName(spriteFile)}");
        ImGui.TextWrapped("Per frame numbers of the body sprite (no preview here: use the live editor's Animation window for that). " +
                          "HeadYOrigins is required by the game and must cover every frame the animations use.");

        foreach (var name in tableNames)
        {
            ImGui.PushID(name);
            ImGui.SeparatorText(name);

            var values = tables.TryGetValue(name, out var existing) ? existing : null;
            if (values == null)
            {
                ImGui.TextDisabled("(not in the file)");
                ImGui.SameLine();
                ImGui.SetNextItemWidth(90);
                ImGui.InputInt("entries", ref newTableSize);
                newTableSize = Math.Clamp(newTableSize, 1, 256);
                ImGui.SameLine();
                if (ImGui.Button("Create"))
                    tables[name] = new int[newTableSize];
            }
            else
            {
                for (var i = 0; i < values.Length; i++)
                {
                    if (i % 8 != 0) ImGui.SameLine();
                    ImGui.SetNextItemWidth(64);
                    ImGui.InputInt($"##{i}", ref values[i], 0, 0);
                }

                ImGui.Text($"{values.Length} entries");
                ImGui.SameLine();
                if (ImGui.SmallButton("+ entry"))
                    tables[name] = values.Append(values.Length > 0 ? values[^1] : 0).ToArray();
                ImGui.SameLine();
                if (ImGui.SmallButton("- entry") && values.Length > 0)
                    tables[name] = values.Take(values.Length - 1).ToArray();
            }

            ImGui.PopID();
        }

        ImGui.SeparatorText("Bow");
        ImGui.Checkbox("Hide bow unless aiming (HideBow)", ref hideBow);

        ImGui.Separator();
        if (ImGui.Button("Save offsets"))
        {
            Try("Save offsets", () =>
            {
                UnloadedEdits.WriteOffsets(spriteFile!, bodySpriteId!, tables, hideBow, hadHideBow);
                hadHideBow |= hideBow;
                Say($"Saved {spriteFile}", false);
            });
        }
    }

    // ---- xml ----

    private static void DrawXml()
    {
        ImGui.SetNextItemWidth(220);
        if (ImGui.Combo("Element", ref xmlKind, xmlKinds, xmlKinds.Length))
            LoadXml();

        if (!xmlAvailable)
        {
            ImGui.TextColored(Yellow, xmlWhy.Length > 0 ? xmlWhy : "Nothing to show.");
            return;
        }

        if (xmlWhy.Length > 0)
            ImGui.TextColored(Yellow, xmlWhy);
        ImGui.TextDisabled(xmlFile);

        ImGui.InputTextMultiline("##xml", ref xmlText, 65536, new Vector2(-1, 320));

        if (ImGui.Button("Save"))
        {
            Try("Save xml", () =>
            {
                UnloadedEdits.WriteEntryXml(xmlFile, xmlId, xmlText, xmlRoots, xmlAddUnder);
                Say($"Saved {xmlFile}", false);
                ReloadMod();
            });
        }

        ImGui.SameLine();
        if (ImGui.Button("Reload from file"))
            LoadXml();
    }

    // ---- validate ----

    private static void DrawValidate()
    {
        ImGui.TextWrapped("Runs the archer editor's validator on the mod's files (atlas names, sprite ids, required elements).");
        if (ImGui.Button("Validate this mod"))
        {
            Try("Validate", () =>
            {
                report = UnloadedValidator.Run(mod!.MetaPath);
                foreach (var entry in report.Archers)
                {
                    EditorConsole.Info($"{entry.Id}: {ArcherRuntimeValidator.Summary(entry.Messages)}");
                    foreach (var message in entry.Messages)
                        EditorConsole.Log(message.type == ValidatorMessageType.ERROR ? ConsoleLevel.Error : ConsoleLevel.Warn,
                            $"{entry.Id}: {message.message.Trim()}");
                }
            });
        }

        if (report == null) return;

        foreach (var problem in report.Problems)
            ImGui.TextColored(Yellow, problem);

        foreach (var entry in report.Archers)
        {
            var mine = entry.Id == archer!.Id;
            var errors = entry.Messages.Count(m => m.type == ValidatorMessageType.ERROR);
            ImGui.PushStyleColor(ImGuiCol.Text, errors > 0 ? Red : entry.Messages.Count > 0 ? Yellow : Green);
            var open = ImGui.TreeNodeEx($"{entry.Id}  {ArcherRuntimeValidator.Summary(entry.Messages)}###{entry.Id}",
                mine ? ImGuiTreeNodeFlags.DefaultOpen : ImGuiTreeNodeFlags.None);
            ImGui.PopStyleColor();
            if (!open) continue;

            if (entry.Messages.Count == 0)
                ImGui.TextColored(Green, "No problems found.");
            foreach (var message in entry.Messages)
            {
                var isError = message.type == ValidatorMessageType.ERROR;
                ImGui.PushStyleColor(ImGuiCol.Text, isError ? Red : Yellow);
                ImGui.TextWrapped((isError ? "ERROR " : "WARN  ") + message.message.Trim());
                ImGui.PopStyleColor();
            }
            ImGui.TreePop();
        }
    }
}
