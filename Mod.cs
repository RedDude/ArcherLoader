using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml;
using ArcherLoaderMod.Ghost;
using ArcherLoaderMod.Hair;
using ArcherLoaderMod.Layers;
using ArcherLoaderMod.Particles;
using ArcherLoaderMod.Patch;
using ArcherLoaderMod.Skin;
using ArcherLoaderMod.Source.Layers.PortraitLayers;
using ArcherLoaderMod.Taunt;
using ArcherLoaderMod.Wings;
using FortRise;
using Monocle;
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
        
        public static Dictionary<string, List<CustomSpriteDataInfo>> customSpriteDataCategoryDict = new();
        
        public static List<Atlas> customAtlasList = new();
        public static List<Atlas> cachedCustomAtlasList = new();
        
        public static List<ArcherData> AllArchersDataDict = new();
        
        public static Dictionary<ArcherData, ArcherCustomData> ArcherCustomDataDict = new();
        
        public static Dictionary<string, ArcherData> BaseArcherByNameDict = new();
        
        public static List<CharacterSounds> customSFXList = new();

        
        public static FortContent Content;
        private static string _customArchersPath;

        public static void LoadContent(FortContent fortContent)
        {
            TauntVariant.LoadContent(fortContent);
            Content = fortContent;
        }
        
        public static void Load()
        {
            Console.WriteLine("Custom Archer Loader is here!");
            
            _separator = Path.DirectorySeparatorChar.ToString();
            _customArchersPath = $"CustomArchers{_separator}";
            _contentCustomArchersPath = $"Mod{_separator}"+_customArchersPath;
                
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

                var matchSettings = new MatchSettings(GameData.VersusTowers[0].GetLevelSystem(), Modes.LevelTest,
                    MatchSettings.MatchLengths.Standard);
                (matchSettings.LevelSystem as VersusLevelSystem).StartOnLevel(-1);
                new Session(matchSettings).StartGame();

                On.TowerFall.MainMenu.Update -= OnMainMenuOnUpdate;
            }

            On.TowerFall.MainMenu.Update += OnMainMenuOnUpdate;
        }

        public static void Start()
        {
            var allCustomArchers = new List<ArcherCustomData>();

            #if DEBUG
            Debugger.Launch();
            #endif
            allCustomArchers.AddRange(LoadContentAtPath($"{Calc.LOADPATH}{_contentCustomArchersPath}", ContentAccess.Content));
            var contentPath = Content.GetContentPath("");
            allCustomArchers.AddRange(LoadContentAtPath(contentPath+$"/{_customArchersPath}", ContentAccess.ModContent));
            allCustomArchers.AddRange(LoadContentAtPath($"{_customArchersPath}", ContentAccess.Root));
            allCustomArchers.AddRange(LoadContentAtPath(contentPath.Replace("/Content", "")+$"/{_customArchersPath}", ContentAccess.Root));
            // allCustomArchers.AddRange(LoadContentAtPath(Content.GetContentPath("").Replace("/Content", ""), ContentAccess.Root));

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
                        Console.WriteLine(
                            $"Secret Archer '{secretArcherData.ID}' skipped: {originalName} is alt and cannot have a secret");
                        // originalName = altOriginal.originalName;
                        continue;
                    }

                    baseArchersIndex = CheckForBaseArchers(originalName);
                }

                if (!indexDict.ContainsKey(originalName) && baseArchersIndex == -1)
                {
                    Console.WriteLine($"Secret Archer '{secretArcherData.ID}' skipped: {originalName} not found");
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
                    Console.WriteLine($"Secret Archer '{secretArcherData.ID}' skipped: {originalName} not found");
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
                var originalIndex = indexDict[originalName];
                var original = newNormalCustom.Find(a => a.ID == originalName);
                if (original == null)
                {
                    Console.WriteLine($"Alt Archer '{altArcherData.ID}' skipped: {originalName} not found");
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

        private static List<ArcherCustomData> LoadContentAtPath(string path, ContentAccess contentAccess)
        {
            var allCustomArchers = new List<ArcherCustomData>();
            if (!Directory.Exists(path))
            {
                Console.WriteLine($"\nNo Archer Found in \"{path}\" Folder");
                return allCustomArchers;
            }
               
            var customArchersFound = Directory.GetDirectories(path);
            
            foreach (var directory in customArchersFound)
            {
                allCustomArchers.AddRange(LoadArchersContent(Content, directory, contentAccess, contentAccess == ContentAccess.Content));
            }

            Console.WriteLine($"\n{allCustomArchers.Count} New Archer(s) Found in \"{path}\" Folder");
            if (allCustomArchers.Count == 0)
            {
                return allCustomArchers;
            }
            foreach (var archerCustomData in allCustomArchers)
            {
                var meta = string.IsNullOrEmpty(archerCustomData?.Meta?.Author)? "" : $"By {archerCustomData.Meta.Author}";
                Console.WriteLine(
                    $"{archerCustomData?.ID} {archerCustomData?.ArcherType} ({archerCustomData?.Name0 + " " + archerCustomData?.Name1}) {meta}");
            }
            
            return allCustomArchers;
        }

        private static List<ArcherCustomData> LoadArchersContent(FortContent content, string directory, ContentAccess contentAccess, bool addContentPrefix = false)
        {
            var archerName = directory.Split(Convert.ToChar(_separator)).Last();
            var path = $"{directory}{_separator}".Replace($"Content{_separator}", $"");
            var atlasArcher = content.CreateAtlas($"{path}atlas.xml", $"{path}atlas.png", true, contentAccess);

            var pathWithContentPrefix = addContentPrefix ? Calc.LOADPATH + path : path;
            // var atlasArcher = new Atlas($"{path}atlas.xml", $"{path}atlas.png", load: true);
            customAtlasList.Add(atlasArcher);

            if (!File.Exists($"{pathWithContentPrefix}spriteData.xml"))
            {
                return new List<ArcherCustomData>(0);
            }

            var spriteData = content.CreateSpriteData($"{path}spriteData.xml", atlasArcher, contentAccess);
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
            }
        
            var atlasArcherMenu = atlasArcher;
            if (File.Exists($"{pathWithContentPrefix}menuAtlas.xml") &&
                File.Exists($"{pathWithContentPrefix}menuAtlas.png"))
            {
                atlasArcherMenu = content.CreateAtlas($"{path}menuAtlas.xml", $"{path}menuAtlas.png", load: true, contentAccess);
                customAtlasList.Add(atlasArcherMenu);
                var spriteDataMenu = content.CreateSpriteData($"{path}menuSpriteData.xml", atlasArcherMenu, contentAccess);
                customSpriteDataList.Add(spriteDataMenu);
            }

            var filePath = $"{pathWithContentPrefix}archerData.xml";
            if (!File.Exists(filePath)) return new List<ArcherCustomData>(0);
            var newArchersFromPack = InitializeArcherData(pathWithContentPrefix, atlasArcher, atlasArcherMenu, archerName.ToUpper());
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
        
        public static List<ArcherCustomData> InitializeArcherData(string path, Atlas atlasArcher, Atlas atlasArcherMenu,
            string archerName)
        {
            // Console.WriteLine("InitializeArcherData");
            return ArcherCustomData.Initialize(path, atlasArcher, atlasArcherMenu, archerName);
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
        }

        public static void OnVariantsRegister(MatchVariants variants, bool noPerPlayer = false)
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
    }
    
    

    public class CustomSpriteDataInfo
    {
        public string PathName;
        public string id;
        public XmlElement Element;
    }
}
