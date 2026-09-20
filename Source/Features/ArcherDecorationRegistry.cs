#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Xml;
using ArcherEditorMod.Source.ModImport;
using FortRise;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework.Graphics;
using Monocle;
using TowerFall;

namespace ArcherEditorMod.Source.Features
{
    // Reads archerCustomData.xml from mods that (optionally) depend on ArcherEditor and, once FortRise has
    // registered every archer, hands each entry to the standalone features.
    //
    // <Archers>
    //   <Archer id="Kermit">             archer "Kermit" of this mod
    //   <AltArcher id="OtherMod/Frog">   alt archer of another mod
    //   <SecretArcher id="Green">        base game archer
    // </Archers>
    public static class ArcherDecorationRegistry
    {
        private const string ContentModName = "FortRise.Content";
        private const string LoaderId = "archerCustomData";
        private const string DefaultPath = "ArcherCustomData/archerCustomData.xml";

        private static readonly string[] BaseArcherNames =
            ["Green", "Blue", "Pink", "Orange", "White", "Yellow", "Cyan", "Purple", "Red"];

        private sealed record PendingDecoration(
            string Id, ArcherData.ArcherTypes ArcherType, XmlElement Xml,
            IModContent ModContent, IResourceInfo Resource, string? EditablePath, IModRegistry? Registry);

        private static readonly List<IArcherFeature> features = new();
        private static readonly List<PendingDecoration> pending = new();
        private static readonly List<ArcherDecoration> decorations = new();

        private static IModuleContext context = null!;
        private static ILogger logger = null!;
        private static IFortRiseContentApi? contentApi;
        private static bool contentApiResolved;

        public static IReadOnlyList<ArcherDecoration> Decorations => decorations;

        public static void Load(IModuleContext moduleContext, ILogger modLogger, params IArcherFeature[] archerFeatures)
        {
            context = moduleContext;
            logger = modLogger;

            foreach (var feature in archerFeatures)
            {
                if (!feature.Enabled)
                    continue;

                feature.Load(context);
                features.Add(feature);
            }

            context.Events.OnBeforeModInstantiation += OnBeforeModInstantiation;
            context.Events.OnModLoadStateFinished += (_, state) =>
            {
                // Archers are pushed into ArcherData arrays right before the Initialize state finishes.
                if (state == LoadState.Initialize)
                    ApplyAll();
            };
        }

        // ArcherEditor's own OnBeforeModInstantiation has already fired by the time FortEntrance's
        // constructor runs Load() and subscribes to it, so it can never see its own bundled content
        // through that event. FortEntrance calls this directly instead, right after Load(), to register
        // ArcherEditor's built-in atlas (Content/CustomArchers/CustomGhostsForBaseArchers/atlas.xml) and
        // read its own ArcherCustomData/archerCustomData.xml the same way any dependent mod's would be.
        public static void LoadSelfContent(IModContent content, IModRegistry registry)
        {
            try
            {
                foreach (var resource in content.Root.EnumerateChildrens(SelfAtlasPath))
                    LoadAtlas(content, registry, resource);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to load {file}", SelfAtlasPath);
            }

            try
            {
                foreach (var resource in content.Root.EnumerateChildrens(DefaultPath))
                    ReadFile(content, resource, registry);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to read {file}", DefaultPath);
            }
        }

        private const string SelfAtlasPath = "Content/CustomArchers/CustomGhostsForBaseArchers/atlas.xml";
        private static readonly Dictionary<string, Monocle.Texture> selfTextureCache = new();

