using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Xml;
using ArcherEditorMod.Editor.ImGuiSupport;
using ArcherEditorMod.Source.Features;
using ImGuiNET;
using Monocle;
using TowerFall;

namespace ArcherEditorMod.Editor;

/// <summary>Inspector for the editor's archer: its runtime data, the xml it came from, and what ArcherEditor loaded.</summary>
public static class InspectorWindow
{
    public static bool Open;

    public static void Draw(ImGuiRenderer renderer, Player player)
    {
        // "Show in the data inspector" asks for the Data tab of this window
        var selectData = DataInspectorWindow.ShowInInspector;
        if (selectData)
        {
            DataInspectorWindow.ShowInInspector = false;
            Open = true;
        }

        if (!Open) return;

        ImGui.SetNextWindowSize(new System.Numerics.Vector2(460, 480), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowPos(new System.Numerics.Vector2(450, 10), ImGuiCond.FirstUseEver);
        if (!ImGui.Begin("Inspector", ref Open))
        {
            ImGui.End();
            return;
        }

        var data = player.ArcherData;
        if (ImGui.BeginTabBar("inspector_tabs"))
        {
            if (ImGui.BeginTabItem("Archer"))
            {
                DrawObject("ArcherData", data, 0);
                ImGui.EndTabItem();
            }

            // the game's xml data (sprite data, archerCustomData...): same tool as the Archer / XML tabs, any entry
            var dataTabOpen = true;
            if (ImGui.BeginTabItem("Data", ref dataTabOpen, selectData ? ImGuiTabItemFlags.SetSelected : ImGuiTabItemFlags.None))
            {
                DataInspectorWindow.DrawContent(renderer, data);
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("XML"))
            {
                DrawXmlTab(data);
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Loaded"))
            {
                DrawLoadedTab(data);
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Validate"))
            {
                DrawValidateTab(data);
                ImGui.EndTabItem();
            }

            ImGui.EndTabBar();
        }

        ImGui.End();
    }

    // ---- Archer tab: reflection dump; only TowerFall structs are expanded (no class cycles) ----

    private static void DrawObject(string name, object? value, int depth)
    {
        if (value == null)
        {
            ImGui.LabelText(name, "null");
            return;
        }

        var type = value.GetType();
        var expandable = depth == 0 || (type.IsValueType && !type.IsPrimitive && !type.IsEnum && type.Namespace == "TowerFall");
        if (!expandable || value is string)
        {
            ImGui.LabelText(name, Format(value));
            return;
        }

        var open = depth == 0
            ? ImGui.TreeNodeEx(name, ImGuiTreeNodeFlags.DefaultOpen)
            : ImGui.TreeNode(name);
        if (!open) return;

        foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
            DrawObject(field.Name, field.GetValue(value), depth + 1);

        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                     .Where(p => p.CanRead && p.GetIndexParameters().Length == 0))
        {
            object? propertyValue;
            try { propertyValue = property.GetValue(value); }
            catch { propertyValue = "<error>"; }
            DrawObject(property.Name, propertyValue, depth + 1);
        }

        ImGui.TreePop();
    }

    private static string Format(object value) => value switch
    {
        Subtexture s => $"Subtexture {s.Width}x{s.Height} @ {s.X},{s.Y}",
        Array a => $"{a.GetType().GetElementType()?.Name}[{a.Length}]",
        _ => value.ToString() ?? ""
    };

    // ---- XML tab ----

    private static void DrawXmlTab(ArcherData data)
    {
        var decorations = ArcherDecorationRegistry.Decorations.Where(d => d.ArcherData == data).ToList();
        foreach (var decoration in decorations)
        {
            if (ImGui.CollapsingHeader($"archerCustomData.xml  ({decoration.ModContent.Metadata.Name})", ImGuiTreeNodeFlags.DefaultOpen))
            {
                ImGui.PushID(decoration.GetHashCode());
                DrawXml(decoration.Xml);
                ImGui.PopID();
            }
        }

        if (decorations.Count == 0)
            ImGui.TextDisabled("No archerCustomData.xml entry decorates this archer.");

        foreach (var id in new[] { data.Sprites.Body, data.Sprites.HeadNormal, data.Sprites.HeadNoHat, data.Sprites.HeadCrown, data.Sprites.Bow }
                     .Where(id => !string.IsNullOrEmpty(id)).Distinct())
        {
            if (!TFGame.SpriteData.Contains(id)) continue;
            if (ImGui.CollapsingHeader($"spriteData: {id}"))
            {
                ImGui.PushID(id);
                DrawXml(TFGame.SpriteData.GetXML(id));
                ImGui.PopID();
            }
        }
    }

    internal static void DrawXml(XmlElement element, bool editable = false, Action? beforeEdit = null)
    {
        var children = element.ChildNodes.OfType<XmlElement>().ToList();

        if (editable)
        {
            DrawXmlEditable(element, children, beforeEdit);
            return;
        }

        var attributes = string.Join(" ", element.Attributes.Cast<XmlAttribute>().Select(a => $"{a.Name}=\"{a.Value}\""));

        if (children.Count == 0)
        {
            var text = element.InnerText.Trim();
            ImGui.TextWrapped(text.Length > 0 ? $"{element.Name}: {text}  {attributes}" : $"{element.Name}  {attributes}");
            return;
        }

        if (!ImGui.TreeNodeEx($"{element.Name}  {attributes}##{element.GetHashCode()}", ImGuiTreeNodeFlags.DefaultOpen))
            return;

        foreach (var child in children)
            DrawXml(child);
        ImGui.TreePop();
    }

    // values and attributes as text fields; edits go straight into the in-memory xml
    private static void DrawXmlEditable(XmlElement element, List<XmlElement> children, Action? beforeEdit)
    {
        ImGui.PushID(element.GetHashCode());

        var open = true;
        if (children.Count > 0)
            open = ImGui.TreeNodeEx(element.Name, ImGuiTreeNodeFlags.DefaultOpen);
        else
            ImGui.Text(element.Name);

        if (open)
        {
            foreach (XmlAttribute attribute in element.Attributes)
            {
                var value = attribute.Value;
                ImGui.SetNextItemWidth(240);
                if (ImGui.InputText("@" + attribute.Name, ref value, 4096))
                {
                    beforeEdit?.Invoke();
                    attribute.Value = value;
                }
            }

            if (children.Count == 0 && (element.Attributes.Count == 0 || element.InnerText.Trim().Length > 0))
            {
                var text = element.InnerText;
                ImGui.SetNextItemWidth(240);
                if (ImGui.InputText("value", ref text, 8192))
                {
                    beforeEdit?.Invoke();
                    element.InnerText = text;
                }
            }

            foreach (var child in children)
                DrawXmlEditable(child, child.ChildNodes.OfType<XmlElement>().ToList(), beforeEdit);

            if (children.Count > 0)
                ImGui.TreePop();
        }

        ImGui.PopID();
    }

    // ---- Validation (also runs on every editor refresh / hot reload) ----

    private static void DrawValidateTab(ArcherData data)
    {
        if (ImGui.Button("Validate now"))
            ArcherEditorScreen.RunValidation(data);
        ImGui.SameLine();
        ImGui.Text(ArcherRuntimeValidator.Summary(ArcherEditorScreen.Validation.ToList()));
        ImGui.Separator();

        if (ArcherEditorScreen.Validation.Count == 0)
            ImGui.TextColored(new System.Numerics.Vector4(0.5f, 1f, 0.5f, 1), "No problems found.");

        foreach (var message in ArcherEditorScreen.Validation)
        {
            var isError = message.type == ValidatorMessageType.ERROR;
            ImGui.PushStyleColor(ImGuiCol.Text, isError
                ? new System.Numerics.Vector4(1f, 0.4f, 0.4f, 1)
                : new System.Numerics.Vector4(1f, 0.85f, 0.3f, 1));
            ImGui.TextWrapped((isError ? "ERROR  " : "WARN   ") + message.message);
            ImGui.PopStyleColor();
        }
    }

    // ---- Loaded custom data ----

    private static void DrawLoadedTab(ArcherData current)
    {
        var decorations = ArcherDecorationRegistry.Decorations;
        ImGui.Text($"{decorations.Count} archerCustomData entries loaded");
        ImGui.Text($"Archers: {ArcherData.Archers.Length}  Alt: {ArcherData.AltArchers.Count(a => a != null)}  Secret: {ArcherData.SecretArchers.Count(a => a != null)}");
        ImGui.Separator();

        foreach (var decoration in decorations)
        {
            var isCurrent = decoration.ArcherData == current;
            ImGui.PushID(decoration.GetHashCode());
            if (isCurrent) ImGui.PushStyleColor(ImGuiCol.Text, new System.Numerics.Vector4(0.5f, 1f, 0.5f, 1));
            var open = ImGui.TreeNodeEx($"{decoration.TargetName}  [{decoration.ArcherType}]", isCurrent ? ImGuiTreeNodeFlags.DefaultOpen : ImGuiTreeNodeFlags.None);
            if (isCurrent) ImGui.PopStyleColor();
            if (open)
            {
                ImGui.Text($"Declared by: {decoration.ModContent.Metadata.Name}");
                ImGui.TextWrapped($"Resource: {decoration.Resource.Path}");
                ImGui.TextWrapped($"On disk: {decoration.EditablePath ?? "(zipped / not editable)"}");
                ImGui.TreePop();
            }
            ImGui.PopID();
        }
    }
}
