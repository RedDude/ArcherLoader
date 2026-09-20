using System;
using System.Collections.Generic;
using System.Linq;
using ImGuiNET;

namespace ArcherEditorMod.Editor;

public enum ConsoleLevel
{
    Info,
    Warn,
    Error
}

/// <summary>In-editor log: validator results, saves and other editor messages, shown in the "Console" window.</summary>
public static class EditorConsole
{
    private sealed record Entry(DateTime Time, ConsoleLevel Level, string Text);

    private const int MaxEntries = 1000;
    private static readonly List<Entry> entries = new();

    public static bool Open;

    private static bool showInfo = true, showWarn = true, showError = true, autoScroll = true;
    private static int lastCount;

    public static void Log(ConsoleLevel level, string text)
    {
        entries.Add(new Entry(DateTime.Now, level, text));
        if (entries.Count > MaxEntries)
            entries.RemoveRange(0, entries.Count - MaxEntries);
    }

    public static void Info(string text) => Log(ConsoleLevel.Info, text);
    public static void Warn(string text) => Log(ConsoleLevel.Warn, text);
    public static void Error(string text) => Log(ConsoleLevel.Error, text);

    public static void Draw()
    {
        if (!Open) return;

        ImGui.SetNextWindowSize(new System.Numerics.Vector2(620, 240), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowPos(new System.Numerics.Vector2(10, 620), ImGuiCond.FirstUseEver);
        if (!ImGui.Begin("Console", ref Open))
        {
            ImGui.End();
            return;
        }

        ImGui.Checkbox("Info", ref showInfo);
        ImGui.SameLine();
        ImGui.Checkbox("Warnings", ref showWarn);
        ImGui.SameLine();
        ImGui.Checkbox("Errors", ref showError);
        ImGui.SameLine();
        ImGui.Checkbox("Auto-scroll", ref autoScroll);
        ImGui.SameLine();
        if (ImGui.Button("Clear"))
            entries.Clear();
        ImGui.SameLine();
        if (ImGui.Button("Copy"))
            ImGui.SetClipboardText(string.Join("\n", entries.Select(e => $"[{e.Time:HH:mm:ss}] {e.Level}: {e.Text}")));

        ImGui.Separator();
        if (ImGui.BeginChild("console_lines", new System.Numerics.Vector2(0, 0), ImGuiChildFlags.None, ImGuiWindowFlags.HorizontalScrollbar))
        {
            foreach (var entry in entries)
            {
                if (entry.Level == ConsoleLevel.Info && !showInfo) continue;
                if (entry.Level == ConsoleLevel.Warn && !showWarn) continue;
                if (entry.Level == ConsoleLevel.Error && !showError) continue;

                ImGui.PushStyleColor(ImGuiCol.Text, entry.Level switch
                {
                    ConsoleLevel.Error => new System.Numerics.Vector4(1f, 0.4f, 0.4f, 1),
                    ConsoleLevel.Warn => new System.Numerics.Vector4(1f, 0.85f, 0.3f, 1),
                    _ => new System.Numerics.Vector4(0.85f, 0.85f, 0.85f, 1)
                });
                ImGui.TextUnformatted($"[{entry.Time:HH:mm:ss}] {entry.Text}");
                ImGui.PopStyleColor();
            }

            if (autoScroll && entries.Count != lastCount)
                ImGui.SetScrollHereY(1f);
            lastCount = entries.Count;
        }
        ImGui.EndChild();

        ImGui.End();
    }
}
