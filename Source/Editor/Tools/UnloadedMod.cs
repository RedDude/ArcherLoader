using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml;
using TowerFall;

namespace ArcherEditorMod.Editor.Tools;

public sealed class UnloadedArcher
{
    public string Id = "";
    public string Element = "";           // Archer / AltArcher / SecretArcher
    public string ArcherDataFile = "";
    public string Name0 = "", Name1 = "";
}

/// <summary>
/// An archer mod on disk that is not loaded: its archers and the files that describe them. Nothing here touches the
/// game's own objects, only xml / json files, so a broken mod can be opened and repaired.
/// </summary>
public sealed class UnloadedMod
{
    public string Directory = "";
    public string MetaPath = "";
    public string Name = "";
    public List<string> ArcherDataFiles = new();
    public List<string> CustomDataFiles = new();
    public List<string> SpriteDataFiles = new();
    public List<UnloadedArcher> Archers = new();

    public static UnloadedMod Load(string metaPath)
    {
        if (!File.Exists(metaPath))
            throw new FileNotFoundException("meta.json not found.", metaPath);

        var directory = Path.GetDirectoryName(Path.GetFullPath(metaPath))!;
        var mod = new UnloadedMod { Directory = directory, MetaPath = metaPath, Name = Path.GetFileName(directory) };

        var meta = JsonNode.Parse(File.ReadAllText(metaPath)) as JsonObject
                   ?? throw new InvalidDataException("meta.json is not a json object.");
        var nameKey = meta.Select(p => p.Key).FirstOrDefault(k => k.Equals("name", StringComparison.OrdinalIgnoreCase));
        if (nameKey != null && !string.IsNullOrWhiteSpace(meta[nameKey]?.ToString()))
            mod.Name = meta[nameKey]!.ToString();

        mod.ArcherDataFiles = UnloadedValidator.ResolvePaths(directory, "archerData", "Content/Atlas/GameData/archerData.xml");
        mod.CustomDataFiles = UnloadedValidator.ResolvePaths(directory, "archerCustomData", "ArcherCustomData/archerCustomData.xml");
        mod.SpriteDataFiles = UnloadedValidator.ResolvePaths(directory, "spriteData", "Content/Atlas/SpriteData/spriteData.xml");

        if (mod.ArcherDataFiles.Count == 0)
            throw new InvalidDataException("This mod has no archerData.xml.");

        foreach (var file in mod.ArcherDataFiles)
        {
            XmlDocument doc;
            try
            {
                doc = new XmlDocument();
                doc.Load(file);
            }
            catch (Exception e)
            {
                throw new InvalidDataException($"{Path.GetFileName(file)} is not valid xml: {e.Message}");
            }

            var root = doc["Archers"];
            if (root == null)
                continue;

            foreach (var element in root.ChildNodes.OfType<XmlElement>())
            {
                var id = element.GetAttribute("id");
                if (string.IsNullOrEmpty(id))
                    continue;

                mod.Archers.Add(new UnloadedArcher
                {
                    Id = id,
                    Element = element.Name,
                    ArcherDataFile = file,
                    Name0 = element["Name0"]?.InnerText.Trim() ?? "",
                    Name1 = element["Name1"]?.InnerText.Trim() ?? ""
                });
            }
        }

        if (mod.Archers.Count == 0)
            throw new InvalidDataException("No archers found in this mod's archerData.xml.");

        return mod;
    }
}

/// <summary>The edits the unloaded archer editor can make, all on files. Each one throws on a problem.</summary>
public static class UnloadedEdits
{
    public sealed class DataForm
    {
        public string Name0 = "", Name1 = "";
        public Vector3 ColorA, ColorB, Light;
        public Vector3 OriginalA, OriginalB, OriginalLight;
        public bool HasA, HasB, HasLight, HasGender, HasStartNoHat;
        public int Gender;
        public bool StartNoHat;
    }

    private static string Pretty(XmlElement element)
    {
        var builder = new StringBuilder();
        var settings = new XmlWriterSettings { Indent = true, IndentChars = "  ", OmitXmlDeclaration = true };
        using (var writer = XmlWriter.Create(builder, settings))
            element.WriteTo(writer);
        return builder.ToString();
    }

    private static Vector3 ParseHex(string? text)
    {
        text = text?.Trim().TrimStart('#') ?? "";
        if (text.Length >= 6 &&
            int.TryParse(text.AsSpan(0, 6), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value))
            return new Vector3(((value >> 16) & 255) / 255f, ((value >> 8) & 255) / 255f, (value & 255) / 255f);
        return Vector3.One;
    }

    private static string ToHex(Vector3 color) =>
        $"{(int)Math.Round(color.X * 255):X2}{(int)Math.Round(color.Y * 255):X2}{(int)Math.Round(color.Z * 255):X2}";

