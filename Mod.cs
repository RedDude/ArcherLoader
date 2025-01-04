using System.Reflection;
using System.Xml;
using ArcherLoaderMod.Ghost;
using ArcherLoaderMod.Hair;
using ArcherLoaderMod.Layers;
using ArcherLoaderMod.Particles;
using ArcherLoaderMod.Patch;
using ArcherLoaderMod.Rainbow;
using ArcherLoaderMod.Skin;
using ArcherLoaderMod.Source.Layers.PortraitLayers;
using ArcherLoaderMod.Taunt;
using ArcherLoaderMod.Teams;
using ArcherLoaderMod.Wings;
using FortRise;
using FortRise.IO;
using Monocle;
using MonoMod.ModInterop;
using MonoMod.Utils;
using TowerFall;

namespace ArcherLoaderMod
{
    public class Mod 
    {
        private static string _separator;
        private static string _contentCustomArchersPath;

        public static List<SpriteData> customSpriteDataList = new ();
        public static List<SpriteData> cachedCustomSpriteDataList = new();
        public static Dictionary<SpriteData, string> customSpriteDataPath = new();
        
        public static Dictionary<string, List<CustomSpriteDataInfo>> customSpriteDataCategoryDict = new();
        
        public static List<Atlas> customAtlasList = new();
        public static List<Atlas> cachedCustomAtlasList = new();
        
        public static List<ArcherData> AllArchersDataDict = new();
        
        public static Dictionary<ArcherData, ArcherCustomData> ArcherCustomDataDict = new();
        
        public static Dictionary<string, ArcherData> BaseArcherByNameDict = new();
        
        public static List<CharacterSounds> customSFXList = new();

        
        public static FortContent Content;
        private static string _customArchersPath;
        private static List<ArcherCustomData> allCustomArchers = new List<ArcherCustomData>();

        public static void LoadContent(FortContent fortContent)
        {
            TauntVariant.LoadContent(fortContent);
            TeamsPatcher.LoadContent(fortContent);

            Content = fortContent;
        }
        
        public static void Load()
        {
            // Console.WriteLine("Custom Archer Loader is here!");
            typeof(EightPlayerImport).ModInterop();
            
            _separator = Path.DirectorySeparatorChar.ToString();
            _customArchersPath = $"CustomArchers{_separator}";
            _contentCustomArchersPath = $"Mod{_separator}{_customArchersPath}";
                
            Directory.CreateDirectory($"{Calc.LOADPATH}{_contentCustomArchersPath}");
            
            ContentLoaderPatcher.Load();
            if (!FortEntrance.Settings.DisableHairs)
            {
                var hairPatcher = new HairPatcher();
                hairPatcher.Load();
            }
            TauntVariant.Load();
            ParticlePatcher.Load();
            WingsPatcher.Load();
            GhostPatcher.Load();
            VictoryMusicPatcher.Load();
            SkinPatcher.Load();
            LayerPatch.Load();
            PortraitLayerPatch.Load();
            PrismaticPatcher.Load();
            TeamsPatcher.Load();
            RiseCore.Events.OnAfterModdedLoadContent += OnAfterLoadContent;
            
            HandleQuickStart();
        }

