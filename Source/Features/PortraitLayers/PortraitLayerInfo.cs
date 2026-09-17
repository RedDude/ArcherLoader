#nullable enable
using Microsoft.Xna.Framework;

namespace ArcherLoaderMod.Source.Features.PortraitLayers
{
    public sealed class PortraitLayerInfo
    {
        public PortraitLayersAttachType AttachTo;

        public string Sprite = null!;
        public Vector2 Position;
        public Color Color = Color.White;
        public bool ToScale = true;
        public Vector2 FloatAnimation;
        public int FloatAnimationRate;
        public Vector2 ScaleAnimation;
        public Vector2 RotationAnimation;

        public bool IsColorA;
        public bool IsColorB;
        public bool IsTeamColor;
        public bool IsRainbowColor;
        public int RainbowOffset;
        public float RainbowSpeed = 1f;
    }
}
