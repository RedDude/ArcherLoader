using System.Collections.Generic;
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
            var portraitLayerInfos = Parse(xml);
            data.LayerInfos ??= new();
            data.LayerInfos.AddRange(portraitLayerInfos);
        }
        
        public static List<LayerInfo> Parse(XmlElement xml)
        {
            var layerInfos = new List<LayerInfo>();
            if (xml.HasChild("Layer"))
            {
                var info = HandleLayer(xml["Layer"]);
                layerInfos.Add(info);
                return layerInfos;
            }

            if (!xml.HasChild("Layers")) return layerInfos;

            var portraitLayersXml = xml["Layers"];
            foreach (var o in portraitLayersXml)
            {
                if (o is not XmlElement {Name: "Layer"} layerXml) continue;
                var info = HandleLayer(layerXml);
                layerInfos.Add(info);
            }

            return layerInfos;
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
                    : attachToText == "bow" || attachToText == "Bow" ? LayerAttachType.Bow
                    : attachToText == "corpse" || attachToText == "Corpse" ? LayerAttachType.Corpse : LayerAttachType.Body,

                Sprite = xml.ChildText("Sprite"),
                Position = xml.ChildPosition("Position", Vector2.Zero),
                Color = Calc.HexToColor(xml.ChildText("Color", "FFFFFF")),
                ColorSwitch = xml.ChildInt("ColorSwitch", 0),
                ColorSwitchLoop = xml.ChildBool("ColorSwitchLoop", false),
                ToScale = xml.ChildBool("ToScale", true),
                
                IsColorA = xml.ChildBool("IsColorA", false),
                IsColorB = xml.ChildBool("IsColorB", false),
                IsRainbowColor = xml.ChildBool("IsRainbowColor", false),
                RainbowOffset = xml.ChildInt("RainbowOffset", 0),
                RainbowSpeed = xml.ChildFloat("RainbowSpeed", 1f),
                IsTeamColor = xml.ChildBool("IsTeamColor", false),
                
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