        private static void HandleQuickStart()
        {
            if (!FortEntrance.Settings.QuickStart) return;
            var once = false;

            void OnMainMenuOnUpdate(On.TowerFall.MainMenu.orig_Update orig, MainMenu self)
            {
                orig(self);
                if (self.State == MainMenu.MenuState.Loading) return;

                if (once) return;
                once = true;
                for (var i = 0; i < 4; i++)
                {
                    TFGame.Players[i] = TFGame.PlayerInputs[i] != null;
                }

                var player1CharacterIndex = FortEntrance.Settings.Player1CharacterIndex;
                if(player1CharacterIndex > -1)
                    TFGame.Characters[0] = player1CharacterIndex >= ArcherData.Archers.Length
                        ? ArcherData.Archers.Length - 1
                        : player1CharacterIndex;
                
                var player2CharacterIndex = FortEntrance.Settings.Player2CharacterIndex;
                if(player2CharacterIndex > -1)
                    TFGame.Characters[1] = player2CharacterIndex >= ArcherData.Archers.Length
                        ? ArcherData.Archers.Length - 1
                        : player2CharacterIndex;
                
                var player3CharacterIndex = FortEntrance.Settings.Player3CharacterIndex;
                if(player3CharacterIndex > -1)
                    TFGame.Characters[2] = player3CharacterIndex >= ArcherData.Archers.Length
                        ? ArcherData.Archers.Length - 1
                        : player3CharacterIndex;
                
                
                // TFGame.Characters[1] = 2;

                var matchSettings = new MatchSettings(GameData.VersusTowers[GameData.VersusTowers.Count-1].GetLevelSystem(), Modes.LevelTest,
                    MatchSettings.MatchLengths.Standard);
                // matchSettings.Variants.GetCustomVariant("ReaperChalice").Value = true;
                
                (matchSettings.LevelSystem as VersusLevelSystem).StartOnLevel(-1);
                new Session(matchSettings).StartGame();

                On.TowerFall.MainMenu.Update -= OnMainMenuOnUpdate;
            }

            On.TowerFall.MainMenu.Update += OnMainMenuOnUpdate;
        }

        private static void OnAfterLoadContent(FortContent content)
        {
            allCustomArchers.AddRange(LoadContentAtPath(null, $"{Calc.LOADPATH}{_contentCustomArchersPath}", ContentAccess.Content));
            allCustomArchers.AddRange(LoadContentAtPath(null, $"{_customArchersPath}", ContentAccess.Root));

            string archerFolder = Path.Combine(content.MetadataPath, "Content", "Archers");

            if (ModIO.IsDirectoryOrFileExists(archerFolder)) 
            {
                List<ArcherCustomData> archerData = LoadContentAtPath(content, archerFolder, ContentAccess.ModContent, true);
                allCustomArchers.AddRange(archerData);
            }
        }
        
        public static void Start()
        {
            var newNormalCustom = allCustomArchers.FindAll(a => a.ArcherType == ArcherData.ArcherTypes.Normal);
            var newAltCustom = allCustomArchers.FindAll(a => a.ArcherType == ArcherData.ArcherTypes.Alt);
            var newSecretCustom = allCustomArchers.FindAll(a => a.ArcherType == ArcherData.ArcherTypes.Secret);
            
            newNormalCustom = new List<ArcherCustomData>(newNormalCustom.OrderBy((a) => a.Order));
            var newNormal = ArcherData.Archers.ToList();

            var indexDict = new Dictionary<string, int>();
            var newIndex = newNormal.Count;
            foreach (var customData in newNormalCustom)
            {
                var data = customData.ToArcherData();
                ArcherCustomDataDict[data] = customData;
               
                newNormal.Add(data);
                indexDict[customData.ID] = newIndex++;
            }
            
            var newNormalArray = newNormal.ToArray();
            var newAlt = new ArcherData[newNormal.Count];
            var newSecret = new ArcherData[newNormal.Count];
            
            LoadAltArcher(newAltCustom, indexDict, newNormalCustom, newAlt);
            LoadSecretArcher(newSecretCustom, indexDict, allCustomArchers, newNormalArray, newSecret, newNormalCustom);
            
            for (var index = 0; index < newNormalArray.Length; index++)
            {
                if(index <= ArcherData.AltArchers.Length - 1)
                    newAlt[index] = ArcherData.AltArchers[index];
                
                if(index <= ArcherData.SecretArchers.Length - 1)
                    newSecret[index] = newSecret[index] == null ? ArcherData.SecretArchers[index] : newSecret[index];
            }

            var properties = typeof(ArcherData).GetProperties(BindingFlags.Public | BindingFlags.Static |  BindingFlags.FlattenHierarchy);
            
            // var archerProp
            foreach (var propertyInfo in properties)
            {
                if (propertyInfo.Name == "Archers")
                {
                    propertyInfo.SetValue(null, newNormal.ToArray());
                }
                if (propertyInfo.Name == "AltArchers")
                {
                    propertyInfo.SetValue(null, newAlt.ToArray());
                }
                if (propertyInfo.Name == "SecretArchers")
                {
                    propertyInfo.SetValue(null, newSecret.ToArray());
                }
            }
            
            AddBaseArcherToDict("green", 0, newNormal, newAlt, newSecret);
            AddBaseArcherToDict("blue", 1, newNormal, newAlt, newSecret);
            AddBaseArcherToDict("pink", 2, newNormal, newAlt, newSecret);
            AddBaseArcherToDict("orange", 3, newNormal, newAlt, newSecret);
            AddBaseArcherToDict("white", 4, newNormal, newAlt, newSecret);
            AddBaseArcherToDict("yellow", 5, newNormal, newAlt, newSecret);
            AddBaseArcherToDict("cyan", 6, newNormal, newAlt, newSecret);
            AddBaseArcherToDict("purple", 7, newNormal, newAlt, newSecret);
            AddBaseArcherToDict("red", 8, newNormal, newAlt, newSecret);
            
            SkinPatcher.LoadSkins(allCustomArchers);
        }

