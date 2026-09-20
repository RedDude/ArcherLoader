using System;
using ArcherEditorMod.Editor.ImGuiSupport;
using ImGuiNET;
using TowerFall;

namespace ArcherEditorMod.Editor;

/// <summary>Pick the archer the editor previews: as a name list, rollcall portraits or win portraits.</summary>
public static class ArcherPickerWindow
{
    public static bool Open;

    private static readonly string[] viewModes = { "Names", "Rollcall", "Win" };
    private static readonly string[] types = { "Normal", "Alt", "Secret" };
    private static int? browseType;
    private const float PortraitScale = 2f;

    public static void Draw(ImGuiRenderer renderer, int playerIndex)
    {
        if (!Open) return;

        var settings = FortEntrance.Instance.Settings;

        ImGui.SetNextWindowSize(new System.Numerics.Vector2(420, 380), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowPos(new System.Numerics.Vector2(10, 320), ImGuiCond.FirstUseEver);
        if (!ImGui.Begin("Archers", ref Open))
        {
            ImGui.End();
            return;
        }

        var mode = Math.Clamp(settings.PickerViewMode, 0, viewModes.Length - 1);
        ImGui.SetNextItemWidth(110);
        if (ImGui.Combo("View", ref mode, viewModes, viewModes.Length))
        {
            settings.PickerViewMode = mode;
            ArcherEditorScreen.SaveSettings();
        }

        ImGui.SameLine();
        // browsing another type only changes the list; the selection itself is saved when an archer is picked
        browseType ??= Math.Clamp(settings.EditorArcherType, 0, types.Length - 1);
        var type = browseType.Value;
        for (var t = 0; t < types.Length; t++)
        {
            if (t > 0) ImGui.SameLine(0, 2);
            var active = t == type;
            if (active) ImGui.PushStyleColor(ImGuiCol.Button, new System.Numerics.Vector4(0.2f, 0.5f, 0.8f, 1));
            if (ImGui.Button(types[t])) browseType = t;
            if (active) ImGui.PopStyleColor();
        }

        ImGui.Text($"Player {playerIndex + 1}");
        ImGui.Separator();

        var archers = (ArcherData.ArcherTypes)type switch
        {
            ArcherData.ArcherTypes.Alt => ArcherData.AltArchers,
            ArcherData.ArcherTypes.Secret => ArcherData.SecretArchers,
            _ => ArcherData.Archers
        };

        var currentIndex = TFGame.Characters[playerIndex];
        var currentType = (int)TFGame.AltSelect[playerIndex];
        var perRow = Math.Max(1, (int)(ImGui.GetContentRegionAvail().X / 90f));
        var shown = 0;

        for (var i = 0; i < archers.Length; i++)
        {
            var archer = archers[i];
            if (archer == null) continue;

            var selected = i == currentIndex && type == currentType;
            var label = $"{archer.Name0} {archer.Name1}".Trim();
            var clicked = false;

            ImGui.PushID(i);
            if (mode == 0)
            {
                clicked = ImGui.Selectable(label, selected);
            }
            else
            {
                var portrait = mode == 1 ? archer.Portraits.Joined : archer.Portraits.Win;
                if (shown % perRow != 0) ImGui.SameLine();
                if (selected) ImGui.PushStyleColor(ImGuiCol.Button, new System.Numerics.Vector4(0.2f, 0.6f, 0.2f, 1));
                clicked = ImGuiTextures.SubtextureButton(renderer, "portrait", portrait, PortraitScale, out var drawn);
                if (!drawn)
                    clicked = ImGui.Button(label);
                if (selected) ImGui.PopStyleColor();
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip(label);
            }
            ImGui.PopID();

            shown++;
            if (clicked && !selected)
                ArcherEditorScreen.SelectArcher(i, type);
        }

        ImGui.End();
    }

    // ---- base archer picker (archer type tool): only archers that can still get an alt / a secret ----

    public static bool BaseOpen;
    private static int baseTargetType;
    private static ArcherData? baseExclude;
    private static Action<ArcherData>? baseCallback;

    /// <param name="targetType">1 = the archer becomes an alt, 2 = a secret</param>
    /// <param name="exclude">the archer being changed, it cannot be its own base</param>
    public static void OpenBasePicker(int targetType, ArcherData? exclude, Action<ArcherData> onPick)
    {
        baseTargetType = targetType;
        baseExclude = exclude;
        baseCallback = onPick;
        BaseOpen = true;
    }

    public static void DrawBasePicker(ImGuiRenderer renderer)
    {
        if (!BaseOpen) return;

        var settings = FortEntrance.Instance.Settings;
        ImGui.SetNextWindowSize(new System.Numerics.Vector2(420, 360), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowPos(new System.Numerics.Vector2(470, 200), ImGuiCond.FirstUseEver);
        if (!ImGui.Begin(baseTargetType == 1 ? "Pick the archer this becomes an alt of" : "Pick the archer this becomes a secret of", ref BaseOpen))
        {
            ImGui.End();
            return;
        }

        var mode = Math.Clamp(settings.PickerViewMode, 0, viewModes.Length - 1);
        ImGui.SetNextItemWidth(110);
        if (ImGui.Combo("View", ref mode, viewModes, viewModes.Length))
        {
            settings.PickerViewMode = mode;
            ArcherEditorScreen.SaveSettings();
        }

        ImGui.TextDisabled(baseTargetType == 1
            ? "Only archers that have no alt yet are listed."
            : "Only archers that have no secret yet are listed.");
        ImGui.Separator();

        var perRow = Math.Max(1, (int)(ImGui.GetContentRegionAvail().X / 90f));
        var shown = 0;
        var target = baseTargetType == 1 ? ArcherData.AltArchers : ArcherData.SecretArchers;

        for (var i = 0; i < ArcherData.Archers.Length; i++)
        {
            var archer = ArcherData.Archers[i];
            if (archer == null || ReferenceEquals(archer, baseExclude)) continue;
            if (i < target.Length && target[i] != null) continue; // already has one

            var label = $"{archer.Name0} {archer.Name1}".Trim();
            var clicked = false;

            ImGui.PushID(i);
            if (mode == 0)
            {
                clicked = ImGui.Selectable(label);
            }
            else
            {
                var portrait = mode == 1 ? archer.Portraits.Joined : archer.Portraits.Win;
                if (shown % perRow != 0) ImGui.SameLine();
                clicked = ImGuiTextures.SubtextureButton(renderer, "portrait", portrait, PortraitScale, out var drawn);
                if (!drawn)
                    clicked = ImGui.Button(label);
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip(label);
            }
            ImGui.PopID();

            shown++;
            if (clicked)
            {
                baseCallback?.Invoke(archer);
                BaseOpen = false;
            }
        }

        if (shown == 0)
            ImGui.TextDisabled("No archer is free for that.");

        ImGui.End();
    }
}
