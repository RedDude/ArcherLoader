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

    public List<ValidatorMessage> Validate(XmlElement xml, Atlas atlas, Atlas menuAtlas, SpriteData spriteData, SpriteData menuSpriteData, ArcherData.ArcherTypes archerType,
      string archerId, string path)
    {
      return ValidateXml(xml, atlas, menuAtlas, spriteData, menuSpriteData, archerType, archerId, path);
    }

    public bool CheckMissingChild(XmlElement xml, string child, string detail, bool errorNotExistence, List<ValidatorMessage> error)
    {
      if (xml.HasChild(child)) return true;
      if(errorNotExistence)
        error.Add(new ValidatorMessage($" - Missing element: {child}. {detail} "));
      return false;
    }

    public bool CheckMissingChild(XmlElement xml, string element, string child, string detail, bool errorNotExistence, List<ValidatorMessage> error)
    {
      if (!xml.HasChild(element))
      {
        if(errorNotExistence)
          error.Add(new ValidatorMessage($" - Missing element: {element}. {detail} "));
        return false;
      }

  
      if (xml[element].HasChild(child)) return true;
      if(errorNotExistence)
        error.Add(new ValidatorMessage($" - Missing element: {element}/{child}; {detail} "));
      return false;
    }

    public bool CheckMissingSpriteData(SpriteData spriteData, SpriteData spriteDataMenu, string child, string detail,
      List<ValidatorMessage> error)
    {
      if (spriteData == null) return false;
      if (spriteData.Contains(child)) return true;
      
      if (spriteData != spriteDataMenu && spriteDataMenu.Contains(child)) return true;
      
      error.Add(new ValidatorMessage($" - Missing SpriteData: {child}. "));
      return false;
    }
    
    public bool CheckMissingSubTexture(Atlas atlas, Atlas menuAtlas, string sprite, string detail,
      List<ValidatorMessage> error)
    {
      if (atlas == null) return false;
      if (atlas.Contains(sprite))
      {
        return false;
      }
      
      if (atlas != menuAtlas && menuAtlas.Contains(sprite))
      {
        return false;
      }

      if (TFGame.Atlas.Contains(sprite))
      {
        error.Add(new ValidatorMessage($" - \"{sprite}\" found in the main atlas, this is valid but not recommended and can cause unexpected errors") { type = ValidatorMessageType.WARN});
        return false;
      }
      
      if (TFGame.MenuAtlas.Contains(sprite))
      {
        error.Add(new ValidatorMessage($" - \"{sprite}\" found in the main atlas, this is valid but not recommended and can cause unexpected errors") { type = ValidatorMessageType.WARN});
        return false;
      }
      
      foreach (var customAtlas in Mod.customAtlasList)
      {
        if (!customAtlas.Contains(sprite)) continue;
        error.Add(new ValidatorMessage($" - \"{sprite}\" found in another custom atlas ({customAtlas.XmlPath}), this is valid but not recommended and can cause unexpected errors") { type = ValidatorMessageType.WARN});
          
        return false;
      }
        
      error.Add(new ValidatorMessage($" - Missing SubTexture in Atlas: {sprite}. {detail} "));
      return true;
    }

    public bool GetSpriteDataXml(SpriteData spriteData, SpriteData menuSpriteData, string dataId, out XmlElement element,
      List<ValidatorMessage> error)
    {
      if (spriteData.Contains(dataId))
      {
        element = spriteData.GetXML(dataId);
        return true;
      }
      
      if (spriteData != menuSpriteData && menuSpriteData.Contains(dataId))
      {
        element = menuSpriteData.GetXML(dataId);
        return true;
      }

      if (TFGame.SpriteData.Contains(dataId))
      {
        error.Add(new ValidatorMessage(
            $" - \"{dataId}\" found in the main spriteData, this is valid but not recommended and can cause unexpected errors")
          {type = ValidatorMessageType.WARN});
        element = TFGame.SpriteData.GetXML(dataId);
        return true;
      }

      if (TFGame.MenuSpriteData.Contains(dataId))
      {
        error.Add(new ValidatorMessage(
            $" - \"{dataId}\" found in the menu sprite data, this is valid but not recommended and can cause unexpected errors")
          {type = ValidatorMessageType.WARN});
        element = TFGame.MenuSpriteData.GetXML(dataId);
        return true;
      }

      if (TFGame.CorpseSpriteData.Contains(dataId))
      {
        error.Add(new ValidatorMessage(
            $" - \"{dataId}\" found in the corpse data, this is valid but not recommended and can cause unexpected errors")
          {type = ValidatorMessageType.WARN});
        element = TFGame.CorpseSpriteData.GetXML(dataId);
        return true;
      }

      foreach (var customAtlas in Mod.customSpriteDataList)
      {
        if (!customAtlas.Contains(dataId)) continue;
        error.Add(new ValidatorMessage(
            $" - \"{dataId}\" found in another custom spriteData ({Mod.customSpriteDataPath[customAtlas]}), this is valid but not recommended and can cause unexpected errors")
          {type = ValidatorMessageType.WARN});
        element = customAtlas.GetXML(dataId);
        return true;
      }

      error.Add(new ValidatorMessage($" - Missing sprite in SpriteData: {dataId}."));
      element = null;
      return false;
    }

    private List<ValidatorMessage> ValidateXml(XmlElement xml, Atlas atlas, Atlas menuAtlas, SpriteData spriteData, SpriteData spriteDataMenu, ArcherData.ArcherTypes archerType,
      string archerId,
      string path)
    {
      var errors = new List<ValidatorMessage>();

      var isErrorWhenNotExist = archerType == ArcherData.ArcherTypes.Normal;
      
      CheckMissingChild(xml, "Name0",
        "The name that appears on the top on character selection. ie <Name0>ASSASSIN</Name0>", isErrorWhenNotExist, errors);

      CheckMissingChild(xml, "Name1",
        "The name that appears on the below on character selection. ie <Name1>PRINCE</Name1>", isErrorWhenNotExist, errors);

      CheckMissingChild(xml, "ColorA", "The Archer main color in HEX value. ie <ColorA>F878F8</ColorA>", isErrorWhenNotExist, errors);

      CheckMissingChild(xml, "ColorB", "The Archer secondary color HEX value. ie <ColorB>F8B8F8</ColorB>", isErrorWhenNotExist, errors);
      
      // CheckMissingChild(xml, "LightbarColor",
      //   "The Lightbar Color of the archer in HEX value. ie <LightbarColor>FF1493</LightbarColor>", errors);

      if (CheckMissingChild(xml, "Aimer", $"The Archer Aimer. SubTexture Id (Atlas file). ie <Aimer>aimers/{archerId}</Aimer>",
        isErrorWhenNotExist, errors))
      {
        CheckMissingSubTexture(atlas, menuAtlas, xml["Aimer"].InnerText, $"Ensure in the atlas file that '{xml["Aimer"].InnerText}' exist", errors);
      }

      if (CheckMissingChild(xml, "Corpse",
        $"The Archer Corpse; SpriteData Id. ie <Corpse>{archerId}</Corpse> check Github wiki and the Archer Resources for examples and details",
        isErrorWhenNotExist, errors))
      {
        var corpseId = xml["Corpse"].InnerText;
        if (CheckMissingSpriteData(spriteData, spriteDataMenu, corpseId, "", errors))
        {
          if (GetSpriteDataXml(spriteData, spriteDataMenu, corpseId, out var corpseData, errors))
          {
            CheckSpriteDataSubSprite(atlas, menuAtlas, corpseData, "Corpse", "Texture", $"corpses/{archerId}/normal", errors);
            // CheckSpriteDataSubSprite(atlas, menuAtlas, corpseData, "Corpse", "BlueTeam", $"corpses/{archerId}/blueTeam", errors);
            // CheckSpriteDataSubSprite(atlas, menuAtlas, corpseData, "Corpse", "RedTeam", $"corpses/{archerId}/redTeam", errors);
            // CheckSpriteDataSubSprite(atlas, menuAtlas, corpseData, "Corpse", "Flash", $"corpses/{archerId}/flash", errors);
          }
        }
      }

      CheckMissingChild(xml, "Sprites",
        "The Archer Sprites; check Github wiki and the Archer Resources for examples and details", isErrorWhenNotExist, errors);

      if (CheckMissingChild(xml, "Sprites", "Body",
        "The Archer Body Sprites; check Github wiki and the Archer Resources for examples and details", isErrorWhenNotExist, errors))
      {
        var bodyId = xml["Sprites"]["Body"].InnerText;
        if (GetSpriteDataXml(spriteData, spriteDataMenu, bodyId, out var bodyData, errors))
        {
          CheckSpriteDataSubSprite(atlas, menuAtlas, bodyData, "Body", "Texture", $"{archerId}/body", errors);
          // CheckSpriteDataSubSprite(atlas, menuAtlas, bodyData, "Body", "RedTexture", $"{archerId}/body_red", errors);
          // CheckSpriteDataSubSprite(atlas, menuAtlas, bodyData, "Body", "BlueTexture", $"{archerId}/body_blue", errors);
        }
      }

      if (CheckMissingChild(xml, "Sprites", "HeadNormal",
        "The Archer Head Normal Sprites; SpriteData Id. check Github wiki and the Archer Resources for examples and details",
        isErrorWhenNotExist, errors))
      {
        var headNormalId = xml["Sprites"]["HeadNormal"].InnerText;
        if (GetSpriteDataXml(spriteData, spriteDataMenu, headNormalId, out var headNormalData, errors))
        {
          CheckSpriteDataSubSprite(atlas, menuAtlas, headNormalData, "Head Normal", "Texture", $"{archerId}/head", errors);
          // CheckSpriteDataSubSprite(atlas, menuAtlas, headNormalData, "Head Normal", "RedTexture", $"{archerId}/head/red", errors);
          // CheckSpriteDataSubSprite(atlas, menuAtlas, headNormalData, "Head Normal", "BlueTexture", $"{archerId}/head/blue", errors);
        }
      }

      if (CheckMissingChild(xml, "Sprites", "HeadNoHat",
        "The Archer Head No Hat Sprites; SpriteData Id. check Github wiki and the Archer Resources for examples and details",
        isErrorWhenNotExist, errors))
      {
        var headNoHatId = xml["Sprites"]["HeadNoHat"].InnerText;
        if (GetSpriteDataXml(spriteData, spriteDataMenu, headNoHatId, out var headNoHatData, errors))
        {
          CheckSpriteDataSubSprite(atlas, menuAtlas, headNoHatData, "HeadNoHat", "Texture", $"{archerId}/head/NoHat", errors);
        }
      }

      if(CheckMissingChild(xml, "Sprites", "HeadCrown",
        "The Archer Head Crown Sprites; SpriteData Id. check Github wiki and the Archer Resources for examples and details",
        isErrorWhenNotExist, errors))
      {
        var headCrownId = xml["Sprites"]["HeadCrown"].InnerText;
        if (GetSpriteDataXml(spriteData, spriteDataMenu, headCrownId, out var headCrownData, errors))
        {
          CheckSpriteDataSubSprite(atlas, menuAtlas, headCrownData, "HeadCrown", "Texture", $"{archerId}/head/crown", errors);
        }
      }
      
      if(CheckMissingChild(xml, "Sprites", "Bow",
        "The Archer Bow Sprites; SpriteData Id. check Github wiki and the Archer Resources for examples and details",
        isErrorWhenNotExist, errors))
      {
        var headCrownId = xml["Sprites"]["Bow"].InnerText;
        if (GetSpriteDataXml(spriteData, spriteDataMenu, headCrownId, out var bowData, errors))
        {
          CheckSpriteDataSubSprite(atlas, menuAtlas, bowData, "Bow", "Texture", $"{archerId}/bow", errors);
        }
      }

      ;

      if (xml.HasChild(nameof(Hat)))
      {
        if (CheckMissingChild(xml, "Hat", "Normal",
          "The Archer Hat Normal Sprite; SubTexture Id (Atlas file). check Github wiki and the Archer Resources for examples and details",
          isErrorWhenNotExist, errors))
        {
          CheckMissingSubTexture(atlas, menuAtlas, xml["Hat"]["Normal"].InnerText, $"Ensure in the atlas file that '{xml["Hat"]["Normal"].InnerText}' exist", errors);
        }

        if (CheckMissingChild(xml, "Hat", "Blue",
          "The Archer Hat Blue Sprite; SubTexture Id (Atlas file). check Github wiki and the Archer Resources for examples and details",
          isErrorWhenNotExist, errors))
        {
          CheckMissingSubTexture(atlas, menuAtlas, xml["Hat"]["Blue"].InnerText, $"Ensure in the atlas file that '{xml["Hat"]["Blue"].InnerText}' exist", errors);
        }
        
        if (CheckMissingChild(xml, "Hat", "Red",
          "The Archer Hat Red Sprite; SubTexture Id (Atlas file). check Github wiki and the Archer Resources for examples and details",
          isErrorWhenNotExist, errors))
        {
          CheckMissingSubTexture(atlas, menuAtlas, xml["Hat"]["Red"].InnerText, $"Ensure in the atlas file that '{xml["Hat"]["Red"].InnerText}' exist", errors);
        }
      }

      if (CheckMissingChild(xml, "Portraits", "NotJoined",
        "The Archer Portraits NotJoined Sprite; SubTexture Id (Atlas file). check Github wiki and the Archer Resources for examples and details",
        isErrorWhenNotExist, errors))
      {
        CheckMissingSubTexture(menuAtlas, atlas, xml["Portraits"]["NotJoined"].InnerText, $"Ensure in the atlas file that '{xml["Portraits"]["NotJoined"].InnerText}' exist", errors);
      }

      if(CheckMissingChild(xml, "Portraits", "Joined",
        "The Archer Portraits Joined Sprite; SubTexture Id (Atlas file). check Github wiki and the Archer Resources for examples and details",
        isErrorWhenNotExist, errors))
      {
        CheckMissingSubTexture(menuAtlas, atlas, xml["Portraits"]["Joined"].InnerText, $"Ensure in the atlas file that '{xml["Portraits"]["Joined"].InnerText}' exist", errors);
      }

      if(CheckMissingChild(xml, "Portraits", "Win",
        "The Archer Portraits Win Sprite; SubTexture Id (Atlas file). check Github wiki and the Archer Resources for examples and details",
        isErrorWhenNotExist, errors))
      {
        CheckMissingSubTexture(menuAtlas, atlas, xml["Portraits"]["Win"].InnerText, $"Ensure in the atlas file that '{xml["Portraits"]["Win"].InnerText}' exist", errors);
      }

      if(CheckMissingChild(xml, "Portraits", "Lose",
        "The Archer Portraits Lose Sprite; SubTexture Id (Atlas file). check Github wiki and the Archer Resources for examples and details",
        isErrorWhenNotExist, errors))
      {
        CheckMissingSubTexture(menuAtlas, atlas,xml["Portraits"]["Lose"].InnerText, $"Ensure in the atlas file that '{xml["Portraits"]["Lose"].InnerText}' exist", errors);
      }

      if (xml.HasChild("Statue"))
      {
        if(CheckMissingChild(xml, "Statue", "Image",
          "The Archer Statue Image Sprite; SubTexture Id (Atlas file). check Github wiki and the Archer Resources for examples and details",
          isErrorWhenNotExist, errors)) {
          CheckMissingSubTexture(atlas, menuAtlas, xml["Statue"]["Image"].InnerText,$"Ensure in the atlas file that '{xml["Statue"]["Image"].InnerText}' exist", errors);
        }

        if(CheckMissingChild(xml, "Statue", "Glow",
          "The Archer Statue Glow Sprite; SubTexture Id (Atlas file). check Github wiki and the Archer Resources for examples and details",
          isErrorWhenNotExist, errors)){
          CheckMissingSubTexture(atlas, menuAtlas, xml["Statue"]["Glow"].InnerText,$"Ensure in the atlas file that '{xml["Statue"]["Glow"].InnerText}' exist", errors);
        }
      }

      if (CheckMissingChild(xml, "Gems", "Menu",
        "The Archer Statue Image Sprite; SubTexture Id (Atlas file). check Github wiki and the Archer Resources for examples and details",
        isErrorWhenNotExist,  errors))
      {
        var idGemMenu = xml["Gems"]["Menu"].InnerText;
        if (GetSpriteDataXml(spriteData, spriteDataMenu, idGemMenu, out var dataGemMenu, errors))
        {
          CheckSpriteDataSubSprite(menuAtlas, atlas, dataGemMenu, "Gems/Gameplay", "Texture", $"{archerId}/GemMenu", errors);
        }
      }

      if (CheckMissingChild(xml, "Gems", "Gameplay",
        "The Archer Gameplay Gem Sprites; SubTexture Id (Atlas file). check Github wiki and the Archer Resources for examples and details",
        isErrorWhenNotExist, errors))
      {
        var idGemGameplay = xml["Gems"]["Gameplay"].InnerText;
        if (GetSpriteDataXml(spriteData, spriteDataMenu, idGemGameplay, out var dataGemGameplay, errors))
        {
          CheckSpriteDataSubSprite(atlas, atlas, dataGemGameplay, "Gems/Gameplay", "Texture", $"pickups/{archerId}/Gem", errors);
        }
      }

      if (xml.HasChild("Breathing"))
      {
        CheckMissingChild(xml, "Breathing", "Interval",
          "The Archer Breathing Interval; Int value (a whole number). ie <Breathing><Interval>90</Interval></Breathing> check Github wiki and the Archer Resources for examples and details",
          isErrorWhenNotExist, errors);

        CheckMissingChild(xml, "Breathing", "Offset",
          "The Archer Breathing Interval; Int value (a whole number). ie <Breathing><Offset x=\"10\" y=\"-6\"/>,</Breathing> check Github wiki and the Archer Resources for examples and details",
          isErrorWhenNotExist, errors);

        CheckMissingChild(xml, "Breathing", "Offset",
          "The Archer Breathing Interval; Int value (a whole number). ie <Breathing><DuckingOffset x=\"10\" y=\"0\"/></Breathing> check Github wiki and the Archer Resources for examples and details",
          isErrorWhenNotExist, errors);

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

    private void CheckSpriteDataSubSprite(Atlas atlas, Atlas menuAtlas, XmlElement spriteData, string parent,
      string sprite, string example, List<ValidatorMessage> errors)
    {
      if (CheckMissingChild(spriteData, sprite,
        $" - {parent} {sprite} Element is missing. In SpriteData. ie <{parent}><{sprite}>{example}</{sprite}></{parent}>", true,
        errors))
      {
        CheckMissingSubTexture(atlas, menuAtlas, spriteData[sprite].InnerText,
          $" - {parent} {sprite} SubTexture is missing. Ensure in the atlas file that '{spriteData[sprite]}' exist ",
          errors);
      }
    }


    public static bool PrintErrors(string archerId, List<ValidatorMessage> messages, string type, string name0, string name1)
    {
      if (messages.Count <= 0) return false;
      var hasError = false;
      PrintLineWithColor($"\n* {archerId} ({name0} {name1}) {type} has the following errors/warns:", ConsoleColor.Yellow);
      foreach (var message in messages)
      {
        if (message.type == ValidatorMessageType.ERROR)
          hasError = true;
        PrintLineWithColor(message);
      }

      return hasError;
    }
    
    public static void PrintLineWithColor(ValidatorMessage message)
    {
      Console.ForegroundColor = message.type == ValidatorMessageType.ERROR ? ConsoleColor.Red : ConsoleColor.Yellow;
      Console.WriteLine(message.message);
      Console.ResetColor();
    }

    public static void PrintLineWithColor(string message, ConsoleColor color)
    {
      Console.ForegroundColor = color;
      Console.WriteLine(message);
      Console.ResetColor();
    }

  }
}

