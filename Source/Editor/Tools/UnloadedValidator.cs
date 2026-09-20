using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Xml;
using Monocle;
using TowerFall;

namespace ArcherEditorMod.Editor.Tools;

/// <summary>
/// Validates the archers of a mod that is NOT loaded, given its meta.json: reads its archerData.xml, atlases and
/// sprite data straight from the mod folder and runs the original ArcherCustomDataValidator on each archer.
/// (Zipped mods are not supported: unzip first.) Textures referenced by png path instead of an atlas name are
/// reported as missing by that validator.
/// </summary>
public static class UnloadedValidator
{
    public sealed record ArcherReport(string Id, string Type, List<ValidatorMessage> Messages);

    public sealed record Report(string ModName, string Directory, List<ArcherReport> Archers, List<string> Problems);

    public static Report Run(string metaPath)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(metaPath))!;
        var problems = new List<string>();
        var archers = new List<ArcherReport>();

        var modName = Path.GetFileName(directory);
        try
        {
            using var meta = JsonDocument.Parse(File.ReadAllText(metaPath));
            foreach (var property in meta.RootElement.EnumerateObject())
            {
                if (property.NameEquals("name") || property.Name.Equals("name", StringComparison.OrdinalIgnoreCase))
                    modName = property.Value.GetString() ?? modName;
            }
        }
        catch (Exception e)
        {
            problems.Add($"meta.json could not be read: {e.Message}");
        }

        var archerDataFiles = ResolvePaths(directory, "archerData", "Content/Atlas/GameData/archerData.xml");
        if (archerDataFiles.Count == 0)
        {
            problems.Add("No archerData.xml found in this mod.");
            return new Report(modName, directory, archers, problems);
        }

        var atlas = LoadAtlas(directory, "atlas", "Content/Atlas/atlas.xml", problems);
        var menuAtlas = LoadAtlas(directory, "menuAtlas", "Content/Atlas/menuAtlas.xml", problems);
        var spriteData = LoadSpriteData(directory, "spriteData", "Content/Atlas/SpriteData/spriteData.xml", atlas, problems);
        var menuSpriteData = LoadSpriteData(directory, "menuSpriteData", "Content/Atlas/SpriteData/menuSpriteData.xml", menuAtlas, problems);
        spriteData ??= TFGame.SpriteData;
        menuSpriteData ??= TFGame.MenuSpriteData;

        var validator = new ArcherCustomDataValidator();
        foreach (var file in archerDataFiles)
        {
            XmlDocument xml;
            try
            {
                xml = new XmlDocument();
                xml.Load(file);
            }
            catch (Exception e)
            {
                problems.Add($"{Path.GetFileName(file)} is not valid xml: {e.Message}");
                continue;
            }

            var root = xml["Archers"];
            if (root == null)
            {
                problems.Add($"{Path.GetFileName(file)} has no <Archers> element.");
                continue;
            }

            foreach (var node in root)
            {
                if (node is not XmlElement element)
                    continue;

                var type = element.Name switch
                {
                    "AltArcher" => ArcherData.ArcherTypes.Alt,
                    "SecretArcher" => ArcherData.ArcherTypes.Secret,
                    _ => ArcherData.ArcherTypes.Normal
                };

                var id = element.GetAttribute("id");
                List<ValidatorMessage> messages;
                try
                {
                    messages = validator.Validate(element, atlas ?? TFGame.Atlas, menuAtlas ?? TFGame.MenuAtlas,
                        spriteData, menuSpriteData, type, id, directory + Path.DirectorySeparatorChar);
                }
                catch (Exception e)
                {
                    messages = new List<ValidatorMessage> { new($"Validator crashed on this archer: {e.Message}") };
                }

                archers.Add(new ArcherReport(id, element.Name, messages));
            }
        }

        return new Report(modName, directory, archers, problems);
    }

    // paths a mod declares for a loader in content.json ("loaders": { "<name>": { "path": [...] } }), or the default
    internal static List<string> ResolvePaths(string directory, string loader, string defaultPath)
    {
        var relative = new List<string>();
        var contentJson = Path.Combine(directory, "content.json");
        if (File.Exists(contentJson))
        {
            try
            {
                using var json = JsonDocument.Parse(File.ReadAllText(contentJson));
                foreach (var top in json.RootElement.EnumerateObject())
                {
                    if (!top.Name.Equals("loaders", StringComparison.OrdinalIgnoreCase)) continue;
                    foreach (var entry in top.Value.EnumerateObject())
                    {
                        if (!entry.Name.Equals(loader, StringComparison.OrdinalIgnoreCase)) continue;
                        foreach (var property in entry.Value.EnumerateObject())
                        {
                            if (!property.Name.Equals("path", StringComparison.OrdinalIgnoreCase)) continue;
                            if (property.Value.ValueKind == JsonValueKind.Array)
                                relative.AddRange(property.Value.EnumerateArray().Select(v => v.GetString()!).Where(v => v != null));
                            else if (property.Value.ValueKind == JsonValueKind.String)
                                relative.Add(property.Value.GetString()!);
                        }
                    }
                }
            }
            catch
            {
                // fall back to the default path
            }
        }

        if (relative.Count == 0)
            relative.Add(defaultPath);

        return relative
            .Select(p => Path.Combine(directory, p.TrimStart('/', '\\').Replace('/', Path.DirectorySeparatorChar)))
            .Where(File.Exists)
            .ToList();
    }

    private static Atlas? LoadAtlas(string directory, string loader, string defaultPath, List<string> problems)
    {
        var xml = ResolvePaths(directory, loader, defaultPath).FirstOrDefault();
        if (xml == null)
            return null;

        try
        {
            var png = Path.ChangeExtension(xml, ".png");
            return new Atlas(xml, png, false);
        }
        catch (Exception e)
        {
            problems.Add($"{Path.GetFileName(xml)} could not be loaded ({e.Message}); texture names are checked against the loaded game instead.");
            return null;
        }
    }

    private static SpriteData? LoadSpriteData(string directory, string loader, string defaultPath, Atlas? atlas, List<string> problems)
    {
        var xml = ResolvePaths(directory, loader, defaultPath).FirstOrDefault();
        if (xml == null || atlas == null)
            return null;

        try
        {
            return new SpriteData(xml, atlas);
        }
        catch (Exception e)
        {
            problems.Add($"{Path.GetFileName(xml)} could not be loaded ({e.Message}); sprite ids are checked against the loaded game instead.");
            return null;
        }
    }
}