        private static void LoadAtlas(IModContent content, IModRegistry registry, IResourceInfo res)
        {
            var xml = res.Xml ?? throw new Exception($"Failed to load Xml file {res.Path}.");
            var textureAtlas = xml["TextureAtlas"] ?? throw new Exception("Missing TextureAtlas element.");

            if (!content.Root.TryGetRelativePath(Path.ChangeExtension(res.Path, "png"), out var pngResource))
                return;

            foreach (XmlElement subtexture in textureAtlas.GetElementsByTagName("SubTexture"))
            {
                var name = subtexture.GetAttribute("name");
                var x = int.Parse(subtexture.GetAttribute("x"));
                var y = int.Parse(subtexture.GetAttribute("y"));
                var width = int.Parse(subtexture.GetAttribute("width"));
                var height = int.Parse(subtexture.GetAttribute("height"));

                registry.Subtextures.RegisterTexture(name, () =>
                {
                    ref var texture = ref CollectionsMarshal.GetValueRefOrAddDefault(selfTextureCache, res.RootPath, out bool exists);
                    if (!exists)
                    {
                        using var stream = pngResource.Stream;
                        var tex2D = Texture2D.FromStream(Engine.Instance.GraphicsDevice, stream);
                        texture = new Monocle.Texture(tex2D);
                    }

                    return new Subtexture(texture, new Microsoft.Xna.Framework.Rectangle(x, y, width, height));
                }, SubtextureAtlasDestination.Atlas);
            }
        }

