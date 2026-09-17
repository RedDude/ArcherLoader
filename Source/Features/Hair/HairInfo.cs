#nullable enable
using Microsoft.Xna.Framework;

namespace ArcherLoaderMod.Source.Features.Hair
{
    public sealed class HairInfo
    {
        public Vector2 Position;
        public string HairSprite = "player/hair";
        public string HairEndSprite = "player/hairEnd";
        public int SineValue = 30;
        public int Size = 1;
        public int Links = 2;
        public float LinksDist = 1;
        public float Alpha = 1;
        public Color Color = Color.White;
        public Vector2 DuckingOffset;
        public Color EndColor = Color.Transparent;
        public bool Gradient;
        public Color OutlineColor = Color.Black;
        public bool Prismatic;
        public bool PrismaticEnd;
        public float PrismaticTime = 1;
        public int GradientOffset;
        public bool VisibleWithHat = true;
        public bool Rainbow;
        public Vector2 WithHatOffset;
    }
}
