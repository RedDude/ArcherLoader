using System;
using System.Xml;
using ArcherLoaderMod.Layers;
using Microsoft.Xna.Framework;
using Monocle;

namespace ArcherLoaderMod.Layer
{
    public class LayerParser
    {
        public static void Parse(ArcherCustomData data, XmlElement xml)
        {
            if (xml.HasChild("Layer"))
            {
                var info = HandleLayer(xml["Layer"]);
                data.LayerInfos.Add(info);
            }

            if (!xml.HasChild("Layers")) return;

            foreach (var o in xml["Layers"])
            {
                if (o is not XmlElement {Name: "Layer"}) continue;
                var info = HandleLayer(xml["Layer"]);
                data.LayerInfos.Add(info);
            }
        }

        private static LayerInfo HandleLayer(XmlElement xml)
        {
            if (FortEntrance.Settings.DisableLayers)
                return null;

            var attachToText = xml.ChildText("AttachTo");
                
            var layerInfo = new LayerInfo
            {
               
                AttachTo = attachToText == "head" || attachToText == "Head" ? LayerAttachType.Head
                    : attachToText == "body" || attachToText == "Body" ? LayerAttachType.Body
                    : attachToText == "bow" || attachToText == "Bow" ? LayerAttachType.Bow : LayerAttachType.Body,

                Sprite = xml.ChildText("Sprite"),
                Position = xml.ChildPosition("Position", Vector2.Zero),
                Color = Calc.HexToColor(xml.ChildText("Color", "FFFFFF")),
                ColorSwitch = xml.ChildInt("ColorSwitch", 0),
                ColorSwitchLoop = xml.ChildBool("ColorSwitchLoop", false),
                IsRainbowColor = xml.ChildBool("IsRainbowColor", false),
                ToScale = xml.ChildBool("ToScale", true),

                IsOnInvisible = xml.ChildBool("IsOnInvisible", false),

                IsAiming = xml.ChildBool("IsAiming", true),
                IsNeutral = xml.ChildBool("IsNeutral", true),
                IsTeamBlue = xml.ChildBool("IsTeamBlue", true),
                IsTeamRed = xml.ChildBool("IsTeamRed", true),

                IsHat = xml.ChildBool("IsHat", true),
                IsNotHat = xml.ChildBool("IsNotHat", true),
                IsCrown = xml.ChildBool("IsCrown", true),

                IsOnGround = xml.ChildBool("IsOnGround", true),
                IsOnAir = xml.ChildBool("IsOnAir", true),
                IsDucking = xml.ChildBool("IsDucking", true),
                IsDodging = xml.ChildBool("IsDodging", true),
                IsLedgeGrab = xml.ChildBool("IsLedgeGrab", true),
                IsNormal = xml.ChildBool("IsNormal", true),
                IsDying = xml.ChildBool("IsDying", true),
                IsShoot = xml.ChildBool("IsShoot", true),

                DuckingOffset = xml.ChildPosition("DuckingOffset", new Vector2(0f, 0f)),
                HatOffset = xml.ChildPosition("HatOffset", new Vector2(0f, 0f)),
                CrownOffset = xml.ChildPosition("CrownOffset", new Vector2(0f, 0f)),

                OnJump = xml.ChildBool("OnJump", false),
                ReplaceJump = xml.ChildBool("ReplaceJump", false)
            };

            return layerInfo;
        }
    }
}