        private static void OnBeforeModInstantiation(object? sender, BeforeModInstantiationEventArgs e)
        {
            var content = e.ModContent;
            if (!DependsOnArcherEditor(content.Metadata))
                return;

            try
            {
                foreach (var path in GetPaths(content))
                {
                    foreach (var resource in content.Root.EnumerateChildrens(path))
                        ReadFile(content, resource, e.Context.Registry);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[{modName}] Failed to read {file}", content.Metadata.Name, LoaderId);
            }
        }

        // FortRise's Interop.IsModDepends only looks at required dependencies, optional ones count too here.
        private static bool DependsOnArcherEditor(ModuleMetadata metadata)
        {
            var name = FortEntrance.Instance.Meta.Name;
            foreach (var dependency in metadata.Dependencies ?? [])
            {
                if (dependency.Name == name)
                    return true;
            }

            foreach (var dependency in metadata.OptionalDependencies ?? [])
            {
                if (dependency.Name == name)
                    return true;
            }

            return false;
        }

        private static IEnumerable<string> GetPaths(IModContent content) =>
            GetLoaderPaths(content, LoaderId, DefaultPath);

        // Paths a mod declared for one of FortRise.Content's loaders (content.json), or the default.
        private static IEnumerable<string> GetLoaderPaths(IModContent content, string loaderId, string defaultPath)
        {
            // Only consult FortRise.Content's own loader configuration API when the mod actually ships a
            // content.json: that API only recognizes FortRise.Content's own built-in loader names and
            // logs an error for any id it doesn't own, which "archerCustomData" never will be.
            if (!content.Root.ExistsRelativePath("content.json"))
                return [defaultPath];

            if (!contentApiResolved)
            {
                contentApiResolved = true;
                try
                {
                    contentApi = context.Interop.GetApi<IFortRiseContentApi>(ContentModName);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Could not get {mod} API, using the default {file} path", ContentModName, loaderId);
                }
            }

            // content.json may declare a custom loader to move or split the file.
            var loader = contentApi?.LoaderApi.GetContentConfiguration(content.Metadata)?.GetLoader(loaderId);
            if (loader == null)
                return [defaultPath];

            if (!loader.Enabled || loader.Path == null)
                return [];

            return loader.Path;
        }

        public sealed record ArcherSource(
            string EntryName, string ModName, string LocalId, ArcherEntryType Type, string? BaseName,
            IModContent? Content, string? ModDirectory, string? ArcherDataFile);

        // Backtracks an ArcherData to the mod that registered it and, for mods on disk, to the archerData.xml
        // holding its element. Null for archers FortRise did not register (the base game's).
        public static ArcherSource? FindArcherSource(ArcherData data)
        {
            foreach (var entry in context.Registry.Archers.RegisteredArchers.Values)
            {
                if (entry.ArcherData != data)
                    continue;

                var slash = entry.Name.IndexOf('/');
                var modName = slash > 0 ? entry.Name[..slash] : "";
                var localId = slash > 0 ? entry.Name[(slash + 1)..] : entry.Name;
                var mod = modName.Length > 0 ? context.Interop.GetMod(modName) : null;
                var directory = mod != null && !string.IsNullOrEmpty(mod.Metadata.PathDirectory) ? mod.Metadata.PathDirectory : null;

                string? file = null;
                if (mod != null && directory != null)
                {
                    foreach (var path in GetLoaderPaths(mod.Content, "archerData", "Content/Atlas/GameData/archerData.xml"))
                    {
                        if (!mod.Content.Root.TryGetRelativePath(path, out var resource))
                            continue;

                        var archers = resource.Xml?["Archers"];
                        if (archers == null)
                            continue;

                        foreach (XmlNode node in archers)
                        {
                            if (node is XmlElement element && element.GetAttribute("id") == localId)
                            {
                                file = Path.Combine(directory, path.TrimStart('/', '\\'));
                                break;
                            }
                        }

                        if (file != null)
                            break;
                    }
                }

                var baseName = entry.Configuration.AltFor?.Name ?? entry.Configuration.SecretFor?.Name;
                return new ArcherSource(entry.Name, modName, localId, entry.Type, baseName, mod?.Content, directory, file);
            }

            return null;
        }

        // Paths of the archerCustomData.xml files a mod reads (content.json may move them).
        public static IReadOnlyList<string> GetCustomDataPaths(IModContent content) => new List<string>(GetPaths(content));

        // The id an archerCustomData.xml entry uses for this archer: "local" is the short id inside the archer's own
        // mod, otherwise the full "Mod/id" name; base game archers use their name ("Green").
        public static string? DecorationId(ArcherData data, ArcherSource? source, bool local)
        {
            if (source != null)
                return local ? source.LocalId : source.EntryName;

            var group = Array.IndexOf(ArcherData.AltArchers, data) >= 0 ? ArcherData.AltArchers
                : Array.IndexOf(ArcherData.SecretArchers, data) >= 0 ? ArcherData.SecretArchers
                : ArcherData.Archers;
            var index = Array.IndexOf(group, data);
            return index >= 0 && index < BaseArcherNames.Length ? BaseArcherNames[index] : null;
        }

        public static ArcherData.ArcherTypes TypeOf(ArcherData data) =>
            Array.IndexOf(ArcherData.AltArchers, data) >= 0 ? ArcherData.ArcherTypes.Alt
            : Array.IndexOf(ArcherData.SecretArchers, data) >= 0 ? ArcherData.ArcherTypes.Secret
            : ArcherData.ArcherTypes.Normal;

        // The name FortRise expects in an Alt="" / Secret="" attribute for a given archer.
        public static string? ArcherReferenceName(ArcherData data)
        {
            foreach (var entry in context.Registry.Archers.RegisteredArchers.Values)
            {
                if (entry.ArcherData == data)
                    return entry.Name;
            }

            var index = Array.IndexOf(ArcherData.Archers, data);
            return index >= 0 && index < BaseArcherNames.Length ? BaseArcherNames[index] : null;
        }

        // Backtracks a registered sprite id ("ModName/PlayerBody") to the spriteData.xml on disk that defined it.
        // Null for base game sprites and for mods that are zipped (nothing editable on disk).
        public static string? FindSpriteDataFile(string spriteId, out string localId)
        {
            localId = spriteId;
            var slash = spriteId.IndexOf('/');
            if (slash <= 0)
                return null;

            var modName = spriteId[..slash];
            localId = spriteId[(slash + 1)..];

            var mod = context.Interop.GetMod(modName);
            if (mod == null || string.IsNullOrEmpty(mod.Metadata.PathDirectory))
                return null;

            foreach (var path in GetLoaderPaths(mod.Content, "spriteData", "Content/Atlas/SpriteData/spriteData.xml"))
            {
                if (!mod.Content.Root.TryGetRelativePath(path, out var resource))
                    continue;

                foreach (XmlNode node in resource.Xml?["SpriteData"] ?? (XmlNode)new XmlDocument())
                {
                    if (node is XmlElement element && element.GetAttribute("id") == localId)
                        return Path.Combine(mod.Metadata.PathDirectory, path.TrimStart('/', '\\'));
                }
            }

            return null;
        }

        private static void ReadFile(IModContent content, IResourceInfo resource, IModRegistry? registry)
        {
            var modName = content.Metadata.Name;
            XmlDocument? xml;
            try
            {
                xml = resource.Xml;
            }
            catch (Exception ex)
            {
                logger.LogError("[{modName}] {path} is not valid XML: {error}", modName, resource.Path, ex.Message);
                return;
            }

            var archers = xml?["Archers"];
            if (archers == null)
            {
                logger.LogError("[{modName}] {path} is missing the Archers element", modName, resource.Path);
                return;
            }

            var editablePath = string.IsNullOrEmpty(content.Metadata.PathDirectory)
                ? null
                : Path.Combine(content.Metadata.PathDirectory, resource.Path.TrimStart('/', '\\'));

            foreach (var node in archers)
            {
                if (node is not XmlElement element)
                    continue;

                ArcherData.ArcherTypes archerType;
                switch (element.Name)
                {
                    case "Archer": archerType = ArcherData.ArcherTypes.Normal; break;
                    case "AltArcher": archerType = ArcherData.ArcherTypes.Alt; break;
                    case "SecretArcher": archerType = ArcherData.ArcherTypes.Secret; break;
                    default:
                        logger.LogWarning("[{modName}] {path}: unknown element <{element}> skipped", modName, resource.Path, element.Name);
                        continue;
                }

                var id = element.GetAttribute("id");
                if (string.IsNullOrEmpty(id))
                {
                    logger.LogWarning("[{modName}] {path}: <{element}> without an 'id' attribute skipped", modName, resource.Path, element.Name);
                    continue;
                }

                pending.Add(new PendingDecoration(id, archerType, element, content, resource, editablePath, registry));
            }
        }

        // one line per archer a mod registered (the base game's are skipped): "NAME SUBNAME (Alt)  [Mod/id]"
        private static void LogLoadedArchers()
        {
            foreach (var entry in context.Registry.Archers.RegisteredArchers.Values)
            {
                if (!entry.Name.Contains('/'))
                    continue;

                var archer = entry.ArcherData;
                var name = archer == null ? "?" : $"{archer.Name0} {archer.Name1}".Trim();
                logger.LogInformation("Archer loaded: {name} ({type})  [{id}]", name, entry.Type, entry.Name);
            }
        }

        private static void ApplyAll()
        {
            LogLoadedArchers();

            foreach (var entry in pending)
            {
                var modName = entry.ModContent.Metadata.Name;
                if (!TryResolve(entry, out var archerData, out var targetName))
                {
                    logger.LogError("[{modName}] {type} '{id}' not found, decoration skipped", modName, entry.Xml.Name, entry.Id);
                    continue;
                }

                var decoration = new ArcherDecoration
                {
                    ArcherData = archerData,
                    TargetName = targetName,
                    ArcherType = entry.ArcherType,
                    Xml = entry.Xml,
                    ModContent = entry.ModContent,
                    Resource = entry.Resource,
                    EditablePath = entry.EditablePath,
                    Registry = entry.Registry
                };
                decorations.Add(decoration);

                var applied = new List<string>();
                foreach (var feature in features)
                {
                    try
                    {
                        if (feature.Decorate(decoration))
                            applied.Add(feature.Name);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError("[{modName}] {feature} failed for '{id}': {error}", modName, feature.Name, entry.Id, ex.Message);
                    }
                }

                logger.LogInformation("[{modName}] {target} decorated with: {features}", modName, targetName,
                    applied.Count == 0 ? "nothing" : string.Join(", ", applied));
            }

            pending.Clear();
        }

        private static bool TryResolve(PendingDecoration entry, out ArcherData archerData, out string targetName)
        {
            archerData = null!;
            targetName = entry.Id;

            var archers = entry.ArcherType switch
            {
                ArcherData.ArcherTypes.Alt => ArcherData.AltArchers,
                ArcherData.ArcherTypes.Secret => ArcherData.SecretArchers,
                _ => ArcherData.Archers
            };

            int index = -1;
            var registry = context.Registry;
            var registryName = entry.Id.Contains('/') ? entry.Id : $"{entry.ModContent.Metadata.Name}/{entry.Id}";
            var archerEntry = registry?.Archers.GetArcher(registryName);
            if (archerEntry != null)
            {
                index = archerEntry.Index;
                targetName = registryName;
            }
            else
            {
                index = Array.FindIndex(BaseArcherNames, n => string.Equals(n, entry.Id, StringComparison.OrdinalIgnoreCase));
            }

            if (index < 0 || index >= archers.Length || archers[index] == null)
                return false;

            archerData = archers[index];
            return true;
        }
    }
}
