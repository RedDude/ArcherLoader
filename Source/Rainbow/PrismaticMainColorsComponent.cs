using System;
using System.Reflection;
using System.Xml;
using ArcherLoaderMod.Layers;
using ArcherLoaderMod.Rainbow;
using Microsoft.Xna.Framework;
using Monocle;
using MonoMod.Utils;
using TowerFall;

namespace ArcherLoaderMod.Source.Layers.PortraitLayers
{
    public class PrismaticMainColorsComponent : Component
    {
        private ArcherPortrait attachedSprite;

        private ArcherData archerData;
        
        private ArcherCustomData archerDataCustom;

        public PrismaticMainColorsComponent(ArcherData archerData, ArcherCustomData archerDataCustom, bool active,
            bool visible) : base(active, visible)
        {
            this.archerData = archerData;
            this.archerDataCustom = archerDataCustom;
        }

        public override void Added()
        {
            base.Added();
        }

        public override void Update()
        {
            if (archerDataCustom.PrismaticArcher)
            {
                archerData.ColorA = RainbowManager.CurrentColor;
                archerData.ColorB = RainbowManager.CurrentColor;
            }
            base.Update();
        }

    }
}
