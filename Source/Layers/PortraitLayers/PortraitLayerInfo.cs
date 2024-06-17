using ArcherLoaderMod.Layers;
using Microsoft.Xna.Framework;

namespace ArcherLoaderMod.Source.Layers.PortraitLayers
{
    public class PortraitLayerInfo
    {
        public PortraitLayersAttachType AttachTo;
        
        public string Sprite;
        public Vector2 Position;
        public Color Color;
        public bool ToScale;
        public Vector2 FloatAnimation;
        public int FloatAnimationRate;
        public Vector2 ScaleAnimation;
        public Vector2 RotationAnimation;
        
        public bool IsColorA;
        public bool IsColorB;
        public bool IsTeamColor;
        public bool IsRainbowColor;
        public int RainbowOffset { get; set; }
        public float RainbowSpeed { get; set; }
        
        public PortraitLayerInfo()
        {
        }
    }
}
