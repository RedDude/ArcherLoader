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
        
        public bool IsRainbowColor;
        
        public PortraitLayerInfo()
        {
        }

    }
}
