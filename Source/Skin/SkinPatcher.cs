using System;
using System.Collections.Generic;
using System.Reflection;
using ArcherLoaderMod.Source.Layers.PortraitLayers;
using Monocle;
using MonoMod.RuntimeDetour;
using MonoMod.Utils;
using TowerFall;

namespace ArcherLoaderMod.Skin
{
    public class SkinPatcher
    {
        public static bool enabled = false;
        
        public static Dictionary<ArcherData, List<ArcherCustomData>> archerSkins = new();
        public static Dictionary<int, Dictionary<ArcherData, int>> archerSkinsIndex = new();
        public static Dictionary<ArcherCustomData, ArcherData> SkinArcherCustomToArcher = new();
        private static Hook hook_ArcherData_Get;
        private static MethodInfo initGem;

        public static void Load()
        {
            initGem = typeof(ArcherPortrait).GetMethod("InitGem", BindingFlags.Instance | BindingFlags.NonPublic);
            
            On.TowerFall.RollcallElement.Update += OnRollcallElementOnUpdate;
            On.TowerFall.ArcherData.Get_int_ArcherTypes += OnArcherDataOnGet_Int_ArcherTypes;
            enabled = true;
        }
  
        public static void Unload()
        {
            if(!enabled)
                return;
            
            On.TowerFall.RollcallElement.Update -= OnRollcallElementOnUpdate;
            On.TowerFall.ArcherData.Get_int_ArcherTypes -= OnArcherDataOnGet_Int_ArcherTypes;
        }

        private static ArcherData OnArcherDataOnGet_Int_ArcherTypes(On.TowerFall.ArcherData.orig_Get_int_ArcherTypes orig, int characterIndex, ArcherData.ArcherTypes type)
        {
            var data = orig(characterIndex, type);
            for (var i = 0; i < TFGame.Players.Length; i++)
            {
                if (!TFGame.Players[i]) continue;
                // var characterIndex = TFGame.Characters[i];
                return GetSkinCharacter(i, data);
            }

            return data;
        }
        
        private static void OnRollcallElementOnUpdate(On.TowerFall.RollcallElement.orig_Update orig, TowerFall.RollcallElement self)
        {
            var playerIndex = DynamicData.For(self).Get<int>("playerIndex");
            var input =  TFGame.PlayerInputs[playerIndex];
            if (input == null)
            {
                orig(self);
                return;
            }
            
            var archerType = DynamicData.For(self).Get<ArcherData.ArcherTypes>("archerType");
            if (input.MenuLeft || input.MenuRight || input.MenuBack || input.MenuAlt)
            {
                var data = ArcherData.Get(self.CharacterIndex, archerType);
                if (archerSkins.ContainsKey(data))
                {
                    archerSkinsIndex[playerIndex][data] = -1;
                }
            }

            orig(self);
        
            var portrait = DynamicData.For(self).Get<ArcherPortrait>("portrait");

            if (input.MenuUp)
            {
                SetCharacter(portrait, playerIndex, self.CharacterIndex, archerType, 1);
            }
            
            if (input.MenuDown)
            {
                SetCharacter(portrait, playerIndex, self.CharacterIndex, archerType, -1);
            }
        }
        
        public static void SetCharacter(ArcherPortrait archerPortrait, int playerIndex, int characterIndex, ArcherData.ArcherTypes altSelect, int moveDir)
        {
            var data = ArcherData.Get(characterIndex, altSelect);

            if(!archerSkins.TryGetValue(data, out var skins))
                return;
            
            var moveIndex = archerSkinsIndex[playerIndex][data] + moveDir;
            if (moveIndex >= skins.Count)
            {
                moveIndex = -1;
            }
            if (moveIndex < -1)
            {
                moveIndex = skins.Count - 1;
            }

            archerSkinsIndex[playerIndex][data] = moveIndex;
            var skinArcherData = data;
            ArcherCustomData skinData = null; 
            if (moveIndex != -1)
            {
                skinData = skins[moveIndex];
                skinArcherData = SkinArcherCustomToArcher[skinData];
            }

            SetCharacterSkinPortrait(archerPortrait, skinArcherData, skinData);
        }

