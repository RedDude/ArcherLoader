using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Xml;
using ArcherEditorMod.Source.Features;
using ArcherEditorMod.Source.Features.Hair;
using ArcherEditorMod.Source.Features.Particles;
using TowerFall;

namespace ArcherEditorMod.Editor.Tools;

/// <summary>
/// Saves what the archer editor adds to an archer (hair, particles, wings, ghost) as an archerCustomData.xml entry,
/// either inside the archer's own mod or in a separate mod, and keeps the ArcherEditor dependency in sync:
/// added when there is something to load, removed when there is not.
/// </summary>
public static class CustomDataSaver
{
    private static readonly string[] FeatureElements = { "Hair", "HairInfo", "HairInfos", "Particle", "Particles", "Wings", "Ghost", "Taunt" };
    private static readonly Regex ModNamePattern = new("^[A-Za-z][A-Za-z0-9_.-]{1,47}$");

    public sealed record Summary(int Hairs, int Particles, bool Wings, bool Ghost, bool Taunt)
    {
        public bool Any => Hairs > 0 || Particles > 0 || Wings || Ghost || Taunt;
    }

    public static Summary Inspect(ArcherData data) => new(
        HairFeature.GetHairs(data)?.Count ?? 0,
        ParticlesFeature.GetAllParticles(data).Count,
        Source.Features.Wings.WingsFeature.TryGet(data, out _, out _),
        Source.Features.Ghost.GhostFeature.TryGet(data, out _, out _),
        Source.Features.Taunt.TauntFeature.TryGet(data, out _));

    public static string ElementName(ArcherData data) => ArcherDecorationRegistry.TypeOf(data) switch
    {
        ArcherData.ArcherTypes.Alt => "AltArcher",
        ArcherData.ArcherTypes.Secret => "SecretArcher",
        _ => "Archer"
    };

    /// <summary>A name for a separate data mod that is not taken yet: "{Mod}_ArcherEditor".</summary>
    public static string SuggestSeparateName(ArcherData data)
    {
        var source = ArcherDecorationRegistry.FindArcherSource(data);
        var baseName = source != null && source.ModName.Length > 0
            ? source.ModName
            : Regex.Replace(ArcherNames.Full(data.Name0, data.Name1), "[^A-Za-z0-9]", "");
        return (baseName.Length > 0 ? baseName : "Archer") + "_ArcherEditor";
    }

    public static string? Validate(ArcherData data, bool sameMod, string separateName)
    {
        var source = ArcherDecorationRegistry.FindArcherSource(data);
        if (sameMod)
        {
            if (source == null) return "A base game archer has no mod of its own: save to a separate mod.";
            if (source.ModDirectory == null) return "The archer's mod is not a folder on disk (zipped?): save to a separate mod.";
            return null;
        }

        if (!ModNamePattern.IsMatch(separateName))
            return "The mod name must start with a letter and use letters, digits, _ . - (2-48 characters).";
        if (ArcherDecorationRegistry.DecorationId(data, source, false) == null)
            return "This archer can't be referenced from another mod.";
        return null;
    }

    /// <returns>What was done, one line each.</returns>
    public static List<string> Save(ArcherData data, bool sameMod, string separateName)
    {
        var problem = Validate(data, sameMod, separateName);
        if (problem != null)
            throw new InvalidOperationException(problem);

        var messages = new List<string>();
        var source = ArcherDecorationRegistry.FindArcherSource(data);
        var fragments = BuildFragments(data, sameMod);
        var elementName = ElementName(data);

        string directory, id;
        if (sameMod)
        {
            directory = source!.ModDirectory!;
            id = source.LocalId;
        }
        else
        {
            directory = Path.Combine(ArcherCopier.ModsDirectory, separateName);
            id = ArcherDecorationRegistry.DecorationId(data, source, false)!;
        }

        var customPath = CustomDataPath(data, source, sameMod, directory);
        var stillUsed = UpdateEntry(customPath, elementName, id, fragments);
        messages.Add(fragments.Length > 0 ? $"Wrote {customPath}" : $"Removed the editor data from {customPath}");

        if (sameMod)
        {
            SetDependency(directory, stillUsed, required: false, messages);
        }
        else
        {
            EnsureSeparateMeta(directory, separateName, data, source, stillUsed, messages);
        }

        return messages;
    }

    // ---- xml ----