        private static void LoadSecretArcher(List<ArcherCustomData> newSecretCustom, Dictionary<string, int> indexDict, List<ArcherCustomData> allCustomArchers,
            ArcherData[] newNormalArray, ArcherData[] newSecret, List<ArcherCustomData> newNormalCustom)
        {
            foreach (var secretArcherData in newSecretCustom)
            {
                if (secretArcherData.parsed)
                {
                    continue;
                }

                var originalName = secretArcherData.originalName;
                var baseArchersIndex = -1;
                if (!indexDict.ContainsKey(originalName))
                {
                    var altOriginal = allCustomArchers.Find(a => a.ID == originalName);
                    if (altOriginal != null)
                    {
                        ArcherCustomDataValidator.PrintLineWithColor(
                            $"Secret Archer '{secretArcherData.ID}' ({secretArcherData.xmlData["Name0"]?.InnerText} {secretArcherData.xmlData["Name1"]?.InnerText}) skipped: {originalName} is alt and cannot have a secret",
                            ConsoleColor.Red);
                        
                        // originalName = altOriginal.originalName;
                        continue;
                    }

                    baseArchersIndex = CheckForBaseArchers(originalName);
                }

                if (!indexDict.ContainsKey(originalName) && baseArchersIndex == -1)
                {
                    ArcherCustomDataValidator.PrintLineWithColor(
                        $"Secret Archer '{secretArcherData.ID}' ({secretArcherData.xmlData["Name0"]?.InnerText} {secretArcherData.xmlData["Name1"]?.InnerText}) skipped: {originalName} not found",
                        ConsoleColor.Red);

                    continue;
                }

                if (FortEntrance.Settings.Validate)
                {
                    var errors =
                        ArcherCustomManager.validator.Validate(
                            secretArcherData.xmlData, 
                            secretArcherData.atlas, 
                            secretArcherData.menuAtlas, 
                            secretArcherData.spriteData, 
                            secretArcherData.menuSpriteData, 
                            ArcherCustomManager.GetArcherType(secretArcherData.xmlData.Name), 
                            secretArcherData.ID, 
                            secretArcherData.FolderPath);
                    if (ArcherCustomDataValidator.PrintErrors(secretArcherData.ID, errors, secretArcherData.xmlData.Name, secretArcherData.xmlData["Name0"]?.InnerText, secretArcherData.xmlData["Name1"]?.InnerText))
                        continue;

                }
       
                var originalIndex = baseArchersIndex == -1 ? indexDict[originalName] : baseArchersIndex;
                if (originalIndex != -1)
                {
                    secretArcherData.Parse(newNormalArray[originalIndex], secretArcherData.FolderPath);
                    newSecret[originalIndex] = secretArcherData.ToArcherData();
                    ArcherCustomDataDict[newSecret[originalIndex]] = secretArcherData;
                    // if(secretArcherData.CharacterSounds != null)
                    // _customSFXList.Add(secretArcherData.CharacterSounds);
                    continue;
                }

                var original = newNormalCustom.Find(a => a.ID == originalName);
                if (original == null)
                {
                    ArcherCustomDataValidator.PrintLineWithColor(
                        $"Secret Archer '{secretArcherData.ID}' ({secretArcherData.xmlData["Name0"]?.InnerText} {secretArcherData.xmlData["Name1"]?.InnerText}) skipped: {originalName} not found",
                        ConsoleColor.Red);
                    
                    continue;
                }

                var originalData = original.ToArcherData();
                secretArcherData.Parse(originalData, secretArcherData.FolderPath);
                newSecret[originalIndex] = secretArcherData.ToArcherData();
                secretArcherData.original = originalData;
                ArcherCustomDataDict[newSecret[originalIndex]] = secretArcherData;
                // if(secretArcherData.CharacterSounds != null)
                //     _customSFXList.Add(secretArcherData.CharacterSounds);
            }
        }

