using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Xml;
using ArcherEditorMod.Source.Features;
using ArcherEditorMod.Source.Features.Ghost;
using ArcherEditorMod.Source.Features.Hair;
using ArcherEditorMod.Source.Features.Particles;
using ArcherEditorMod.Source.Features.Wings;
using Microsoft.Xna.Framework.Graphics;
using Monocle;
using TowerFall;

namespace ArcherEditorMod.Editor.Tools;

/// <summary>
/// Creates a new mod that is a copy of an archer.
/// - Archer from a mod folder on disk: the whole mod folder is copied (assets, atlases, images, xml), then renamed
///   and cut down to this one archer.
/// - Zipped mod or base game archer: the mod is generated from the archer in memory (sprite data, portraits and
///   the other textures are exported as png files, xml is written from the live objects).
/// Experimental: the result is only checked when the game loads it.
/// </summary>
public static class ArcherCopier
{
    private static readonly Regex IdPattern = new("^[A-Za-z][A-Za-z0-9_]{1,31}$");

    public static string ModsDirectory => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Mods");

    public sealed record Result(bool Ok, string? Directory, List<string> Messages);

    /// <summary>Everything that stops this id / name from being used; empty when it is fine.</summary>
    public static List<string> Check(ArcherData data, string id, string name0, string name1)
    {
        var errors = new List<string>();

        if (!IdPattern.IsMatch(id))
            errors.Add("The id must start with a letter and use only letters, digits and _ (2-32 characters).");
        if (string.IsNullOrWhiteSpace(name0)) errors.Add("Name is empty.");
        if (string.IsNullOrWhiteSpace(name1)) errors.Add("Subname is empty.");

        if (IdPattern.IsMatch(id))
        {
            if (Directory.Exists(Path.Combine(ModsDirectory, id)))
                errors.Add($"A mod folder named '{id}' already exists.");
            if (FortEntrance.Instance.Context.Interop.IsModExists(id))
                errors.Add($"A loaded mod is already named '{id}'.");
            if (FortEntrance.Instance.Context.Registry.Archers.RegisteredArchers.Keys
                .Any(k => k.Equals(id, StringComparison.OrdinalIgnoreCase) || k.StartsWith(id + "/", StringComparison.OrdinalIgnoreCase)))
                errors.Add($"An archer with the id '{id}' is already registered.");
        }

        var collision = ArcherNames.FindCollision(ArcherData.Archers, null, name0, name1);
        if (collision != null)
            errors.Add($"'{ArcherNames.Full(name0, name1)}' is already the name of another archer.");

        return errors;
    }

    public static Result Create(ArcherData data, string id, string name0, string name1)
    {
        var messages = new List<string>();
        var errors = Check(data, id, name0, name1);
        if (errors.Count > 0)
            return new Result(false, null, errors);

        var target = Path.Combine(ModsDirectory, id);
        try
        {
            Directory.CreateDirectory(target);

            var source = ArcherDecorationRegistry.FindArcherSource(data);
            if (source?.ModDirectory != null && source.ArcherDataFile != null)
            {
                messages.Add($"Copying the mod folder of '{source.ModName}'...");
                CopyFromFolder(source, data, id, name0, name1, target, messages);
            }
            else
            {
                messages.Add(source == null
                    ? "Base game archer: generating the mod from memory..."
                    : "The mod is not a folder on disk (zipped?): generating the mod from memory...");
                FromMemory(data, id, name0, name1, target, messages);
            }

            messages.Add($"Created {target}. Restart the game to load it.");
            return new Result(true, target, messages);
        }
        catch (Exception e)
        {
            try { Directory.Delete(target, true); } catch { /* leave it */ }
            messages.Add("Failed: " + e.Message);
            return new Result(false, null, messages);
        }
    }

    // ---------------------------------------------------------------- copy from a mod folder

