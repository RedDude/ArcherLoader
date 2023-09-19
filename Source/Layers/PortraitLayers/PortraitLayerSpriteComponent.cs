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
    public class PortraitLayerSpriteComponent : Component
    {
        private ArcherPortrait attachedSprite;

        public PortraitLayerInfo layerInfo;
        public Sprite<string> layerSprite;
        private ArcherData archerData;

        public float Rate = (float) Math.PI / 64f;
        private float SelectionLerp = 1f;

        public float Value { get; private set; }

        public float ValueOverTwo { get; private set; }

        public float TwoValue { get; private set; }

        public float Counter { get; private set; }

        
        public PortraitLayerSpriteComponent(PortraitLayerInfo layerInfo, ArcherData archerData, bool active,
            bool visible) : base(active, visible)
        {
            this.layerInfo = layerInfo;
            this.archerData = archerData;
        }

        public override void Added()
        {
            layerSprite = TFGame.SpriteData.GetSpriteString(layerInfo.Sprite);
            layerSprite.Color = layerInfo.Color;

            if(layerInfo.FloatAnimationRate != 0)
                SetFrames(layerInfo.FloatAnimationRate);

            if (layerInfo.IsColorA || layerInfo.IsColorB)
            {
                if(layerInfo.IsColorA)
                    layerSprite.Color = archerData.ColorA;
                if(layerInfo.IsColorB)
                    layerSprite.Color = archerData.ColorB;
            }

            DynamicData.For(layerSprite).Set("Parent", Entity);
            if(layerInfo.AttachTo == PortraitLayersAttachType.Won || layerInfo.AttachTo == PortraitLayersAttachType.Lose)
                DynamicData.For(layerSprite).Set("Entity", Entity);
            base.Added();
        }

      

        public override void Update()
        {
            if (layerInfo.IsRainbowColor)
            {
                layerSprite.Color = RainbowManager.CurrentColor;//RainbowManager.GetColor(Environment.TickCount);
            }
            
            Counter = (float) ((Counter + Rate * (double) Engine.TimeMult) % 25.1327419281006);
            Value = (float) Math.Sin(Counter);
            ValueOverTwo = (float) Math.Sin(Counter / 2.0);
            TwoValue = (float) Math.Sin(Counter * 2.0);
            
            base.Update();
        }

        public void SetFrames(int framesPerWave) => Rate = 6.283185f / framesPerWave;

        
        public override void Render()
        {
            if (Parent is ArcherPortrait portrait)
            {   
                var portraitImage = DynamicData.For(portrait).Get<Image>("portrait");
                var offset = DynamicData.For(portrait).Get<Vector2>("offset");
                var lastShake = DynamicData.For(portrait).Get<Vector2>("lastShake");

                SelectionLerp = Math.Min(1f, SelectionLerp + 0.1f * Engine.TimeMult);
                float AddYMult = 1;
                var ImageY = (float)Math.Round(MathHelper.Lerp(0f,  AddYMult - Value * layerInfo.FloatAnimation.Y, SelectionLerp));
                
                layerSprite.Position = ((RollcallElement)portrait.Parent).Position + offset + lastShake + layerInfo.Position + new Vector2(0, ImageY );
                layerSprite.Scale = portraitImage.Scale;
            }

            if (Parent is VersusPlayerMatchResults result)
            {
                var portraitImage = DynamicData.For(result).Get<Image>("portrait");
                layerSprite.Origin = portraitImage.Origin;
                layerSprite.Position = portraitImage.Position;
            }

            // if (!this.joined)
            // {
            //     if ((double) this.flipEase < 0.5)
            //     {
            //         this.portraitAlt.Visible = true;
            //         this.portrait.Visible = false;
            //         this.portraitAlt.Scale.X *= MathHelper.Lerp(1f, 0.0f, this.flipEase * 2f);
            //     }
            //     else
            //     {
            //         this.portraitAlt.Visible = false;
            //         this.portrait.Visible = true;
            //         this.portrait.Scale.X *= MathHelper.Lerp(0.0f, 1f, (float) (((double) this.flipEase - 0.5) * 2.0));
            //     }
            // }

            layerSprite.Render();
            base.Render();
        }
    }
}
