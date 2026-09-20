using System;
using System.Collections.Generic;
using System.Xml;
using ImGuiNET;

namespace ArcherEditorMod.Editor;

/// <summary>
/// One undo / redo history for the editor's data edits (sprite data xml, animations, head / bow offsets, atlas
/// rectangles, added sprites). Xml edits are snapshots of the element: call <see cref="BeforeXmlEdit"/> right before
/// changing it; edits that keep going while a widget is held or typed into become one step.
/// </summary>
public static class EditHistory
{
    private sealed record Entry(string Label, Action Undo, Action Redo);

    private static readonly List<Entry> undo = new();
    private static readonly List<Entry> redo = new();

    // the xml edit in progress
    private static XmlElement? groupXml;
    private static string? groupBefore;
    private static Action? groupRestored;
    private static string groupLabel = "";

    // the value edit in progress (state that is observed every frame instead of edited through xml)
    private static readonly Dictionary<object, object> lastSeen = new();
    private static object? valueKey;
    private static object? valueBefore;
    private static Action<object>? valueSet;
    private static string valueLabel = "";

    /// <summary>Rebuilds the editor's archer after an undo / redo (set by the editor screen).</summary>
    public static Action? Refresh;

    public static bool CanUndo => undo.Count > 0 || groupXml != null || valueKey != null;
    public static bool CanRedo => redo.Count > 0;
    public static string? NextUndo => groupXml != null ? groupLabel : valueKey != null ? valueLabel : undo.Count > 0 ? undo[^1].Label : null;
    public static string? NextRedo => redo.Count > 0 ? redo[^1].Label : null;

    /// <summary>Records a step that is already applied.</summary>
    public static void Push(string label, Action undoAction, Action redoAction)
    {
        CloseGroup();
        undo.Add(new Entry(label, undoAction, redoAction));
        if (undo.Count > 300) undo.RemoveAt(0);
        redo.Clear();
    }

    /// <summary>Call before an edit of <paramref name="xml"/>; <paramref name="restored"/> runs after undo / redo puts it back.</summary>
    public static void BeforeXmlEdit(XmlElement xml, string label, Action restored)
    {
        if (groupXml != null && ReferenceEquals(groupXml, xml)) return; // the same edit keeps going

        CloseGroup();
        groupXml = xml;
        groupBefore = xml.OuterXml;
        groupRestored = restored;
        groupLabel = label;
    }

    /// <summary>Once per frame: an xml edit ends when nothing is being held or typed into any more.</summary>
    public static void Tick()
    {
        if ((groupXml != null || valueKey != null) && !ImGui.IsAnyItemActive() && !ImGui.IsMouseDown(ImGuiMouseButton.Left))
            CloseGroup();
    }

    private static void CloseGroup()
    {
        CloseValueGroup();
        CloseXmlGroup();
    }

    private static void CloseXmlGroup()
    {
        if (groupXml == null) return;

        var xml = groupXml;
        var before = groupBefore!;
        var after = xml.OuterXml;
        var restored = groupRestored;
        var label = groupLabel;
        groupXml = null;
        groupBefore = null;
        groupRestored = null;

        if (before == after) return;

        undo.Add(new Entry(label, () => Restore(xml, before, restored), () => Restore(xml, after, restored)));
        if (undo.Count > 300) undo.RemoveAt(0);
        redo.Clear();
    }

    /// <summary>
    /// For state that windows edit in place (widgets write straight into it): call every frame after drawing with the
    /// state as it is now. A change against the last frame starts (or continues) one undo step; <paramref name="set"/>
    /// puts a state back. The first call for a key only records the starting state.
    /// </summary>
    public static void Observe<T>(object key, string label, T current, Action<T> set) where T : notnull
    {
        if (!lastSeen.TryGetValue(key, out var previous))
        {
            lastSeen[key] = current;
            return;
        }

        if (Equals(previous, current)) return;

        if (valueKey == null || !valueKey.Equals(key))
        {
            CloseGroup();
            valueKey = key;
            valueBefore = previous;
            valueSet = state => set((T)state);
            valueLabel = label;
        }

        lastSeen[key] = current;
    }

    /// <summary>The state behind a key was replaced from outside (a reload): start observing it from scratch.</summary>
    public static void Forget(object key) => lastSeen.Remove(key);

    private static void CloseValueGroup()
    {
        if (valueKey == null) return;

        var key = valueKey;
        var before = valueBefore!;
        var set = valueSet!;
        var after = lastSeen[key];
        var label = valueLabel;
        valueKey = null;
        valueBefore = null;
        valueSet = null;

        if (Equals(before, after)) return;

        undo.Add(new Entry(label, () => { set(before); lastSeen[key] = before; }, () => { set(after); lastSeen[key] = after; }));
        if (undo.Count > 300) undo.RemoveAt(0);
        redo.Clear();
    }

    private static void Restore(XmlElement live, string snapshot, Action? restored)
    {
        var doc = new XmlDocument { PreserveWhitespace = true };
        doc.LoadXml(snapshot);
        var source = doc.DocumentElement!;

        live.RemoveAll(); // children and attributes
        foreach (XmlAttribute attribute in source.Attributes)
            live.SetAttribute(attribute.Name, attribute.Value);
        foreach (XmlNode child in source.ChildNodes)
            live.AppendChild(live.OwnerDocument.ImportNode(child, true));

        restored?.Invoke();
    }

    public static void Undo()
    {
        CloseGroup();
        if (undo.Count == 0) return;

        var entry = undo[^1];
        undo.RemoveAt(undo.Count - 1);
        entry.Undo();
        redo.Add(entry);
        Refresh?.Invoke();
    }

    public static void Redo()
    {
        CloseGroup();
        if (redo.Count == 0) return;

        var entry = redo[^1];
        redo.RemoveAt(redo.Count - 1);
        entry.Redo();
        undo.Add(entry);
        Refresh?.Invoke();
    }
}
