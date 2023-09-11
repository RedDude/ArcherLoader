using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using ArcherLoaderMod.Hair;
using ArcherLoaderMod.Layer;
using ArcherLoaderMod.Layers;
using ArcherLoaderMod.Particles;
using ArcherLoaderMod.Source.Layers.PortraitLayers;
using Microsoft.Xna.Framework;
using Monocle;
using TowerFall;

namespace ArcherLoaderMod
{
  public class ArcherCustomData
  {
    public string ID;
    public int Order;
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

    public List<ParticlesInfo> ParticlesInfos = new();
    
    public string originalName;
    public ArcherData original;
    
    public XmlElement xmlData;
    public Atlas atlas;
    public Atlas menuAtlas;

    public SpriteData spriteData;
    public SpriteData menuSpriteData;

    
    public int EightPlayersNotJoinedPortraitTopOffset = 0;
    public int EightPlayersJoinedPortraitTopOffset = 0;
    
    public CharacterSounds CharacterSounds;

    public bool replace = false;
    public bool parsed = false;

    public string FolderPath;
    public SFX victory;
    
    public string Wings;
    // public Color? WingsColor;

    public string Ghost;
    public Color? GhostColor;
    
    public string Taunt;
    public Sprite<string> TauntSpriteData;
    public ArcherCustomMeta Meta;
    
    public bool IsPrismaticGem;
    public bool PrismaticArcher;

    public bool IsGemColorA;
    public bool IsGemColorB;
    public Color GemColor;
    
