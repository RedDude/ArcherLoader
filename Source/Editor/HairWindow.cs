using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using ArcherEditorMod.Editor.ImGuiSupport;
using ArcherEditorMod.Source.Features.Hair;
using ImGuiNET;
using Microsoft.Xna.Framework;
using TowerFall;
using Vector2 = System.Numerics.Vector2;
using Vector4 = System.Numerics.Vector4;

namespace ArcherEditorMod.Editor;

/// <summary>
/// Live hair customization for the editor archer.
/// Hair 1 is the archer's own hair: its HairInfo from archerCustomData.xml, or the game's default hair when it has
/// none (which can be overridden). Every hair after that is created here, can be named, and lives in the editor
/// until you copy it out. Colors and offsets change instantly; structural settings (links, size, sprites, sine)
/// are pushed into the live hairs.
/// </summary>
public static class HairWindow
{
    public static bool Open;

    private static string newName = "";

    public static void Draw(ImGuiRenderer renderer, Player player, Action refreshEditor)
    {
        if (!Open) return;

        ImGui.SetNextWindowSize(new Vector2(640, 600), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowPos(new Vector2(900, 10), ImGuiCond.FirstUseEver);
        if (!ImGui.Begin("Hair", ref Open))
        {
            ImGui.End();
            return;
        }

        var data = player.ArcherData;
        var own = HairFeature.GetOwnHairs(data);
        var editor = HairFeature.GetEditorHairs(data);
        var structural = false;
        var refresh = false;

        // ---- the archer's own hair ----
        ImGui.SeparatorText("Archer hair");
        if (own != null && own.Count > 0)
        {
            var removeOwn = -1;
            for (var i = 0; i < own.Count; i++)
            {
                ImGui.PushID("own" + i);
                if (ImGui.CollapsingHeader($"{Label(own[i], i + 1)} (archer)###h", ImGuiTreeNodeFlags.DefaultOpen))
                {
                    structural |= DrawHair(renderer, data, "own" + i, own[i]);
                    if (ImGui.Button(i == 0 ? "Back to the game's default hair" : "Remove this hair"))
                        removeOwn = i;
                }
                ImGui.PopID();
            }

            if (removeOwn >= 0)
            {
                HairFeature.RemoveOwnHair(data, removeOwn);
                refresh = true;
            }
        }
        else if (data.Hair)
        {
            ImGui.TextWrapped("Default hair (from the game): this archer has no HairInfo, so the game's own hair is used.");
            if (ImGui.Button("Customize the default hair"))
            {
                HairFeature.OverrideDefaultHair(data);
                refresh = true;
            }
        }
        else
        {
            ImGui.TextWrapped("This archer has no hair of its own.");
        }

        // ---- hairs created in the editor ----
        ImGui.SeparatorText("Editor hairs");
        var removeEditor = -1;
        for (var i = 0; i < editor.Count; i++)
        {
            ImGui.PushID("editor" + i);
            if (ImGui.CollapsingHeader($"{Label(editor[i], (own?.Count ?? (data.Hair ? 1 : 0)) + i + 1)}###h", ImGuiTreeNodeFlags.DefaultOpen))
            {
                structural |= DrawHair(renderer, data, "editor" + i, editor[i]);
                if (ImGui.Button("Remove this hair"))
                    removeEditor = i;
            }
            ImGui.PopID();
        }

        ImGui.SetNextItemWidth(160);
        ImGui.InputTextWithHint("##newname", "name (optional)", ref newName, 48);
        ImGui.SameLine();
        if (ImGui.Button("Add hair"))
        {
            // new players pick the hair up when they are created, so restart the preview
            HairFeature.AddEditorHair(data, string.IsNullOrWhiteSpace(newName) ? null : newName.Trim());
            newName = "";
            refresh = true;
        }

        if (removeEditor >= 0)
        {
            HairFeature.RemoveEditorHair(data, removeEditor);
            refresh = true;
        }

        // ---- export ----
        ImGui.Separator();
        if (ImGui.Button("Copy XML (archer + editor hairs)"))
        {
            var all = HairFeature.GetHairs(data);
            ImGui.SetClipboardText(all == null ? "" : ToXml(all));
        }

        // one undo step per edit (a drag, a typed value, an added / removed hair)
        EditHistory.Observe(("hair", data), "Edit hair",
            new InfoState<HairInfo>(HairFeature.GetOwnHairs(data), HairFeature.GetEditorHairs(data) is { Count: > 0 } editorNow ? editorNow : null),
            state =>
            {
                var (restoredOwn, restoredEditor) = state.Fresh();
                HairFeature.SetLists(data, restoredOwn, restoredEditor);
                HairFeature.ReapplyAll();
            });

        ImGui.End();

        if (structural)
            HairFeature.ReapplyAll();

        if (refresh)
            refreshEditor();
    }

    private static string Label(HairInfo hair, int number) =>
        string.IsNullOrWhiteSpace(hair.Name) ? $"Hair {number}" : $"{hair.Name}";

    // returns true when a structural value changed (needs ReapplyAll)
    private static bool DrawHair(ImGuiRenderer renderer, ArcherData data, string id, HairInfo hair)
    {
        var structural = false;

        var name = hair.Name ?? "";
        ImGui.SetNextItemWidth(200);
        if (ImGui.InputTextWithHint("Name", "(optional)", ref name, 48))
            hair.Name = string.IsNullOrWhiteSpace(name) ? null : name;

        structural |= Text("HairSprite", ref hair.HairSprite);
        structural |= Text("HairEndSprite", ref hair.HairEndSprite);
        structural |= Int("Links", ref hair.Links, 1, 16);
        structural |= Int("Size", ref hair.Size, 0, 8);
        structural |= ImGui.DragFloat("LinksDist", ref hair.LinksDist, 0.05f, 0f, 8f);
        structural |= Int("SineValue", ref hair.SineValue, 1, 240);
        structural |= ImGui.Checkbox("VisibleWithHat", ref hair.VisibleWithHat);

        ImGui.SeparatorText("Placement");
        DrawPlacement(renderer, data, id, hair);
        Pos("Position", ref hair.Position);
        Pos("DuckingOffset", ref hair.DuckingOffset);
        Pos("WithHatOffset", ref hair.WithHatOffset);

        ImGui.SeparatorText("Color");
        Color("Color", ref hair.Color);
        Color("EndColor", ref hair.EndColor);
        Color("OutlineColor", ref hair.OutlineColor);
        ImGui.SliderFloat("Alpha", ref hair.Alpha, 0f, 1f);
        ImGui.Checkbox("Gradient", ref hair.Gradient);
        Int("GradientOffset", ref hair.GradientOffset, 0, 16);
        ImGui.Checkbox("Rainbow", ref hair.Rainbow);
        ImGui.Checkbox("Prismatic", ref hair.Prismatic);
        ImGui.Checkbox("PrismaticEnd", ref hair.PrismaticEnd);
        ImGui.DragFloat("PrismaticTime", ref hair.PrismaticTime, 0.05f, 0.1f, 10f);

        return structural;
    }

    // three poses like the frames window: standing (Position), ducking (DuckingOffset), with hat (WithHatOffset)
    private static void DrawPlacement(ImGuiRenderer renderer, ArcherData data, string id, HairInfo hair)
    {
        var windowRight = ImGui.GetWindowPos().X + ImGui.GetWindowWidth() - 16;

        PoseWidget.Draw(renderer, data, id + "stand", "Position", Pose.Stand, ref hair.Position, ctx => Overlay(ctx, hair));

        if (ImGui.GetItemRectMax().X + PoseWidget.CellWidth < windowRight) ImGui.SameLine();
        PoseWidget.Draw(renderer, data, id + "duck", "DuckingOffset", Pose.Duck, ref hair.DuckingOffset, ctx => Overlay(ctx, hair));

        if (ImGui.GetItemRectMax().X + PoseWidget.CellWidth < windowRight) ImGui.SameLine();
        PoseWidget.Draw(renderer, data, id + "hat", "WithHatOffset", Pose.Hat, ref hair.WithHatOffset, ctx => Overlay(ctx, hair));
    }

    // the hair links hanging from where the game puts its own hair, plus this hair's offsets for the pose
    private static void Overlay(PoseContext ctx, HairInfo hair)
    {
        var position = ctx.HeadHairBase + new Vector2(hair.Position.X, hair.Position.Y);
        if (ctx.Pose == Pose.Duck) position += new Vector2(hair.DuckingOffset.X, hair.DuckingOffset.Y);
        if (ctx.Pose == Pose.Hat) position += new Vector2(hair.WithHatOffset.X, hair.WithHatOffset.Y);

        var links = Math.Clamp(hair.Links, 1, 16);
        for (var i = 0; i < links; i++)
        {
            var name = i == links - 1 ? hair.HairEndSprite : hair.HairSprite;
            if (name == null || !TFGame.Atlas.Contains(name)) continue;

            var sub = TFGame.Atlas[name];
            if (!sub.Loaded) continue;

            var (tex, uv0, uv1) = ImGuiTextures.Region(ctx.Renderer, sub.Texture2D, sub.Rect);
            var size = new Vector2(sub.Width, sub.Height) * ctx.Scale;
            var center = ctx.ToScreen(position + new Vector2(0, hair.Size * i));
            var tint = i == links - 1 && hair.EndColor.A != 0 ? hair.EndColor : hair.Color;
            ctx.DrawList.AddImage(tex, center - size / 2, center + size / 2, uv0, uv1,
                ImGui.GetColorU32(new Vector4(tint.R, tint.G, tint.B, 255f * hair.Alpha) / 255f));
        }
    }

    private static bool Text(string label, ref string value)
    {
        var text = value;
        if (!ImGui.InputText(label, ref text, 128)) return false;
        value = text;
        return true;
    }

    private static bool Int(string label, ref int value, int min, int max) => ImGui.SliderInt(label, ref value, min, max);

    private static void Pos(string label, ref Microsoft.Xna.Framework.Vector2 value)
    {
        var v = new Vector2(value.X, value.Y);
        if (ImGui.DragFloat2(label, ref v, 0.25f))
            value = new Microsoft.Xna.Framework.Vector2(v.X, v.Y);
    }

    private static void Color(string label, ref Color value)
    {
        var v = new Vector4(value.R, value.G, value.B, value.A) / 255f;
        if (ImGui.ColorEdit4(label, ref v))
            value = new Color(v.X, v.Y, v.Z, v.W);
    }

    // ---- xml ----

    private static string Hex(Color color) => $"{color.R:X2}{color.G:X2}{color.B:X2}";

    private static string F(float value) => value.ToString(CultureInfo.InvariantCulture);

    internal static string ToXml(List<HairInfo> hairs)
    {
        var builder = new StringBuilder();
        var single = hairs.Count == 1;
        var indent = single ? "" : "  ";
        if (!single) builder.AppendLine("<HairInfos>");

        foreach (var hair in hairs)
        {
            builder.AppendLine($"{indent}<HairInfo>");
            void Line(string name, string value) => builder.AppendLine($"{indent}  <{name}>{value}</{name}>");

            if (!string.IsNullOrWhiteSpace(hair.Name)) Line("Name", hair.Name);
            Line("HairSprite", hair.HairSprite);
            Line("HairEndSprite", hair.HairEndSprite);
            Line("Links", hair.Links.ToString());
            Line("Size", hair.Size.ToString());
            Line("LinksDist", F(hair.LinksDist));
            Line("SineValue", hair.SineValue.ToString());
            Line("Color", Hex(hair.Color));
            if (hair.EndColor.A != 0) Line("EndColor", Hex(hair.EndColor));
            Line("OutlineColor", Hex(hair.OutlineColor));
            Line("Alpha", F(hair.Alpha));
            Line("Gradient", hair.Gradient.ToString().ToLowerInvariant());
            Line("GradientOffset", hair.GradientOffset.ToString());
            Line("Rainbow", hair.Rainbow.ToString().ToLowerInvariant());
            Line("Prismatic", hair.Prismatic.ToString().ToLowerInvariant());
            Line("PrismaticEnd", hair.PrismaticEnd.ToString().ToLowerInvariant());
            Line("PrismaticTime", F(hair.PrismaticTime));
            Line("VisibleWithHat", hair.VisibleWithHat.ToString().ToLowerInvariant());
            Line("X", F(hair.Position.X));
            Line("Y", F(hair.Position.Y));
            builder.AppendLine($"{indent}  <DuckingOffset x=\"{F(hair.DuckingOffset.X)}\" y=\"{F(hair.DuckingOffset.Y)}\"/>");
            builder.AppendLine($"{indent}  <WithHatOffset x=\"{F(hair.WithHatOffset.X)}\" y=\"{F(hair.WithHatOffset.Y)}\"/>");
            builder.AppendLine($"{indent}</HairInfo>");
        }

        if (!single) builder.AppendLine("</HairInfos>");
        return builder.ToString();
    }
}
