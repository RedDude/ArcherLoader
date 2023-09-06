using System;
using System.Collections.Generic;
using System.Xml;
using Monocle;
using TowerFall;

namespace ArcherLoaderMod
{
  public class ArcherCustomDataValidator
  {
    // public ArcherCustomDataValidator(XmlElement xml, Atlas atlas, Atlas menuAtlas, ArcherData.ArcherTypes archerType,
    //   string archerId, string path)
    // {
    //   ParseBaseArcher(xml, atlas, menuAtlas, archerType, archerId, path);
    // }

    public List<string> Validate(XmlElement xml, Atlas atlas, Atlas menuAtlas, SpriteData spriteData, SpriteData spriteDataMenu, ArcherData.ArcherTypes archerType,
      string archerId, string path)
    {
      return ValidateXml(xml, atlas, menuAtlas, spriteData, spriteDataMenu, archerType, archerId, path);
    }

    public bool CheckMissingChild(XmlElement xml, string child, string detail, bool errorNotExistence, List<string> error)
    {
      if (xml.HasChild(child)) return true;
      if(errorNotExistence)
        error.Add($"Missing element: {child}. {detail} ");
      return false;
    }

    public bool CheckMissingChild(XmlElement xml, string element, string child, string detail, bool errorNotExistence, List<string> error)
    {
      if (!xml.HasChild(element))
      {
        if(errorNotExistence)
          error.Add($"Missing element: {element}. {detail} ");
        return false;
      }

  
      if (xml[element].HasChild(child)) return true;
      if(errorNotExistence)
        error.Add($"Missing element: {element}/{child}; {detail} ");
      return false;
    }

    public bool CheckMissingSpriteData(SpriteData spriteData, string child, string detail, List<string> error)
    {
      if (spriteData == null) return false;
      if (spriteData.Contains(child)) return true;
      error.Add($"Missing SpriteData: {child}. ");
      return false;
    }
    
    public bool CheckMissingSubTexture(Atlas atlas, string sprite, string detail, List<string> error)
    {
      if (atlas == null) return false;
      if (atlas.Contains(sprite)) return false;
      error.Add($"Missing SubTexture in Atlas: {sprite}. ");
      return true;
    }
    
