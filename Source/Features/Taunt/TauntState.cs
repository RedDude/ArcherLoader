#nullable enable
using Monocle;

namespace ArcherLoaderMod.Source.Features.Taunt
{
    // Live per-player state while a taunt is in progress.
    public sealed class TauntState
    {
        public string? Animation;
        public string? TextureName;
        public Sprite<string> BodySprite = null!;
        public Sprite<string>? Sprite;
    }
}
