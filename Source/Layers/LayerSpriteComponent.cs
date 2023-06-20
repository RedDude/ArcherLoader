using System;
using System.Reflection;
using System.Xml;
using ArcherLoaderMod.Rainbow;
using Monocle;
using MonoMod.Utils;
using TowerFall;

namespace ArcherLoaderMod.Layers
{
    public class LayerSpriteComponent : Component
    {
        private Sprite<string> attachedSprite;
        private readonly ArcherCustomData data;

        public LayerInfo layerInfo;
        private Sprite<string> layerSprite;
        private XmlElement layer;
        private PropertyInfo drawSelfPropertyInfo;
        private XmlElement xml;

        public bool teamColorSetted = false;
        private Player.HatStates lastHatState;
        public LayerSpriteComponent(LayerInfo layerInfo, Sprite<string> attachedSprite, ArcherCustomData data,
            bool active, bool visible) : base(active, visible)
        {
            this.layerInfo = layerInfo;
            drawSelfPropertyInfo = typeof(Player).GetProperty("DrawSelf", BindingFlags.Public | BindingFlags.Instance);
            this.attachedSprite = attachedSprite;
            this.data = data;
        }

        public override void Added()
        {
            string attachedSpriteInfo;
            if (Parent is Player player)
            {
                attachedSpriteInfo = layerInfo.AttachTo == LayerAttachType.Body ? player.ArcherData.Sprites.Body :
                    layerInfo.AttachTo == LayerAttachType.Bow ? player.ArcherData.Sprites.Bow :
                    layerInfo.AttachTo == LayerAttachType.Head ? 
                        player.HatState == Player.HatStates.Normal ? player.ArcherData.Sprites.HeadNormal :
                        player.HatState == Player.HatStates.Crown ? player.ArcherData.Sprites.HeadCrown :
                        player.ArcherData.Sprites.HeadNoHat : player.ArcherData.Sprites.HeadNormal;
                
                lastHatState = player.HatState;
                xml = TFGame.SpriteData.GetXML(attachedSpriteInfo);
                layerSprite = TFGame.SpriteData.GetSpriteString(attachedSpriteInfo);
                
                if (layerInfo.IsTeamColor && player.Level.Session.MatchSettings.TeamMode)
                {
                    var matchVariants = player.Level.Session.MatchSettings.Variants;
                    if (player.Allegiance != Allegiance.Neutral &&
                        !matchVariants.GetCustomVariant("TeamOutline")[player.PlayerIndex])
                    {
                        layerSprite.Color = ArcherData.GetColorA(player.PlayerIndex, player.TeamColor);
                        teamColorSetted = true;
                    }
                }
            }
            else if (Parent is PlayerCorpse corpse)
            {
                attachedSpriteInfo = data.Corpse;
                xml = TFGame.CorpseSpriteData.GetXML(attachedSpriteInfo);
                layerSprite = TFGame.CorpseSpriteData.GetSpriteString(attachedSpriteInfo);
                
                if (layerInfo.IsTeamColor && corpse.Level.Session.MatchSettings.TeamMode)
                {
                    layerSprite.Color = ArcherData.GetColorA(corpse.PlayerIndex, corpse.TeamColor);
                }
            }

            var atlas = TFGame.Atlas[layerInfo.Sprite];
            if (atlas != null)
            {
                layerSprite.SwapSubtexture(atlas);
            }

            layerSprite.Visible = attachedSprite.Visible;
            if (!teamColorSetted)
            {
                layerSprite.Color = layerInfo.Color;
                if (layerInfo.IsColorA || layerInfo.IsColorB)
                {
                    if (layerInfo.IsColorA)
                        layerSprite.Color = data.ColorA;
                    if (layerInfo.IsColorB)
                        layerSprite.Color = data.ColorB;    
                }
            }

            DynamicData.For(layerSprite).Set("Entity", Entity);
            DynamicData.For(layerSprite).Set("Parent", Entity);
        }


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

        public override void Update()
        {
            layerSprite.Visible = attachedSprite.Visible;
            layerSprite.Effects = attachedSprite.Effects;

            if (Parent is Player player)
            {
                if (layerInfo.AttachTo == LayerAttachType.Head)
                {
                    if (lastHatState != player.HatState)
                    {
                        var headSprite = DynamicData.For(player).Get<Sprite<string>>("headSprite");
                        Parent.Add(new LayerSpriteComponent(layerInfo, headSprite, data, true, true));
                        Parent.Remove(this);
                    }

                    lastHatState = player.HatState;
                }

                if (layerInfo.AttachTo == LayerAttachType.Body)
                    layerSprite.FlipX = player.Facing != Facing.Right;
                
                switch (player.HatState)
                {
                    case Player.HatStates.Normal when !layerInfo.IsHat:
                    case Player.HatStates.Crown when !layerInfo.IsCrown:
                    case Player.HatStates.NoHat when !layerInfo.IsNotHat:
                        layerSprite.Visible = false;
                        return;
                }
                
                switch (player.Allegiance)
                {
                    case Allegiance.Neutral when !layerInfo.IsNeutral:
                    case Allegiance.Blue when !layerInfo.IsTeamBlue:
                    case Allegiance.Red when !layerInfo.IsTeamRed:
                        layerSprite.Visible = false;
                        return;
                }
                
                if (layerInfo.AttachTo == LayerAttachType.Bow)
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

            if (layerInfo.IsRainbowColor)
            {
                layerSprite.Color = RainbowManager.GetColor(Environment.TickCount);
            }
         
            base.Update();
        }

        public override void Render()
        {
            // var player = ((Player) Parent);
            // if (layerInfo.AttachTo == LayerAttachType.Bow)
            // {
            //     if (attachedSprite.Visible)
            //         layerSprite.Render();
            //     return;
            // }

            if (attachedSprite.Visible && layerSprite.Visible)
                layerSprite.Render();
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