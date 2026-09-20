#nullable enable
using Monocle;

namespace ArcherEditorMod.Source.Features.Taunt
{
    // Live per-player state while a taunt is in progress.
    public sealed class TauntState
    {
        public string? Animation;
        public Subtexture? Texture;
        public Sprite<string> BodySprite = null!;
        public Sprite<string>? Sprite;
    }
}