        private static void LoadAltArcher(List<ArcherCustomData> newAltCustom, Dictionary<string, int> indexDict, List<ArcherCustomData> newNormalCustom, ArcherData[] newAlt)
        {
            foreach (var altArcherData in newAltCustom)
            {
                if (altArcherData.parsed)
                {
                    continue;
                }

                var originalName = altArcherData.originalName;
                var original = newNormalCustom.Find(a => a.ID == originalName);
                if (original == null)
                {
                    ArcherCustomDataValidator.PrintLineWithColor(
                        $"Alt Archer '{altArcherData.ID}' ({altArcherData.xmlData["Name0"]?.InnerText} {altArcherData.xmlData["Name1"]?.InnerText}) skipped: {originalName} not found",
                        ConsoleColor.Red);

                    continue;
                }

                if (FortEntrance.Settings.Validate)
                {
                    var errors =
                        ArcherCustomManager.validator.Validate(
                            altArcherData.xmlData,
                            altArcherData.atlas,
                            altArcherData.menuAtlas,
                            altArcherData.spriteData,
                            altArcherData.menuSpriteData,
                            ArcherCustomManager.GetArcherType(altArcherData.xmlData.Name),
                            altArcherData.ID,
                            altArcherData.FolderPath);

                    if (ArcherCustomDataValidator.PrintErrors(altArcherData.ID, errors, altArcherData.xmlData.Name,
                        altArcherData.xmlData["Name0"]?.InnerText, altArcherData.xmlData["Name1"]?.InnerText))
                        continue;
                }

                if (!indexDict.TryGetValue(originalName, out var originalIndex))
                {
                    ArcherCustomDataValidator.PrintLineWithColor(
                        $"Alt Archer '{altArcherData.ID}' ({altArcherData.xmlData["Name0"]?.InnerText} {altArcherData.xmlData["Name1"]?.InnerText}) skipped: {originalName} not found",
                        ConsoleColor.Red);

                    continue;
                }

                var originalData = original.ToArcherData();
                altArcherData.original = originalData;
                altArcherData.Parse(originalData, original.FolderPath);
                newAlt[originalIndex] = altArcherData.ToArcherData();
                ArcherCustomDataDict[newAlt[originalIndex]] = altArcherData;

                // if(altArcherData.CharacterSounds != null)
                // _customSFXList.Add(altArcherData.CharacterSounds);
            }
        }