    private static void CopyFromFolder(ArcherDecorationRegistry.ArcherSource source, ArcherData data, string id,
        string name0, string name1, string target, List<string> messages)
    {
        var sourceDirectory = source.ModDirectory!;
        var copied = CopyDirectory(sourceDirectory, target);
        messages.Add($"Copied {copied} files (dll / pdb / .git skipped).");

        // meta.json: new identity, no code
        var metaPath = Path.Combine(target, "meta.json");
        if (File.Exists(metaPath))
        {
            var meta = JsonNode.Parse(File.ReadAllText(metaPath))!.AsObject();
            SetJson(meta, "name", id);
            SetJson(meta, "description", $"Copy of {ArcherNames.Full(data.Name0, data.Name1)}");
            SetJson(meta, "version", "1.0.0");
            RemoveJson(meta, "dll");
            RemoveJson(meta, "update");
            File.WriteAllText(metaPath, meta.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        }
        else
        {
            WriteMeta(target, id, $"Copy of {ArcherNames.Full(data.Name0, data.Name1)}", false);
        }

        // archerData.xml: only this archer, renamed
        var relative = Path.GetRelativePath(sourceDirectory, source.ArcherDataFile!);
        var archerDataPath = Path.Combine(target, relative);
        var doc = ArcherXml.Load(archerDataPath);
        var element = ArcherXml.FindById(doc, source.LocalId)
                      ?? throw new Exception($"'{source.LocalId}' not found in the copied {relative}.");

        var root = element.ParentNode!;
        foreach (var other in root.ChildNodes.OfType<XmlElement>().Where(e => e != element).ToList())
            root.RemoveChild(other);

        element.SetAttribute("id", id);
        ArcherXml.SetChild(element, "Name0", name0.Trim());
        ArcherXml.SetChild(element, "Name1", name1.Trim());
        ArcherXml.Save(doc, archerDataPath);
        messages.Add($"Rewrote {relative}.");

        // archerCustomData.xml: only this archer's decoration
        var decoration = ArcherDecorationRegistry.Decorations.FirstOrDefault(d => d.ArcherData == data);
        if (decoration?.EditablePath != null && decoration.EditablePath.StartsWith(sourceDirectory, StringComparison.OrdinalIgnoreCase))
        {
            var customRelative = Path.GetRelativePath(sourceDirectory, decoration.EditablePath);
            var customPath = Path.Combine(target, customRelative);
            if (File.Exists(customPath))
            {
                var custom = ArcherXml.Load(customPath);
                var oldId = decoration.Xml.GetAttribute("id");
                var entry = ArcherXml.FindById(custom, oldId);
                if (entry != null)
                {
                    var customRoot = entry.ParentNode!;
                    foreach (var other in customRoot.ChildNodes.OfType<XmlElement>().Where(e => e != entry).ToList())
                        customRoot.RemoveChild(other);
                    entry.SetAttribute("id", id);
                    ArcherXml.Save(custom, customPath);
                    messages.Add($"Rewrote {customRelative}.");
                }
            }
        }

        if (Directory.GetFiles(sourceDirectory, "*.dll").Length > 0)
            messages.Add("Warning: the original mod has code (dll); it was not copied.");
    }

    private static int CopyDirectory(string from, string to)
    {
        var count = 0;
        foreach (var file in Directory.EnumerateFiles(from, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(from, file);
            var extension = Path.GetExtension(file).ToLowerInvariant();
            if (extension is ".dll" or ".pdb") continue;
            if (relative.StartsWith(".git", StringComparison.OrdinalIgnoreCase)) continue;

            var destination = Path.Combine(to, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(file, destination, true);
            count++;
        }
        return count;
    }

    private static void SetJson(JsonObject obj, string key, string value)
    {
        var existing = obj.Select(p => p.Key).FirstOrDefault(k => k.Equals(key, StringComparison.OrdinalIgnoreCase));
        obj[existing ?? key] = value;
    }

    private static void RemoveJson(JsonObject obj, string key)
    {
        var existing = obj.Select(p => p.Key).FirstOrDefault(k => k.Equals(key, StringComparison.OrdinalIgnoreCase));
        if (existing != null) obj.Remove(existing);
    }

    private static void WriteMeta(string target, string id, string description, bool needsArcherEditor)
    {
        var dependencies = new JsonArray
        {
            new JsonObject { ["name"] = "FortRise", ["version"] = "5.0.0" },
            new JsonObject { ["name"] = "FortRise.Content", ["version"] = "5.0.0" }
        };
        if (needsArcherEditor)
            dependencies.Add(new JsonObject { ["name"] = "ArcherEditor", ["version"] = "2.0.0" });

        var meta = new JsonObject
        {
            ["name"] = id,
            ["description"] = description,
            ["version"] = "1.0.0",
            ["dependencies"] = dependencies
        };
        File.WriteAllText(Path.Combine(target, "meta.json"), meta.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    // ---------------------------------------------------------------- generate from memory

    private sealed class Exporter
    {
        private readonly string target;
        private readonly string folder;
        private readonly Dictionary<Subtexture, string> done = new();
        private readonly HashSet<string> names = new(StringComparer.OrdinalIgnoreCase);

        public Exporter(string target, string folder)
        {
            this.target = target;
            this.folder = folder;
        }

        // writes the subtexture as a png and returns the mod-relative path the xml should reference
        public string Export(Subtexture subtexture, string suggested)
        {
            if (done.TryGetValue(subtexture, out var existing))
                return existing;

            var name = Regex.Replace(suggested, "[^A-Za-z0-9_]", "_");
            var unique = name;
            for (var i = 2; !names.Add(unique); i++)
                unique = name + i;

            var relative = $"{folder}/{unique}.png";
            var path = Path.Combine(target, relative.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);

            var rect = subtexture.Rect;
            var pixels = new Microsoft.Xna.Framework.Color[rect.Width * rect.Height];
            subtexture.Texture2D.GetData(0, rect, pixels, 0, pixels.Length);

            using var copy = new Texture2D(Engine.Instance.GraphicsDevice, rect.Width, rect.Height);
            copy.SetData(pixels);
            using (var stream = File.Create(path))
                copy.SaveAsPng(stream, rect.Width, rect.Height);

            done[subtexture] = relative;
            return relative;
        }
    }

    private static readonly string[] SpriteTextureTags = { "Texture", "RedTexture", "BlueTexture", "RedTeam", "BlueTeam", "Flash" };

    private static void FromMemory(ArcherData data, string id, string name0, string name1, string target, List<string> messages)
    {
        var exporter = new Exporter(target, $"Content/Atlas/{id}");
        var atlasFolder = Path.Combine(target, "Content", "Atlas");
        Directory.CreateDirectory(Path.Combine(atlasFolder, "GameData"));
        Directory.CreateDirectory(Path.Combine(atlasFolder, "SpriteData"));

        // ---- sprite data: one file per container, ids local to the new mod ----
        var spriteDoc = NewSpriteDataDocument();
        var menuDoc = NewSpriteDataDocument();
        var corpseDoc = NewSpriteDataDocument();
        var newIds = new Dictionary<string, string>();

        string AddSprite(XmlDocument doc, SpriteData source, string sourceId, string newId, Atlas[] atlases)
        {
            var key = $"{doc.GetHashCode()}|{sourceId}";
            if (newIds.TryGetValue(key, out var existing))
                return existing;

            if (string.IsNullOrEmpty(sourceId) || !source.Contains(sourceId))
                throw new Exception($"Sprite '{sourceId}' is not in the sprite data.");

            var clone = (XmlElement)doc.ImportNode(source.GetXML(sourceId), true);
            clone.SetAttribute("id", newId);
            foreach (var tag in SpriteTextureTags)
            {
                var child = clone[tag];
                if (child == null) continue;

                var textureName = child.InnerText.Trim();
                var subtexture = atlases.Where(a => a != null && a.Contains(textureName)).Select(a => a[textureName]).FirstOrDefault();
                if (subtexture == null)
                {
                    messages.Add($"Warning: texture '{textureName}' of {newId} was not found, left as it is.");
                    continue;
                }
                child.InnerText = exporter.Export(subtexture, $"{newId}_{tag}");
            }

            doc.DocumentElement!.AppendChild(clone);
            newIds[key] = newId;
            return newId;
        }

        var mainAtlases = new[] { TFGame.Atlas };
        var body = AddSprite(spriteDoc, TFGame.SpriteData, data.Sprites.Body, "Body", mainAtlases);
        var headNormal = AddSprite(spriteDoc, TFGame.SpriteData, data.Sprites.HeadNormal, "HeadNormal", mainAtlases);
        var headNoHat = AddSprite(spriteDoc, TFGame.SpriteData, data.Sprites.HeadNoHat, "HeadNoHat", mainAtlases);
        var headCrown = AddSprite(spriteDoc, TFGame.SpriteData, data.Sprites.HeadCrown, "HeadCrown", mainAtlases);
        var bow = AddSprite(spriteDoc, TFGame.SpriteData, data.Sprites.Bow, "Bow", mainAtlases);
        var headBack = string.IsNullOrEmpty(data.Sprites.HeadBack)
            ? null
            : AddSprite(spriteDoc, TFGame.SpriteData, data.Sprites.HeadBack, "HeadBack", mainAtlases);
        var gemGameplay = AddSprite(spriteDoc, TFGame.SpriteData, data.Gems.Gameplay, "GemGameplay", mainAtlases);
        var gemMenu = AddSprite(menuDoc, TFGame.MenuSpriteData, data.Gems.Menu, "GemMenu", new[] { TFGame.MenuAtlas });
        var corpse = AddSprite(corpseDoc, TFGame.CorpseSpriteData, data.Corpse, "Corpse", mainAtlases);

        spriteDoc.Save(Path.Combine(atlasFolder, "SpriteData", "spriteData.xml"));
        menuDoc.Save(Path.Combine(atlasFolder, "SpriteData", "menuSpriteData.xml"));
        corpseDoc.Save(Path.Combine(atlasFolder, "SpriteData", "corpseSpriteData.xml"));

        // ---- archerData.xml ----
        var archerDoc = new XmlDocument();
        archerDoc.AppendChild(archerDoc.CreateXmlDeclaration("1.0", "utf-8", null));
        var archers = archerDoc.AppendChild(archerDoc.CreateElement("Archers"))!;
        var archer = (XmlElement)archers.AppendChild(archerDoc.CreateElement("Archer"))!;
        archer.SetAttribute("id", id);

        void Text(XmlElement parent, string name, string value) =>
            parent.AppendChild(archerDoc.CreateElement(name)).InnerText = value;

        Text(archer, "Name0", name0.Trim());
        Text(archer, "Name1", name1.Trim());
        Text(archer, "ColorA", ArcherXml.Hex(data.ColorA));
        Text(archer, "ColorB", ArcherXml.Hex(data.ColorB));
        Text(archer, "LightbarColor", ArcherXml.Hex(data.LightbarColor));
        Text(archer, "Aimer", exporter.Export(data.Aimer, "aimer"));
        Text(archer, "Corpse", corpse);
        Text(archer, "Genders", data.Gender.ToString());
        Text(archer, "StartNoHat", data.StartNoHat.ToString().ToLowerInvariant());
        Text(archer, "SFX", data.SFXID.ToString());
        if (!string.IsNullOrEmpty(data.VictoryMusic))
            Text(archer, "VictoryMusic", data.VictoryMusic);
        if (data.Hair)
            Text(archer, "Hair", "true");

        var sprites = (XmlElement)archer.AppendChild(archerDoc.CreateElement("Sprites"))!;
        Text(sprites, "Body", body);
        Text(sprites, "HeadNormal", headNormal);
        Text(sprites, "HeadNoHat", headNoHat);
        Text(sprites, "HeadCrown", headCrown);
        Text(sprites, "Bow", bow);
        if (headBack != null)
            Text(sprites, "HeadBack", headBack);

        var portraits = (XmlElement)archer.AppendChild(archerDoc.CreateElement("Portraits"))!;
        Text(portraits, "NotJoined", exporter.Export(data.Portraits.NotJoined, "portrait_notJoined"));
        Text(portraits, "Joined", exporter.Export(data.Portraits.Joined, "portrait_joined"));
        Text(portraits, "Win", exporter.Export(data.Portraits.Win, "portrait_win"));
        Text(portraits, "Lose", exporter.Export(data.Portraits.Lose, "portrait_lose"));

        var statue = (XmlElement)archer.AppendChild(archerDoc.CreateElement("Statue"))!;
        Text(statue, "Image", exporter.Export(data.Statue.Image, "statue"));
        Text(statue, "Glow", exporter.Export(data.Statue.Glow, "statue_glow"));

        if (data.Hat.Normal != null)
        {
            var hat = (XmlElement)archer.AppendChild(archerDoc.CreateElement("Hat"))!;
            Text(hat, "Material", data.Hat.Material.ToString());
            Text(hat, "Normal", exporter.Export(data.Hat.Normal, "hat"));
            Text(hat, "Red", exporter.Export(data.Hat.Red ?? data.Hat.Normal, "hat_red"));
            Text(hat, "Blue", exporter.Export(data.Hat.Blue ?? data.Hat.Normal, "hat_blue"));
        }

        var gems = (XmlElement)archer.AppendChild(archerDoc.CreateElement("Gems"))!;
        Text(gems, "Menu", gemMenu);
        Text(gems, "Gameplay", gemGameplay);

        archerDoc.Save(Path.Combine(atlasFolder, "GameData", "archerData.xml"));

        // ---- archerCustomData.xml: what this editor adds on top (hair, particles, wings, ghost) ----
        var needsEditor = WriteCustomData(data, id, target, exporter, messages);

        WriteMeta(target, id, $"Copy of {ArcherNames.Full(data.Name0, data.Name1)}", needsEditor);
        messages.Add("Exported the sprite data, portraits and textures as png files.");
    }

    private static XmlDocument NewSpriteDataDocument()
    {
        var doc = new XmlDocument();
        doc.AppendChild(doc.CreateXmlDeclaration("1.0", "utf-8", null));
        doc.AppendChild(doc.CreateElement("SpriteData"));
        return doc;
    }

    private static bool WriteCustomData(ArcherData data, string id, string target, Exporter exporter, List<string> messages)
    {
        var fragments = new List<string>();

        var hairs = HairFeature.GetHairs(data);
        if (hairs != null)
            fragments.Add(HairWindow.ToXml(hairs));

        var particles = ParticlesFeature.GetAllParticles(data);
        if (particles.Count > 0)
            fragments.Add(ParticlesWindow.ToXml(particles));

        if (WingsFeature.TryGet(data, out var wingsTexture, out var wingsColor))
            fragments.Add(TextureColorXml("Wings", wingsTexture == null ? null : exporter.Export(wingsTexture, "wings"), wingsColor));

        if (GhostFeature.TryGet(data, out var ghostTexture, out var ghostColor))
            fragments.Add(TextureColorXml("Ghost", ghostTexture == null ? null : exporter.Export(ghostTexture, "ghost"), ghostColor));

        if (ArcherDecorationRegistry.Decorations.Any(d => d.ArcherData == data &&
                d.Xml.ChildNodes.OfType<XmlElement>().Any(c => c.Name is "Taunt" or "Layers" or "PortraitLayers" or "Skin")))
            messages.Add("Warning: taunt / layers / portrait layers are not copied from memory (they need their own assets).");

        if (fragments.Count == 0)
            return false;

        var doc = new XmlDocument();
        doc.AppendChild(doc.CreateXmlDeclaration("1.0", "utf-8", null));
        var root = doc.AppendChild(doc.CreateElement("Archers"))!;
        var entry = (XmlElement)root.AppendChild(doc.CreateElement("Archer"))!;
        entry.SetAttribute("id", id);
        entry.InnerXml = string.Join("\n", fragments);

        var directory = Path.Combine(target, "ArcherCustomData");
        Directory.CreateDirectory(directory);
        doc.Save(Path.Combine(directory, "archerCustomData.xml"));
        messages.Add("Wrote ArcherCustomData/archerCustomData.xml (needs the ArcherEditor mod).");
        return true;
    }

    private static string TextureColorXml(string name, string? texture, Microsoft.Xna.Framework.Color? color)
    {
        var parts = new List<string>();
        if (texture != null) parts.Add($"<Texture>{texture}</Texture>");
        if (color.HasValue) parts.Add($"<Color>{ArcherXml.Hex(color.Value)}</Color>");
        return $"<{name}>{string.Concat(parts)}</{name}>";
    }
}
