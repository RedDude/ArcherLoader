using Microsoft.Xna.Framework;
using Monocle;
using System.Xml;
using TowerFall;
using Color = Microsoft.Xna.Framework.Color;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CustomArcherLoad
{
  public class ArcherCustomData
  {
    public string ID;
    public string Name0;
    public string Name1;
    public Color ColorA;
    public Color ColorB;
    public Color LightbarColor;
    public Subtexture Aimer;
    public bool Hair;
    public int SFXID;
    public string Corpse;
    public bool StartNoHat;
    public string VictoryMusic;
    public bool PurpleParticles;
    public int SleepHeadFrame;
    public TFGame.Genders Gender;
    public ArcherData.SpriteInfo Sprites;
    public ArcherData.HatInfo Hat;
    public ArcherData.PortraitInfo Portraits;
    public ArcherData.StatueInfo Statue;
    public ArcherData.GemInfo Gems;
    public ArcherData.BreathingInfo Breathing;
    public ArcherData.ArcherTypes ArcherType;

    public HairInfo HairInfo;
    
    public string originalName;
    public ArcherData original;
    
    public XmlElement xmlData;
    public Atlas atlas;
    public Atlas menuAtlas;

    public int EightPlayersNotJoinedPortraitTopOffset = 0;
    public int EightPlayersJoinedPortraitTopOffset = 0;
    
    public int SFXName;
    public CharacterSounds CharacterSounds;

    public bool replace = false;
    public bool parsed = false;

    public string FolderPath;
    public SFX victory;

    public static List<ArcherCustomData> Initialize(string path, Atlas atlas, Atlas menuAtlas, string archerId)
    {
      var filePath = $"{Calc.LOADPATH}{path}archerData.xml";
      var xmlDocument = Calc.LoadXML(filePath);
      var archers = xmlDocument["Archers"];
      var archersArray = new List<ArcherCustomData>();
  
      if (archers == null)
      {
        var archerCustomData = HandleArcher(path, atlas, menuAtlas, archerId, xmlDocument.DocumentElement);
        if (archerCustomData == null) return archersArray;;
        archersArray.Add(archerCustomData);
        return archersArray;
      }
        
      foreach (var childNode in xmlDocument["Archers"].ChildNodes)
      {
        if (!(childNode is XmlElement)) continue;
        var xml = childNode as XmlElement;
        var archerCustomData = HandleArcher(path, atlas, menuAtlas, archerId, xml, false);
        if (archerCustomData == null) continue;
        
        if (xml.Name == "AltArcher" || xml.Name == "SecretArcher")
        {
          archerCustomData.originalName = archerId;
        }
        archersArray.Add(archerCustomData);

      }

      return archersArray;
    }

    private static ArcherCustomData HandleArcher(string path, Atlas atlas, Atlas menuAtlas, string archerId,
      XmlElement xml, bool awarnFor = true)
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

        if (!xml.HasAttribute("For") && awarnFor)
        {
          Console.WriteLine(
            $"Alt Archers '{archerId}' skipped: need the a 'For' attribute on ArcherData.xml to know each archer this alt if for.");
          return null;
        }

        var forArcher = xml.GetAttribute("For").ToUpper();

        var archerCustomData =
          new ArcherCustomData(forArcher, xml, true, atlas, menuAtlas, ArcherData.ArcherTypes.Alt, archerId, path);
        if (xml.HasAttribute("Replace"))
        {
          archerCustomData.replace = true;
        }

        return archerCustomData;
      }

      // if (xml["SecretArcher"] != null)
      if (xml.Name == "SecretArcher")
      {
        if (!xml.HasAttribute("For") && awarnFor)
        {
          Console.WriteLine(
            $"Secret Archer '{archerId}' skipped: need the a 'For' attribute on ArcherData.xml to know each archer this secret if for.");
          return null;
        }

        var forArcher = xml.GetAttribute("For").ToUpper();

        var archerCustomData = new ArcherCustomData(forArcher, xml, true, atlas, menuAtlas,
          ArcherData.ArcherTypes.Secret, archerId, path);
        if (xml.HasAttribute("Replace"))
        {
          archerCustomData.replace = true;
        }

        return archerCustomData;
      }

      return null;
    }
    public ArcherData ToArcherData()
    {
      var ad = (ArcherData)System.Runtime.Serialization.FormatterServices
        .GetUninitializedObject(typeof(ArcherData));
      ad.Name0 = Name0;
      ad.Name1 = Name1;
      ad.ColorA = ColorA;
      ad.ColorB = ColorB;
      ad.LightbarColor = LightbarColor;
      ad.Aimer = Aimer;
      ad.Hair = Hair;
      ad.SFXID = SFXID;
      ad.Corpse = Corpse;
      ad.StartNoHat = StartNoHat;
      ad.VictoryMusic = VictoryMusic;
      ad.PurpleParticles = PurpleParticles;
      ad.SleepHeadFrame = SleepHeadFrame;
      ad.Gender = Gender;
      ad.Sprites = Sprites;
      
      ad.Sprites.Body = Sprites.Body;
      ad.Sprites.HeadNormal = Sprites.HeadNormal;
      ad.Sprites.HeadNoHat = Sprites.HeadNoHat;
      ad.Sprites.HeadCrown = Sprites.HeadCrown;
      ad.Sprites.HeadBack = Sprites.HeadBack;
      ad.Sprites.Bow = Sprites.Bow;
        
      ad.Hat = Hat;
      ad.Hat.Material = Hat.Material;
      ad.Hat.Normal = Hat.Normal;
      ad.Hat.Blue = Hat.Blue;
      ad.Hat.Red = Hat.Red;
      
      
      ad.Portraits = Portraits;
      ad.Portraits.NotJoined = Portraits.NotJoined;
      ad.Portraits.Joined = Portraits.Joined;
      ad.Portraits.Win = Portraits.Win;
      ad.Portraits.Lose = Portraits.Lose;

      if (TFGame.Players.Length > 4)
      {
        ad.Portraits.NotJoined.Rect.Y += EightPlayersNotJoinedPortraitTopOffset;
        ad.Portraits.NotJoined.Rect.Height = 60;
        
        ad.Portraits.Joined.Rect.Y += EightPlayersJoinedPortraitTopOffset;
        ad.Portraits.Joined.Rect.Height = 60;
      }
      ad.Statue = Statue;
      ad.Statue.Image = Statue.Image;
      ad.Statue.Glow = Statue.Glow;
      
      ad.Gems = Gems;
      ad.Gems.Menu = Gems.Menu;
      ad.Gems.Gameplay = Gems.Gameplay;
      
      ad.Breathing = Breathing;
      ad.Breathing.Interval = Breathing.Interval;
      ad.Breathing.Offset = Breathing.Offset;
      ad.Breathing.DuckingOffset = Breathing.DuckingOffset;
      typeof(ArcherData).GetProperty("RequiresDarkWorldDLC")?.SetValue(ad, RequiresDarkWorldDLC);
      //Plugin.Logger.LogInfo($"done: {ID} {ArcherType}");
      parsed = true;
      return ad;
    }
    
    public bool RequiresDarkWorldDLC { get; set; }
    
    public ArcherCustomData(XmlElement xml, Atlas atlas, Atlas menuAtlas, ArcherData.ArcherTypes archerType,
      string archerId, string path)
    {
      FolderPath = path;
      ArcherType = archerType;
      ID = archerId;
      xmlData = xml;
      Name0 = xml.ChildText(nameof (Name0));
      Name1 = xml.ChildText(nameof (Name1));
      ColorA = xml.ChildHexColor(nameof (ColorA));
      ColorB = xml.ChildHexColor(nameof (ColorB));
      LightbarColor = xml.ChildHexColor(nameof (LightbarColor));
      Aimer = atlas[xml.ChildText(nameof (Aimer))];
      Hair = xml.ChildBool(nameof (Hair), false);
     
      Corpse = xml.ChildText(nameof (Corpse));
      StartNoHat = xml.ChildBool(nameof (StartNoHat), false);
      VictoryMusic = xml.ChildText(nameof (VictoryMusic), "Team");
      PurpleParticles = xml.ChildBool(nameof (PurpleParticles), false);
      SleepHeadFrame = xml.ChildInt(nameof (SleepHeadFrame), -1);
      Gender = xml.ChildEnum<TFGame.Genders>(nameof (Gender), TFGame.Genders.Female);
      Sprites.Body = xml[nameof (Sprites)].ChildText("Body");
      Sprites.HeadNormal = xml[nameof (Sprites)].ChildText("HeadNormal");
      Sprites.HeadNoHat = xml[nameof (Sprites)].ChildText("HeadNoHat");
      Sprites.HeadCrown = xml[nameof (Sprites)].ChildText("HeadCrown");
      Sprites.HeadBack = xml[nameof (Sprites)].ChildText("HeadBack", "");
      Sprites.Bow = xml[nameof (Sprites)].ChildText("Bow");
     
      if (xml.HasChild(nameof (Hat)))
      {
        Hat = new ArcherData.HatInfo();
        Hat.Material = xml[nameof (Hat)].ChildEnum<ArcherData.HatMaterials>("Material", ArcherData.HatMaterials.Default);
        Hat.Normal = atlas[xml[nameof (Hat)].ChildText("Normal")];
        Hat.Blue = atlas[xml[nameof (Hat)].ChildText("Blue")];
        Hat.Red = atlas[xml[nameof (Hat)].ChildText("Red")];
      }
      Portraits.NotJoined = menuAtlas[xml[nameof (Portraits)].ChildText("NotJoined")];
      Portraits.Joined = menuAtlas[xml[nameof (Portraits)].ChildText("Joined")];
      Portraits.Win = menuAtlas[xml[nameof (Portraits)].ChildText("Win")];
      Portraits.Lose = menuAtlas[xml[nameof (Portraits)].ChildText("Lose")];
      var toUse = atlas.Contains(xml[nameof(Statue)].ChildText("Image")) ? atlas : TFGame.Atlas;
      Statue.Image = toUse[xml[nameof (Statue)].ChildText("Image")];
      toUse = atlas.Contains(xml[nameof(Statue)].ChildText("Glow")) ? atlas : TFGame.Atlas;
      Statue.Glow = toUse[xml[nameof (Statue)].ChildText("Glow")];
      Gems.Menu = xml[nameof (Gems)].ChildText("Menu");
      Gems.Gameplay = xml[nameof (Gems)].ChildText("Gameplay");
      if (xml.HasChild(nameof (Breathing)))
      {
        Breathing.Interval = xml[nameof (Breathing)].ChildInt("Interval");
        Breathing.Offset = xml[nameof (Breathing)].ChildPosition("Offset");
        Breathing.DuckingOffset = xml[nameof (Breathing)].ChildPosition("DuckingOffset");
      }
      else
        Breathing.Interval = -1;
      RequiresDarkWorldDLC = CheckIfRequiresDarkWorldDLC();
      
      HandleHair(xml);
      // SFX need to be done after the sounds are loaded
      
      EightPlayersNotJoinedPortraitTopOffset = xml.ChildInt(nameof (EightPlayersNotJoinedPortraitTopOffset), 0);
      EightPlayersJoinedPortraitTopOffset = xml.ChildInt(nameof (EightPlayersJoinedPortraitTopOffset), 0);
      
      parsed = true;
    }

    public ArcherCustomData(string originalName, XmlElement xml, bool requiresDarkWorldDLC, Atlas atlas,
      Atlas menuAtlas, ArcherData.ArcherTypes archerType, string archerId, string path)
    {
      this.originalName = originalName;
      xmlData = xml;
      this.atlas = atlas;
      this.menuAtlas = menuAtlas;
      ArcherType = archerType;
      ID = archerId;
      FolderPath = path;
      // FillAltData(original, xml, requiresDarkWorldDLC, atlas, menuAtlas, archerType);
    }

    public ArcherCustomData(ArcherData original, XmlElement xml, bool requiresDarkWorldDLC, Atlas atlas, Atlas menuAtlas, ArcherData.ArcherTypes archerType, string archerId, string folderPath)
    {
      Parse(original, xml, requiresDarkWorldDLC, atlas, menuAtlas, archerType, archerId, folderPath);
    }

    public void Parse(ArcherData original, string originalFolderPath)
    {
      Parse(original, xmlData, RequiresDarkWorldDLC, atlas, menuAtlas, ArcherType, ID, originalFolderPath);
    }

    private void Parse(ArcherData original, XmlElement xml, bool requiresDarkWorldDLC, Atlas atlas,
      Atlas menuAtlas, ArcherData.ArcherTypes archerType, string archerId, string folderPath)
    {
      FolderPath = folderPath;
      ArcherType = archerType;
      ID = archerId;
      xmlData = xml;
      Name0 = xml.ChildText(nameof(Name0), original.Name0);
      Name1 = xml.ChildText(nameof(Name1), original.Name1);
      ColorA = xml.ChildHexColor(nameof(ColorA), original.ColorA);
      ColorB = xml.ChildHexColor(nameof(ColorB), original.ColorB);
      LightbarColor = xml.ChildHexColor(nameof(LightbarColor), original.LightbarColor);
      Aimer = !xml.HasChild(nameof(Aimer)) ? original.Aimer : atlas[xml.ChildText(nameof(Aimer))];
      Hair = xml.ChildBool(nameof(Hair), original.Hair);
      SFXID = xml.ChildInt(nameof(SFX), original.SFXID);
      Corpse = xml.ChildText(nameof(Corpse), original.Corpse);
      StartNoHat = xml.ChildBool(nameof(StartNoHat), original.StartNoHat);
      VictoryMusic = xml.ChildText(nameof(VictoryMusic), original.VictoryMusic);
      PurpleParticles = xml.ChildBool(nameof(PurpleParticles), original.PurpleParticles);
      SleepHeadFrame = xml.ChildInt(nameof(SleepHeadFrame), original.SleepHeadFrame);
      Gender = xml.ChildEnum<TFGame.Genders>(nameof(Gender), original.Gender);
      Sprites = original.Sprites;
      if (xml.HasChild(nameof(Sprites)))
      {
        Sprites.Body = xml[nameof(Sprites)].ChildText("Body", original.Sprites.Body);
        Sprites.HeadNormal = xml[nameof(Sprites)].ChildText("HeadNormal", original.Sprites.HeadNormal);
        Sprites.HeadNoHat = xml[nameof(Sprites)].ChildText("HeadNoHat", original.Sprites.HeadNoHat);
        Sprites.HeadCrown = xml[nameof(Sprites)].ChildText("HeadCrown", original.Sprites.HeadCrown);
        Sprites.HeadBack = xml[nameof(Sprites)].ChildText("HeadBack", original.Sprites.HeadBack);
        Sprites.Bow = xml[nameof(Sprites)].ChildText("Bow", original.Sprites.Bow);
      }

      Hat = original.Hat;
      if (xml.HasChild(nameof(Hat)))
      {
        if (xml[nameof(Hat)].HasChild("Material"))
          Hat.Material = xml[nameof(Hat)].ChildEnum<ArcherData.HatMaterials>("Material");
        if (xml[nameof(Hat)].HasChild("Normal"))
          Hat.Normal = atlas[xml[nameof(Hat)].ChildText("Normal")];
        if (xml[nameof(Hat)].HasChild("Blue"))
          Hat.Blue = atlas[xml[nameof(Hat)].ChildText("Blue")];
        if (xml[nameof(Hat)].HasChild("Red"))
          Hat.Red = atlas[xml[nameof(Hat)].ChildText("Red")];
      }

      Portraits = original.Portraits;
      if (xml.HasChild(nameof(Portraits)))
      {
        if (xml[nameof(Portraits)].HasChild("NotJoined"))
          Portraits.NotJoined = menuAtlas[xml[nameof(Portraits)].ChildText("NotJoined")];
        if (xml[nameof(Portraits)].HasChild("Joined"))
          Portraits.Joined = menuAtlas[xml[nameof(Portraits)].ChildText("Joined")];
        if (xml[nameof(Portraits)].HasChild("Win"))
          Portraits.Win = menuAtlas[xml[nameof(Portraits)].ChildText("Win")];
        if (xml[nameof(Portraits)].HasChild("Lose"))
          Portraits.Lose = menuAtlas[xml[nameof(Portraits)].ChildText("Lose")];
      }

      Statue = original.Statue;
      if (xml.HasChild(nameof(Statue)))
      {
        if (xml[nameof(Statue)].HasChild("Image"))
        {
          var toUse = atlas.Contains(xml[nameof(Statue)].ChildText("Image")) ? atlas : TFGame.Atlas;
          Statue.Image = toUse[xml[nameof(Statue)].ChildText("Image")];
        }

        if (xml[nameof(Statue)].HasChild("Glow"))
        {
          var toUse = atlas.Contains(xml[nameof(Statue)].ChildText("Glow")) ? atlas : TFGame.Atlas;
          Statue.Glow = toUse[xml[nameof(Statue)].ChildText("Glow")];
        }
      }

      Gems = original.Gems;
      if (xml.HasChild(nameof(Gems)))
      {
        if (xml[nameof(Gems)].HasChild("Menu"))
          Gems.Menu = xml[nameof(Gems)].ChildText("Menu");
        if (xml[nameof(Gems)].HasChild("Gameplay"))
          Gems.Gameplay = xml[nameof(Gems)].ChildText("Gameplay");
      }

      Breathing = original.Breathing;
      if (xml.HasChild(nameof(Breathing)))
      {
        if (xml[nameof(Breathing)].HasChild("Interval"))
          Breathing.Interval = xml[nameof(Breathing)].ChildInt("Interval");
        if (xml[nameof(Breathing)].HasChild("Offset"))
          Breathing.Offset = xml[nameof(Breathing)].ChildPosition("Offset");
        if (xml[nameof(Breathing)].HasChild("DuckingOffset"))
          Breathing.DuckingOffset = xml[nameof(Breathing)].ChildPosition("DuckingOffset");
      }

      HandleHair(xml, original);
      // SFX need to be done after the sounds are loaded
      RequiresDarkWorldDLC = requiresDarkWorldDLC || CheckIfRequiresDarkWorldDLC();
      
      EightPlayersNotJoinedPortraitTopOffset = xml.ChildInt(nameof (EightPlayersNotJoinedPortraitTopOffset), 0);
      EightPlayersJoinedPortraitTopOffset = xml.ChildInt(nameof (EightPlayersJoinedPortraitTopOffset), 0);
    }
    
    
    private void HandleHair(XmlElement xml, ArcherData archerData = null)
    {
      if (!xml.HasChild(nameof(HairInfo)) || !Hair) return;
     
      HairInfo = new HairInfo();
      HairInfo hairInfo = null;

      if (archerData != null)
      {
        Mod.ArcherCustomDataDict.TryGetValue(archerData, out var customArcher);
        if (customArcher != null)
        {
          hairInfo = customArcher.HairInfo;
        }
      }

      HairInfo.Alpha = xml[nameof(HairInfo)].ChildFloat("Alpha", hairInfo?.Alpha ?? 1);
      HairInfo.Links = xml[nameof(HairInfo)].ChildInt("Links", hairInfo?.Links ?? 2);
      HairInfo.Size = xml[nameof(HairInfo)].ChildInt("Size", hairInfo?.Size ?? 1);
      HairInfo.LinksDist = xml[nameof(HairInfo)].ChildFloat("LinksDist",  hairInfo?.LinksDist ?? 1);
      HairInfo.SineValue = xml[nameof(HairInfo)].ChildInt("SineValue", hairInfo?.SineValue ?? 30);
      HairInfo.HairSprite = xml[nameof(HairInfo)].ChildText("HairSprite", hairInfo?.HairSprite ?? "player/hair");
      HairInfo.HairEndSprite = xml[nameof(HairInfo)].ChildText("HairEndSprite", hairInfo?.HairEndSprite ??  "player/hairEnd");
      HairInfo.Color = Calc.HexToColor(xml[nameof(HairInfo)].ChildText("Color", "FFFFFF"));
      var endColor = xml[nameof(HairInfo)].ChildText("EndColor", null);
      HairInfo.EndColor = endColor != null ? Calc.HexToColor(endColor) : Color.Transparent;
      
      HairInfo.Rainbow = xml[nameof(HairInfo)].ChildBool("Rainbow", true);
      HairInfo.OutlineColor = Calc.HexToColor(xml[nameof(HairInfo)].ChildText("OutlineColor", "000000"));
      HairInfo.Gradient = xml[nameof(HairInfo)].ChildBool("Gradient", false);
      HairInfo.GradientOffset = xml[nameof(HairInfo)].ChildInt("GradientOffset",  hairInfo?.GradientOffset ?? 0);
      try{
        HairInfo.DuckingOffset = xml[nameof (HairInfo)].ChildPosition("DuckingOffset");
      }catch(Exception e){
        HairInfo.DuckingOffset = new Vector2(0, xml[nameof(HairInfo)].ChildInt("DuckingOffset", 0));
      }
    
      HairInfo.Prismatic = xml[nameof(HairInfo)].ChildBool("Prismatic", false);
      HairInfo.PrismaticEnd = xml[nameof(HairInfo)].ChildBool("PrismaticEnd", false);
      HairInfo.PrismaticTime = xml[nameof(HairInfo)].ChildFloat("PrismaticTime", 1);
      HairInfo.position = new Vector2(
        xml[nameof(HairInfo)].ChildFloat("X", hairInfo?.position.X ?? 0),
        xml[nameof(HairInfo)].ChildFloat("Y", hairInfo?.position.Y ?? 0)
      );
      HairInfo.VisibleWithHat = xml[nameof(HairInfo)].ChildBool("VisibleWithHat", true);
      if (xml[nameof(HairInfo)].HasChild("WithHatOffset"))
        HairInfo.WithHatOffset = xml[nameof(HairInfo)].ChildPosition("WithHatOffset");
    }

    public void HandleSFX(XmlElement xml, ArcherData archerData = null)
    {
      SFXID = xml.ChildInt(nameof(SFX));
      var originalIndex = -1;
      var sfxName = "";
      if (xml.HasChild("CustomSFX")) {
        sfxName = xml["CustomSFX"].ChildText("Name", ID);
        if (archerData == null)
        {
          originalIndex = Mod.CheckForBaseSFXArchers(originalName.ToUpper());
          if (originalIndex != -1){
            SFXID = originalIndex;
          }
          if (originalIndex == -1)
          {
            var items = new List<ArcherCustomData>(Mod.ArcherCustomDataDict.Values);
            var data = items.First(o => o.ID.ToUpper() == originalName.ToUpper());
            originalIndex = items.IndexOf(data);
          }
          if (originalIndex == -1)
          {
            var originalName = xml["CustomSFX"].ChildText("Fallback", "GREEN");
            originalIndex = Mod.CheckForBaseSFXArchers(originalName);
            if (originalIndex == -1)
              SFXID = originalIndex;
          }
        }
        else
        {
          SFXID = archerData.SFXID;
        }
      }
   
      var originalAudiosPath = Audio.LOAD_PREFIX;
      Audio.LOAD_PREFIX = $"{Calc.LOADPATH}{FolderPath}SFX{Path.DirectorySeparatorChar.ToString()}";
      CharacterSounds = new CharacterSounds(sfxName, Sounds.Characters[SFXID]);
      Mod.customSFXList.Add(CharacterSounds);
      victory = CharacterSounds.Load("VICTORY");
      Audio.LOAD_PREFIX = originalAudiosPath;
    }

    public bool CheckIfRequiresDarkWorldDLC() => SFXID >= 8 || Corpse == "Red" || (CheckSprite(Sprites.Body) || CheckSprite(Sprites.HeadNormal)) || (CheckSprite(Sprites.HeadNoHat) || CheckSprite(Sprites.HeadCrown) || (CheckSprite(Sprites.HeadBack) || CheckSprite(Sprites.Bow))) || (Gems.Gameplay == "Gem8" || Gems.Menu == "Gem8");

    private bool CheckSprite(string sprite)
    {
      switch (sprite)
      {
        case "Blue_Alt":
        case "Blue_AltBow":
        case "Blue_AltHead":
        case "Blue_AltHeadCrown":
        case "Blue_AltNoHead":
        case "Cyan_Alt":
        case "Cyan_AltBow":
        case "Cyan_AltCrown":
        case "Cyan_AltHead":
        case "Cyan_AltNoHead":
        case "Green_Alt":
        case "Green_AltBow":
        case "Green_AltHead":
        case "Green_AltHeadCrown":
        case "Green_AltNoHead":
        case "Orange_Alt":
        case "Orange_AltBow":
        case "Orange_AltCrown":
        case "Orange_AltHead":
        case "Orange_AltNoHead":
        case "Pink_Alt":
        case "Pink_AltBow":
        case "Pink_AltCrown":
        case "Pink_AltHead":
        case "Pink_AltNoHead":
        case "PlayerBody8":
        case "PlayerBow8":
        case "PlayerHeadCrown8":
        case "PlayerHeadNoHat8":
        case "PlayerHeadNormal8":
        case "PlayerHeadWarlord8":
        case "Purple_Alt":
        case "Purple_AltBow":
        case "Purple_AltCrown":
        case "Purple_AltHead":
        case "Purple_AltNoHead":
        case "Red_Alt":
        case "Red_AltBow":
        case "Red_AltCrown":
        case "Red_AltHead":
        case "Red_AltNoHead":
        case "White_Alt":
        case "White_AltBow":
        case "White_AltCrown":
        case "White_AltHead":
        case "White_AltNoHead":
        case "Yellow_Alt":
        case "Yellow_AltBow":
        case "Yellow_AltCrown":
        case "Yellow_AltHead":
        case "Yellow_AltNoHead":
          return true;
        default:
          return false;
      }
    }

  }
}