    private static XmlElement Entry(XmlDocument doc, string id) =>
        ArcherXml.FindById(doc, id) ?? throw new InvalidDataException($"'{id}' is not in this file (it changed on disk?).");

    // ---- names, colors ----

    public static DataForm ReadForm(UnloadedArcher archer)
    {
        var element = Entry(ArcherXml.Load(archer.ArcherDataFile), archer.Id);
        var form = new DataForm
        {
            Name0 = element["Name0"]?.InnerText.Trim() ?? "",
            Name1 = element["Name1"]?.InnerText.Trim() ?? "",
            HasA = element["ColorA"] != null,
            HasB = element["ColorB"] != null,
            HasLight = element["LightbarColor"] != null,
            HasGender = element["Genders"] != null,
            HasStartNoHat = element["StartNoHat"] != null,
            StartNoHat = element["StartNoHat"]?.InnerText.Trim().Equals("true", StringComparison.OrdinalIgnoreCase) ?? false
        };

        form.ColorA = form.OriginalA = ParseHex(element["ColorA"]?.InnerText);
        form.ColorB = form.OriginalB = ParseHex(element["ColorB"]?.InnerText);
        form.Light = form.OriginalLight = ParseHex(element["LightbarColor"]?.InnerText);

        if (Enum.TryParse<TFGame.Genders>(element["Genders"]?.InnerText.Trim(), true, out var gender))
            form.Gender = (int)gender;
        return form;
    }

    /// <summary>The problems with these names: empty, or already used in this mod / by a loaded archer of the same kind.</summary>
    public static List<string> CheckNames(UnloadedMod mod, UnloadedArcher archer, string name0, string name1)
    {
        var problems = new List<string>();
        if (string.IsNullOrWhiteSpace(name0)) problems.Add("Name is empty.");
        if (string.IsNullOrWhiteSpace(name1)) problems.Add("Subname is empty.");
        if (problems.Count > 0) return problems;

        var full = ArcherNames.Full(name0, name1);
        var sibling = mod.Archers.FirstOrDefault(a => a != archer && a.Element == archer.Element &&
            string.Equals(ArcherNames.Full(a.Name0, a.Name1), full, StringComparison.OrdinalIgnoreCase));
        if (sibling != null)
            problems.Add($"'{full}' is already the name of '{sibling.Id}' in this mod.");

        var group = archer.Element switch
        {
            "AltArcher" => ArcherData.AltArchers,
            "SecretArcher" => ArcherData.SecretArchers,
            _ => ArcherData.Archers
        };
        if (ArcherNames.FindCollision(group, null, name0, name1) != null)
            problems.Add($"'{full}' is already the name of a loaded archer.");

        return problems;
    }

    public static void WriteForm(UnloadedArcher archer, DataForm form)
    {
        var doc = ArcherXml.Load(archer.ArcherDataFile);
        var element = Entry(doc, archer.Id);

        ArcherXml.SetChild(element, "Name0", form.Name0.Trim());
        ArcherXml.SetChild(element, "Name1", form.Name1.Trim());

        // an alt / secret inherits what it does not define: only write a value that was there or was changed
        if (form.HasA || form.ColorA != form.OriginalA) ArcherXml.SetChild(element, "ColorA", ToHex(form.ColorA));
        if (form.HasB || form.ColorB != form.OriginalB) ArcherXml.SetChild(element, "ColorB", ToHex(form.ColorB));
        if (form.HasLight || form.Light != form.OriginalLight) ArcherXml.SetChild(element, "LightbarColor", ToHex(form.Light));
        ArcherXml.SetChild(element, "Genders", ((TFGame.Genders)form.Gender).ToString());
        ArcherXml.SetChild(element, "StartNoHat", form.StartNoHat.ToString().ToLowerInvariant());
        ArcherXml.Save(doc, archer.ArcherDataFile);

        archer.Name0 = form.Name0.Trim();
        archer.Name1 = form.Name1.Trim();
    }

    // ---- meta.json ----

    public static (string Author, string Description, string Version) ReadMeta(string metaPath)
    {
        var meta = JsonNode.Parse(File.ReadAllText(metaPath)) as JsonObject ?? throw new InvalidDataException("meta.json is not a json object.");
        string Get(string key) => meta.FirstOrDefault(p => p.Key.Equals(key, StringComparison.OrdinalIgnoreCase)).Value?.ToString() ?? "";
        return (Get("author"), Get("description"), Get("version"));
    }

