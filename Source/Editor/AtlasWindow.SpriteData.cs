using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Xml;
using ArcherEditorMod.Source.Features;
using ImGuiNET;
using Rectangle = Microsoft.Xna.Framework.Rectangle;
using Monocle;
using TowerFall;

namespace ArcherEditorMod.Editor;

/// <summary>
/// The sprite data editor of the atlas window: every sprite data entry of the atlas in a list (the ones the archer
/// needs but does not have in red, click to add them from a template), all the fields of the selected entry, and its
/// texture rectangles on the atlas page (move, resize, X / Y / OriginX / OriginY arrows). Edits are in memory until
/// the Save archer data window writes them to the archer's mod (atlas xml + sprite data xml).
/// </summary>
public static partial class AtlasWindow
{
    private static readonly string[] RectTags = { "Texture", "RedTexture", "BlueTexture", "RedTeam", "BlueTeam", "Flash" };
    private static readonly string[] AtlasFiles = { "atlas.xml", "menuAtlas.xml", "bgAtlas.xml", "bossAtlas.xml" };
    private static readonly string[] IntFields = { "X", "Y", "OriginX", "OriginY", "FrameWidth", "FrameHeight" };

    private static readonly Vector4 Red = new(1f, 0.4f, 0.4f, 1);
    private static readonly Vector4 Yellow = new(1f, 0.85f, 0.3f, 1);

    /// <summary>Show the sprite data list / fields next to the atlas.</summary>
    public static bool ShowSpritePanels = true;


    private static string? selSource, selId;
    private static string activeTag = "Texture";
    private static string spriteFilter = "";
    private static string newField = "";

    private static readonly HashSet<string> dirtySprites = new();  // "source|id"
    private static readonly HashSet<string> dirtyRects = new();    // "atlas|name"
    private static string? focusTexture;                            // texture name to bring on screen
    private static Action? refreshEditor;

    private static ArcherData? boundArcher;
    private static Vector2 pan;
    private static string? lastSelection;

    // sprite data / rectangles changed: the Animation window drops its cached sprites and the editor rebuilds its archer
    private static void NotifyEdited()
    {
        FramesWindow.ClearCache();
        refreshEditor?.Invoke();
    }

    /// <summary>The Animation window edited a sprite (frames, animation attributes): it is written on save.</summary>
    internal static void MarkSpriteDirty(string source, string id) => dirtySprites.Add($"{source}|{id}");

    public static bool HasSpriteChanges => dirtySprites.Count > 0 || dirtyRects.Count > 0;

    // ---- sources ----

    private static SpriteData? SpriteDataOf(string source) => source switch
    {
        "SpriteData" => TFGame.SpriteData,
        "MenuSpriteData" => TFGame.MenuSpriteData,
        "CorpseSpriteData" => TFGame.CorpseSpriteData,
        "BGSpriteData" => TFGame.BGSpriteData,
        "BossSpriteData" => TFGame.BossSpriteData,
        _ => null
    };

    private static int AtlasOf(string source) => source switch
    {
        "MenuSpriteData" => 1,
        "BGSpriteData" => 2,
        "BossSpriteData" => 3,
        _ => 0
    };

    private static string SpriteFileOf(string source) => source switch
    {
        "MenuSpriteData" => "menuSpriteData.xml",
        "CorpseSpriteData" => "corpseSpriteData.xml",
        "BGSpriteData" => "bgSpriteData.xml",
        "BossSpriteData" => "bossSpriteData.xml",
        _ => "spriteData.xml"
    };

    private static Dictionary<string, XmlElement>? EntriesOf(string source)
    {
        var data = SpriteDataOf(source);
        return data == null ? null : Monocle.SpriteDataExt.GetSprites(data);
    }

    private static XmlElement? SelectedXml() =>
        selSource != null && selId != null && EntriesOf(selSource) is { } entries && entries.TryGetValue(selId, out var xml) ? xml : null;

    private static string? ResolveName(int atlasIndex, string? text)
    {
        var atlas = AtlasAt(atlasIndex);
        if (atlas == null || string.IsNullOrWhiteSpace(text)) return null;

        text = text.Trim();
        if (atlas.SubTextures.ContainsKey(text)) return text;

        var trimmed = text.TrimStart('@');
        return atlas.SubTextures.ContainsKey(trimmed) ? trimmed : null;
    }

    private static Subtexture? ResolveSub(int atlasIndex, string? text) =>
        ResolveName(atlasIndex, text) is { } name ? AtlasAt(atlasIndex)!.SubTextures[name] : null;

    private static Vector4 TagColor(string tag) => tag switch
    {
        "RedTexture" or "RedTeam" => new Vector4(1f, 0.25f, 0.25f, 1),
        "BlueTexture" or "BlueTeam" => new Vector4(0.3f, 0.5f, 1f, 1),
        "Flash" => new Vector4(1f, 1f, 1f, 1),
        _ => new Vector4(1f, 0.85f, 0.2f, 1)
    };