        private static void SetCharacterSkinPortrait(ArcherPortrait archerPortrait, ArcherData skinArcherData,
            ArcherCustomData archerCustomData)
        {
            var portrait = DynamicData.For(archerPortrait).Get<Image>("portrait");
            
            var wiggler = DynamicData.For(archerPortrait).Get<Wiggler>("wiggler");
            var gemWiggler = DynamicData.For(archerPortrait).Get<Wiggler>("gemWiggler");
            portrait.SwapSubtexture(skinArcherData.Portraits.NotJoined);
            
            if (PortraitLayerPatch.Enabled)
            {
                PortraitLayersManager.HideAllLayersFromPortrait(archerPortrait);
                PortraitLayersManager.CreateLayersComponents(archerPortrait, skinArcherData);
                PortraitLayersManager.ShowAllLayersFromType(PortraitLayersAttachType.NotJoin, archerPortrait, skinArcherData);
            }

            DynamicData.For(archerPortrait).Set("ArcherData", skinArcherData);
            initGem?.Invoke(archerPortrait, new object[] { });
            var gem = DynamicData.For(archerPortrait).Get<Sprite<string>>("gem");
            if (archerCustomData != null)
            {
                if (archerCustomData.IsGemColorA)
                {
                    gem.Color = archerCustomData.ColorA;
                }
                if (archerCustomData.IsGemColorB)
                {
                    gem.Color = archerCustomData.ColorB;
                }
            }
            
            Sounds.ui_move1.Play();
            wiggler.Start();
            gemWiggler.Start();
        }

        public static ArcherData GetSkinCharacter(int playerIndex, ArcherData data)
        {
            if(!archerSkins.TryGetValue(data, out var skins))
                return data;

            var skinIndex = archerSkinsIndex[playerIndex][data];
            return skinIndex == -1 ? data : SkinArcherCustomToArcher[skins[skinIndex]];
        }

        public static void LoadSkins(List<ArcherCustomData> allCustomArchers)
        {
            var skinsArchers = allCustomArchers.FindAll(a => a.ArcherType == (ArcherData.ArcherTypes) 3);

            for (var i = 0; i < TFGame.Players.Length; i++)
            {
                archerSkinsIndex[i] = new Dictionary<ArcherData, int>();
            }

            foreach (var skinCustomData in skinsArchers)
            {
                LoadSkinArcher(skinCustomData);
            }
        }
        
        private static void LoadSkinArcher(ArcherCustomData skinCustomData)
        {
                if (skinCustomData.parsed)
                {
                    return;
                }

                var originalName = skinCustomData.originalName;
                if (!Mod.BaseArcherByNameDict.TryGetValue(skinCustomData.originalName.ToLower(), out var data))
                {
                    foreach (var archerCustomData in Mod.ArcherCustomDataDict)
                    {
                        if (archerCustomData.Value.ID != skinCustomData.originalName) continue;
                        data = archerCustomData.Key;
                        break;
                    }
                }

                if (data == null)
                {
                    Console.WriteLine($"Skin Archer '{skinCustomData.ID}' skipped: {originalName} not found");
                    return;
                }
                
                if (!archerSkins.ContainsKey(data))
                {
                    archerSkins.Add(data, new List<ArcherCustomData>());
                }
                skinCustomData.Parse(data, "");
                archerSkins[data].Add(skinCustomData);
                var skinArcherData = skinCustomData.ToArcherData();
                SkinArcherCustomToArcher[skinCustomData] = skinArcherData;
                Mod.ArcherCustomDataDict[skinArcherData] = skinCustomData;
                
                for (var i = 0; i < TFGame.Players.Length; i++)
                {
                    if (!archerSkinsIndex[i].ContainsKey(data))
                    {
                        archerSkinsIndex[i][data] = -1;
                    }
                }
                
                // if(skinArcherData.CharacterSounds != null)
                // _customSFXList.Add(skinArcherData.CharacterSounds);
        }
    }
}