using System;
using System.Numerics;
using ImGuiNET;
using TowerFall;

namespace ArcherEditorMod.Editor;

/// <summary>The archer's own data (names, colors, gender, mod meta) in its own window; the body is the Archer Tools "Data" tab it used to be.</summary>
public static class ArcherDataWindow
{
    public static bool Open;

    public static void Draw(Player player, Action refreshEditor)
    {
        if (!Open) return;

        ImGui.SetNextWindowSize(new Vector2(520, 460), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowPos(new Vector2(300, 120), ImGuiCond.FirstUseEver);
        if (!ImGui.Begin("Archer Data", ref Open))
        {
            ImGui.End();
            return;
        }

        try
        {
            ToolsWindow.DrawDataTab(player.ArcherData, refreshEditor);
        }
        finally
        {
            ImGui.End();
        }
    }
}
