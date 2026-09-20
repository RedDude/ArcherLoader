using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Xml;
using ArcherEditorMod.Editor.ImGuiSupport;
using ArcherEditorMod.Source.Features;
using ArcherEditorMod.Source.Features.Ghost;
using ArcherEditorMod.Source.Features.Wings;
using ImGuiNET;
using Microsoft.Xna.Framework;
using Monocle;
using TowerFall;
using Color = Microsoft.Xna.Framework.Color;
using Vector2 = System.Numerics.Vector2;
using Vector3 = System.Numerics.Vector3;
using Vector4 = System.Numerics.Vector4;

namespace ArcherEditorMod.Editor;

/// <summary>
/// Create / edit the custom wings and ghost of the editor archer. Each tab shows whether the archer already has
/// one, lets you create it, pick the art from the atlas (or from the textures the sprite data uses) and tint it.
/// Changes apply to the preview at once; "Save to file" writes the archer's entry in archerCustomData.xml.
/// </summary>
public static class WingsGhostWindow
{
    public static bool Open;

    private sealed class Kind
    {
        public string Title = "", XmlName = "", SpriteId = "";
        public Func<ArcherData, (bool Found, Subtexture? Texture, Color? Color)> Get = null!;
        public Action<ArcherData, Subtexture?, Color?> Set = null!;
        public Action<ArcherData> Remove = null!;

        // per-tab UI state
        public string Filter = "", Manual = "", Status = "";
        public readonly Dictionary<ArcherData, string> ChosenNames = new();
    }

    private static readonly Kind wings = new()
    {
        Title = "Wings", XmlName = "Wings", SpriteId = "Wings",
        Get = a => (WingsFeature.TryGet(a, out var t, out var c), t, c),
        Set = WingsFeature.Set,
        Remove = WingsFeature.Remove
    };

    private static readonly Kind ghost = new()
    {
        Title = "Ghost", XmlName = "Ghost", SpriteId = "PlayerGhost",
        Get = a => (GhostFeature.TryGet(a, out var t, out var c), t, c),
        Set = GhostFeature.Set,
        Remove = GhostFeature.Remove
    };

    private const int MaxListed = 300;

    /// <summary>The atlas name of the archer's custom wings (or ghost) texture; null when it uses the default art.</summary>
    internal static string? CurrentTextureName(ArcherData data, bool wingsKind)
    {
        var kind = wingsKind ? wings : ghost;
        var (found, texture, _) = kind.Get(data);
        if (!found || texture == null) return null;

        var name = TextureName(kind, data, texture);
        if (string.IsNullOrEmpty(name) || name == "(unknown name)")
            throw new InvalidOperationException($"Can't tell the atlas name of the custom {kind.Title.ToLowerInvariant()} texture.");
        return name;
    }

