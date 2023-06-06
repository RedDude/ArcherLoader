using System;
using System.Reflection;
using System.Xml;
using ArcherLoaderMod.Layers;
using ArcherLoaderMod.Rainbow;
using Monocle;
using MonoMod.Utils;
using TowerFall;

namespace ArcherLoaderMod.Source.Layers.PortraitLayers
{
    public class PortraitLayerSpriteComponent : Component
    {
        private ArcherPortrait attachedSprite;

        public PortraitLayerInfo layerInfo;
        private Sprite<string> layerSprite;

        public PortraitLayerSpriteComponent(PortraitLayerInfo layerInfo, bool active, bool visible) : base(active, visible)
        {
            this.layerInfo = layerInfo;
        }

        public override void Added()
        {
            var portrait = ((ArcherPortrait) Parent);
            // if(player.Allegiance == Allegiance.Neutral)
            // {
            // Visible = false;
            // Parent.Remove(this);
            // return;
            // }
            //
            // bodySprite = DynamicData.For(player).Get<Sprite<string>>("bodySprite");
            // headSprite = DynamicData.For(player).Get<Sprite<string>>("headSprite");

            // layer = TFGame.SpriteData.GetXML(layerInfo.Sprite);

            // var attachedSpriteInfo = layerInfo.AttachTo == PortraitLayersAttachType.Join
            //     ? portrait.ArcherData.Portraits.Joined
            //     : portrait.ArcherData.Portraits.NotJoined;
            // var attachedSpriteInfo = portrait;
            // var xml = TFGame.SpriteData.GetXML(attachedSpriteInfo);

            layerSprite = TFGame.SpriteData.GetSpriteString(layerInfo.Sprite);
            layerSprite.Color = layerInfo.Color;
            // var childText = layer.ChildText("Texture");
            // var atlas = TFGame.Atlas[childText];

            // layerSprite.SwapSubtexture(atlas);
            // layerSprite.Color = layerInfo.Color;
            // layerSprite.Visible = true;
            //
            // layerSprite.Visible = attachedSprite.Visible;
            // DynamicData.For(layerSprite).Set("Entity", Entity);
            // DynamicData.For(layerSprite).Set("Parent", Entity);

            // sprite = new Sprite<string>(atlas, xML.ChildInt("FrameWidth"), xML.ChildInt("FrameHeight"))
            // {
            //     Origin = new Vector2(xML.ChildFloat("OriginX", 0.0f), xML.ChildFloat("OriginY", 0.0f)),
            //     Position = new Vector2(xML.ChildFloat("X", 0.0f), xML.ChildFloat("Y", 0.0f)),
            //     Color = xML.ChildHexColor("Color", Color.White)
            // };
            // XmlElement xmlElement = xML["Animations"];
            // if (xmlElement != null)
            // {
            //     foreach (XmlElement xml in xmlElement.GetElementsByTagName("Anim"))
            //         sprite.Add(xml.Attr("id"), xml.AttrFloat("delay", 0.0f), xml.AttrBool("loop", true), Calc.ReadCSVInt(xml.Attr("frames")));
            // }

            // var spriteDataBody = TFGame.SpriteData.GetXML(player.ArcherData.Sprites.Body);
            // var spriteDataBow = TFGame.SpriteData.GetXML(player.ArcherData.Sprites.Bow);
            // var spriteDataHeadNoHat = TFGame.SpriteData.GetXML(player.ArcherData.Sprites.HeadNoHat);
            // var spriteDataHeadCrown = TFGame.SpriteData.GetXML(player.ArcherData.Sprites.HeadCrown);
            // var spriteDataHeadNormal = TFGame.SpriteData.GetXML(player.ArcherData.Sprites.HeadNormal);
            //
            // CheckSprite(spriteDataBody, ref bodyOutline, ref originalBody);
            // CheckSprite(spriteDataHeadNormal,ref headOutline, ref originalHead);
            // CheckSprite(spriteDataHeadNoHat, ref headOutlineNotHat, ref originalNotHat);
            // CheckSprite(spriteDataHeadCrown, ref headOutlineCrown, ref originalHeadCrown);
            // CheckSprite(spriteDataBow, ref bowOutline, ref originalBow);
            //
            // 
            // headSprite = DynamicData.For(player).Get<Sprite<string>>("headSprite");
            // bowSprite = DynamicData.For(player).Get<Sprite<string>>("bowSprite");
        }

      

        public override void Update()
        {
            var player = ((Player) Parent);
            // drawSelfPropertyInfo.SetValue(player, false);
            // var childText = layer.ChildText("Texture");
            // var atlas = TFGame.Atlas[childText];
            // sprite.SwapSubtexture(atlas);
            
            layerSprite.Visible = attachedSprite.Visible;
            // layerSprite.Effects = attachedSprite.Effects;
            // if(layerInfo.AttachTo != LayerAttachType.Head)
                // layerSprite.FlipX = player.Facing != Facing.Right;
            
            // layerSprite.FlipY = attachedSprite.FlipY;
            // layerSprite.Scale = attachedSprite.Scale;
            // layerSprite.Position = attachedSprite.Position;
            // layerSprite.Origin = attachedSprite.Origin;
            // layerSprite.Rotation = attachedSprite.Rotation;
            // layerSprite.Zoom = attachedSprite.Zoom;
            //
            // layerSprite.Play(attachedSprite.CurrentAnimID);
            // layerSprite.CurrentFrame = attachedSprite.CurrentFrame;

            if (layerInfo.IsRainbowColor)
            {
                layerSprite.Color = RainbowManager.GetColor(Environment.TickCount);
            }
            base.Update();
        }

        public override void Render()
        {
            var player = ((Player) Parent);
            layerSprite.Render();
            // layerSprite2.Render();
            base.Render();

            // bodySprite.Render();
            //     // if(player.Allegiance == Allegiance.Neutral)
            //     //     return;
            //     //
            //     //
            //     // foreach (var component in player.Components)
            //     // {
            //     //     if (component is not Sprite<string> sprite) continue;
            //     //     if (!sprite.Visible) continue;
            //     //     if (bodySprite == component)
            //     //     {
            //     //         SwapSprite(sprite, bodyOutline, originalBody);
            //     //         continue;
            //     //     }
            //     //     if (headSprite == component)
            //     //     {
            //     //         if(player.HatState == Player.HatStates.Normal)
            //     //         {
            //     //             SwapSprite(sprite, headOutline, originalHead);
            //     //             continue;
            //     //         }
            //     //         if(player.HatState == Player.HatStates.NoHat)
            //     //         {
            //     //             SwapSprite(sprite, headOutlineNotHat, originalNotHat);
            //     //             continue;
            //     //         }
            //     //         if(player.HatState == Player.HatStates.Crown)
            //     //         {
            //     //             SwapSprite(sprite, headOutlineCrown, originalHeadCrown);
            //     //             continue;
            //     //         }
            //     //     }
            //     //     if (bowSprite == component)
            //     //     {
            //     //         SwapSprite(sprite, bowOutline, originalBow);
            //     //         continue;
            //     //     }
            //     // }
            //

        }
    }
}
