#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using ArcherLoaderMod.Source.ModImport;
using FortRise;
using Microsoft.Extensions.Logging;
using TowerFall;

namespace ArcherLoaderMod.Source.Registry
{
    // Extra archer information (taunts, layers, particles, skins...) read from other mods' archerData.xml.
    public sealed class ArcherExtraData
    {
        // Same name FortRise gives the archer in its registry: "{modName}/{id}".
        public string Name { get; init; } = null!;
        public string ID { get; init; } = null!;
        public ArcherData.ArcherTypes ArcherType { get; init; }
        public XmlElement Xml { get; init; } = null!;
        public IModContent ModContent { get; init; } = null!;
        public IResourceInfo Resource { get; init; } = null!;

        // Absolute path on disk when the mod is in folder format (editable), null when zipped.
        public string? EditablePath { get; init; }
    }

    public static class ArcherExtraDataRegistry
    {
        private const string ContentModName = "FortRise.Content";
        private const string ArcherDataLoaderId = "archerData";
        private const string DefaultArcherDataPath = "Content/Atlas/GameData/archerData.xml";

        private static readonly Dictionary<string, ArcherExtraData> byName = new();
        private static IFortRiseContentApi? contentApi;
        private static bool contentApiResolved;
        private static IModRegistry? registry;

        public static IReadOnlyDictionary<string, ArcherExtraData> All => byName;

        public static void Load(IModuleContext context)
        {
            registry = context.Registry;
            context.Events.OnBeforeModInstantiation += (_, e) => OnBeforeModInstantiation(context, e);
        }

        public static ArcherExtraData? Get(string name) => byName.GetValueOrDefault(name);

        // Resolves the extra data for a game ArcherData once FortRise has registered the archers.
        public static ArcherExtraData? Get(ArcherData archerData)
        {
            foreach (var extra in byName.Values)
            {
                var entry = registry?.Archers.GetArcher(extra.Name);
                if (entry == null)
                    continue;

                var archers = extra.ArcherType switch
                {
                    ArcherData.ArcherTypes.Alt => ArcherData.AltArchers,
                    ArcherData.ArcherTypes.Secret => ArcherData.SecretArchers,
                    _ => ArcherData.Archers
                };

                if (entry.Index < archers.Length && archers[entry.Index] == archerData)
                    return extra;
            }

            return null;
        }

        private static void OnBeforeModInstantiation(IModuleContext context, BeforeModInstantiationEventArgs e)
        {
            var content = e.ModContent;
            var ownMetadata = FortEntrance.Instance.Meta;
            if (!DependsOn(content.Metadata, ownMetadata.Name))
                return;

            var logger = FortEntrance.Instance.Logger;
            try
            {
                foreach (var path in GetArcherDataPaths(context, content.Metadata))
                {
                    foreach (var resource in content.Root.EnumerateChildrens(path))
                    {
                        LoadArcherDataFile(content, resource, logger);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[{modName}] Failed to read archer data", content.Metadata.Name);
            }
        }

        // FortRise's Interop.IsModDepends only looks at required dependencies, optional ones count too here.
        private static bool DependsOn(ModuleMetadata metadata, string modName)
        {
            foreach (var dependency in metadata.Dependencies ?? [])
            {
                if (dependency.Name == modName)
                    return true;
            }

            foreach (var dependency in metadata.OptionalDependencies ?? [])
            {
                if (dependency.Name == modName)
                    return true;
            }

            return false;
        }

        private static IEnumerable<string> GetArcherDataPaths(IModuleContext context, ModuleMetadata metadata)
        {
            if (!contentApiResolved)
            {
                contentApiResolved = true;
                try
                {
                    contentApi = context.Interop.GetApi<IFortRiseContentApi>(ContentModName);
                }
                catch (Exception ex)
                {
                    FortEntrance.Instance.Logger.LogWarning(ex, "Could not get {mod} API, using default archer data path", ContentModName);
                }
            }

            var loader = contentApi?.LoaderApi.GetContentConfiguration(metadata)?.GetLoader(ArcherDataLoaderId);
            if (loader == null)
                return [DefaultArcherDataPath];

            if (!loader.Enabled || loader.Path == null)
                return [];

            return loader.Path;
        }

        private static void LoadArcherDataFile(IModContent content, IResourceInfo resource, ILogger logger)
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
                : Path.Combine(content.Metadata.PathDirectory, resource.Path);

            foreach (var node in archers)
            {
                if (node is not XmlElement element)
                    continue;

                var archerType = element.Name switch
                {
                    "Archer" => ArcherData.ArcherTypes.Normal,
                    "AltArcher" => ArcherData.ArcherTypes.Alt,
                    "SecretArcher" => ArcherData.ArcherTypes.Secret,
                    _ => (ArcherData.ArcherTypes?)null
                };

                if (archerType == null)
                {
                    logger.LogWarning("[{modName}] {path}: unknown element <{element}> skipped", modName, resource.Path, element.Name);
                    continue;
                }

                var id = element.GetAttribute("id");
                if (string.IsNullOrEmpty(id))
                {
                    logger.LogWarning("[{modName}] {path}: <{element}> without an 'id' attribute skipped", modName, resource.Path, element.Name);
                    continue;
                }

                var name = $"{modName}/{id}";
                if (byName.ContainsKey(name))
                    logger.LogWarning("[{modName}] archer '{id}' is declared more than once, the last one wins", modName, id);

                byName[name] = new ArcherExtraData
                {
                    Name = name,
                    ID = id,
                    ArcherType = archerType.Value,
                    Xml = element,
                    ModContent = content,
                    Resource = resource,
                    EditablePath = editablePath
                };
            }
        }
    }
}
