#nullable enable
using ArcherEditorMod.Rainbow;
using Monocle;
using MonoMod.Utils;
using TowerFall;

namespace ArcherEditorMod.Source.Features.Layers
{
    // Same rendering logic as the old ArcherEditorMod.Layers.LayerSpriteComponent, driven by ArcherData
    // directly (ColorA/ColorB/Corpse already live there) instead of the old ArcherCustomData wrapper.
    public sealed class LayerSpriteComponent : Component
    {
        private readonly Sprite<string> attachedSprite;
        private readonly ArcherData archerData;

        public readonly LayerInfo LayerInfo;
        private Sprite<string> layerSprite = null!;

        private bool teamColorSet;
        private Player.HatStates lastHatState;

        public LayerSpriteComponent(LayerInfo layerInfo, Sprite<string> attachedSprite, ArcherData archerData,
            bool active, bool visible) : base(active, visible)
        {
            LayerInfo = layerInfo;
            this.attachedSprite = attachedSprite;
            this.archerData = archerData;
        }

        public override void Added()
        {
            string attachedSpriteInfo;
            if (Parent is Player player)
            {
                attachedSpriteInfo = LayerInfo.AttachTo == LayerAttachType.Body ? player.ArcherData.Sprites.Body :
                    LayerInfo.AttachTo == LayerAttachType.Bow ? player.ArcherData.Sprites.Bow :
                    LayerInfo.AttachTo == LayerAttachType.Head ?
                        player.HatState == Player.HatStates.Normal ? player.ArcherData.Sprites.HeadNormal :
                        player.HatState == Player.HatStates.Crown ? player.ArcherData.Sprites.HeadCrown :
                        player.ArcherData.Sprites.HeadNoHat : player.ArcherData.Sprites.HeadNormal;

                lastHatState = player.HatState;
                layerSprite = TFGame.SpriteData.GetSpriteString(attachedSpriteInfo);

                if (LayerInfo.IsTeamColor && player.Level.Session.MatchSettings.TeamMode)
                {
                    var matchVariants = player.Level.Session.MatchSettings.Variants;
                    if (player.Allegiance != Allegiance.Neutral &&
                        !matchVariants.GetCustomVariant("TeamOutline")[player.PlayerIndex])
                    {
                        layerSprite.Color = ArcherData.GetColorA(player.PlayerIndex, player.TeamColor);
                        teamColorSet = true;
                    }
                }
            }
            else if (Parent is PlayerCorpse corpse)
            {
                if (corpse.PlayerIndex == -1)
                {
                    RemoveSelf();
                    return;
                }

                layerSprite = TFGame.CorpseSpriteData.GetSpriteString(archerData.Corpse);

                if (LayerInfo.IsTeamColor && corpse.Level.Session.MatchSettings.TeamMode)
                    layerSprite.Color = ArcherData.GetColorA(corpse.PlayerIndex, corpse.TeamColor);
            }

            var atlas = TFGame.Atlas[LayerInfo.Sprite];
            if (atlas != null)
                layerSprite.SwapSubtexture(atlas);

            layerSprite.Visible = attachedSprite.Visible;
            if (!teamColorSet)
            {
                layerSprite.Color = LayerInfo.Color;
                if (LayerInfo.IsColorA)
                    layerSprite.Color = archerData.ColorA;
                if (LayerInfo.IsColorB)
                    layerSprite.Color = archerData.ColorB;
            }

            DynamicData.For(layerSprite).Set("Entity", Entity);
            DynamicData.For(layerSprite).Set("Parent", Entity);
        }

        public override void Update()
        {
            layerSprite.Visible = attachedSprite.Visible;
            layerSprite.Effects = attachedSprite.Effects;

            if (Parent is Player player)
            {
                if (LayerInfo.AttachTo == LayerAttachType.Head)
                {
                    if (lastHatState != player.HatState)
                    {
                        var headSprite = DynamicData.For(player).Get<Sprite<string>>("headSprite");
                        Parent.Add(new LayerSpriteComponent(LayerInfo, headSprite, archerData, true, true));
                        Parent.Remove(this);
                    }

                    lastHatState = player.HatState;
                }

                if (LayerInfo.AttachTo == LayerAttachType.Body)
                    layerSprite.FlipX = player.Facing != Facing.Right;

                switch (player.HatState)
                {
                    case Player.HatStates.Normal when !LayerInfo.IsHat:
                    case Player.HatStates.Crown when !LayerInfo.IsCrown:
                    case Player.HatStates.NoHat when !LayerInfo.IsNotHat:
                        layerSprite.Visible = false;
                        return;
                }

                switch (player.Allegiance)
                {
                    case Allegiance.Neutral when !LayerInfo.IsNeutral:
                    case Allegiance.Blue when !LayerInfo.IsTeamBlue:
                    case Allegiance.Red when !LayerInfo.IsTeamRed:
                        layerSprite.Visible = false;
                        return;
                }

                if (LayerInfo.AttachTo == LayerAttachType.Bow)
                {
                    var hideBow = DynamicData.For(player).Get<bool>("hideBow");
                    layerSprite.Visible = !hideBow || player.Aiming;
                }
            }

            layerSprite.FlipY = attachedSprite.FlipY;
            layerSprite.Scale = attachedSprite.Scale;
            layerSprite.Position = attachedSprite.Position;
            layerSprite.Origin = attachedSprite.Origin;
            layerSprite.Rotation = attachedSprite.Rotation;
            layerSprite.Zoom = attachedSprite.Zoom;

            if (attachedSprite.Visible)
            {
                layerSprite.Play(attachedSprite.CurrentAnimID);
                layerSprite.CurrentFrame = attachedSprite.CurrentFrame;
            }

            if (LayerInfo.IsRainbowColor)
                layerSprite.Color = RainbowManager.GetColor(LayerInfo.RainbowOffset, LayerInfo.RainbowSpeed);

            base.Update();
        }

        public override void Render()
        {
            if (attachedSprite.Visible && layerSprite.Visible)
                layerSprite.Render();
            base.Render();
        }
    }
}