    private List<string> ValidateXml(XmlElement xml, Atlas atlas, Atlas menuAtlas, SpriteData spriteData, SpriteData spriteDataMenu, ArcherData.ArcherTypes archerType,
      string archerId,
      string path)
    {
      var errors = new List<string>();

      var errorNotExistence = archerType == ArcherData.ArcherTypes.Normal;
      
      CheckMissingChild(xml, "Name0",
        "The name that appears on the top on character selection. ie <Name0>ASSASSIN</Name0>", errorNotExistence, errors);

      CheckMissingChild(xml, "Name1",
        "The name that appears on the below on character selection. ie <Name0>ASSASSIN</PRINCE>", errorNotExistence, errors);

      CheckMissingChild(xml, "ColorA", "The Archer main color in HEX value. ie <ColorA>F878F8</ColorA>", errorNotExistence, errors);

      CheckMissingChild(xml, "ColorB", "The Archer secondary color HEX value. ie <ColorB>F8B8F8</ColorB>", errorNotExistence, errors);
      
      // CheckMissingChild(xml, "LightbarColor",
      //   "The Lightbar Color of the archer in HEX value. ie <LightbarColor>FF1493</LightbarColor>", errors);

      if (CheckMissingChild(xml, "Aimer", "The Archer Aimer. SubTexture Id (Atlas file). ie <Aimer>aimers/pink</Aimer>",
        errorNotExistence, errors))
      {
        CheckMissingSubTexture(atlas, xml["Aimer"].InnerText, $"Ensure in the atlas file that '{xml["Aimer"].InnerText}' exist", errors);
      }

      if (CheckMissingChild(xml, "Corpse",
        "The Archer Corpse; SpriteData Id. ie <Corpse>Pink</Corpse> check Github wiki and the Archer Resources for examples and details",
        errorNotExistence, errors))
      {
        var corpseId = xml["Corpse"].InnerText;
        if (CheckMissingSpriteData(spriteData, corpseId, "", errors))
        {
          var corpseData = spriteData.GetXML(corpseId);
          CheckSpriteDataSubSprite(atlas, corpseData, "Corpse", "Texture", "corpses/pink/normal", errors);
          CheckSpriteDataSubSprite(atlas, corpseData, "Corpse", "BlueTeam", "corpses/pink/blueTeam", errors);
          CheckSpriteDataSubSprite(atlas, corpseData, "Corpse", "RedTeam", "corpses/pink/redTeam", errors);
          CheckSpriteDataSubSprite(atlas, corpseData, "Corpse", "Flash", "corpses/pink/flash", errors);
        }
      }

      CheckMissingChild(xml, "Sprites",
        "The Archer Sprites; check Github wiki and the Archer Resources for examples and details", errorNotExistence, errors);

      if (CheckMissingChild(xml, "Sprites", "Body",
        "The Archer Body Sprites; check Github wiki and the Archer Resources for examples and details", errorNotExistence, errors))
      {
        var bodyId = xml["Sprites"]["Body"].InnerText;
        var bodyData = spriteData.GetXML(bodyId);
        CheckSpriteDataSubSprite(atlas, bodyData, "Body", "Texture", "pink/body", errors);
        CheckSpriteDataSubSprite(atlas, bodyData, "Body", "RedTexture", "pink/body_red", errors);
        CheckSpriteDataSubSprite(atlas, bodyData, "Body", "BlueTexture", "pink/body_blue", errors);
      }

      if (CheckMissingChild(xml, "Sprites", "HeadNormal",
        "The Archer Head Normal Sprites; SpriteData Id. check Github wiki and the Archer Resources for examples and details",
        errorNotExistence, errors))
      {
        var headNormalId = xml["Sprites"]["HeadNormal"].InnerText;
        var headNormalData = spriteData.GetXML(headNormalId);
        CheckSpriteDataSubSprite(atlas, headNormalData, "Head Normal", "Texture", "pink/head", errors);
        CheckSpriteDataSubSprite(atlas, headNormalData, "Head Normal", "RedTexture", "pink/head/red", errors);
        CheckSpriteDataSubSprite(atlas, headNormalData, "Head Normal", "BlueTexture", "pink/head/blue", errors);
      }

      if (CheckMissingChild(xml, "Sprites", "HeadNoHat",
        "The Archer Head No Hat Sprites; SpriteData Id. check Github wiki and the Archer Resources for examples and details",
        errorNotExistence, errors))
      {
        var headNoHatId = xml["Sprites"]["HeadNoHat"].InnerText;
        var headNoHatData = spriteData.GetXML(headNoHatId);
        CheckSpriteDataSubSprite(atlas, headNoHatData, "HeadNoHat", "Texture", "pink/head/NoHat", errors);
      }

      if(CheckMissingChild(xml, "Sprites", "HeadCrown",
        "The Archer Head Crown Sprites; SpriteData Id. check Github wiki and the Archer Resources for examples and details",
        errorNotExistence, errors))
      {
        var headCrownId = xml["Sprites"]["HeadCrown"].InnerText;
        var headCrownData = spriteData.GetXML(headCrownId);
        CheckSpriteDataSubSprite(atlas, headCrownData, "HeadCrown", "Texture", "pink/head/crown", errors);
      }

      CheckMissingChild(xml, "Sprites", "Bow",
        "The Archer Bow Sprites; SpriteData Id. check Github wiki and the Archer Resources for examples and details",
        errorNotExistence, errors);

      if (xml.HasChild(nameof(Hat)))
      {
        if (CheckMissingChild(xml, "Hat", "Normal",
          "The Archer Hat Normal Sprite; SubTexture Id (Atlas file). check Github wiki and the Archer Resources for examples and details",
          errorNotExistence, errors))
        {
          CheckMissingSubTexture(atlas, xml["Hat"]["Normal"].InnerText, $"Ensure in the atlas file that '{xml["Hat"]["Normal"].InnerText}' exist", errors);
        }

        if (CheckMissingChild(xml, "Hat", "Blue",
          "The Archer Hat Blue Sprite; SubTexture Id (Atlas file). check Github wiki and the Archer Resources for examples and details",
          errorNotExistence, errors))
        {
          CheckMissingSubTexture(atlas, xml["Hat"]["Blue"].InnerText, $"Ensure in the atlas file that '{xml["Hat"]["Blue"].InnerText}' exist", errors);
        }
        
        if (CheckMissingChild(xml, "Hat", "Red",
          "The Archer Hat Red Sprite; SubTexture Id (Atlas file). check Github wiki and the Archer Resources for examples and details",
          errorNotExistence, errors))
        {
          CheckMissingSubTexture(atlas, xml["Hat"]["Red"].InnerText, $"Ensure in the atlas file that '{xml["Hat"]["Red"].InnerText}' exist", errors);
        }
      }

      if (CheckMissingChild(xml, "Portraits", "NotJoined",
        "The Archer Portraits NotJoined Sprite; SubTexture Id (Atlas file). check Github wiki and the Archer Resources for examples and details",
        errorNotExistence, errors))
      {
        CheckMissingSubTexture(menuAtlas, xml["Portraits"]["NotJoined"].InnerText, $"Ensure in the atlas file that '{xml["Portraits"]["NotJoined"].InnerText}' exist", errors);
      }

      if(CheckMissingChild(xml, "Portraits", "Joined",
        "The Archer Portraits Joined Sprite; SubTexture Id (Atlas file). check Github wiki and the Archer Resources for examples and details",
        errorNotExistence, errors))
      {
        CheckMissingSubTexture(menuAtlas, xml["Portraits"]["Joined"].InnerText, $"Ensure in the atlas file that '{xml["Portraits"]["Joined"].InnerText}' exist", errors);
      }

      if(CheckMissingChild(xml, "Portraits", "Win",
        "The Archer Portraits Win Sprite; SubTexture Id (Atlas file). check Github wiki and the Archer Resources for examples and details",
        errorNotExistence, errors))
      {
        CheckMissingSubTexture(menuAtlas, xml["Portraits"]["Win"].InnerText, $"Ensure in the atlas file that '{xml["Portraits"]["Win"].InnerText}' exist", errors);
      }

      if(CheckMissingChild(xml, "Portraits", "Lose",
        "The Archer Portraits Lose Sprite; SubTexture Id (Atlas file). check Github wiki and the Archer Resources for examples and details",
        errorNotExistence, errors))
      {
        CheckMissingSubTexture(menuAtlas, xml["Portraits"]["Lose"].InnerText, $"Ensure in the atlas file that '{xml["Portraits"]["Lose"].InnerText}' exist", errors);
      }

      if (xml.HasChild("Statue"))
      {
        if(CheckMissingChild(xml, "Statue", "Image",
          "The Archer Statue Image Sprite; SubTexture Id (Atlas file). check Github wiki and the Archer Resources for examples and details",
          errorNotExistence, errors)) {
          CheckMissingSubTexture(atlas, xml["Statue"]["Image"].InnerText,$"Ensure in the atlas file that '{xml["Statue"]["Image"].InnerText}' exist", errors);
        }

        if(CheckMissingChild(xml, "Statue", "Glow",
          "The Archer Statue Glow Sprite; SubTexture Id (Atlas file). check Github wiki and the Archer Resources for examples and details",
          errorNotExistence, errors)){
          CheckMissingSubTexture(atlas, xml["Statue"]["Glow"].InnerText,$"Ensure in the atlas file that '{xml["Statue"]["Glow"].InnerText}' exist", errors);
        }
      }

      if (CheckMissingChild(xml, "Gems", "Menu",
        "The Archer Statue Image Sprite; SubTexture Id (Atlas file). check Github wiki and the Archer Resources for examples and details",
        errorNotExistence,  errors))
      {
        var idGemMenu = xml["Gems"]["Menu"].InnerText;
        var dataGemMenu = spriteData.GetXML(idGemMenu);
        CheckSpriteDataSubSprite(menuAtlas, dataGemMenu, "Gems/Gameplay", "Texture", "pink/GemMenu", errors);
      }

      if (CheckMissingChild(xml, "Gems", "Gameplay",
        "The Archer Gameplay Gem Sprites; SubTexture Id (Atlas file). check Github wiki and the Archer Resources for examples and details",
        errorNotExistence, errors))
      {
        var idGemGameplay = xml["Gems"]["Gameplay"].InnerText;
        var dataGemGameplay = spriteData.GetXML(idGemGameplay);
        CheckSpriteDataSubSprite(atlas, dataGemGameplay, "Gems/Gameplay", "Texture", "pickups/pink/Gem", errors);
      }

      if (xml.HasChild("Breathing"))
      {
        CheckMissingChild(xml, "Breathing", "Interval",
          "The Archer Breathing Interval; Int value (a whole number). ie <Breathing><Interval>90</Interval></Breathing> check Github wiki and the Archer Resources for examples and details",
          errorNotExistence, errors);

        CheckMissingChild(xml, "Breathing", "Offset",
          "The Archer Breathing Interval; Int value (a whole number). ie <Breathing><Offset x=\"10\" y=\"-6\"/>,</Breathing> check Github wiki and the Archer Resources for examples and details",
          errorNotExistence, errors);

        CheckMissingChild(xml, "Breathing", "Offset",
          "The Archer Breathing Interval; Int value (a whole number). ie <Breathing><DuckingOffset x=\"10\" y=\"0\"/></Breathing> check Github wiki and the Archer Resources for examples and details",
          errorNotExistence, errors);

      }

      return errors;
    }

    // private XmlElement GetSpriteDataXML(XmlElement xml, SpriteData spriteData, string id, string child)
    // {
    //   var innerText = xml[id][child].InnerText;
    //   if (spriteData.Contains(innerText))
    //   {
    //     return spriteData.GetXML(innerText);
    //   }
    //
    //   // if (TFGame.MenuAtlas.Contains(innerText))
    //   // {
    //     // Console.WriteLine($"{innerText} not found in the ");
    //   // }
    // }

    private void CheckSpriteDataSubSprite(Atlas atlas, XmlElement spriteData, string parent, string sprite, string example, List<string> errors)
    {
      if (CheckMissingChild(spriteData, sprite,
        $"{parent} {sprite} Element is missing. In SpriteData. ie <{parent}><{sprite}>{example}</{sprite}></{parent}>", true,
        errors))
      {
        CheckMissingSubTexture(atlas, spriteData[sprite].InnerText,
          $"{parent} {sprite} SubTexture is missing. Ensure in the atlas file that '{spriteData[sprite]}' exist ",
          errors);
      }
    }
  }
}