    public ArcherCustomData(XmlElement xml, Atlas atlas, Atlas menuAtlas, ArcherData.ArcherTypes archerType,
      string archerId, string path)
    {
      ParseBaseArcher(xml, atlas, menuAtlas, archerType, archerId, path);
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

    private void ParseBaseArcher(XmlElement xml, Atlas atlas, Atlas menuAtlas, ArcherData.ArcherTypes archerType, string archerId,
      string path)
    {
      originalName = archerId;
      FolderPath = path;
      ArcherType = archerType;
      ID = archerId;
      xmlData = xml;

      Order = xml.ChildInt(nameof(Order), 0);

      Name0 = xml.ChildText(nameof(Name0));
      Name1 = xml.ChildText(nameof(Name1));
      ColorA = xml.ChildHexColor(nameof(ColorA));
      ColorB = xml.ChildHexColor(nameof(ColorB));
      LightbarColor = xml.ChildHexColor(nameof(LightbarColor), ColorA);
      Aimer = atlas[xml.ChildText(nameof(Aimer))];
      Hair = xml.ChildBool(nameof(Hair), false);

      Corpse = xml.ChildText(nameof(Corpse));
      Wings = xml.ChildText(nameof(Wings), null); //, original.Wings);
      // WingsColor = xml.HasChild(nameof(WingsColor)) ? xml.ChildHexColor(nameof(WingsColor)) : null;
      Ghost = xml.ChildText(nameof(Ghost), null); //, original.Ghost);
      GhostColor = xml.HasChild(nameof(GhostColor)) ? xml.ChildHexColor(nameof(GhostColor)) : null;

      StartNoHat = xml.ChildBool(nameof(StartNoHat), false);
      VictoryMusic = xml.ChildText(nameof(VictoryMusic), "Team");
      PurpleParticles = xml.ChildBool(nameof(PurpleParticles), false);
      SleepHeadFrame = xml.ChildInt(nameof(SleepHeadFrame), -1);
      Gender = xml.ChildEnum<TFGame.Genders>(nameof(Gender), TFGame.Genders.Female);
      Sprites.Body = xml[nameof(Sprites)].ChildText("Body");
      Sprites.HeadNormal = xml[nameof(Sprites)].ChildText("HeadNormal");
      Sprites.HeadNoHat = xml[nameof(Sprites)].ChildText("HeadNoHat");
      Sprites.HeadCrown = xml[nameof(Sprites)].ChildText("HeadCrown");

      // Sprites.HeadNormal = xml[nameof (Sprites)].ChildText("HeadNormalRedTeam", "");
      Sprites.HeadNoHat = xml[nameof(Sprites)].ChildText("HeadNoHat");
      Sprites.HeadCrown = xml[nameof(Sprites)].ChildText("HeadCrown");

      Sprites.HeadBack = xml[nameof(Sprites)].ChildText("HeadBack", "");
      Sprites.Bow = xml[nameof(Sprites)].ChildText("Bow");

      if (xml.HasChild(nameof(Hat)))
      {
        Hat = new ArcherData.HatInfo
        {
          Material = xml[nameof(Hat)].ChildEnum("Material", ArcherData.HatMaterials.Default),
          Normal = atlas[xml[nameof(Hat)].ChildText("Normal")],
          Blue = atlas[xml[nameof(Hat)].ChildText("Blue")],
          Red = atlas[xml[nameof(Hat)].ChildText("Red")]
        };
      }

      Portraits.NotJoined = menuAtlas[xml[nameof(Portraits)].ChildText("NotJoined")];
      Portraits.Joined = menuAtlas[xml[nameof(Portraits)].ChildText("Joined")];
      Portraits.Win = menuAtlas[xml[nameof(Portraits)].ChildText("Win")];
      Portraits.Lose = menuAtlas[xml[nameof(Portraits)].ChildText("Lose")];

      if (xml.HasChild(nameof(Statue)))
      {
        var toUse = atlas.Contains(xml[nameof(Statue)].ChildText("Image")) ? atlas : TFGame.Atlas;
        Statue.Image = toUse[xml[nameof(Statue)].ChildText("Image")];
        toUse = atlas.Contains(xml[nameof(Statue)].ChildText("Glow")) ? atlas : TFGame.Atlas;
        Statue.Glow = toUse[xml[nameof(Statue)].ChildText("Glow")];
      }
   
      Gems.Menu = xml[nameof(Gems)].ChildText("Menu");
      Gems.Gameplay = xml[nameof(Gems)].ChildText("Gameplay");
      if (xml.HasChild(nameof(Breathing)))
      {
        Breathing.Interval = xml[nameof(Breathing)].ChildInt("Interval");
        Breathing.Offset = xml[nameof(Breathing)].ChildPosition("Offset");
        Breathing.DuckingOffset = xml[nameof(Breathing)].ChildPosition("DuckingOffset");
      }
      else
        Breathing.Interval = -1;

      Taunt = xml.ChildText(nameof(Taunt), null);
      RequiresDarkWorldDLC = CheckIfRequiresDarkWorldDLC();

      HairParser.Parse(this, xml);
      ParticleParser.ParseParticles(this, xml);
      LayerParser.Parse(this, xml);
      PortraitLayerParser.Parse(this, xml);
      HandleMeta(xml);
      // SFX need to be done after the sounds are loaded

      EightPlayersNotJoinedPortraitTopOffset = xml.ChildInt(nameof(EightPlayersNotJoinedPortraitTopOffset), 0);
      EightPlayersJoinedPortraitTopOffset = xml.ChildInt(nameof(EightPlayersJoinedPortraitTopOffset), 0);

      GemColor = xml.ChildHexColor(nameof(GemColor), Color.White);
      IsGemColorA = xml.ChildBool(nameof(IsGemColorA), false);
      IsGemColorB = xml.ChildBool(nameof(IsGemColorB), false);
      IsPrismaticGem = xml.ChildBool(nameof(IsPrismaticGem), false);
      PrismaticArcher = xml.ChildBool(nameof(PrismaticArcher), false);
      parsed = true;
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

      var playerAmount = EightPlayerImport.LaunchedEightPlayer != null ? EightPlayerImport.LaunchedEightPlayer() ? 8 : 4 : 4;

      if (TFGame.Players.Length > playerAmount)
      {
        ad.Portraits.NotJoined.Rect.Y += EightPlayersNotJoinedPortraitTopOffset;
        // ad.Portraits.NotJoined.Rect.Height = 60;
        
        ad.Portraits.Joined.Rect.Y += EightPlayersJoinedPortraitTopOffset;
        // ad.Portraits.Joined.Rect.Height = 60;
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
      //Plugin.Console.WriteLine($"done: {ID} {ArcherType}");
      parsed = true;
      return ad;
    }
    
    public void FromArcherData(ArcherData archerData)
    {
      Name0 = archerData.Name0;
      Name1 = archerData.Name1;
      ColorA = archerData.ColorA;
      ColorB = archerData.ColorB;
      LightbarColor = archerData.LightbarColor;
      Aimer = archerData.Aimer;
      Hair = archerData.Hair;
      SFXID = archerData.SFXID;
      Corpse = archerData.Corpse;
      StartNoHat = archerData.StartNoHat;
      VictoryMusic = archerData.VictoryMusic;
      PurpleParticles = archerData.PurpleParticles;
      SleepHeadFrame = archerData.SleepHeadFrame;
      Gender = archerData.Gender;
      Sprites = archerData.Sprites;
      Sprites.Body = archerData.Sprites.Body;
      Sprites.HeadNormal = archerData.Sprites.HeadNormal;
      Sprites.HeadNoHat = archerData.Sprites.HeadNoHat;
      Sprites.HeadCrown = archerData.Sprites.HeadCrown;
      Sprites.HeadBack = archerData.Sprites.HeadBack;
      Sprites.Bow = archerData.Sprites.Bow;

      Hat.Material = archerData.Hat.Material;
      Hat.Normal = archerData.Hat.Normal;
      Hat.Blue = archerData.Hat.Blue;
      Hat.Red = archerData.Hat.Red;
      Portraits = archerData.Portraits;
      Portraits.NotJoined = archerData.Portraits.NotJoined;
      Portraits.Joined = archerData.Portraits.Joined;
      Portraits.Win = archerData.Portraits.Win;
      Portraits.Lose = archerData.Portraits.Lose;

      var playerAmount = EightPlayerImport.LaunchedEightPlayer != null ? EightPlayerImport.LaunchedEightPlayer() ? 8 : 4 : 4;

      if (TFGame.Players.Length > playerAmount)
      {
        Portraits.NotJoined.Rect.Y += EightPlayersNotJoinedPortraitTopOffset;
        Portraits.NotJoined.Rect.Height = 60;

        Portraits.Joined.Rect.Y += EightPlayersJoinedPortraitTopOffset;
        Portraits.Joined.Rect.Height = 60;
      }
      Statue = archerData.Statue;
      Statue.Image = archerData.Statue.Image;
      Statue.Glow = archerData.Statue.Glow;

      Gems = archerData.Gems;
      Gems.Menu = archerData.Gems.Menu;
      Gems.Gameplay = archerData.Gems.Gameplay;

      Breathing = archerData.Breathing;
      Breathing.Interval = archerData.Breathing.Interval;
      Breathing.Offset = archerData.Breathing.Offset;
      Breathing.DuckingOffset = archerData.Breathing.DuckingOffset;
      
      typeof(ArcherData).GetProperty("RequiresDarkWorldDLC")?.SetValue(archerData, RequiresDarkWorldDLC);
      //Plugin.Console.WriteLine($"done: {ID} {ArcherType}");
      parsed = true;
    }
    
    public bool RequiresDarkWorldDLC { get; set; }
    public List<LayerInfo> LayerInfos { get; set; }
    public List<PortraitLayerInfo> PortraitLayerInfos { get; set; }

    private void Parse(ArcherData original, XmlElement xml, bool requiresDarkWorldDLC, Atlas atlas,
      Atlas menuAtlas, ArcherData.ArcherTypes archerType, string archerId, string folderPath)
    {
      
      FolderPath = folderPath;
      ArcherType = archerType;
      ID = archerId;
      xmlData = xml;
      
      Order = xml.ChildInt(nameof(Order), 0);
      
      Name0 = xml.ChildText(nameof(Name0), original.Name0);
      Name1 = xml.ChildText(nameof(Name1), original.Name1);
      ColorA = xml.ChildHexColor(nameof(ColorA), original.ColorA);
      ColorB = xml.ChildHexColor(nameof(ColorB), original.ColorB);
      LightbarColor = xml.ChildHexColor(nameof(LightbarColor), original.LightbarColor);
      Aimer = !xml.HasChild(nameof(Aimer)) ? original.Aimer : atlas[xml.ChildText(nameof(Aimer))];
      Hair = xml.ChildBool(nameof(Hair), original.Hair);
      SFXID = xml.ChildInt(nameof(SFX), original.SFXID);
      Corpse = xml.ChildText(nameof(Corpse), original.Corpse);
      Wings = xml.ChildText(nameof(Wings), null);//, original.Wings);
      // WingsColor = xml.HasChild(nameof(WingsColor)) ? xml.ChildHexColor(nameof(WingsColor)) : null;
      Ghost = xml.ChildText(nameof(Ghost), null);//, original.Ghost);
      GhostColor =  xml.HasChild(nameof(GhostColor)) ? xml.ChildHexColor(nameof(GhostColor)) : null;
      
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

      HairParser.Parse(this, xml, original);
      ParticleParser.ParseParticles(this, xml);
      LayerParser.Parse(this, xml);
      PortraitLayerParser.Parse(this, xml);
      HandleMeta(xml);
        
      Taunt = xml.ChildText(nameof(Taunt), null);
      
      // SFX need to be done after the sounds are loaded
      RequiresDarkWorldDLC = requiresDarkWorldDLC || CheckIfRequiresDarkWorldDLC();
      
      EightPlayersNotJoinedPortraitTopOffset = xml.ChildInt(nameof (EightPlayersNotJoinedPortraitTopOffset), 0);
      EightPlayersJoinedPortraitTopOffset = xml.ChildInt(nameof (EightPlayersJoinedPortraitTopOffset), 0);
      
      GemColor = xml.ChildHexColor(nameof(GemColor), Color.White);
      IsGemColorA = xml.ChildBool(nameof(IsGemColorA), false);
      IsGemColorB = xml.ChildBool(nameof(IsGemColorB), false);
      IsPrismaticGem = xml.ChildBool(nameof(IsPrismaticGem), false);
      PrismaticArcher = xml.ChildBool(nameof(PrismaticArcher), false);
    }
    
    public void HandleSFX(XmlElement xml, ArcherData archerData = null)
    {
      SFXID = xml.ChildInt(nameof(SFX), archerData?.SFXID ?? 1);
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
      Audio.LOAD_PREFIX = $"{FolderPath}SFX{Path.DirectorySeparatorChar.ToString()}";
      CharacterSounds = new CharacterSounds(sfxName, Sounds.Characters[SFXID]);
      Mod.customSFXList.Add(CharacterSounds);
      victory = CharacterSounds.Load("VICTORY");
      Audio.LOAD_PREFIX = originalAudiosPath;
    }
    
    private void HandleMeta(XmlElement xml, ArcherCustomData archerData = null)
    {
      Meta = new ArcherCustomMeta
      {
        Author = xml.ChildText("Author", archerData?.Meta.Author),
        Description = xml.ChildText("Description", archerData?.Meta.Description),
        Version = xml.ChildText("Version", archerData?.Meta.Version),
        Name = xml.ChildText("MetaName", archerData?.Meta.Name),
        Email = xml.ChildText("Email", archerData?.Meta.Email),
        Discord = xml.ChildText("Discord", archerData?.Meta.Discord),
        Github = xml.ChildText("Github", archerData?.Meta.Github),
        Url = xml.ChildText("Github", archerData?.Meta.Url),
        ArcherLoaderVersion = xml.ChildText("ArcherLoaderVersion", archerData?.Meta.ArcherLoaderVersion),
      };
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