    // ---- what the archer needs ----

    private sealed record Missing(string Source, string Id, string Kind);

    private static List<Missing> MissingSprites(ArcherData? archer)
    {
        var result = new List<Missing>();
        if (archer == null) return result;

        void Check(string source, string? id, string kind)
        {
            if (string.IsNullOrWhiteSpace(id)) return;
            var data = SpriteDataOf(source);
            if (data != null && !data.Contains(id))
                result.Add(new Missing(source, id, kind));
        }

        Check("SpriteData", archer.Sprites.Body, "Body");
        Check("SpriteData", archer.Sprites.HeadNormal, "HeadNormal");
        Check("SpriteData", archer.Sprites.HeadNoHat, "HeadNoHat");
        Check("SpriteData", archer.Sprites.HeadCrown, "HeadCrown");
        Check("SpriteData", archer.Sprites.HeadBack, "HeadBack");
        Check("SpriteData", archer.Sprites.Bow, "Bow");
        Check("CorpseSpriteData", archer.Corpse, "Corpse");
        Check("MenuSpriteData", archer.Gems.Menu, "GemMenu");
        Check("SpriteData", archer.Gems.Gameplay, "GemGameplay");
        return result;
    }

    private static string? TemplateId(string kind)
    {
        var basic = ArcherData.Archers[0];
        return kind switch
        {
            "Body" => basic.Sprites.Body,
            "HeadNormal" => basic.Sprites.HeadNormal,
            "HeadNoHat" => basic.Sprites.HeadNoHat,
            "HeadCrown" => basic.Sprites.HeadCrown,
            "HeadBack" => basic.Sprites.HeadBack is { Length: > 0 } back ? back : basic.Sprites.HeadNormal,
            "Bow" => basic.Sprites.Bow,
            "Corpse" => basic.Corpse,
            "GemMenu" => basic.Gems.Menu,
            _ => basic.Gems.Gameplay
        };
    }

    private static string SuffixOf(string tag) => tag switch
    {
        "RedTexture" => "_red",
        "BlueTexture" => "_blue",
        "RedTeam" => "_redTeam",
        "BlueTeam" => "_blueTeam",
        "Flash" => "_flash",
        _ => ""
    };

    // A copy of what the base game's first archer has for this kind of sprite, with new texture rectangles in the
    // archer's own atlas page (to be moved onto the art afterwards).
    private static void AddFromTemplate(Missing missing, ArcherData archer)
    {
        var entries = EntriesOf(missing.Source) ?? throw new InvalidOperationException("No sprite data for " + missing.Source);
        var baseId = TemplateId(missing.Kind);
        if (baseId == null || !entries.TryGetValue(baseId, out var template))
            throw new InvalidOperationException($"The template sprite '{baseId}' was not found.");

        var atlasIndex = AtlasOf(missing.Source);
        var atlas = AtlasAt(atlasIndex)!;
        var xml = (XmlElement)template.CloneNode(true);
        xml.SetAttribute("id", missing.Id);

        // the page the archer's other art is on, else the template's
        var own = new[] { archer.Sprites.Body, archer.Sprites.Bow, archer.Sprites.HeadNormal }
            .Select(id => id != null && EntriesOf("SpriteData") is { } main && main.TryGetValue(id, out var x)
                ? ResolveSub(0, x["Texture"]?.InnerText) : null)
            .FirstOrDefault(s => s != null);

        var createdSubs = new List<(string Name, Subtexture Sub)>();
        foreach (var tag in RectTags)
        {
            var element = xml[tag];
            var source = ResolveSub(atlasIndex, element?.InnerText);
            if (element == null || source == null) continue;

            var texture = own != null && atlasIndex == 0 ? own.Texture : source.Texture;
            var name = missing.Id + SuffixOf(tag);
            while (atlas.SubTextures.ContainsKey(name))
                name += "_new";

            // below the lowest thing on that page, or the corner when there is no room
            var bottom = atlas.SubTextures.Values.Where(s => ReferenceEquals(s.Texture, texture)).Select(s => s.Rect.Bottom).DefaultIfEmpty(0).Max();
            var y = texture.Texture2D != null && bottom + source.Height > texture.Texture2D.Height ? 0 : bottom;
            var created = new Subtexture(texture, 0, y, source.Width, source.Height);
            atlas.SubTextures[name] = created;
            createdSubs.Add((name, created));
            dirtyRects.Add($"{atlasIndex}|{name}");
            element.InnerText = name;
        }

        entries[missing.Id] = xml;
        dirtySprites.Add($"{missing.Source}|{missing.Id}");
        Select(missing.Source, missing.Id);

        // undo takes the sprite and its new rectangles away again (it goes back to the red template entry)
        EditHistory.Push("Add sprite " + missing.Id, () =>
        {
            entries.Remove(missing.Id);
            foreach (var (name, _) in createdSubs)
            {
                atlas.SubTextures.Remove(name);
                dirtyRects.Remove($"{atlasIndex}|{name}");
            }

            dirtySprites.Remove($"{missing.Source}|{missing.Id}");
            if (selId == missing.Id) selId = null;
            FramesWindow.ClearCache();
        }, () =>
        {
            entries[missing.Id] = xml;
            foreach (var (name, sub) in createdSubs)
            {
                atlas.SubTextures[name] = sub;
                dirtyRects.Add($"{atlasIndex}|{name}");
            }

            dirtySprites.Add($"{missing.Source}|{missing.Id}");
            FramesWindow.ClearCache();
        });
        NotifyEdited();
    }