    /// <param name="refreshEditor">Rebuilds the preview so new players / ghosts pick the change up.</param>
    /// <param name="enableWings">Turns the editor's wings toggle on, so the wings are visible.</param>
    public static void Draw(ImGuiRenderer renderer, Player player, Action refreshEditor, Action enableWings)
    {
        if (!Open) return;

        ImGui.SetNextWindowSize(new Vector2(440, 560), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowPos(new Vector2(900, 60), ImGuiCond.FirstUseEver);
        if (!ImGui.Begin("Wings & Ghost", ref Open))
        {
            ImGui.End();
            return;
        }

        var data = player.ArcherData;
        if (ImGui.BeginTabBar("wingsghost_tabs"))
        {
            if (ImGui.BeginTabItem("Wings"))
            {
                DrawKind(renderer, wings, data, refreshEditor, enableWings);
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Ghost"))
            {
                DrawKind(renderer, ghost, data, refreshEditor, null);
                ImGui.EndTabItem();
            }

            ImGui.EndTabBar();
        }

        ImGui.End();
    }

    private static void DrawKind(ImGuiRenderer renderer, Kind kind, ArcherData data, Action refresh, Action? afterChange)
    {
        var (found, texture, color) = kind.Get(data);

        void Apply(Subtexture? newTexture, Color? newColor)
        {
            kind.Set(data, newTexture, newColor);
            afterChange?.Invoke();
            refresh();
        }

        if (!found)
        {
            ImGui.TextWrapped($"{kind.Title}: this archer has no custom {kind.Title.ToLowerInvariant()}, the game default is used.");
            if (ImGui.Button($"Create custom {kind.Title.ToLowerInvariant()}"))
                Apply(null, Color.White);
            return;
        }

        ImGui.TextColored(new Vector4(0.5f, 1f, 0.5f, 1), $"Custom {kind.Title.ToLowerInvariant()} defined");

        // ---- current art ----
        var name = TextureName(kind, data, texture);
        ImGui.Text("Texture: " + (texture == null ? "(default art)" : name));
        if (texture != null)
        {
            DrawPreview(renderer, texture, 3f);
            CheckSize(kind, texture);
            if (ImGui.Button("Use default art"))
                Apply(null, color);
        }

        // ---- tint ----
        var useColor = color.HasValue;
        if (ImGui.Checkbox("Tint", ref useColor))
            Apply(texture, useColor ? Color.White : null);
        if (color.HasValue)
        {
            var c = new Vector4(color.Value.R, color.Value.G, color.Value.B, 255f) / 255f;
            var rgb = new Vector3(c.X, c.Y, c.Z);
            if (ImGui.ColorEdit3("Color", ref rgb))
                Apply(texture, new Color(rgb.X, rgb.Y, rgb.Z));
        }

        // ---- art picker: only sprite data entries with every animation the game plays for this sprite ----
        ImGui.SeparatorText("Pick art");
        var required = RequiredAnimations(kind);
        ImGui.TextDisabled($"Sprite data with the animations: {string.Join(", ", required.OrderBy(a => a))}");
        ImGui.SetNextItemWidth(220);
        ImGui.InputText("Filter", ref kind.Filter, 64);

        if (ImGui.BeginChild("art_list", new Vector2(0, 200), ImGuiChildFlags.Borders))
        {
            var listed = 0;
            foreach (var (label, atlasName) in Candidates(kind))
            {
                if (listed++ >= MaxListed)
                {
                    ImGui.TextDisabled($"... narrow the filter ({MaxListed} shown)");
                    break;
                }

                var subtexture = ResolveTexture(data, atlasName);
                if (ImGui.Selectable(label, atlasName == name) && subtexture != null)
                {
                    kind.ChosenNames[data] = atlasName;
                    Apply(subtexture, color);
                }

                if (subtexture != null && ImGui.IsItemHovered())
                {
                    ImGui.BeginTooltip();
                    DrawPreview(renderer, subtexture, 3f);
                    ImGui.EndTooltip();
                }
            }
        }
        ImGui.EndChild();

        // ---- actions ----
        ImGui.Separator();
        if (ImGui.Button("Remove"))
        {
            kind.Remove(data);
            kind.ChosenNames.Remove(data);
            afterChange?.Invoke();
            refresh();
        }

        ImGui.SameLine();
        if (ImGui.Button("Copy XML"))
            ImGui.SetClipboardText(ToXml(kind, texture == null ? null : name, color));

        var decoration = ArcherDecorationRegistry.Decorations.FirstOrDefault(d => d.ArcherData == data);
        ImGui.SameLine();
        ImGui.BeginDisabled(decoration?.EditablePath == null);
        if (ImGui.Button("Save to file"))
        {
            try
            {
                SaveToFile(decoration!, kind, texture == null ? null : name, color);
                kind.Status = "Saved " + decoration!.EditablePath;
                EditorConsole.Info(kind.Status);
            }
            catch (Exception e)
            {
                kind.Status = "Save failed: " + e.Message;
                EditorConsole.Error(kind.Status);
            }
        }
        ImGui.EndDisabled();
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip(decoration?.EditablePath ?? "This archer has no editable archerCustomData.xml entry: use Copy XML");
        if (kind.Status.Length > 0)
            ImGui.TextWrapped(kind.Status);
    }

    // ---- art helpers ----

    private static IEnumerable<(string Label, string AtlasName)> Candidates(Kind kind)
    {
        var filter = kind.Filter.Trim();
        bool Matches(string text) => filter.Length == 0 || text.Contains(filter, StringComparison.OrdinalIgnoreCase);

        // only sprite data that can play everything the game asks of the original sprite
        var required = RequiredAnimations(kind);
        foreach (var (id, xml) in Monocle.SpriteDataExt.GetSprites(TFGame.SpriteData).OrderBy(p => p.Key))
        {
            var texture = xml["Texture"]?.InnerText.Trim();
            if (string.IsNullOrEmpty(texture) || !(Matches(id) || Matches(texture))) continue;
            if (!AnimationsOf(xml).IsSupersetOf(required)) continue;

            yield return ($"{id}  ->  {texture}", texture);
        }
    }

    // the animations of the game's own sprite (Wings, PlayerGhost): what it plays, so what a replacement must have
    private static HashSet<string> RequiredAnimations(Kind kind) =>
        TFGame.SpriteData.Contains(kind.SpriteId) ? AnimationsOf(TFGame.SpriteData.GetXML(kind.SpriteId)) : new HashSet<string>();

    private static HashSet<string> AnimationsOf(XmlElement xml)
    {
        var set = new HashSet<string>();
        if (xml["Animations"] is { } animations)
        {
            foreach (XmlElement anim in animations.GetElementsByTagName("Anim"))
                set.Add(anim.GetAttribute("id"));
        }

        return set;
    }

    // mod-registered textures aren't in the atlas dictionary until merged, so also ask the archer's mod registry
    private static Subtexture? ResolveTexture(ArcherData data, string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;

        var decoration = ArcherDecorationRegistry.Decorations.FirstOrDefault(d => d.ArcherData == data);
        var found = decoration?.FindTexture(name);
        if (found != null) return found;

        return TFGame.Atlas.Contains(name) ? TFGame.Atlas[name] : null;
    }

    private static string TextureName(Kind kind, ArcherData data, Subtexture? texture)
    {
        if (texture == null) return "";
        if (kind.ChosenNames.TryGetValue(data, out var chosen)) return chosen;

        var byReference = TFGame.Atlas.SubTextures.FirstOrDefault(p => ReferenceEquals(p.Value, texture)).Key;
        if (byReference != null) return byReference;

        // loaded from the archer's own xml: read the name back from there
        var decoration = ArcherDecorationRegistry.Decorations.FirstOrDefault(d => d.ArcherData == data);
        var element = decoration?.Xml[kind.XmlName];
        var text = element == null ? null : element.FirstChild is XmlText ? element.InnerText.Trim() : element["Texture"]?.InnerText.Trim();
        return string.IsNullOrEmpty(text) ? "(unknown name)" : text;
    }

    private static void DrawPreview(ImGuiRenderer renderer, Subtexture texture, float scale)
    {
        if (!texture.Loaded) return;
        var (id, uv0, uv1) = ImGuiTextures.Region(renderer, texture.Texture2D, texture.Rect);
        ImGui.Image(id, new Vector2(texture.Width, texture.Height) * scale, uv0, uv1);
    }

    // the swapped art replaces the sprite's sheet, so its size has to match the original for frames to line up
    private static void CheckSize(Kind kind, Subtexture texture)
    {
        if (!TFGame.SpriteData.Contains(kind.SpriteId)) return;

        var original = TFGame.SpriteData.GetSpriteString(kind.SpriteId).ClipRect;
        ImGui.TextDisabled($"{texture.Width}x{texture.Height}  (original sheet {original.Width}x{original.Height})");
        if (original.Width != texture.Width || original.Height != texture.Height)
            ImGui.TextColored(new Vector4(1f, 0.85f, 0.3f, 1), "Size differs from the original sheet: frames will not line up.");
    }

    // ---- xml ----

    private static string Hex(Color color) => $"{color.R:X2}{color.G:X2}{color.B:X2}";

    private static string ToXml(Kind kind, string? texture, Color? color)
    {
        var lines = new List<string> { $"<{kind.XmlName}>" };
        if (texture != null) lines.Add($"  <Texture>{texture}</Texture>");
        if (color.HasValue) lines.Add($"  <Color>{Hex(color.Value)}</Color>");
        lines.Add($"</{kind.XmlName}>");
        return string.Join("\n", lines);
    }

    // rewrites this archer's <Wings>/<Ghost> child inside its archerCustomData.xml entry
    private static void SaveToFile(ArcherDecoration decoration, Kind kind, string? texture, Color? color)
    {
        var path = decoration.EditablePath!;
        var id = decoration.Xml.GetAttribute("id");

        var doc = new XmlDocument { PreserveWhitespace = true };
        doc.Load(path);

        var entry = doc.SelectSingleNode($"//*[@id='{id}' and name()='{decoration.Xml.Name}']") as XmlElement
            ?? throw new Exception($"'{id}' not found in {path}");

        var existing = entry[kind.XmlName];
        var replacement = doc.CreateElement(kind.XmlName);
        if (texture != null) replacement.AppendChild(doc.CreateElement("Texture")).InnerText = texture;
        if (color.HasValue) replacement.AppendChild(doc.CreateElement("Color")).InnerText = Hex(color.Value);

        if (existing != null)
        {
            entry.ReplaceChild(replacement, existing);
        }
        else
        {
            // keep the file tidy: reuse the indentation of the entry's last child
            var indent = entry.LastChild?.PreviousSibling as XmlWhitespace;
            if (indent != null) entry.InsertAfter(doc.CreateWhitespace(indent.Value), entry.LastChild);
            entry.InsertAfter(replacement, entry.LastChild);
        }

        doc.Save(path);
    }
}
