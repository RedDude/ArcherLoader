using System;
using System.Xml;
using Microsoft.Xna.Framework;
using Monocle;
using TowerFall;

namespace ArcherLoaderMod.Hair
{
  public class HairParser
  {
    public static void Parse(ArcherCustomData data, XmlElement xml, ArcherData archerData = null)
    {
      if (!xml.HasChild(nameof(HairInfo)) || !data.Hair) return;

      data.HairInfo = new HairInfo();
      HairInfo hairInfo = null;

      if (archerData != null)
      {
        Mod.ArcherCustomDataDict.TryGetValue(archerData, out var customArcher);
        if (customArcher != null)
        {
          hairInfo = customArcher.HairInfo;
        }
      }

      var element = xml[nameof(HairInfo)];
      data.HairInfo.Alpha = element.ChildFloat("Alpha", hairInfo?.Alpha ?? 1);
      data.HairInfo.Links = element.ChildInt("Links", hairInfo?.Links ?? 2);
      data.HairInfo.Size = element.ChildInt("Size", hairInfo?.Size ?? 1);
      data.HairInfo.LinksDist = element.ChildFloat("LinksDist", hairInfo?.LinksDist ?? 1);
      data.HairInfo.SineValue = element.ChildInt("SineValue", hairInfo?.SineValue ?? 30);
      data.HairInfo.HairSprite = element.ChildText("HairSprite", hairInfo?.HairSprite ?? "player/hair");
      data.HairInfo.HairEndSprite = element.ChildText("HairEndSprite", hairInfo?.HairEndSprite ?? "player/hairEnd");
      data.HairInfo.Color = Calc.HexToColor(element.ChildText("Color", "FFFFFF"));
      var endColor = element.ChildText("EndColor", null);
      data.HairInfo.EndColor = endColor != null ? Calc.HexToColor(endColor) : Color.Transparent;

      data.HairInfo.Rainbow = element.ChildBool("Rainbow", false);
      data.HairInfo.OutlineColor = Calc.HexToColor(element.ChildText("OutlineColor", "000000"));
      data.HairInfo.Gradient = element.ChildBool("Gradient", false);
      data.HairInfo.GradientOffset = element.ChildInt("GradientOffset", hairInfo?.GradientOffset ?? 0);
      try
      {
        data.HairInfo.DuckingOffset = element.ChildPosition("DuckingOffset");
      }
      catch (Exception e)
      {
        data.HairInfo.DuckingOffset = new Vector2(0, element.ChildInt("DuckingOffset", 0));
      }

      data.HairInfo.Prismatic = element.ChildBool("Prismatic", false);
      data.HairInfo.PrismaticEnd = element.ChildBool("PrismaticEnd", false);
      data.HairInfo.PrismaticTime = element.ChildFloat("PrismaticTime", 1);
      data.HairInfo.Position = new Vector2(
        element.ChildFloat("X", hairInfo?.Position.X ?? 0),
        element.ChildFloat("Y", hairInfo?.Position.Y ?? 0)
      );
      data.HairInfo.VisibleWithHat = element.ChildBool("VisibleWithHat", true);
      if (element.HasChild("WithHatOffset"))
        data.HairInfo.WithHatOffset = element.ChildPosition("WithHatOffset");
    }
  }
}