    private static void Select(string source, string id)
    {
        selSource = source;
        selId = id;
        activeTag = "Texture";
        var xml = SelectedXml();
        focusTexture = xml == null ? null : ResolveName(AtlasOf(source), xml["Texture"]?.InnerText);
        if (focusTexture != null)
        {
            atlasIndex = AtlasOf(source);
            pageIndex = -1; // found by the window from the texture name
            focusPending = true;
        }
    }

    private static bool focusPending;

    // ---- list ----


    // What the game reads from each kind of sprite (Player, PlayerCorpse...); missing ones can be added from the fields panel.
    private static (string Name, string Default)[] ExpectedFields(string source, string id)
    {
        var a = boundArcher;
        var common = new (string, string)[]
        {
            ("Texture", ""), ("FrameWidth", "16"), ("FrameHeight", "16"), ("OriginX", "0"), ("OriginY", "0"), ("X", "0"), ("Y", "0")
        };

        if (source == "CorpseSpriteData")
            return common.Concat(new (string, string)[] { ("RedTeam", ""), ("BlueTeam", ""), ("Flash", "") }).ToArray();

        var team = new (string, string)[] { ("RedTexture", ""), ("BlueTexture", "") };
        if (a != null && id == a.Sprites.Body)
            return common.Concat(team).Concat(new (string, string)[] { ("HeadYOrigins", "0"), ("HeadXOrigins", "0"), ("SlideHead", "True"), ("HideBow", "False") }).ToArray();
        if (a != null && id == a.Sprites.Bow)
            return common.Concat(team).Concat(new (string, string)[] { ("DownY", "0") }).ToArray();
        if (a != null && (id == a.Sprites.HeadNormal || id == a.Sprites.HeadNoHat || id == a.Sprites.HeadCrown || id == a.Sprites.HeadBack))
            return common.Concat(team).ToArray();

        return common;
    }

    private static void DrawSpriteList(ArcherData? archer)
    {
        ImGui.SetNextItemWidth(-1);
        ImGui.InputTextWithHint("##spritefilter", "filter sprite data", ref spriteFilter, 96);
        var filterText = spriteFilter.Trim();

        // in the archer window everything the archer refers to is listed, whatever atlas it lives in
        var sources = archerMode
            ? new[] { "SpriteData", "CorpseSpriteData", "MenuSpriteData" }.Select(s => (Source: s, Data: SpriteDataOf(s))).ToList()
            : SpriteDataFor(atlasIndex).Select(s => (Source: s.Source, Data: s.Data)).ToList();
        var references = archerMode && archer != null ? ReferencedSprites(archer) : null;

        // ---- missing (red): click adds it from the template ----
        foreach (var missing in MissingSprites(archer))
        {
            if (!archerMode && AtlasOf(missing.Source) != atlasIndex) continue;
            if (filterText.Length > 0 && !missing.Id.Contains(filterText, StringComparison.OrdinalIgnoreCase)) continue;

            ImGui.PushStyleColor(ImGuiCol.Text, Red);
            var clicked = ImGui.Selectable($"+ {missing.Id}##missing{missing.Source}{missing.Id}");
            ImGui.PopStyleColor();
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip($"The archer uses '{missing.Id}' ({missing.Kind}) but no sprite data has it.\nClick to add it from the template.");
            if (clicked)
            {
                try { AddFromTemplate(missing, archer!); }
                catch (Exception e) { EditorConsole.Error("Adding the sprite data failed: " + e.Message); }
            }
        }

        // referenced by the custom data (taunt, portrait layers) but not there: red, nothing to add it from
        if (references != null)
        {
            foreach (var reference in references)
            {
                if (SpriteDataOf(reference.Source) is not { } data || data.Contains(reference.Id)) continue;
                if (MissingSprites(archer).Any(m => m.Source == reference.Source && m.Id == reference.Id)) continue;

                ImGui.PushStyleColor(ImGuiCol.Text, Red);
                ImGui.Selectable($"! {reference.Id}##absent{reference.Source}{reference.Id}");
                ImGui.PopStyleColor();
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip($"{reference.Label}: '{reference.Id}' is not in {reference.Source}.");
            }
        }

        foreach (var (source, data) in sources)
        {
            if (data == null) continue;

            var listedHeader = false;
            foreach (var id in Monocle.SpriteDataExt.GetSprites(data).Keys.OrderBy(k => k).ToList())
            {
                if (filterText.Length > 0 && !id.Contains(filterText, StringComparison.OrdinalIgnoreCase)) continue;

                string? role = null;
                if (references != null)
                {
                    role = references.FirstOrDefault(r => r.Source == source && r.Id == id)?.Label;
                    if (role == null) continue;
                }

                if (!listedHeader)
                {
                    ImGui.SeparatorText(source);
                    listedHeader = true;
                }

                var selected = source == selSource && id == selId;
                var dirty = dirtySprites.Contains($"{source}|{id}");
                if (ImGui.Selectable((dirty ? "* " : "") + (role != null ? $"{role}: " : "") + id + $"##{source}{id}", selected))
                    Select(source, id);
            }
        }

        if (archerMode)
            DrawTextureList(archer, filterText);
    }

