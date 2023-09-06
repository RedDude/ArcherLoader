using System;
using System.Collections.Generic;
using System.Xml;
using Monocle;
using TowerFall;

namespace ArcherLoaderMod
{
  public class ArcherCustomManager
  {
    private static ArcherCustomDataValidator archerCustomDataValidator;

    public static List<ArcherCustomData> Initialize(string path, Atlas atlas, Atlas menuAtlas, SpriteData spriteData, SpriteData spriteDataMenu, string archerId, bool validate = false)
    {
      var filePath = $"{path}archerData.xml";
      var xmlDocument = Calc.LoadXML(filePath);
      var archers = xmlDocument["Archers"];
      var archersArray = new List<ArcherCustomData>();
      archerCustomDataValidator ??= new ArcherCustomDataValidator();
      
      if (archers == null)
      {
        if (validate)
        {
          var errors = archerCustomDataValidator.Validate(xmlDocument.DocumentElement, atlas, menuAtlas, spriteData, spriteDataMenu, GetArcherType(xmlDocument.DocumentElement.Name), archerId, path);
          if (PrintErrors(archerId, errors, xmlDocument.DocumentElement.Name))
            return archersArray;
          
          Console.WriteLine(archerId +" is Valid");
        }
        
        var archerCustomData = HandleArcher(path, atlas, menuAtlas, archerId, xmlDocument.DocumentElement);
        if (archerCustomData == null) return archersArray;
        archersArray.Add(archerCustomData);
        return archersArray;
      }

      foreach (var childNode in xmlDocument["Archers"].ChildNodes)
      {
        if (!(childNode is XmlElement)) continue;
        var xml = childNode as XmlElement;
        
        if (validate)
        {
          var errors = archerCustomDataValidator.Validate(xml, atlas, menuAtlas, spriteData, spriteDataMenu, GetArcherType(xml.Name), archerId, path);
          if (PrintErrors(archerId, errors, xml.Name))
            continue;
          
          Console.WriteLine(archerId +" is Valid");
        }

        var archerCustomData = HandleArcher(path, atlas, menuAtlas, archerId, xml, false);
        if (archerCustomData == null) continue;

        if (xml.Name == "AltArcher" || xml.Name == "SecretArcher")
        {
          archerCustomData.originalName = archerId;
          archerCustomData.Name0 = xml.ChildText("Name0", "");
          archerCustomData.Name1 = xml.ChildText("Name1", "");
        }

        archersArray.Add(archerCustomData);

      }

      return archersArray;
    }

    private static bool PrintErrors(string archerId, List<string> errors, string type)
    {
      if (errors.Count <= 0) return false;
      Console.WriteLine($"{archerId} custom {type} has the following errors:");
      foreach (var error in errors)
      {
        Console.WriteLine(error);
      }

      return true;
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
            Console.WriteLine(
              $"Alt Archers '{archerId}' skipped: need the a 'for' attribute on ArcherData.xml to know each archer this alt is for.");
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
            Console.WriteLine(
              $"Secret Archer '{archerId}' skipped: need the a 'for' attribute on ArcherData.xml to know each archer this secret is for.");
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
            Console.WriteLine(
              $"Skin Archer '{archerId}' skipped: need the a 'for' attribute on ArcherData.xml to know each archer this skin is for.");
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
        Console.WriteLine($"{archerId} custom {xml.Name} exception:" + e.Message);
        var errors = archerCustomDataValidator.Validate(xml, atlas, menuAtlas, null, null, GetArcherType(xml.Name), archerId, path);
        PrintErrors(archerId, errors, xml.Name);
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