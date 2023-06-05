using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using BepInEx.NetLauncher.Common;
using Monocle;
using TowerFall;

namespace CustomArcherLoad
{
    [BepInPlugin("reddude.archerLoader", "Custom Archer Loader Plug-In", "1.0.0.0")]
    public class Mod : BasePlugin
    {
        private static string _separator;
        private static string _customArchersPath;

        public static ManualLogSource Logger;

        public static List<SpriteData> customSpriteDataList = new List<SpriteData>();
        public static List<SpriteData> cachedCustomSpriteDataList = new List<SpriteData>();
        
        public static List<Atlas> customAtlasList = new List<Atlas>();
        public static List<Atlas> cachedCustomAtlasList = new List<Atlas>();
        
        public static Dictionary<ArcherData, ArcherCustomData> ArcherCustomDataDict
            = new Dictionary<ArcherData, ArcherCustomData>();
        
        public static List<CharacterSounds> customSFXList = new List<CharacterSounds>();
        
        public override void Load()
        {
            Logger = Log;
            Log.LogInfo("Custom Archer Loader is here!");
            
            _separator = Path.DirectorySeparatorChar.ToString();
            _customArchersPath = $"Mod{_separator}CustomArchers{_separator}";
                
            Directory.CreateDirectory($"{Calc.LOADPATH}{_customArchersPath}");
            var patcher = new ContentLoaderPatcher(Log);
            patcher.Patch();
            
            var hairPatcher = new HairPatcher(Log);
            hairPatcher.Patch();
            
            var victoryMusicPatcher = new VictoryMusicPatcher();
            victoryMusicPatcher.Patch();
        }

        public static void Start()
        {
            var customArchersFound = Directory.GetDirectories($"{Calc.LOADPATH}{_customArchersPath}");
         
            var allCustomArchers = new List<ArcherCustomData>();
            
            foreach (var directory in customArchersFound)
            {
                var archerName = directory.Split(Convert.ToChar(_separator)).Last();
                var path = $"{directory}{_separator}".Replace($"Content{_separator}", $"");
                var atlasArcher = new Atlas($"{path}atlas.xml", $"{path}atlas.png", load: true);
                customAtlasList.Add(atlasArcher);

                var spriteData = new SpriteData($"{path}spriteData.xml", atlasArcher);

                customSpriteDataList.Add(spriteData);
                // foreach (var atlasArcherSubTexture in spriteData.sprites)
                // {
                //     Logger.LogInfo(atlasArcherSubTexture.Key);
                // }
                var atlasArcherMenu = atlasArcher; 
                if (File.Exists(Calc.LOADPATH + $"{path}menuAtlas.xml") &&
                    File.Exists(Calc.LOADPATH + $"{path}menuAtlas.png"))
                {
                    atlasArcherMenu = new Atlas($"{path}menuAtlas.xml", $"{path}menuAtlas.png", load: true);
                    customAtlasList.Add(atlasArcherMenu);
                    var spriteDataMenu = new SpriteData($"{path}menuSpriteData.xml", atlasArcherMenu);
                    customSpriteDataList.Add(spriteDataMenu);
                }
               
                var newArchersFromPack = InitializeArcherData(path, atlasArcher, atlasArcherMenu, archerName.ToUpper());
                allCustomArchers.AddRange(newArchersFromPack);
            }

            Logger.LogInfo("New Archers Found:");
            foreach (var archerCustomData in allCustomArchers)
            {
                Logger.LogInfo($"{archerCustomData.ID} {archerCustomData.ArcherType} ({archerCustomData.Name0 + " " + archerCustomData.Name1})");
            }
           
            var newNormalCustom = allCustomArchers.FindAll(a => a.ArcherType == ArcherData.ArcherTypes.Normal);
            var newAltCustom = allCustomArchers.FindAll(a => a.ArcherType == ArcherData.ArcherTypes.Alt);
            var newSecretCustom = allCustomArchers.FindAll(a => a.ArcherType == ArcherData.ArcherTypes.Secret);

            var newNormal = ArcherData.Archers.ToList();

            var indexDict = new Dictionary<string, int>();
            var newIndex = newNormal.Count;
            foreach (var customData in newNormalCustom)
            {
                var data = customData.ToArcherData();
                ArcherCustomDataDict[data] = customData;
               
                newNormal.Add(data);
                indexDict[customData.ID] = newIndex++;

                // if(customData.CharacterSounds != null)
                //     _customSFXList.Add(customData.CharacterSounds);
            }
            
            var newNormalArray = newNormal.ToArray();
            var newAlt = new ArcherData[newNormal.Count];
            var newSecret = new ArcherData[newNormal.Count];
            
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
                    Logger.LogError($"Alt Archer '{altArcherData.ID}' skipped: {originalName} not found");
                    continue;
                }
                    
                altArcherData.Parse(original.ToArcherData(), original.FolderPath);
                newAlt[originalIndex] = altArcherData.ToArcherData();
                ArcherCustomDataDict[newAlt[originalIndex]] = altArcherData;
                // if(altArcherData.CharacterSounds != null)
                    // _customSFXList.Add(altArcherData.CharacterSounds);
            }
            
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
                        Logger.LogError($"Secret Archer '{secretArcherData.ID}' skipped: {originalName} is alt and cannot have a secret");
                        // originalName = altOriginal.originalName;
                        continue;
                    }

                    baseArchersIndex = CheckForBaseArchers(originalName);
                }
                
                if (!indexDict.ContainsKey(originalName) && baseArchersIndex == -1)
                {
                    Logger.LogError($"Secret Archer '{secretArcherData.ID}' skipped: {originalName} not found");
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
                    Logger.LogError($"Secret Archer '{secretArcherData.ID}' skipped: {originalName} not found");
                    continue;
                }
                secretArcherData.Parse(original.ToArcherData(), secretArcherData.FolderPath);
                newSecret[originalIndex] = secretArcherData.ToArcherData();
                ArcherCustomDataDict[newSecret[originalIndex]] = secretArcherData;
                // if(secretArcherData.CharacterSounds != null)
                //     _customSFXList.Add(secretArcherData.CharacterSounds);
            }
            
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
            victory.Play(160, Music.MasterVolume * 2f);
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
            return -1;
        }
        
        public static List<ArcherCustomData> InitializeArcherData(string path, Atlas atlasArcher, Atlas atlasArcherMenu,
            string archerName)
        {
            // Logger.LogInfo("InitializeArcherData");
            return ArcherCustomData.Initialize(path, atlasArcher, atlasArcherMenu, archerName);
        }
    }    
}