    public static void WriteMeta(string metaPath, string author, string description, string version)
    {
        var meta = JsonNode.Parse(File.ReadAllText(metaPath)) as JsonObject ?? throw new InvalidDataException("meta.json is not a json object.");
        void Set(string key, string value)
        {
            var existing = meta.Select(p => p.Key).FirstOrDefault(k => k.Equals(key, StringComparison.OrdinalIgnoreCase));
            meta[existing ?? key] = value;
        }

        Set("author", author);
        Set("description", description);
        Set("version", version);
        File.WriteAllText(metaPath, meta.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    // ---- entries of the other files ----

    /// <summary>The archerCustomData.xml entry of this archer: (file, id it uses), or null when it has none.</summary>
    public static (string File, string Id)? FindCustomEntry(UnloadedMod mod, UnloadedArcher archer)
    {
        foreach (var file in mod.CustomDataFiles)
        {
            var doc = ArcherXml.Load(file);
            foreach (var id in new[] { archer.Id, $"{mod.Name}/{archer.Id}" })
            {
                if (ArcherXml.FindById(doc, id) != null)
                    return (file, id);
            }
        }
        return null;
    }

    public static string? BodySpriteId(UnloadedArcher archer)
    {
        var element = Entry(ArcherXml.Load(archer.ArcherDataFile), archer.Id);
        var id = element["Sprites"]?["Body"]?.InnerText.Trim();
        return string.IsNullOrEmpty(id) ? null : id;
    }

    public static string? FindSpriteFile(UnloadedMod mod, string spriteId)
    {
        foreach (var file in mod.SpriteDataFiles)
        {
            if (ArcherXml.FindById(ArcherXml.Load(file), spriteId) != null)
                return file;
        }
        return null;
    }

    public static string ReadEntryXml(string file, string id) => Pretty(Entry(ArcherXml.Load(file), id));

    /// <summary>Replaces (or adds) the element with this id by the given xml text, after checking it is well formed.</summary>
    public static void WriteEntryXml(string file, string id, string text, string[] allowedRoots, string? addUnder)
    {
        XmlDocument replacement = new();
        try
        {
            replacement.LoadXml(text);
        }
        catch (XmlException e)
        {
            throw new InvalidDataException("The xml is not well formed: " + e.Message);
        }

        var root = replacement.DocumentElement!;
        if (allowedRoots.Length > 0 && !allowedRoots.Contains(root.Name))
            throw new InvalidDataException($"The root element must be one of: {string.Join(", ", allowedRoots)}.");
        if (root.GetAttribute("id") != id)
            throw new InvalidDataException($"The element must keep id=\"{id}\".");

        XmlDocument doc;
        if (File.Exists(file))
        {
            doc = ArcherXml.Load(file);
        }
        else
        {
            if (addUnder == null) throw new FileNotFoundException("The file does not exist.", file);
            doc = new XmlDocument { PreserveWhitespace = true };
            doc.AppendChild(doc.CreateXmlDeclaration("1.0", "utf-8", null));
            doc.AppendChild(doc.CreateElement(addUnder));
        }

        var imported = doc.ImportNode(root, true);
        var existing = ArcherXml.FindById(doc, id);
        if (existing != null)
        {
            existing.ParentNode!.ReplaceChild(imported, existing);
        }
        else
        {
            var container = addUnder != null ? doc[addUnder] : doc.DocumentElement;
            (container ?? throw new InvalidDataException("The file has no root element.")).AppendChild(imported);
        }

        System.IO.Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        doc.Save(file);
    }

    // ---- head / bow offsets of the body sprite ----

    public static int[]? ReadTable(string spriteFile, string spriteId, string name)
    {
        var text = Entry(ArcherXml.Load(spriteFile), spriteId)[name]?.InnerText.Trim();
        if (text == null) return null;
        if (text.Length == 0) return Array.Empty<int>();

        return text.Split(',').Select(v => int.TryParse(v.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n)
            ? n
            : throw new InvalidDataException($"{name} has an entry that is not a number: '{v.Trim()}'")).ToArray();
    }

    public static bool ReadBool(string spriteFile, string spriteId, string name) =>
        Entry(ArcherXml.Load(spriteFile), spriteId)[name]?.InnerText.Trim().Equals("true", StringComparison.OrdinalIgnoreCase) ?? false;

    public static void WriteOffsets(string spriteFile, string spriteId, IReadOnlyDictionary<string, int[]?> tables, bool hideBow, bool hadHideBow)
    {
        var doc = ArcherXml.Load(spriteFile);
        var element = Entry(doc, spriteId);

        foreach (var (name, values) in tables)
        {
            if (values != null)
                ArcherXml.SetChild(element, name, string.Join(",", values));
        }

        if (hideBow || hadHideBow)
            ArcherXml.SetChild(element, "HideBow", hideBow ? "True" : "False");

        ArcherXml.Save(doc, spriteFile);
    }
}