    // ---- fields ----

    private static void DrawSpriteFields()
    {
        if (selSource == TexturesSource && selId != null)
        {
            DrawTextureFields();
            return;
        }

        var xml = SelectedXml();
        if (xml == null || selSource == null || selId == null)
        {
            ImGui.TextDisabled("Pick a sprite data entry.");
            return;
        }

        var key = $"{selSource}|{selId}";
        // every change of this entry goes through the shared undo history (one step per widget interaction)
        void Track(string label) => EditHistory.BeforeXmlEdit(xml, label, () =>
        {
            dirtySprites.Add(key);
            FramesWindow.ClearCache();
        });

        void Edited()
        {
            dirtySprites.Add(key);
            NotifyEdited();
        }

        ImGui.BeginDisabled(!EditHistory.CanUndo);
        if (ImGui.SmallButton("Undo")) EditHistory.Undo();
        ImGui.EndDisabled();
        ImGui.SameLine();
        ImGui.BeginDisabled(!EditHistory.CanRedo);
        if (ImGui.SmallButton("Redo")) EditHistory.Redo();
        ImGui.EndDisabled();
        ImGui.SameLine();
        ImGui.TextDisabled("Ctrl+Z / Ctrl+Y");

        ImGui.Text(selId);
        ImGui.TextDisabled($"{xml.Name} in {selSource}");
        ImGui.Separator();

        var atlasIdx = AtlasOf(selSource);
        foreach (var child in xml.ChildNodes.OfType<XmlElement>().ToList())
        {
            ImGui.PushID(child.Name);

            if (child.Name == "Animations")
            {
                var count = child.GetElementsByTagName("Anim").Count;
                if (ImGui.Button($"Animations ({count})..."))
                    FramesWindow.ShowSprite(selSource, selId);
            }
            else if (child.ChildNodes.OfType<XmlElement>().Any())
            {
                if (ImGui.TreeNode(child.Name))
                {
                    InspectorWindow.DrawXml(child, true, () => Track("Edit " + child.Name));
                    ImGui.TreePop();
                }
            }
            else if (Array.IndexOf(IntFields, child.Name) >= 0)
            {
                var value = int.TryParse(child.InnerText.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;
                ImGui.SetNextItemWidth(110);
                if (ImGui.InputInt(child.Name, ref value))
                {
                    Track("Edit " + child.Name);
                    child.InnerText = value.ToString(CultureInfo.InvariantCulture);
                    Edited();
                }
            }
            else
            {
                var text = child.InnerText;
                var isTexture = Array.IndexOf(RectTags, child.Name) >= 0;
                var missing = isTexture && ResolveName(atlasIdx, text) == null;
                if (missing) ImGui.PushStyleColor(ImGuiCol.Text, Red);
                ImGui.SetNextItemWidth(200);
                if (ImGui.InputText(child.Name, ref text, 256))
                {
                    Track("Edit " + child.Name);
                    child.InnerText = text;
                    Edited();
                }
                if (missing) ImGui.PopStyleColor();

                if (isTexture && ResolveName(atlasIdx, text) is { } name && ImGui.IsItemHovered())
                    ImGui.SetTooltip("Click the field name to pick this rectangle: " + name);
                if (isTexture && ImGui.IsItemClicked())
                    activeTag = child.Name;
            }

            ImGui.PopID();
        }

        // fields the game reads for this kind of sprite and that this entry does not have yet
        var absent = ExpectedFields(selSource, selId).Where(f => xml[f.Name] == null).ToList();
        if (absent.Count > 0)
        {
            ImGui.Separator();
            ImGui.TextDisabled("Missing fields the game reads:");
            foreach (var (fieldName, fieldDefault) in absent)
            {
                if (!ImGui.SmallButton("+ " + fieldName)) continue;

                Track("Add " + fieldName);
                var added = xml.OwnerDocument.CreateElement(fieldName);
                added.InnerText = fieldDefault;
                xml.AppendChild(added);
                Edited();
            }
        }

        ImGui.Separator();
        ImGui.SetNextItemWidth(120);
        ImGui.InputTextWithHint("##newfield", "new field", ref newField, 32);
        ImGui.SameLine();
        if (ImGui.SmallButton("Add") && newField.Trim().Length > 0 && xml[newField.Trim()] == null)
        {
            Track("Add field");
            xml.AppendChild(xml.OwnerDocument.CreateElement(newField.Trim()));
            newField = "";
            Edited();
        }

        // ---- the active rectangle, to the pixel ----
        var sub = ResolveSub(atlasIdx, xml[activeTag]?.InnerText);
        if (sub == null) return;

        ImGui.SeparatorText($"{activeTag} rectangle");
        DrawRectBoxes(atlasIdx, ResolveName(atlasIdx, xml[activeTag]!.InnerText)!, sub);
    }

    // x / y / width / height of a rectangle as numbers; one interaction is one undo step
    private static void DrawRectBoxes(int atlasIdx, string name2, Subtexture sub)
    {
        var rect = sub.Rect;
        ImGui.SetNextItemWidth(110); var a = ImGui.InputInt("x", ref rect.X);
        ImGui.SetNextItemWidth(110); var b = ImGui.InputInt("y", ref rect.Y);
        ImGui.SetNextItemWidth(110); var c = ImGui.InputInt("width", ref rect.Width);
        ImGui.SetNextItemWidth(110); var d = ImGui.InputInt("height", ref rect.Height);
        if (a | b | c | d)
        {
            rectFieldBefore ??= sub.Rect;
            rect.Width = Math.Max(1, rect.Width);
            rect.Height = Math.Max(1, rect.Height);
            sub.Rect = rect;
            dirtyRects.Add($"{atlasIdx}|{name2}");
            NotifyEdited();
        }
        else if (rectFieldBefore is { } before && !ImGui.IsAnyItemActive())
        {
            PushRectEdit(new RectEdit(atlasIdx, name2, before, sub.Rect));
            rectFieldBefore = null;
        }
    }

    // a texture picked from the archer's textures (portrait, hat...): only its rectangle
    private static void DrawTextureFields()
    {
        ImGui.BeginDisabled(!EditHistory.CanUndo);
        if (ImGui.SmallButton("Undo")) EditHistory.Undo();
        ImGui.EndDisabled();
        ImGui.SameLine();
        ImGui.BeginDisabled(!EditHistory.CanRedo);
        if (ImGui.SmallButton("Redo")) EditHistory.Redo();
        ImGui.EndDisabled();

        ImGui.Text(selId);
        ImGui.TextDisabled($"texture in {atlasNames[selTextureAtlas]}");
        ImGui.Separator();
        if (AtlasAt(selTextureAtlas)?.SubTextures.TryGetValue(selId!, out var sub) == true)
            DrawRectBoxes(selTextureAtlas, selId!, sub);
    }

    // ---- canvas: rectangles of the selected sprite ----

    private enum Drag { None, Move, Resize }

    private static Drag drag;
    private static Rectangle dragStartRect;
    private static Vector2 dragStartMouse;
    private static Rectangle? rectFieldBefore;


    // draws the rectangles of the selected sprite that live on this page and handles the mouse on them
    private static void DrawSpriteOverlay(ImDrawListPtr drawList, Vector2 origin, Page page, bool clicked, bool hoveredCanvas)
    {
        var xml = SelectedXml();
        if (selSource == null) return;

        var atlasIdx = SelectedAtlas();
        var io = ImGui.GetIO();

        var rects = new List<(string Tag, string Name, Subtexture Sub)>();
        if (xml != null)
        {
            foreach (var tag in RectTags)
            {
                var name = ResolveName(atlasIdx, xml[tag]?.InnerText);
                if (name == null) continue;

                var sub = AtlasAt(atlasIdx)!.SubTextures[name];
                if (ReferenceEquals(sub.Texture, page.Texture))
                    rects.Add((tag, name, sub));
            }
        }
        else if (selSource == TexturesSource && selId != null && AtlasAt(atlasIdx)?.SubTextures.TryGetValue(selId, out var picked) == true &&
                 ReferenceEquals(picked.Texture, page.Texture))
        {
            rects.Add(("Texture", selId, picked)); // a texture on its own: just the one rectangle
        }

        Vector2 ToScreen(Vector2 pixel) => origin + pixel * zoom;

        // ---- mouse ----
        if (clicked)
        {
            var mouse = (io.MousePos - origin) / zoom;
            var hit = -1;
            var best = long.MaxValue;

            // the resize handle of the active rectangle wins over everything
            var handleHit = false;
            foreach (var (tag, _, sub) in rects)
            {
                if (tag != activeTag) continue;
                var corner = ToScreen(new Vector2(sub.Rect.Right, sub.Rect.Bottom));
                if (Vector2.Distance(io.MousePos, corner) <= 8) handleHit = true;
            }

            if (!handleHit)
            {
                for (var i = 0; i < rects.Count; i++)
                {
                    var r = rects[i].Sub.Rect;
                    if (!r.Contains((int)Math.Floor(mouse.X), (int)Math.Floor(mouse.Y))) continue;
                    var area = (long)r.Width * r.Height;
                    if (area < best) { best = area; hit = i; }
                }

                if (hit >= 0) activeTag = rects[hit].Tag;
            }

            var active = rects.FirstOrDefault(r => r.Tag == activeTag);
            if (active.Sub != null && (handleHit || hit >= 0))
            {
                drag = handleHit ? Drag.Resize : Drag.Move;
                dragStartRect = active.Sub.Rect;
                dragStartMouse = io.MousePos;
            }
        }

        var current = rects.FirstOrDefault(r => r.Tag == activeTag);
        if (drag != Drag.None)
        {
            if (ImGui.IsMouseDown(ImGuiMouseButton.Left) && current.Sub != null)
            {
                var delta = (io.MousePos - dragStartMouse) / zoom;
                var dx = (int)Math.Round(delta.X);
                var dy = (int)Math.Round(delta.Y);
                var r = dragStartRect;
                if (drag == Drag.Move)
                {
                    r.X += dx;
                    r.Y += dy;
                }
                else
                {
                    r.Width = Math.Max(1, r.Width + dx);
                    r.Height = Math.Max(1, r.Height + dy);
                }

                if (r != current.Sub.Rect)
                {
                    current.Sub.Rect = r;
                    dirtyRects.Add($"{atlasIdx}|{current.Name}");
                }
            }
            else
            {
                // one undo step per drag
                if (current.Sub != null)
                    PushRectEdit(new RectEdit(atlasIdx, current.Name, dragStartRect, current.Sub.Rect));
                drag = Drag.None;
                NotifyEdited();
            }
        }

        // ---- drawing ----
        foreach (var (tag, _, sub) in rects)
        {
            var isActive = tag == activeTag;
            var color = TagColor(tag);
            var min = ToScreen(new Vector2(sub.Rect.X, sub.Rect.Y));
            var max = ToScreen(new Vector2(sub.Rect.Right, sub.Rect.Bottom));
            drawList.AddRectFilled(min, max, ImGui.GetColorU32(color with { W = isActive ? 0.18f : 0.08f }));
            drawList.AddRect(min, max, ImGui.GetColorU32(color), 0, ImDrawFlags.None, isActive ? 2.5f : 1.5f);
            drawList.AddText(min + new Vector2(2, -14), ImGui.GetColorU32(color), tag);

            // the frame grid of the sprite on top of its rectangle
            if (xml != null) DrawFrameGrid(drawList, origin, sub, xml);

            if (isActive)
                drawList.AddRectFilled(max - new Vector2(5, 5), max + new Vector2(5, 5), ImGui.GetColorU32(color));
        }

        // ---- the sprite's origin, in every frame of the active rectangle (same color as the origin buttons) ----
        if (current.Sub == null || xml == null) return;

        var originColor = new Vector4(0.35f, 1f, 0.45f, 1f);
        {
            var fw = XmlInt(xml, "FrameWidth");
            var fh = XmlInt(xml, "FrameHeight");
            var ox = XmlInt(xml, "OriginX");
            var oy = XmlInt(xml, "OriginY");
            var columns = fw > 0 ? Math.Max(1, current.Sub.Width / fw) : 1;
            var rows = fh > 0 ? Math.Max(1, current.Sub.Height / fh) : 1;
            for (var row = 0; row < rows; row++)
            {
                for (var column = 0; column < columns; column++)
                {
                    var point = ToScreen(new Vector2(current.Sub.Rect.X + column * Math.Max(fw, 0) + ox,
                        current.Sub.Rect.Y + row * Math.Max(fh, 0) + oy));
                    var first = row == 0 && column == 0;
                    drawList.AddCircleFilled(point, first ? 5f : 3f, ImGui.GetColorU32(originColor));
                    drawList.AddCircle(point, first ? 5f : 3f, 0xFF000000);
                }
            }
        }

        // ---- three buttons on each side of the active rectangle ----
        //   rectangle color: X / Y of the sprite,  green: OriginX / OriginY (like the point),  blue: move the rectangle
        var amin = ToScreen(new Vector2(current.Sub.Rect.X, current.Sub.Rect.Y));
        var amax = ToScreen(new Vector2(current.Sub.Rect.Right, current.Sub.Rect.Bottom));
        var mid = (amin + amax) / 2;
        var size = ImGui.GetFrameHeight();
        var gap = 4f;
        var rectColor = TagColor(activeTag);
        var moveColor = new Vector4(0.2f, 0.45f, 1f, 1f);

        void Button(string id, ImGuiDir direction, Vector4 color, Vector2 at, string tip, Action click)
        {
            ImGui.SetCursorScreenPos(at);
            ImGui.PushStyleColor(ImGuiCol.Button, color * new Vector4(0.7f, 0.7f, 0.7f, 1f));
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, color);
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, color * new Vector4(1.2f, 1.2f, 1.2f, 1f));
            var pressed = ImGui.ArrowButton(id, direction);
            ImGui.PopStyleColor(3);
            if (ImGui.IsItemHovered()) ImGui.SetTooltip(tip);
            if (pressed) click();
        }