        private static List<ArcherCustomData> LoadContentAtPath(FortContent content, string path, ContentAccess contentAccess, bool warnNotFound = true)
        {
            var allCustomArchers = new List<ArcherCustomData>();
            if (!ModIO.IsDirectoryOrFileExists(path))
            {
                if(warnNotFound)
                    Console.WriteLine($"\nNo Archer Found in \"{path}\" Folder");
                return allCustomArchers;
            }
               
            var customArchersFound = ModIO.GetDirectories(path);
            
            foreach (var directory in customArchersFound)
            {
                // if(directory.EndsWith("Content"))
                //     continue;

                if (contentAccess == ContentAccess.ModContent) 
                {
                    allCustomArchers.AddRange(LoadContent(content, directory, contentAccess, contentAccess == ContentAccess.Content));
                    continue;
                } 
                
                allCustomArchers.AddRange(LoadContent(null, directory, contentAccess, contentAccess == ContentAccess.Content));
            }

            if (warnNotFound && allCustomArchers.Count == 0)
            {
                Console.WriteLine($"\nNo New Archers Found in \"{path}\" Folder");
            }
            if (allCustomArchers.Count > 0)
            {
                ArcherCustomDataValidator.PrintLineWithColor($"\n{allCustomArchers.Count} New Archer(s) Found in \"{path}\" Folder", ConsoleColor.DarkGreen);
            }
               
            if (allCustomArchers.Count == 0)
            {
                return allCustomArchers;
            }
            foreach (var archerCustomData in allCustomArchers)
            {
                var meta = string.IsNullOrEmpty(archerCustomData?.Meta?.Author)? "" : $"By {archerCustomData.Meta.Author}";
                var type = archerCustomData?.ArcherType == (ArcherData.ArcherTypes) 3 ? "Skin" : archerCustomData?.ArcherType.ToString();
                ArcherCustomDataValidator.PrintLineWithColor($"{archerCustomData?.ID} {type} ({archerCustomData?.Name0 + " " + archerCustomData?.Name1}) {meta}", ConsoleColor.Green);
            }
            
            return allCustomArchers;
        }

        private static List<ArcherCustomData> LoadContent(FortContent content, string directory, ContentAccess contentAccess, bool addContentPrefix = false)
        {
            var newArchers = new List<ArcherCustomData>();

            var archerName = directory.Split(Convert.ToChar(_separator)).Last();
            var path = directory + "/";

            
            Atlas atlas = null;
            if (ModIO.IsDirectoryOrFileExists($"{path}atlas.xml") && 
                ModIO.IsDirectoryOrFileExists($"{path}atlas.png"))
            {
                atlas = AtlasExt.CreateAtlas($"{path}atlas.xml", $"{path}atlas.png");
                customAtlasList.Add(atlas);
            }
            
            if (!ModIO.IsDirectoryOrFileExists($"{path}spriteData.xml") || atlas == null)
            {
                return newArchers;
            }
            
            var spriteData = SpriteDataExt.CreateSpriteData($"{path}spriteData.xml", atlas);
            var sprites = DynamicData.For(spriteData).Get<Dictionary<string, XmlElement>>("sprites");

            if (sprites.Count > 0)
            {
                foreach (var sprite in sprites)
                {
                    if (!sprite.Value.HasAttribute("Category") && !sprite.Value.HasAttribute("category")) continue;
                    var category = sprite.Value.HasAttribute("Category")
                        ? sprite.Value.GetAttribute("Category")
                        : sprite.Value.GetAttribute("category");

                    var lower = category.ToLower();
                    if (!customSpriteDataCategoryDict.ContainsKey(lower))
                    {
                        customSpriteDataCategoryDict[lower]
                            = new List<CustomSpriteDataInfo>();
                    }

                    customSpriteDataCategoryDict[lower].Add(new CustomSpriteDataInfo()
                    {
                        id = sprite.Key,
                        Element = sprite.Value,
                        PathName = directory + Path.DirectorySeparatorChar
                    });
                }

                customSpriteDataList.Add(spriteData);
                customSpriteDataPath.Add(spriteData, $"{path}spriteData.xml");
            }
        
            var atlasArcherMenu = atlas;
            SpriteData spriteDataMenu = spriteData;
            if (ModIO.IsDirectoryOrFileExists($"{path}menuAtlas.xml") &&
                ModIO.IsDirectoryOrFileExists($"{path}menuAtlas.png"))
            {
                atlasArcherMenu = AtlasExt.CreateAtlas($"{path}menuAtlas.xml", $"{path}menuAtlas.png");
                customAtlasList.Add(atlasArcherMenu);
                spriteDataMenu = SpriteDataExt.CreateSpriteData($"{path}menuSpriteData.xml", atlasArcherMenu);
                customSpriteDataList.Add(spriteDataMenu);
                customSpriteDataPath.Add(spriteDataMenu, $"{path}menuSpriteData.xml");
            }

            var filePath = $"{path}archerData.xml";
            if (!ModIO.IsDirectoryOrFileExists(filePath)) return newArchers;
            var newArchersFromPack = 
                InitializeArcherData(path, atlas, atlasArcherMenu, spriteData, spriteDataMenu, archerName.ToUpper());
            return newArchersFromPack;
        }

