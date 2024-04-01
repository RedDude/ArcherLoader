using System.Xml;
using FortRise.IO;
using Monocle;
using TowerFall;

namespace ArcherLoaderMod
{
    public class ArcherCustomManager
  {
    public static ArcherCustomDataValidator validator;

    public static List<ArcherCustomData> Initialize(string path, Atlas atlas, Atlas menuAtlas, SpriteData spriteData, SpriteData menuSpriteData, string archerId, bool validate = false)
    {
      var filePath = $"{path}archerData.xml";
      var xmlDocument = ModFs.LoadXml(filePath);
      var archers = xmlDocument["Archers"];
      var archersArray = new List<ArcherCustomData>();
      validator ??= new ArcherCustomDataValidator();
      
      if (archers == null)
      {
        var xml = xmlDocument.DocumentElement;
        if (validate)
        {
          var errors = validator.Validate(xml, atlas, menuAtlas, spriteData, menuSpriteData, GetArcherType(xml?.Name), archerId, path);
          if (ArcherCustomDataValidator.PrintErrors(archerId, errors, xml?.Name, xml?["Name0"]?.InnerText, xml?["Name1"]?.InnerText))
            return archersArray;
        }
        
        var archerCustomData = HandleArcher(path, atlas, menuAtlas, archerId, xml);
        if (archerCustomData == null) return archersArray;
        
        archersArray.Add(archerCustomData);
        archerCustomData.Name0 = xml.ChildText("Name0", "");
        archerCustomData.Name1 = xml.ChildText("Name1", "");
        
        // if (xml.Name == "AltArcher" || xml.Name == "SecretArcher")
        // {
        //   archerCustomData.originalName = archerId;
        // }
        archerCustomData.spriteData = spriteData;
        archerCustomData.menuSpriteData = menuSpriteData;
        
        return archersArray;
      }

      foreach (var childNode in xmlDocument["Archers"].ChildNodes)
      {
        if (!(childNode is XmlElement)) continue;
        var xml = childNode as XmlElement;

        if (validate && xml.Name == "Archer")
        {
          var errors = validator.Validate(xml, atlas, menuAtlas, spriteData, menuSpriteData, GetArcherType(xml.Name), archerId, path);
          if (ArcherCustomDataValidator.PrintErrors(archerId, errors, xml.Name, xml["Name0"]?.InnerText, xml["Name1"]?.InnerText))
            continue;
        }

        var archerCustomData = HandleArcher(path, atlas, menuAtlas, archerId, xml, false);
        if (archerCustomData == null) continue;

        if (archerCustomData.ArcherType == ArcherData.ArcherTypes.Alt || archerCustomData.ArcherType == ArcherData.ArcherTypes.Secret)
        {
          archerCustomData.originalName = archerId;
        }

        archerCustomData.Name0 = xml.ChildText("Name0", "");
        archerCustomData.Name1 = xml.ChildText("Name1", "");

        archerCustomData.spriteData = spriteData;
        archerCustomData.menuSpriteData = menuSpriteData;
        
        archersArray.Add(archerCustomData);
      }

      return archersArray;
    }

    private static ArcherCustomData HandleArcher(string path, Atlas atlas, Atlas menuAtlas, string archerId,
      XmlElement xml, bool awarnFor = true)
    {
      try
      {
        if (xml.Name == "Archer")
        {
          // var xml = xmlDocument["Archer"];
          return new ArcherCustomData(xml, atlas, menuAtlas, ArcherData.ArcherTypes.Normal, archerId, path);
        }

        // if (xmlDocument["AltArcher"] != null)
        if (xml.Name == "AltArcher")
        {
          // var xml = xmlDocument["AltArcher"];
          var forAttribute = Mod.GetForAttribute(xml);
          if (string.IsNullOrEmpty(forAttribute) && awarnFor)
          {
            ArcherCustomDataValidator.PrintLineWithColor(
              $"Alt Archers '{archerId}' ({xml["Name0"]?.InnerText} {xml["Name1"]?.InnerText}) skipped: need the 'for' attribute on ArcherData.xml to know each archer this alt is for.",
              ConsoleColor.Red);
            
            return null;
          }

          var forArcher = xml.GetAttribute(forAttribute).ToUpper();

          var archerCustomData =
            new ArcherCustomData(forArcher, xml, true, atlas, menuAtlas, ArcherData.ArcherTypes.Alt, archerId, path)
            {
              Name0 = xml.ChildText("Name0", ""),
              Name1 = xml.ChildText("Name1", "")
            };

          if (xml.HasAttribute("Replace"))
          {
            archerCustomData.replace = true;
          }

          return archerCustomData;
        }

        // if (xml["SecretArcher"] != null)
        if (xml.Name == "SecretArcher")
        {
          var forAttribute = Mod.GetForAttribute(xml);
          if (string.IsNullOrEmpty(forAttribute) && awarnFor)
          {
            ArcherCustomDataValidator.PrintLineWithColor(
              $"Secret Archer '{archerId}' ({xml["Name0"]?.InnerText} {xml["Name1"]?.InnerText}) skipped: need the a 'for' attribute on ArcherData.xml to know each archer this secret is for.",
              ConsoleColor.Red);
            
            return null;
          }

          var forArcher = xml.GetAttribute(forAttribute).ToUpper();

          var archerCustomData = new ArcherCustomData(forArcher, xml, true, atlas, menuAtlas,
            ArcherData.ArcherTypes.Secret, archerId, path);
          if (xml.HasAttribute("Replace"))
          {
            archerCustomData.replace = true;
          }

          return archerCustomData;
        }

        if (xml.Name == "SkinArcher")
        {
          var forAttribute = Mod.GetForAttribute(xml);
          if (string.IsNullOrEmpty(forAttribute) && awarnFor)
          {
            ArcherCustomDataValidator.PrintLineWithColor(
              $"Skin Archer '{archerId}' ({xml["Name0"]?.InnerText} {xml["Name1"]?.InnerText}) skipped: need the 'for' attribute on ArcherData.xml to specify the archer this skin is for.",
              ConsoleColor.Red);

            return null;
          }

          var forArcher = xml.GetAttribute(forAttribute).ToUpper();

          var archerCustomData = new ArcherCustomData(forArcher, xml, true, atlas, menuAtlas,
            (ArcherData.ArcherTypes) 3, archerId, path);

          return archerCustomData;
        }
      }
      catch (Exception e)
      {
        ArcherCustomDataValidator.PrintLineWithColor(
          $"{archerId} custom {xml.Name} exception:" + e.Message,
          ConsoleColor.Red);
        
        var errors = validator.Validate(xml, atlas, menuAtlas, null, null, GetArcherType(xml.Name), archerId, path);
        ArcherCustomDataValidator.PrintErrors(archerId, errors, xml.Name, 
          xml["Name0"]?.InnerText,
          xml["Name1"]?.InnerText);
        return null;
      }

      return null;
    }
    
    public static ArcherData.ArcherTypes GetArcherType(string name)
    {
      return name == "Archer" ? ArcherData.ArcherTypes.Normal :
        name == "ArcherAlt" ? ArcherData.ArcherTypes.Alt :
        name == "SecretArcher" ? ArcherData.ArcherTypes.Secret :
        (ArcherData.ArcherTypes) 3;
    }
    
  }
}