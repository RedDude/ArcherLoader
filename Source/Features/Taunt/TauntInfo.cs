#nullable enable
using Monocle;

namespace ArcherLoaderMod.Source.Features.Taunt
{
    // Resolved once per archer: which spriteData container to reuse for the taunt animation, and which
    // per-team textures to swap onto it (the same technique the game uses for hats: one shared animation,
    // retextured per archer).
    public sealed class TauntInfo
    {
        public string SpriteId = null!;

        public bool HasTaunt;
        public bool HasTauntNoHat;
        public bool HasTauntCrown;

        public string? TauntTexture;
        public string? NoHatTexture;
        public string? CrownTexture;

        public string? TauntTextureRed;
        public string? NoHatTextureRed;
        public string? CrownTextureRed;

        public string? TauntTextureBlue;
        public string? NoHatTextureBlue;
        public string? CrownTextureBlue;

        public bool HasTauntRed;
        public bool HasTauntNoHatRed;
        public bool HasTauntCrownRed;

        public bool HasTauntBlue;
        public bool HasTauntNoHatBlue;
        public bool HasTauntCrownBlue;

        public bool SelfDestruction;
        public SFX? Sound;
    }
}
