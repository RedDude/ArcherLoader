using System;
using System.Linq;
using TowerFall;

namespace ArcherEditorMod.Editor.Tools;

/// <summary>Name checks shared by the archer data and copy tools.</summary>
public static class ArcherNames
{
    // archers of the same kind (normal / alt / secret) may not share a name; an alt is allowed to carry its base's
    public static ArcherData[] GroupOf(ArcherData data)
    {
        if (ArcherData.AltArchers.Contains(data)) return ArcherData.AltArchers;
        if (ArcherData.SecretArchers.Contains(data)) return ArcherData.SecretArchers;
        return ArcherData.Archers;
    }

    public static string Full(string name0, string name1) => $"{name0.Trim()} {name1.Trim()}".Trim();

    /// <summary>The archer already using this name, or null.</summary>
    public static ArcherData? FindCollision(ArcherData[] group, ArcherData? exclude, string name0, string name1)
    {
        var wanted = Full(name0, name1);
        return group.FirstOrDefault(other =>
            other != null && !ReferenceEquals(other, exclude) &&
            string.Equals(Full(other.Name0 ?? "", other.Name1 ?? ""), wanted, StringComparison.OrdinalIgnoreCase));
    }
}