    private static string BuildFragments(ArcherData data, bool sameModIds)
    {
        var parts = new List<string>();

        var hairs = HairFeature.GetHairs(data);
        if (hairs != null) parts.Add(HairWindow.ToXml(hairs));

        var particles = ParticlesFeature.GetAllParticles(data);
        if (particles.Count > 0) parts.Add(ParticlesWindow.ToXml(particles));

        if (Source.Features.Wings.WingsFeature.TryGet(data, out _, out var wingsColor))
            parts.Add(TextureColor("Wings", WingsGhostWindow.CurrentTextureName(data, true), wingsColor));

        if (Source.Features.Ghost.GhostFeature.TryGet(data, out _, out var ghostColor))
            parts.Add(TextureColor("Ghost", WingsGhostWindow.CurrentTextureName(data, false), ghostColor));

        if (Source.Features.Taunt.TauntFeature.TryGet(data, out var taunt))
        {
            // the taunt points at a spriteData id: inside the archer's own mod its short form resolves, elsewhere the full one
            var source = ArcherDecorationRegistry.FindArcherSource(data);
            var id = taunt.IdText ?? taunt.SpriteId;
            if (sameModIds && source != null && id.StartsWith(source.ModName + "/", StringComparison.Ordinal))
                id = id[(source.ModName.Length + 1)..];
            else if (!sameModIds && !id.Contains('/'))
                id = taunt.SpriteId;
            parts.Add(TauntWindow.ToXml(taunt, id));
        }

        return string.Join("\n", parts);
    }

    private static string TextureColor(string name, string? texture, Microsoft.Xna.Framework.Color? color)
    {
        var inner = "";
        if (texture != null) inner += $"<Texture>{System.Security.SecurityElement.Escape(texture)}</Texture>";
        if (color.HasValue) inner += $"<Color>{ArcherXml.Hex(color.Value)}</Color>";
        return $"<{name}>{inner}</{name}>";
    }

    // where this archer's entry lives: the file it was already read from, or the mod's archerCustomData.xml
    private static string CustomDataPath(ArcherData data, ArcherDecorationRegistry.ArcherSource? source, bool sameMod, string directory)
    {
        if (sameMod && source?.Content != null)
        {
            var existing = ArcherDecorationRegistry.Decorations.FirstOrDefault(d =>
                d.ArcherData == data && d.ModContent.Metadata.Name == source.ModName && d.EditablePath != null);
            if (existing != null)
                return existing.EditablePath!;

            // content.json may have moved the loader: use the first declared file
            var declared = ArcherDecorationRegistry.GetCustomDataPaths(source.Content).FirstOrDefault();
            if (declared != null)
                return Path.Combine(directory, declared.TrimStart('/', '\\').Replace('/', Path.DirectorySeparatorChar));
        }
        else if (!sameMod)
        {
            var existing = Path.Combine(directory, "ArcherCustomData", "archerCustomData.xml");
            return existing;
        }

        return Path.Combine(directory, "ArcherCustomData", "archerCustomData.xml");
    }

    /// <returns>True when the file still has entries that need ArcherEditor.</returns>
    private static bool UpdateEntry(string path, string elementName, string id, string fragments)
    {
        XmlDocument doc;
        if (File.Exists(path))
        {
            doc = ArcherXml.Load(path);
        }
        else
        {
            doc = new XmlDocument { PreserveWhitespace = true };
            doc.AppendChild(doc.CreateXmlDeclaration("1.0", "utf-8", null));
            doc.AppendChild(doc.CreateElement("Archers"));
        }

        var root = doc["Archers"] ?? throw new Exception($"{path} has no <Archers> element.");
        var entry = root.ChildNodes.OfType<XmlElement>().FirstOrDefault(e => e.GetAttribute("id") == id && e.Name == elementName)
                    ?? root.ChildNodes.OfType<XmlElement>().FirstOrDefault(e => e.GetAttribute("id") == id);

        if (entry == null && fragments.Length == 0)
            return HasEntriesWithContent(root);

        if (entry == null)
        {
            entry = doc.CreateElement(elementName);
            entry.SetAttribute("id", id);
            root.AppendChild(doc.CreateWhitespace("\n  "));
            root.AppendChild(entry);
            root.AppendChild(doc.CreateWhitespace("\n"));
        }

        ArcherXml.RemoveChildren(entry, FeatureElements);
        if (fragments.Length > 0)
            entry.InnerXml += "\n    " + fragments.Replace("\n", "\n    ") + "\n  ";

        // an entry with nothing left in it only adds noise
        if (entry.ChildNodes.OfType<XmlElement>().Count() == 0)
            root.RemoveChild(entry);

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        doc.Save(path);
        return HasEntriesWithContent(root);
    }

