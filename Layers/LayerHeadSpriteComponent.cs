using System;
using System.Reflection;
using System.Xml;
using Microsoft.Xna.Framework;
using Monocle;
using MonoMod.Utils;
using TowerFall;

namespace ArcherLoaderMod.Layers
{
    public class LayerHeadSpriteComponent : Component
    {
        private Sprite<string> headSprite;
        private Sprite<string> layerSprite;
        private XmlElement layer;
        private PropertyInfo drawSelfPropertyInfo;

        public LayerHeadSpriteComponent(bool active, bool visible) : base(active, visible)
        {
            
        }

        public override void Added()
        {
            var player = ((Player) Parent);
            // if(player.Allegiance == Allegiance.Neutral)
            // {
                // Visible = false;
                // Parent.Remove(this);
                // return;
            // }
            
            headSprite = DynamicData.For(player).Get<Sprite<string>>("headSprite");
            
            layer = TFGame.SpriteData.GetXML( "red/color/layer");
            
            layerSprite = TFGame.SpriteData.GetSpriteString(player.ArcherData.Sprites.HeadNormal);
            var childText2 = layer.ChildText("Hat");
            var atlas2 = TFGame.Atlas[childText2];
            
            layerSprite.SwapSubtexture(atlas2);
            // layerSprite.Color = Calc.HexToColor("eb1717");
            layerSprite.Color = Color.Yellow;
            layerSprite.Visible = true;
            
            layerSprite.Visible = headSprite.Visible;
            DynamicData.For(layerSprite).Set("Entity", Entity);
            DynamicData.For(layerSprite).Set("Parent", Entity);
            
            
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
            
            // sprite.Position = new Vector2(0, -5);

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
        
        public static Color[] PrismaticColors = new Color[6]
        {
            Color.Red,
            new Color(255, 120, 0),
            new Color(255, 217, 0),
            Color.Lime,
            Color.Cyan,
            Color.Violet
        };

        public static Color GetPrismaticColor(float time, int offset = 0, float speedMultiplier = 1f)
        {
            var interval = 1500f;
            var currentIndex = ((int) ((float) time * speedMultiplier / interval) + offset) % PrismaticColors.Length;
            var nextIndex = (currentIndex + 1) % PrismaticColors.Length;
            var position = (float) time * speedMultiplier / interval % 1f;
            var prismaticColor = default(Color);
            prismaticColor.R = (byte) (MathHelper.Lerp((float) (int) PrismaticColors[currentIndex].R / 255f,
                (float) (int) PrismaticColors[nextIndex].R / 255f, position) * 255f);
            prismaticColor.G = (byte) (MathHelper.Lerp((float) (int) PrismaticColors[currentIndex].G / 255f,
                (float) (int) PrismaticColors[nextIndex].G / 255f, position) * 255f);
            prismaticColor.B = (byte) (MathHelper.Lerp((float) (int) PrismaticColors[currentIndex].B / 255f,
                (float) (int) PrismaticColors[nextIndex].B / 255f, position) * 255f);
            prismaticColor.A = (byte) (MathHelper.Lerp((float) (int) PrismaticColors[currentIndex].A / 255f,
                (float) (int) PrismaticColors[nextIndex].A / 255f, position) * 255f);
            return prismaticColor;
        }

        public override void Update()
        {
            var player = ((Player) Parent);
            
            
            // layerSprite.Color = GetPrismaticColor(Environment.TickCount, 0, 1);
            // drawSelfPropertyInfo.SetValue(player, false);
            // var childText = layer.ChildText("Texture");
            // var atlas = TFGame.Atlas[childText];
            // sprite.SwapSubtexture(atlas);
            layerSprite.Origin = headSprite.Origin;
            layerSprite.Rotation = headSprite.Rotation;
            layerSprite.Zoom = headSprite.Zoom;
            layerSprite.Position = headSprite.Position;
            layerSprite.Visible = headSprite.Visible;
            layerSprite.Effects = headSprite.Effects;
            // layerSprite.FlipX = player.Facing != Facing.Right;
            layerSprite.FlipX = headSprite.FlipX;
            layerSprite.FlipY = headSprite.FlipY;
            layerSprite.Scale = headSprite.Scale;
            layerSprite.Play(headSprite.CurrentAnimID);
            layerSprite.CurrentFrame = headSprite.CurrentFrame;
            base.Update();
        }

        public void CheckSprite(XmlElement element, ref Subtexture outline, ref Subtexture original)
        {
            // var texture = element.ChildText("Texture");
            // outline = TeamOutlineVariant.outlines[texture+_outline] 
            //           ?? TFGame.Atlas[texture + _outline];
            //
            // if (outline == null)
            // {
            //     original = null;
            //     return;
            // }

            // original = TFGame.Atlas[texture];
        }
        
        public void SwapSprite(Sprite<string> sprite, Subtexture outline, Subtexture original)
        {
            // if(outline == null || original == null)
            //     return;
            // sprite.SwapSubtexture(outline);
            // sprite.DrawOutline(_color, _offset);
            // sprite.SwapSubtexture(original);
            // sprite.DrawOutline(Color.Black);
        }
        
        public override void Render()
        {
            var player = ((Player) Parent);
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