        // side: 0 top, 1 bottom, 2 left, 3 right
        void Side(int side)
        {
            var horizontal = side < 2;
            var axis = horizontal ? "Y" : "X";
            var sign = side is 0 or 2 ? -1 : 1;
            var direction = side switch { 0 => ImGuiDir.Up, 1 => ImGuiDir.Down, 2 => ImGuiDir.Left, _ => ImGuiDir.Right };
            var word = side switch { 0 => "up", 1 => "down", 2 => "left", _ => "right" };

            for (var i = 0; i < 3; i++)
            {
                Vector2 at;
                if (horizontal)
                {
                    at = new Vector2(mid.X - 1.5f * size + i * size, side == 0 ? amin.Y - gap - size : amax.Y + gap);
                }
                else
                {
                    at = new Vector2(side == 2 ? amin.X - gap - size : amax.X + gap, mid.Y - 1.5f * size + i * size);
                }

                var id = $"##side{side}_{i}";
                if (i == 0)
                    Button(id, direction, rectColor, at, $"{axis} {XmlInt(xml, axis)} ({sign:+0;-0})", () => ChangeInt(xml, axis, sign));
                else if (i == 1)
                    Button(id, direction, originColor, at, $"Origin{axis} {XmlInt(xml, "Origin" + axis)} ({sign:+0;-0})",
                        () => ChangeInt(xml, "Origin" + axis, sign));
                else
                    Button(id, direction, moveColor, at, $"Move the rectangle 1 pixel {word}",
                        () => MoveRect(current.Name, atlasIdx, horizontal ? 0 : sign, horizontal ? sign : 0));
            }
        }

