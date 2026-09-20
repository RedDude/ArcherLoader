using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Xml;
using ArcherEditorMod.Editor.ImGuiSupport;
using ArcherEditorMod.Source.Features;
using ImGuiNET;
using Monocle;
using TowerFall;

namespace ArcherEditorMod.Editor;

/// <summary>
/// Generic browser for the game's xml data: every sprite data container (main, menu, corpse, bg, boss) and the
/// archerCustomData entries. Pick an entry, see its xml tree and texture, optionally edit values in memory.
/// </summary>
public static class DataInspectorWindow
{
    public static bool Open;

    /// <summary>Set by <see cref="Show"/>: the Inspector window should come up on its Data tab.</summary>
    public static bool ShowInInspector;

    private sealed class Source
    {
        public string Label = "";
        public Func<Dictionary<string, XmlElement>> Entries = null!;
        public Func<Atlas?> Atlas = null!;
        public List<string> SortedKeys = new();
        public int KeyCount = -1;
        public string Filter = "";
        public string? Selected;
    }

    private static readonly Source[] sources =
    {
        new() { Label = "SpriteData", Entries = () => Monocle.SpriteDataExt.GetSprites(TFGame.SpriteData), Atlas = () => TFGame.Atlas },
        new() { Label = "MenuSpriteData", Entries = () => Monocle.SpriteDataExt.GetSprites(TFGame.MenuSpriteData), Atlas = () => TFGame.MenuAtlas },
        new() { Label = "CorpseSpriteData", Entries = () => Monocle.SpriteDataExt.GetSprites(TFGame.CorpseSpriteData), Atlas = () => TFGame.Atlas },
        new() { Label = "BGSpriteData", Entries = () => Monocle.SpriteDataExt.GetSprites(TFGame.BGSpriteData), Atlas = () => TFGame.BGAtlas },
        new() { Label = "BossSpriteData", Entries = () => Monocle.SpriteDataExt.GetSprites(TFGame.BossSpriteData), Atlas = () => TFGame.BossAtlas },
        new()
        {
            Label = "archerCustomData",
            Entries = () => ArcherDecorationRegistry.Decorations
                .GroupBy(d => d.TargetName)
                .ToDictionary(g => g.Key, g => g.First().Xml),
            Atlas = () => TFGame.Atlas
        },
    };

    private const int MaxListed = 500;
    private static int sourceIndex;
    private static bool editValues;

    /// <summary>Opens the inspector on the given sprite data entry (e.g. the archer's body sprite).</summary>
    public static void Show(int source, string id)
    {
        sourceIndex = source;
        sources[source].Selected = id;
        sources[source].Filter = id;
        Open = true;
        ShowInInspector = true;
    }

    public static void Draw(ImGuiRenderer renderer, ArcherData? current)
    {
        if (!Open) return;

        ImGui.SetNextWindowSize(new Vector2(720, 460), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowPos(new Vector2(450, 500), ImGuiCond.FirstUseEver);
        if (!ImGui.Begin("Data Inspector", ref Open))
        {
            ImGui.End();
            return;
        }

        try
        {
            DrawContent(renderer, current);
        }
        finally
        {
            ImGui.End();
        }
    }

    /// <summary>The browser itself, for a window or a tab to host.</summary>
    public static void DrawContent(ImGuiRenderer renderer, ArcherData? current)
    {
        // source tabs
        for (var i = 0; i < sources.Length; i++)
        {
            if (i > 0) ImGui.SameLine();
            if (i == sourceIndex) ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.2f, 0.5f, 0.8f, 1));
            if (ImGui.Button(sources[i].Label)) sourceIndex = i;
            if (i == sourceIndex) ImGui.PopStyleColor();
        }

        var source = sources[sourceIndex];
        var entries = source.Entries();
        if (source.KeyCount != entries.Count)
        {
            source.SortedKeys = entries.Keys.OrderBy(k => k).ToList();
            source.KeyCount = entries.Count;
        }

        if (current != null)
        {
            ImGui.SameLine();
            if (ImGui.Button("Current archer") && !string.IsNullOrEmpty(current.Sprites.Body))
                Show(0, current.Sprites.Body);
        }

        ImGui.Checkbox("Edit values (in memory, use Refresh to apply)", ref editValues);

        // ---- id list ----
        ImGui.BeginChild("data_ids", new Vector2(260, 0), ImGuiChildFlags.Borders);
        ImGui.SetNextItemWidth(-1);
        ImGui.InputText("##filter", ref source.Filter, 96);
        var filter = source.Filter.Trim();
        var listed = 0;
        foreach (var key in source.SortedKeys)
        {
            if (filter.Length > 0 && !key.Contains(filter, StringComparison.OrdinalIgnoreCase)) continue;
            if (listed++ >= MaxListed)
            {
                ImGui.TextDisabled($"... narrow the filter ({MaxListed} shown)");
                break;
            }

            if (ImGui.Selectable(key, key == source.Selected))
                source.Selected = key;
        }
        ImGui.EndChild();

        ImGui.SameLine();

        // ---- selected entry ----
        ImGui.BeginChild("data_detail", new Vector2(0, 0), ImGuiChildFlags.Borders);
        if (source.Selected != null && entries.TryGetValue(source.Selected, out var xml))
        {
            ImGui.Text(source.Selected);
            ImGui.SameLine();
            if (ImGui.SmallButton("Copy XML"))
                ImGui.SetClipboardText(xml.OuterXml);

            DrawTexture(renderer, source, xml);
            ImGui.Separator();
            InspectorWindow.DrawXml(xml, editValues);
        }
        else
        {
            ImGui.TextDisabled("Select an entry on the left.");
        }
        ImGui.EndChild();
    }

    private static void DrawTexture(ImGuiRenderer renderer, Source source, XmlElement xml)
    {
        var name = xml["Texture"]?.InnerText.Trim();
        var atlas = source.Atlas();
        if (string.IsNullOrEmpty(name) || atlas == null || !atlas.Contains(name)) return;

        var texture = atlas[name];
        if (!texture.Loaded) return;

        var (id, uv0, uv1) = ImGuiTextures.Region(renderer, texture.Texture2D, texture.Rect);
        var scale = Math.Max(1f, Math.Min(4f, 300f / Math.Max(texture.Width, texture.Height)));
        ImGui.Image(id, new Vector2(texture.Width, texture.Height) * scale, uv0, uv1);
        ImGui.TextDisabled($"{name}  {texture.Width}x{texture.Height}");
    }
}
