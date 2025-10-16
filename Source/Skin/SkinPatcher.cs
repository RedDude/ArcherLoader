using System;
using System.Collections.Generic;
using System.Reflection;
using ArcherLoaderMod.Source.Layers.PortraitLayers;
using HarmonyLib;
using Monocle;
using MonoMod.Utils;
using TowerFall;

namespace ArcherLoaderMod.Skin
{
    public class SkinPatcher
    {
        private static Harmony harmony;
        private static MethodInfo initGem;
        public static bool Enabled = false;

        public static Dictionary<ArcherData, List<ArcherCustomData>> archerSkins = new();
        public static Dictionary<int, Dictionary<ArcherData, int>> archerSkinsIndex = new();
        public static Dictionary<ArcherCustomData, ArcherData> SkinArcherCustomToArcher = new();

        public static void Load()
        {
            if (FortEntrance.Instance.Settings.DisableLayers) return;

            harmony = new Harmony("mod.archerloader.skin");

            // Patch methods
            harmony.Patch(
                typeof(RollcallElement).GetMethod("Update"),
                prefix: new HarmonyMethod(typeof(SkinPatcher), nameof(RollcallElement_Update_Prefix)),
                postfix: new HarmonyMethod(typeof(SkinPatcher), nameof(RollcallElement_Update_Postfix))
            );

            harmony.Patch(
                typeof(ArcherData).GetMethod("Get", new[] { typeof(int), typeof(ArcherData.ArcherTypes) }),
                prefix: new HarmonyMethod(typeof(SkinPatcher), nameof(ArcherData_Get_Prefix))
            );

            initGem = typeof(ArcherPortrait).GetMethod("InitGem", BindingFlags.Instance | BindingFlags.NonPublic);

            Enabled = true;
        }

        public static void Unload()
        {
            harmony?.UnpatchAll();
        }

        [HarmonyPrefix]
        private static void RollcallElement_Update_Prefix(RollcallElement __instance)
        {
            var playerIndex = DynamicData.For(__instance).Get<int>("playerIndex");
            var input = TFGame.PlayerInputs[playerIndex];
            if (input == null) return;

            var archerType = DynamicData.For(__instance).Get<ArcherData.ArcherTypes>("archerType");
            if (input.MenuLeft || input.MenuRight || input.MenuBack || input.MenuAlt)
            {
                var data = ArcherData.Get(__instance.CharacterIndex, archerType);
                if (archerSkins.ContainsKey(data))
                {
                    archerSkinsIndex[playerIndex][data] = -1;
                }
            }
        }

        [HarmonyPostfix]
        private static void RollcallElement_Update_Postfix(RollcallElement __instance)
        {
            var playerIndex = DynamicData.For(__instance).Get<int>("playerIndex");
            var input = TFGame.PlayerInputs[playerIndex];
            if (input == null) return;

            var state = DynamicData.For(__instance).Get<StateMachine>("state");
            if (state.State == 1) return;

            var portrait = DynamicData.For(__instance).Get<ArcherPortrait>("portrait");
            var characterIndex = __instance.CharacterIndex;
            var archerType = DynamicData.For(__instance).Get<ArcherData.ArcherTypes>("archerType");

            if (input.MenuUp)
            {
                SetCharacter(portrait, playerIndex, characterIndex, archerType, 1);
            }
            else if (input.MenuDown)
            {
                SetCharacter(portrait, playerIndex, characterIndex, archerType, -1);
            }
        }

        [HarmonyPrefix]
        private static bool ArcherData_Get_Prefix(int characterIndex, ArcherData.ArcherTypes type, ref ArcherData __result)
        {
            // Call original method to get base data
            // __result = ArcherData.Archers[characterIndex].ArcherTypes[(int)type];

            // Apply skin override for players
            for (var i = 0; i < TFGame.Players.Length; i++)
            {
                if (!TFGame.Players[i]) continue;
                if (TFGame.Characters[i] != characterIndex) continue;

                __result = GetSkinCharacter(i, __result);
                return false; // Skip original
            }

            return true; // Continue to original
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
            if (!ArcherLoaderMod.BaseArcherByNameDict.TryGetValue(skinCustomData.originalName.ToLower(), out var data))
            {
                foreach (var archerCustomData in ArcherLoaderMod.ArcherCustomDataDict)
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

            if (FortEntrance.Instance.Settings.Validate)
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
            ArcherLoaderMod.ArcherCustomDataDict[skinArcherData] = skinCustomData;

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