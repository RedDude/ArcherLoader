﻿using System;
using System.Collections.Generic;
using System.Reflection;
using ArcherLoaderMod.Source.Layers.PortraitLayers;
using FortRise;
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
            var archerPortraitDynamic = DynamicData.For(archerPortrait);
            var offset = archerPortraitDynamic.Get<Microsoft.Xna.Framework.Vector2>("offset");
            var portrait = archerPortraitDynamic.Get<Image>("portrait");
            
            var wiggler = archerPortraitDynamic.Get<Wiggler>("wiggler");
            var gemWiggler = archerPortraitDynamic.Get<Wiggler>("gemWiggler");
            
            Microsoft.Xna.Framework.Rectangle? rect = 
                EightPlayerImport.LaunchedEightPlayer != null ? EightPlayerImport.LaunchedEightPlayer() 
                ? skinArcherData.Portraits.NotJoined.GetAbsoluteClipRect(new Microsoft.Xna.Framework.Rectangle(0, 10, 60, 60))
                : null 
                : null;
            
            portrait.SwapSubtexture(skinArcherData.Portraits.NotJoined, rect);
            
            if (PortraitLayerPatch.Enabled)
            {
                PortraitLayersManager.HideAllLayersFromPortrait(archerPortrait);
                PortraitLayersManager.CreateSelectionLayersComponents(archerPortrait, skinArcherData);
                PortraitLayersManager.ShowAllLayersFromType(PortraitLayersAttachType.NotJoined, archerPortrait, skinArcherData);
            }

            archerPortraitDynamic.Set("ArcherData", skinArcherData);
            var gem = archerPortraitDynamic.Get<Sprite<string>>("gem");
            if (gem != null)
            {
                archerPortrait.Remove(gem);
            }
            var newGem = TFGame.MenuSpriteData.GetSpriteString(archerPortrait.ArcherData.Gems.Menu);
            newGem.Position = offset + new Microsoft.Xna.Framework.Vector2(gem.Position.X, gem.Position.Y);
            newGem.Visible = false;
            archerPortrait.Add(newGem);
            archerPortraitDynamic.Set("gem", newGem);

            if (archerCustomData != null)
            {
                if (archerCustomData.IsGemColorA)
                {
                    newGem.Color = archerCustomData.ColorA;
                }
                if (archerCustomData.IsGemColorB)
                {
                    newGem.Color = archerCustomData.ColorB;
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
                    ArcherCustomDataValidator.PrintLineWithColor(
                        $"Skin Archer '{skinCustomData.ID} ({skinCustomData.xmlData["Name0"]?.InnerText} {skinCustomData.xmlData["Name1"]?.InnerText})' skipped: {originalName} not found",
                        ConsoleColor.Red);

                    return;
                }

                if (FortEntrance.Settings.Validate)
                {
                    var errors =
                        ArcherCustomManager.validator.Validate(
                            skinCustomData.xmlData,
                            skinCustomData.atlas,
                            skinCustomData.menuAtlas,
                            skinCustomData.spriteData,
                            skinCustomData.menuSpriteData,
                            ArcherCustomManager.GetArcherType(skinCustomData.xmlData.Name),
                            skinCustomData.ID,
                            skinCustomData.FolderPath);
                    if (ArcherCustomDataValidator.PrintErrors(skinCustomData.ID, errors, skinCustomData.xmlData.Name,
                        skinCustomData.xmlData["Name0"]?.InnerText, skinCustomData.xmlData["Name1"]?.InnerText))
                        return;
                }

                if (!archerSkins.ContainsKey(data))
                {
                    archerSkins.Add(data, new List<ArcherCustomData>());
                }
                skinCustomData.Parse(data, "");
                
                skinCustomData.original = data;
                
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