#nullable enable
using System.Xml;
using FortRise;
using TowerFall;

namespace ArcherLoaderMod.Source.Features
{
    // A standalone feature that decorates archers FortRise has already registered.
    public interface IArcherFeature
    {
        // Name used in logs.
        string Name { get; }

        // False when the user disabled the feature in settings; it is then never loaded nor applied.
        bool Enabled { get; }

        // Called once, before any mod is loaded. Install patches here.
        void Load(IModuleContext context);

        // Called for each archerLoaderData.xml entry once its target ArcherData exists.
        // Return false when the element has nothing for this feature.
        bool Decorate(ArcherDecoration decoration);
    }

    public sealed class ArcherDecoration
    {
        public ArcherData ArcherData { get; init; } = null!;

        // Registry name of the decorated archer ("Mod/id"), or the base archer name (e.g. "Green").
        public string TargetName { get; init; } = null!;

        public ArcherData.ArcherTypes ArcherType { get; init; }
        public XmlElement Xml { get; init; } = null!;

        // Mod that declared this decoration (can differ from the mod owning the archer).
        public IModContent ModContent { get; init; } = null!;
        public IResourceInfo Resource { get; init; } = null!;

        // Absolute path of the xml on disk when the mod is in folder format (editable), null when zipped.
        public string? EditablePath { get; init; }

        // Resolves a texture name declared by the mod: "{mod}/{name}" first, then the raw name.
        public Monocle.Subtexture? FindTexture(string name)
        {
            var prefixed = $"{ModContent.Metadata.Name}/{name}";
            if (TFGame.Atlas.Contains(prefixed))
                return TFGame.Atlas[prefixed];
            return TFGame.Atlas.Contains(name) ? TFGame.Atlas[name] : null;
        }
    }
}
