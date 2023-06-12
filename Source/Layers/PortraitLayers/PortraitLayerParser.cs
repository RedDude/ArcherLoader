using System.Collections.Generic;
using System.Xml;
using Microsoft.Xna.Framework;
using Monocle;

namespace ArcherLoaderMod.Source.Layers.PortraitLayers
{
    public class PortraitLayerParser
    {
        public static void Parse(ArcherCustomData data, XmlElement xml)
        {
            var portraitLayerInfos = Parse(xml);
            data.PortraitLayerInfos ??= new();
            data.PortraitLayerInfos.AddRange(portraitLayerInfos);
        }
        
        public static List<PortraitLayerInfo> Parse(XmlElement xml)
        {
            var portraitLayerInfos = new List<PortraitLayerInfo>();
            if (xml.HasChild("PortraitLayer"))
            {
                var info = ParseLayer(xml["PortraitLayer"]);
                portraitLayerInfos.Add(info);
                return portraitLayerInfos;
            }

            if (!xml.HasChild("PortraitLayers")) return portraitLayerInfos;

            var portraitLayersXml = xml["PortraitLayers"];
            foreach (var o in portraitLayersXml)
            {
                if (o is not XmlElement {Name: "Layer"} layerXml) continue;
                var info = ParseLayer(layerXml);
                portraitLayerInfos.Add(info);
            }

            return portraitLayerInfos;
        }

        private static PortraitLayerInfo ParseLayer(XmlElement xml)
        {
            if (FortEntrance.Settings.DisableLayers)
                return null;

            var attachToText = xml.ChildText("AttachTo", null);
                
            var portraitLayerInfo = new PortraitLayerInfo
            {
               
                AttachTo = attachToText == "join" || attachToText == "Join"
                    || attachToText == "joined" || attachToText == "Joined" ? PortraitLayersAttachType.Joined : 
                    
                    attachToText == "notJoin" || attachToText == "NotJoin"
                                           || attachToText == "notJoined" || attachToText == "NotJoined" ? PortraitLayersAttachType.NotJoined :
                    
                    attachToText == "won" || attachToText == "Won" ? PortraitLayersAttachType.Won : PortraitLayersAttachType.Lose,
                    

                Sprite = xml.ChildText("Sprite", xml.GetAttribute("id")),
                Position = xml.ChildPosition("Position", Vector2.Zero),
                Color = Calc.HexToColor(xml.ChildText("Color", "FFFFFF")),
                // ColorSwitch = xml.ChildInt("ColorSwitch", 0),
                // ColorSwitchLoop = xml.ChildBool("ColorSwitchLoop", false),
                ScaleAnimation = xml.ChildPosition("ScaleAnimation", Vector2.Zero),
                RotationAnimation = xml.ChildPosition("RotationAnimation", Vector2.Zero),
                FloatAnimation = xml.ChildPosition("FloatAnimation", Vector2.Zero),
                
                IsColorA = xml.ChildBool("IsColorA", false),
                IsColorB = xml.ChildBool("IsColorB", false),
                IsRainbowColor = xml.ChildBool("IsRainbowColor", false),
                ToScale = xml.ChildBool("ToScale", true),
            };

            return portraitLayerInfo;
        }
    }
}