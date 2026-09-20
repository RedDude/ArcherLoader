using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using ArcherEditorMod.Editor.ImGuiSupport;
using ArcherEditorMod.Source.Features.Particles;
using ImGuiNET;
using Microsoft.Xna.Framework;
using TowerFall;
using Vector2 = System.Numerics.Vector2;
using Vector4 = System.Numerics.Vector4;

namespace ArcherEditorMod.Editor;

/// <summary>
/// Particle emitters of the editor archer, same model as the hair window: the archer's own particles (from
/// archerCustomData.xml) come first, every one after that is created here and can be named. The window is driven
/// by the fields of ParticlesInfo, so new options show up without touching it. Edits are applied to the preview
/// when the mouse is released (the emitters are rebuilt), "Copy XML" exports everything.
/// </summary>
public static class ParticlesWindow
{
    public static bool Open;

    private static readonly FieldInfo[] fields = typeof(ParticlesInfo).GetFields(BindingFlags.Public | BindingFlags.Instance);
    private static readonly HashSet<string> angles = new() { nameof(ParticlesInfo.Direction), nameof(ParticlesInfo.DirectionRange) };

    private static string newName = "";
    private static bool dirty;

    public static void Draw(ImGuiRenderer renderer, Player player, Action refreshEditor)
    {
        if (!Open) return;

        ImGui.SetNextWindowSize(new Vector2(720, 600), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowPos(new Vector2(900, 90), ImGuiCond.FirstUseEver);
        if (!ImGui.Begin("Particles", ref Open))
        {
            ImGui.End();
            return;
        }

        var data = player.ArcherData;
        var own = ParticlesFeature.GetOwnParticles(data);
        var editor = ParticlesFeature.GetEditorParticles(data);
        var refresh = false;

        // ---- the archer's own particles ----
        ImGui.SeparatorText("Archer particles");
        if (own == null || own.Count == 0)
            ImGui.TextDisabled("This archer has no <Particle> entries.");

        var removeOwn = -1;
        for (var i = 0; own != null && i < own.Count; i++)
        {
            ImGui.PushID("own" + i);
            if (ImGui.CollapsingHeader($"{Label(own[i], i + 1)} (archer)###p", ImGuiTreeNodeFlags.DefaultOpen))
            {
                dirty |= DrawInfo(renderer, data, "own" + i, own[i]);
                if (ImGui.Button("Remove this emitter"))
                    removeOwn = i;
            }
            ImGui.PopID();
        }

        if (removeOwn >= 0)
        {
            ParticlesFeature.RemoveOwnParticles(data, removeOwn);
            refresh = true;
        }

        // ---- particles created in the editor ----
        ImGui.SeparatorText("Editor particles");
        var removeEditor = -1;
        for (var i = 0; i < editor.Count; i++)
        {
            ImGui.PushID("editor" + i);
            if (ImGui.CollapsingHeader($"{Label(editor[i], (own?.Count ?? 0) + i + 1)}###p", ImGuiTreeNodeFlags.DefaultOpen))
            {
                dirty |= DrawInfo(renderer, data, "editor" + i, editor[i]);
                if (ImGui.Button("Remove this emitter"))
                    removeEditor = i;
            }
            ImGui.PopID();
        }

        ImGui.SetNextItemWidth(160);
        ImGui.InputTextWithHint("##newname", "name (optional)", ref newName, 48);
        ImGui.SameLine();
        if (ImGui.Button("Add particles"))
        {
            ParticlesFeature.AddEditorParticles(data, string.IsNullOrWhiteSpace(newName) ? null : newName.Trim());
            newName = "";
            refresh = true;
        }

        if (removeEditor >= 0)
        {
            ParticlesFeature.RemoveEditorParticles(data, removeEditor);
            refresh = true;
        }

        ImGui.Separator();
        if (ImGui.Button("Copy XML (archer + editor particles)"))
            ImGui.SetClipboardText(ToXml(ParticlesFeature.GetAllParticles(data)));

        // emitters are rebuilt once the slider / field is released, not on every tick
        if (dirty && !ImGui.IsAnyItemActive())
        {
            dirty = false;
            refresh = true;
        }

        EditHistory.Observe(("particles", data), "Edit particles",
            new InfoState<ParticlesInfo>(ParticlesFeature.GetOwnParticles(data), ParticlesFeature.GetEditorParticles(data) is { Count: > 0 } editorNow ? editorNow : null),
            state =>
            {
                var (restoredOwn, restoredEditor) = state.Fresh();
                ParticlesFeature.SetLists(data, restoredOwn, restoredEditor);
                ArcherParticlesComponent.ClearCache();
            });

        ImGui.End();

        if (refresh)
        {
            ArcherParticlesComponent.ClearCache();
            refreshEditor();
        }
    }

    private static string Label(ParticlesInfo info, int number) =>
        string.IsNullOrWhiteSpace(info.Name) ? $"Particles {number}" : info.Name;

    private static bool DrawInfo(ImGuiRenderer renderer, ArcherData data, string id, ParticlesInfo info)
    {
        var edited = false;

        var name = info.Name ?? "";
        ImGui.SetNextItemWidth(200);
        if (ImGui.InputTextWithHint("Name", "(optional)", ref name, 48))
            info.Name = string.IsNullOrWhiteSpace(name) ? null : name;

        ImGui.SeparatorText("Positioning");
        DrawPlacement(renderer, data, id, info);
        ImGui.Separator();

        var conditionsOpen = false;
        foreach (var field in fields.Where(f => f.Name != nameof(ParticlesInfo.Name) && !IsCondition(f)))
            edited |= DrawField(info, field);

        if (ImGui.TreeNode("Conditions (when it emits)"))
        {
            conditionsOpen = true;
            foreach (var field in fields.Where(IsCondition))
                edited |= DrawField(info, field);
        }

        if (conditionsOpen)
            ImGui.TreePop();

        return edited;
    }

    // one pose per offset, like the frames window: standing, ducking, with hat, with crown
    private static void DrawPlacement(ImGuiRenderer renderer, ArcherData data, string id, ParticlesInfo info)
    {
        var windowRight = ImGui.GetWindowPos().X + ImGui.GetWindowWidth() - 16;

        PoseWidget.Draw(renderer, data, id + "stand", "Position", Pose.Stand, ref info.Position, ctx => Overlay(ctx, info));

        if (ImGui.GetItemRectMax().X + PoseWidget.CellWidth < windowRight) ImGui.SameLine();
        PoseWidget.Draw(renderer, data, id + "duck", "DuckingOffset", Pose.Duck, ref info.DuckingOffset, ctx => Overlay(ctx, info));

        if (ImGui.GetItemRectMax().X + PoseWidget.CellWidth < windowRight) ImGui.SameLine();
        PoseWidget.Draw(renderer, data, id + "hat", "HatOffset", Pose.Hat, ref info.HatOffset, ctx => Overlay(ctx, info));

        if (ImGui.GetItemRectMax().X + PoseWidget.CellWidth < windowRight) ImGui.SameLine();
        PoseWidget.Draw(renderer, data, id + "crown", "CrownOffset", Pose.Crown, ref info.CrownOffset, ctx => Overlay(ctx, info));
    }

    // where the emitter spawns for the pose: ArcherParticlesComponent adds Position twice (kept as is), plus the
    // pose offset; the box is PositionRange and the sprite is the particle texture
    private static void Overlay(PoseContext ctx, ParticlesInfo info)
    {
        var offset = new Vector2(info.Position.X * 2, info.Position.Y * 2);
        if (ctx.Pose == Pose.Duck) offset += new Vector2(info.DuckingOffset.X, info.DuckingOffset.Y);
        if (ctx.Pose == Pose.Hat) offset += new Vector2(info.HatOffset.X, info.HatOffset.Y);
        if (ctx.Pose == Pose.Crown) offset += new Vector2(info.CrownOffset.X, info.CrownOffset.Y);

        var center = ctx.ToScreen(offset);
        var range = new Vector2(info.PositionRange.X, info.PositionRange.Y) * ctx.Scale;
        var color = ImGui.GetColorU32(new Vector4(info.Color.R, info.Color.G, info.Color.B, 255f) / 255f);
        ctx.DrawList.AddRect(center - range, center + range, color);
        ctx.DrawList.AddCircleFilled(center, 3f, color);

        if (info.Source != null && TFGame.Atlas.Contains(info.Source))
        {
            var sub = TFGame.Atlas[info.Source];
            if (sub.Loaded)
            {
                var (tex, uv0, uv1) = ImGuiTextures.Region(ctx.Renderer, sub.Texture2D, sub.Rect);
                var size = new Vector2(sub.Width, sub.Height) * ctx.Scale * Math.Max(0.5f, info.Size);
                ctx.DrawList.AddImage(tex, center - size / 2, center + size / 2, uv0, uv1, color);
            }
        }
    }

    // Is* flags and the jump options say when the emitter is active
    private static bool IsCondition(FieldInfo field) =>
        (field.Name.StartsWith("Is") && field.FieldType == typeof(bool)) ||
        field.Name is nameof(ParticlesInfo.OnJump) or nameof(ParticlesInfo.ReplaceJump);

    private static bool DrawField(ParticlesInfo info, FieldInfo field)
    {
        var value = field.GetValue(info);
        var name = field.Name;

        switch (value)
        {
            case string text when name == nameof(ParticlesInfo.Source):
            {
                var valid = TFGame.Atlas.Contains(text);
                if (!valid) ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1f, 0.4f, 0.4f, 1));
                ImGui.SetNextItemWidth(220);
                var changed = ImGui.InputText(name, ref text, 128);
                if (!valid) ImGui.PopStyleColor();

                // only atlas textures are accepted: an unknown Source would crash the emitter
                if (changed && TFGame.Atlas.Contains(text))
                {
                    field.SetValue(info, text);
                    return true;
                }
                return false;
            }
            case bool flag:
                if (!ImGui.Checkbox(name, ref flag)) return false;
                field.SetValue(info, flag);
                return true;
            case int number:
                ImGui.SetNextItemWidth(120);
                if (!ImGui.DragInt(name, ref number)) return false;
                field.SetValue(info, number);
                return true;
            case float number when angles.Contains(name):
                ImGui.SetNextItemWidth(160);
                if (!ImGui.SliderAngle(name, ref number, -360f, 360f)) return false;
                field.SetValue(info, number);
                return true;
            case float number:
                ImGui.SetNextItemWidth(120);
                if (!ImGui.DragFloat(name, ref number, 0.01f)) return false;
                field.SetValue(info, number);
                return true;
            case Color color:
            {
                var v = new Vector4(color.R, color.G, color.B, color.A) / 255f;
                if (!ImGui.ColorEdit4(name, ref v)) return false;
                field.SetValue(info, new Color(v.X, v.Y, v.Z, v.W));
                return true;
            }
            case Microsoft.Xna.Framework.Vector2 vector:
            {
                var v = new Vector2(vector.X, vector.Y);
                ImGui.SetNextItemWidth(160);
                if (!ImGui.DragFloat2(name, ref v, 0.1f)) return false;
                field.SetValue(info, new Microsoft.Xna.Framework.Vector2(v.X, v.Y));
                return true;
            }
        }

