#nullable enable
using System;
using ArcherEditorMod.Rainbow;
using Microsoft.Xna.Framework;
using Monocle;
using MonoMod.Utils;
using TowerFall;

namespace ArcherEditorMod.Source.Features.PortraitLayers
{
    public sealed class PortraitLayerSpriteComponent : Component
    {
        public readonly PortraitLayerInfo LayerInfo;
        public Sprite<string>? LayerSprite;
        private readonly ArcherData archerData;

        public float Rate = (float)Math.PI / 64f;
        private float selectionLerp = 1f;

        public float Value { get; private set; }
        public float ValueOverTwo { get; private set; }
        public float TwoValue { get; private set; }
        public float Counter { get; private set; }

        public PortraitLayerSpriteComponent(PortraitLayerInfo layerInfo, ArcherData archerData, bool active,
            bool visible) : base(active, visible)
        {
            LayerInfo = layerInfo;
            this.archerData = archerData;
        }

        public override void Added()
        {
            LayerSprite = LayerInfo.IsMenuSprite
                ? TFGame.MenuSpriteData.GetSpriteString(LayerInfo.Sprite)
                : TFGame.SpriteData.GetSpriteString(LayerInfo.Sprite);
            if (LayerSprite == null)
                return;

            LayerSprite.Color = LayerInfo.Color;

            if (LayerInfo.FloatAnimationRate != 0)
                SetFrames(LayerInfo.FloatAnimationRate);

            if (LayerInfo.IsColorA)
                LayerSprite.Color = archerData.ColorA;
            if (LayerInfo.IsColorB)
                LayerSprite.Color = archerData.ColorB;

            DynamicData.For(LayerSprite).Set("Parent", Entity);
            if (LayerInfo.AttachTo is PortraitLayersAttachType.Won or PortraitLayersAttachType.Lose)
                DynamicData.For(LayerSprite).Set("Entity", Entity);

            base.Added();
        }

        public override void Update()
        {
            if (LayerSprite == null)
                return;

            if (LayerInfo.IsRainbowColor)
                LayerSprite.Color = RainbowManager.GetColor(LayerInfo.RainbowOffset, LayerInfo.RainbowSpeed);

            Counter = (float)((Counter + Rate * Engine.TimeMult) % 25.1327419281006);
            Value = (float)Math.Sin(Counter);
            ValueOverTwo = (float)Math.Sin(Counter / 2.0);
            TwoValue = (float)Math.Sin(Counter * 2.0);

            base.Update();
        }

        public void SetFrames(int framesPerWave) => Rate = 6.283185f / framesPerWave;

        public override void Render()
        {
            if (LayerSprite == null)
                return;

            if (Parent is ArcherPortrait portrait)
            {
                var portraitImage = DynamicData.For(portrait).Get<Image>("portrait");
                var offset = DynamicData.For(portrait).Get<Vector2>("offset");
                var lastShake = DynamicData.For(portrait).Get<Vector2>("lastShake");

                selectionLerp = Math.Min(1f, selectionLerp + 0.1f * Engine.TimeMult);
                var imageY = (float)Math.Round(MathHelper.Lerp(0f, 1f - (Value * LayerInfo.FloatAnimation.Y), selectionLerp));

                LayerSprite.Position = ((RollcallElement)portrait.Parent).Position + offset + lastShake + LayerInfo.Position + new Vector2(0, imageY);
                LayerSprite.Scale = portraitImage.Scale;
            }

            if (Parent is VersusPlayerMatchResults result)
            {
                var portraitImage = DynamicData.For(result).Get<Image>("portrait");
                LayerSprite.Origin = portraitImage.Origin;
                LayerSprite.Position = portraitImage.Position;
            }

            LayerSprite.Render();
            base.Render();
        }
    }
}
