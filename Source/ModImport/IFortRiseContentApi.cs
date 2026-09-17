#nullable enable
using FortRise;

namespace ArcherLoaderMod.Source.ModImport
{
    // Subset of FortRise.Content's public API, obtained through Context.Interop.GetApi (proxied by FortRise).
    public interface IFortRiseContentApi
    {
        ILoaderAPI LoaderApi { get; }

        public interface ILoaderAPI
        {
            IContentConfiguration? GetContentConfiguration(ModuleMetadata metadata);

            public interface IContentConfiguration
            {
                ILoader? GetLoader(string loaderID);
            }

            public interface ILoader
            {
                string[]? Path { get; }
                bool Enabled { get; }
            }
        }
    }
}