        private static void AddBaseArcherToDict(string name, int index, List<ArcherData> newNormal, ArcherData[] newAlt, ArcherData[] newSecret)
        {
            BaseArcherByNameDict.Add(name, newNormal[index]);
            BaseArcherByNameDict.Add(name + "_alt", newAlt[index]);
            BaseArcherByNameDict.Add(name + "_secret", newSecret[index]);
        }

        public static void FixSFX()
        {
            foreach (var archerCustomData in ArcherCustomDataDict)
            {
                var customData = archerCustomData.Value;
                customData.HandleSFX(customData.xmlData, customData.original);
            } 

            var newSFXList = new List<CharacterSounds>(Sounds.Characters);
            newSFXList.AddRange(customSFXList);
            Sounds.Characters = newSFXList.ToArray();

            foreach (var archerCustomData in ArcherCustomDataDict)
            {
                var customData = archerCustomData.Value;
                var index = newSFXList.IndexOf(customData.CharacterSounds);
                if(index > -1)
                    archerCustomData.Key.SFXID = newSFXList.IndexOf(customData.CharacterSounds);
            } 
        }

        private static void TestVictorySong(ArcherCustomData customData)
        {
            var victory = customData?.victory;
            if (victory == null)
                return;
            var masterVolume = Audio.MasterVolume;
            if (Music.MasterVolume > 0 && masterVolume == 0)
                Audio.MasterVolume = 1;
            Music.Stop();
            var volume = Music.MasterVolume * 2f;
            victory.Play(160,  volume > 1 ? 1 : volume);
            Audio.MasterVolume = masterVolume;
        }

        public static int CheckForBaseArchers(string name)
        {
            if (name == "GREEN")
                return 0;
            if (name == "BLUE")
                return 1;
            if (name == "PINK")
                return 2;
            if (name == "ORANGE")
                return 3;
            if (name == "WHITE")
                return 4;
            if (name == "YELLOW")
                return 5;
            if (name == "CYAN")
                return 6;
            if (name == "PURPLE")
                return 7;
            if (name == "RED")
                return 8;
            return -1;
        }
        
        public static int CheckForBaseSFXArchers(string name)
        {
            if (name == "GREEN")
                return 0;
            if (name == "BLUE")
                return 1;
            if (name == "PINK")
                return 2;
            if (name == "ORANGE")
                return 3;
            if (name == "WHITE")
                return 4;
            if (name == "YELLOW")
                return 5;
            if (name == "CYAN")
                return 6;
            if (name == "PURPLE")
                return 7;
            if (name == "RED")
                return 8;
            if (name == "YELLOW_ALT")
                return 9;
            if (name == "GREEN_ALT")
                return 10;
            if (name == "ORANGE_ALT")
                return 11;
            if (name == "PINK_ALT")
                return 12;
            if (name == "RED_ALT")
                return 13;
            if (name == "GREEN")
                return 0;
            if (name == "BLUE_ALT")
                return 1;
            if (name == "WHITE_ALT")
                return 4;
            if (name == "CYAN_ALT")
                return 6;
            if (name == "PURPLE_ALT")
                return 7;
            return -1;
        }
        
