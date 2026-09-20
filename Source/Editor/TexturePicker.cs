using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using ArcherEditorMod.Editor.ImGuiSupport;
using ImGuiNET;
using Monocle;
using TowerFall;

namespace ArcherEditorMod.Editor;

/// <summary>A button that opens a filterable list of atlas textures, with a preview on hover.</summary>
public static class TexturePicker
{
    private const int MaxListed = 200;

    private static readonly Dictionary<string, string> filters = new();
    private static List<string> sortedKeys = new();
    private static int sortedCount = -1;

    /// <returns>True when the choice changed; name and texture are then updated (both null when cleared).</returns>
    public static bool Draw(ImGuiRenderer renderer, string id, string label, ref string? name, ref Subtexture? texture)
    {
        var changed = false;
        ImGui.PushID(id);

        ImGui.AlignTextToFramePadding();
        ImGui.Text(label);
        ImGui.SameLine(170);

        if (ImGui.Button((name ?? "(none)") + "###pick", new Vector2(240, 0)))
            ImGui.OpenPopup("pick_popup");

        if (texture is { Loaded: true })
        {
            ImGui.SameLine();
            var (tex, uv0, uv1) = ImGuiTextures.Region(renderer, texture.Texture2D, texture.Rect);
            var scale = System.Math.Min(1f, 32f / System.Math.Max(texture.Width, texture.Height));
            ImGui.Image(tex, new Vector2(texture.Width, texture.Height) * System.Math.Max(scale, 0.25f), uv0, uv1);
        }

        if (ImGui.BeginPopup("pick_popup"))
        {
            filters.TryGetValue(id, out var filter);
            filter ??= "";
            ImGui.SetNextItemWidth(240);
            ImGui.InputText("Filter", ref filter, 64);
            filters[id] = filter;

            ImGui.SameLine();
            if (ImGui.Button("Clear"))
            {
                name = null;
                texture = null;
                changed = true;
                ImGui.CloseCurrentPopup();
            }

            var atlas = TFGame.Atlas;
            if (sortedCount != atlas.SubTextures.Count)
            {
                sortedKeys = atlas.SubTextures.Keys.OrderBy(k => k).ToList();
                sortedCount = atlas.SubTextures.Count;
            }

            if (ImGui.BeginChild("list", new Vector2(340, 240), ImGuiChildFlags.Borders))
            {
                var trimmed = filter.Trim();
                var listed = 0;
                foreach (var key in sortedKeys)
                {
                    if (trimmed.Length > 0 && !key.Contains(trimmed, System.StringComparison.OrdinalIgnoreCase)) continue;
                    if (listed++ >= MaxListed)
                    {
                        ImGui.TextDisabled($"... narrow the filter ({MaxListed} shown)");
                        break;
                    }

                    var sub = atlas[key];
                    if (ImGui.Selectable(key, key == name))
                    {
                        name = key;
                        texture = sub;
                        changed = true;
                        ImGui.CloseCurrentPopup();
                    }

                    if (ImGui.IsItemHovered() && sub.Loaded)
                    {
                        ImGui.BeginTooltip();
                        var (tex, uv0, uv1) = ImGuiTextures.Region(renderer, sub.Texture2D, sub.Rect);
                        ImGui.Image(tex, new Vector2(sub.Width, sub.Height) * 2f, uv0, uv1);
                        ImGui.EndTooltip();
                    }
                }
            }
            ImGui.EndChild();

            ImGui.EndPopup();
        }

        ImGui.PopID();
        return changed;
    }
}