        for (var side = 0; side < 4; side++)
            Side(side);
    }

    // ---- undo / redo of the rectangle edits (drag, resize and the move buttons): part of the shared history ----

    private sealed record RectEdit(int Atlas, string Name, Rectangle Before, Rectangle After);

    private static void PushRectEdit(RectEdit edit)
    {
        if (edit.Before == edit.After) return;

        EditHistory.Push("Move rectangle", () => SetRect(edit, edit.Before), () => SetRect(edit, edit.After));
    }

    // no refresh here: undo / redo refresh the editor once, the callers of an edit do it themselves
    private static void SetRect(RectEdit edit, Rectangle rect)
    {
        if (AtlasAt(edit.Atlas) is not { } atlas || !atlas.SubTextures.TryGetValue(edit.Name, out var sub)) return;

        sub.Rect = rect;
        dirtyRects.Add($"{edit.Atlas}|{edit.Name}");
        FramesWindow.ClearCache();
    }

    private static void MoveRect(string name, int atlasIdx, int dx, int dy)
    {
        if (!AtlasAt(atlasIdx)!.SubTextures.TryGetValue(name, out var sub)) return;

        var before = sub.Rect;
        var after = before;
        after.X += dx;
        after.Y += dy;
        var edit = new RectEdit(atlasIdx, name, before, after);
        SetRect(edit, after);
        PushRectEdit(edit);
        NotifyEdited();
    }

    private static void ChangeInt(XmlElement xml, string field, int delta)
    {
        var changedKey = $"{selSource}|{selId}";
        EditHistory.BeforeXmlEdit(xml, "Change " + field, () =>
        {
            dirtySprites.Add(changedKey);
            FramesWindow.ClearCache();
        });
        var element = xml[field] ?? xml.AppendChild(xml.OwnerDocument.CreateElement(field)) as XmlElement;
        element!.InnerText = (XmlInt(xml, field) + delta).ToString(CultureInfo.InvariantCulture);
        dirtySprites.Add($"{selSource}|{selId}");
        NotifyEdited();
    }

    // ---- problems + saving ----

    /// <summary>What would make the sprite data / atlas edits unusable; empty when they are fine to save.</summary>
    public static List<string> SpriteProblems()
    {
        var problems = new List<string>();

        foreach (var key in dirtyRects)
        {
            var bar = key.IndexOf('|');
            var atlas = AtlasAt(int.Parse(key[..bar]));
            var name = key[(bar + 1)..];
            if (atlas == null || !atlas.SubTextures.TryGetValue(name, out var sub)) continue;

            var texture = sub.Texture.Texture2D;
            if (sub.Width < 1 || sub.Height < 1)
                problems.Add($"{name}: the rectangle is empty.");
            else if (texture != null && (sub.X < 0 || sub.Y < 0 || sub.Rect.Right > texture.Width || sub.Rect.Bottom > texture.Height))
                problems.Add($"{name}: the rectangle is outside its texture ({texture.Width}x{texture.Height}).");
        }

        foreach (var key in dirtySprites)
        {
            var bar = key.IndexOf('|');
            var source = key[..bar];
            var id = key[(bar + 1)..];
            if (EntriesOf(source) is not { } entries || !entries.TryGetValue(id, out var xml)) continue;

            if (xml["Texture"] == null || ResolveName(AtlasOf(source), xml["Texture"]!.InnerText) == null)
                problems.Add($"{id}: its Texture is not in the atlas.");
            if (source != "CorpseSpriteData" && (XmlInt(xml, "FrameWidth") <= 0 || XmlInt(xml, "FrameHeight") <= 0))
                problems.Add($"{id}: FrameWidth / FrameHeight must be more than 0.");
        }

        return problems;
    }

    private static string Local(string name, string modName) =>
        modName.Length > 0 && name.StartsWith(modName + "/", StringComparison.Ordinal) ? name[(modName.Length + 1)..] : name;

    /// <returns>The files that were written.</returns>
    public static List<string> SaveSprites(ArcherData archer)
    {
        var source = ArcherDecorationRegistry.FindArcherSource(archer);
        if (source?.ModDirectory == null)
            throw new InvalidOperationException("The archer's mod is not a folder on disk: atlas and sprite data can't be saved.");

        var written = new List<string>();
        var content = Path.Combine(source.ModDirectory, "Content", "Atlas");

        // ---- atlas rectangles ----
        foreach (var group in dirtyRects.GroupBy(k => int.Parse(k[..k.IndexOf('|')])))
        {
            var path = Path.Combine(content, AtlasFiles[group.Key]);
            if (!File.Exists(path))
                throw new FileNotFoundException("The mod has no atlas file for these rectangles.", path);

            var doc = new XmlDocument { PreserveWhitespace = true };
            doc.Load(path);
            var root = doc["TextureAtlas"] ?? throw new InvalidDataException("No <TextureAtlas> in " + path);

            foreach (var key in group)
            {
                var name = key[(key.IndexOf('|') + 1)..];
                if (!AtlasAt(group.Key)!.SubTextures.TryGetValue(name, out var sub)) continue;

                var local = Local(name, source.ModName);
                var element = root.GetElementsByTagName("SubTexture").Cast<XmlElement>().FirstOrDefault(e => e.GetAttribute("name") == local);
                if (element == null)
                {
                    element = doc.CreateElement("SubTexture");
                    element.SetAttribute("name", local);
                    root.AppendChild(doc.CreateWhitespace("  "));
                    root.AppendChild(element);
                    root.AppendChild(doc.CreateWhitespace("\n"));
                }

                element.SetAttribute("x", sub.X.ToString(CultureInfo.InvariantCulture));
                element.SetAttribute("y", sub.Y.ToString(CultureInfo.InvariantCulture));
                element.SetAttribute("width", sub.Width.ToString(CultureInfo.InvariantCulture));
                element.SetAttribute("height", sub.Height.ToString(CultureInfo.InvariantCulture));
            }

            doc.Save(path);
            written.Add(path);
        }

        // ---- sprite data ----
        foreach (var group in dirtySprites.GroupBy(k => k[..k.IndexOf('|')]))
        {
            var path = Path.Combine(content, "SpriteData", SpriteFileOf(group.Key));
            var doc = new XmlDocument { PreserveWhitespace = true };
            if (File.Exists(path))
                doc.Load(path);
            else
                doc.LoadXml("<SpriteData>\n</SpriteData>");

            var root = doc.DocumentElement!;
            var usesAt = doc.InnerXml.Contains(">@", StringComparison.Ordinal);
            var entries = EntriesOf(group.Key)!;

            foreach (var key in group)
            {
                var id = key[(key.IndexOf('|') + 1)..];
                if (!entries.TryGetValue(id, out var live)) continue;

                var local = Local(id, source.ModName);
                var existing = root.ChildNodes.OfType<XmlElement>().FirstOrDefault(e => e.GetAttribute("id") == local);
                var copy = (XmlElement)doc.ImportNode(live, true);
                copy.SetAttribute("id", local);

                foreach (var tag in RectTags)
                {
                    if (copy[tag] is not { } element) continue;

                    var text = Local(element.InnerText.Trim().TrimStart('@'), source.ModName);
                    var wasAt = existing?[tag]?.InnerText.TrimStart().StartsWith('@') ?? usesAt;
                    element.InnerText = wasAt ? "@" + text : text;
                }

                if (existing != null)
                {
                    root.ReplaceChild(copy, existing);
                }
                else
                {
                    root.InsertBefore(doc.CreateWhitespace("  "), root.LastChild);
                    root.InsertBefore(copy, root.LastChild);
                    root.InsertBefore(doc.CreateWhitespace("\n"), root.LastChild);
                }
            }

            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            doc.Save(path);
            written.Add(path);
        }

        dirtyRects.Clear();
        dirtySprites.Clear();
        return written;
    }
}