        return false;
    }

    // ---- xml ----

    private static string F(float value) => value.ToString(CultureInfo.InvariantCulture);

    internal static string ToXml(List<ParticlesInfo> infos)
    {
        var builder = new StringBuilder();
        var single = infos.Count == 1;
        var indent = single ? "" : "  ";
        if (infos.Count == 0) return "";
        if (!single) builder.AppendLine("<Particles>");

        foreach (var info in infos)
        {
            builder.AppendLine($"{indent}<Particle>");
            foreach (var field in fields)
            {
                var value = field.GetValue(info);
                var line = value switch
                {
                    null => null,
                    string text when text.Length == 0 => null,
                    bool flag => flag.ToString().ToLowerInvariant(),
                    float number => F(number),
                    Color color => $"{color.R:X2}{color.G:X2}{color.B:X2}",
                    Microsoft.Xna.Framework.Vector2 vector => null,
                    _ => value.ToString()
                };

                if (value is Microsoft.Xna.Framework.Vector2 v)
                    builder.AppendLine($"{indent}  <{field.Name} x=\"{F(v.X)}\" y=\"{F(v.Y)}\"/>");
                else if (line != null)
                    builder.AppendLine($"{indent}  <{field.Name}>{line}</{field.Name}>");
            }
            builder.AppendLine($"{indent}</Particle>");
        }

        if (!single) builder.AppendLine("</Particles>");
        return builder.ToString();
    }
}