        public static List<ArcherCustomData> InitializeArcherData(string path, Atlas atlasArcher, Atlas atlasArcherMenu, SpriteData spriteData, SpriteData spriteDataMenu,
            string archerName)
        {
            // Console.WriteLine("InitializeArcherData");
            return ArcherCustomManager.Initialize(path, atlasArcher, atlasArcherMenu, spriteData, spriteDataMenu, archerName, FortEntrance.Settings.Validate);
        }
        
        public static void Unload()
        {
            ContentLoaderPatcher.Unload();
            TauntVariant.Unload();
            ParticlePatcher.Unload();
            WingsPatcher.Unload();
            GhostPatcher.Unload();
            HairPatcher.Unload();
            VictoryMusicPatcher.Unload();
            SkinPatcher.Unload();
            LayerPatch.Unload();
            PortraitLayerPatch.Unload();
            PrismaticPatcher.Unload();
            TeamsPatcher.Unload();

            RiseCore.Events.OnAfterModdedLoadContent -= OnAfterLoadContent;
        }

        public static void OnVariantsRegister(VariantManager variants, bool noPerPlayer = false)
        {
            TauntVariant.OnVariantsRegister(variants, noPerPlayer);
        }

        public static string GetForAttribute(XmlElement xmlElement)
        {
            var forAttribute = "For";
            if (xmlElement.HasAttribute(forAttribute)) return forAttribute;
            forAttribute = "for";
            if (xmlElement.HasAttribute(forAttribute)) return forAttribute;
            forAttribute = "FOR";
            if (!xmlElement.HasAttribute(forAttribute))
            {
                forAttribute = null;
            }

            return forAttribute;
        }


        public static XmlElement FindSpriteDataXmlOnCategories(string category, ArcherData archerData)
        {
            XmlElement spriteDataXml = null;
            var id = FindOnCustomCategories(category, archerData);
            if (id != null)
            {
                spriteDataXml = TFGame.SpriteData.GetXML(id);
            }

            return spriteDataXml;
        }

        public static Sprite<string> FindSpriteOnCategories(string category, ArcherData archerData)
        {
            Sprite<string> spriteDataXml = null;
            var id = FindOnCustomCategories(category, archerData);
            if (id != null)
            {
                spriteDataXml = TFGame.SpriteData.GetSpriteString(id);
            }

            return spriteDataXml;
        }
        
        public static string FindOnCustomCategories(string categoryName, ArcherData archerData)
        {
            if (!customSpriteDataCategoryDict.TryGetValue(categoryName.ToLower(), out var category))
            {
                return null;
            }

            foreach (var customSpriteData in category)
            {
                var xmlElement = customSpriteData.Element;

                var forAttribute = GetForAttribute(xmlElement);
                if (string.IsNullOrEmpty(forAttribute)) continue;
                BaseArcherByNameDict.TryGetValue(xmlElement.GetAttribute(forAttribute).ToLower(),
                    out var searchArcherData);
                if (searchArcherData == null)
                {
                    foreach (var customData in ArcherCustomDataDict)
                    {
                        if (customData.Value.ID == xmlElement.GetAttribute(forAttribute))
                        {
                            searchArcherData = customData.Key;
                        }
                    }
                }

                if (archerData != searchArcherData)
                {
                    continue;
                }
                
                return customSpriteData.id;
            }

            return null;
        }
    }
    
   

    public class CustomSpriteDataInfo
    {
        public string PathName;
        public string id;
        public XmlElement Element;
    }
}