    private static bool HasEntriesWithContent(XmlElement root) =>
        root.ChildNodes.OfType<XmlElement>().Any(e => e.ChildNodes.OfType<XmlElement>().Any());

    // ---- meta.json ----

    private static JsonObject ReadMeta(string path) => JsonNode.Parse(File.ReadAllText(path))!.AsObject();

    private static void WriteMeta(string path, JsonObject meta) =>
        File.WriteAllText(path, meta.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));

    private static string? FindKey(JsonObject obj, string key) =>
        obj.Select(p => p.Key).FirstOrDefault(k => k.Equals(key, StringComparison.OrdinalIgnoreCase));

    private static JsonObject? FindDependency(JsonArray array, string name) =>
        array.OfType<JsonObject>().FirstOrDefault(o =>
            o.TryGetPropertyValue("name", out var n) && string.Equals(n?.ToString(), name, StringComparison.Ordinal));

    /// <summary>Adds ArcherEditor to the mod's dependencies when it is needed, takes it out when it is not.</summary>
    private static void SetDependency(string directory, bool needed, bool required, List<string> messages)
    {
        var metaPath = Path.Combine(directory, "meta.json");
        if (!File.Exists(metaPath))
        {
            messages.Add("No meta.json here: the ArcherEditor dependency was not touched.");
            return;
        }

        var meta = ReadMeta(metaPath);
        var editor = FortEntrance.Instance.Meta.Name;
        var version = FortEntrance.Instance.Meta.Version.ToString();

        var requiredKey = FindKey(meta, "dependencies");
        var optionalKey = FindKey(meta, "optionalDependencies");
        var requiredList = requiredKey != null ? meta[requiredKey] as JsonArray : null;
        var optionalList = optionalKey != null ? meta[optionalKey] as JsonArray : null;

        // a required dependency is the mod author's call: never removed, and it makes the optional one pointless
        if (requiredList != null && FindDependency(requiredList, editor) != null)
        {
            messages.Add($"{editor} is already a required dependency, left as it is.");
            return;
        }

        if (needed)
        {
            if (optionalList != null && FindDependency(optionalList, editor) != null)
            {
                messages.Add($"{editor} is already an optional dependency.");
                return;
            }

            if (optionalList == null)
            {
                optionalList = new JsonArray();
                meta[optionalKey ?? "optionalDependencies"] = optionalList;
            }

            optionalList.Add(new JsonObject { ["name"] = editor, ["version"] = version });
            WriteMeta(metaPath, meta);
            messages.Add($"Added {editor} {version} as an optional dependency.");
        }
        else if (optionalList != null && FindDependency(optionalList, editor) is { } existing)
        {
            optionalList.Remove(existing);
            if (optionalList.Count == 0 && optionalKey != null)
                meta.Remove(optionalKey);
            WriteMeta(metaPath, meta);
            messages.Add($"Removed the optional {editor} dependency: nothing left needs it.");
        }
    }

    // a separate mod is useless without the editor and without the archer's own mod: both are required
    private static void EnsureSeparateMeta(string directory, string modName, ArcherData data,
        ArcherDecorationRegistry.ArcherSource? source, bool needed, List<string> messages)
    {
        var metaPath = Path.Combine(directory, "meta.json");
        if (File.Exists(metaPath))
        {
            messages.Add("Updated the existing mod " + modName);
            return;
        }

        var editorName = FortEntrance.Instance.Meta.Name;
        var dependencies = new JsonArray
        {
            new JsonObject { ["name"] = "FortRise", ["version"] = "5.0.0" },
            new JsonObject { ["name"] = editorName, ["version"] = FortEntrance.Instance.Meta.Version.ToString() }
        };

        if (source != null && source.ModName.Length > 0)
        {
            var owner = FortEntrance.Instance.Context.Interop.GetMod(source.ModName);
            dependencies.Add(new JsonObject
            {
                ["name"] = source.ModName,
                ["version"] = owner?.Metadata.Version.ToString() ?? "1.0.0"
            });
        }

        var meta = new JsonObject
        {
            ["name"] = modName,
            ["description"] = $"ArcherEditor data (hair, particles, wings, ghost) for {ArcherNames.Full(data.Name0, data.Name1)}",
            ["version"] = "1.0.0",
            ["dependencies"] = dependencies
        };

        Directory.CreateDirectory(directory);
        WriteMeta(metaPath, meta);
        messages.Add($"Created the mod {modName}, requiring {editorName}" +
                     (source != null && source.ModName.Length > 0 ? $" and {source.ModName}." : "."));
    }
}
