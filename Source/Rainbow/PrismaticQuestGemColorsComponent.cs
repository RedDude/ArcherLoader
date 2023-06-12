using System;
using System.Collections.Generic;
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
    public class PrismaticQuestGemColorsComponent : Component
    {
        private ArcherPortrait attachedSprite;

        private ArcherData archerData;
        
        private ArcherCustomData archerDataCustom;
        private QuestPlayerHUD hud;

        public PrismaticQuestGemColorsComponent(QuestPlayerHUD questPlayerHud, ArcherData archerData,
            ArcherCustomData archerDataCustom, bool active,
            bool visible) : base(active, visible)
        {
            hud = questPlayerHud;
            this.archerData = archerData;
            this.archerDataCustom = archerDataCustom;
        }

        public override void Update()
        {
            var gems = DynamicData.For(hud).Get<List<Sprite<int>>>("gems");
            foreach (var sprite in gems)
            {
                sprite.Color = RainbowManager.CurrentColor;
            }
        }
    }
}
