using System;
using System.Collections.Generic;
using ArcherEditorMod.Rainbow;
using ArcherEditorMod.Skin;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Monocle;
using MonoMod.Utils;
using TowerFall;

namespace ArcherEditorMod.Source.Layers.PortraitLayers
{
    public class PortraitLayerPatch
    {
        private static Harmony harmony;
        public static bool Enabled;

        public static void Load()
        {
            if (FortEntrance.Instance.Settings.DisableLayers)
                return;

            harmony = new Harmony("mod.archereditor.portraitlayers");
            
            // Patch methods
            harmony.Patch(
                typeof(PauseMenu).GetMethod("VersusArcherSelect"),
                prefix: new HarmonyMethod(typeof(PortraitLayerPatch), nameof(PauseMenu_VersusArcherSelect_Prefix))
            );
            
            harmony.Patch(
                typeof(MainMenu).GetMethod("DestroyRollcall"),
                postfix: new HarmonyMethod(typeof(PortraitLayerPatch), nameof(MainMenu_DestroyRollcall_Postfix))
            );
            
            harmony.Patch(
                typeof(ArcherPortrait).GetMethod("SetCharacter"),
                prefix: new HarmonyMethod(typeof(PortraitLayerPatch), nameof(ArcherPortrait_SetCharacter_Prefix))
            );
            
            harmony.Patch(
                typeof(ArcherPortrait).GetMethod("StartJoined"),
                postfix: new HarmonyMethod(typeof(PortraitLayerPatch), nameof(ArcherPortrait_StartJoined_Postfix))
            );
            
            harmony.Patch(
                typeof(ArcherPortrait).GetMethod("Leave"),
                postfix: new HarmonyMethod(typeof(PortraitLayerPatch), nameof(ArcherPortrait_Leave_Postfix))
            );
            
            harmony.Patch(
                typeof(RollcallElement).GetConstructor(new[] { typeof(int) }),
                postfix: new HarmonyMethod(typeof(PortraitLayerPatch), nameof(RollcallElement_ctor_Postfix))
            );
            
            harmony.Patch(
                typeof(VersusPlayerMatchResults).GetConstructor(new[] { 
                    typeof(Session), typeof(VersusMatchResults), typeof(int), 
                    typeof(Vector2), typeof(Vector2), typeof(List<AwardInfo>) 
                }),
                postfix: new HarmonyMethod(typeof(PortraitLayerPatch), nameof(VersusPlayerMatchResults_ctor_Postfix))
            );
        }

        public static void Unload()
        {
            harmony?.UnpatchAll();
        }

        [HarmonyPrefix]
        private static void PauseMenu_VersusArcherSelect_Prefix()
        {
            // Reset skin indexes
            foreach (var playerDict in SkinPatcher.archerSkinsIndex.Values)
            {
                foreach (var archer in playerDict.Keys)
                {
                    playerDict[archer] = -1;
                }
            }
        }

        [HarmonyPostfix]
        private static void MainMenu_DestroyRollcall_Postfix()
        {
            PortraitLayersManager.Clear();
        }

        [HarmonyPrefix]
        private static void ArcherPortrait_SetCharacter_Prefix(ArcherPortrait __instance, 
            ref int characterIndex, ref ArcherData.ArcherTypes altSelect, ref int moveDir)
        {
            PortraitLayersManager.HideAllLayersFromPortrait(__instance);
        }

        [HarmonyPostfix]
        private static void ArcherPortrait_SetCharacter_Postfix(ArcherPortrait __instance, 
            int characterIndex, ArcherData.ArcherTypes altSelect, int moveDir)
        {
            var data = ArcherData.Get(characterIndex, altSelect);
            PortraitLayersManager.CreateSelectionLayersComponents(__instance, data);
            PortraitLayersManager.ShowAllLayersFromType(PortraitLayersAttachType.NotJoined, __instance);
        }

        [HarmonyPostfix]
        private static void ArcherPortrait_StartJoined_Postfix(ArcherPortrait __instance)
        {
            PortraitLayersManager.OnPortraitStartJoin(__instance);
        }

        [HarmonyPostfix]
        private static void ArcherPortrait_Leave_Postfix(ArcherPortrait __instance)
        {
            PortraitLayersManager.OnPortraitLeave(__instance);
        }

        [HarmonyPostfix]
        private static void RollcallElement_ctor_Postfix(RollcallElement __instance, int index)
        {
            var portrait = DynamicData.For(__instance).Get<ArcherPortrait>("portrait");
            var joined = DynamicData.For(portrait).Get<bool>("joined");
            
            PortraitLayerPatch.CreateLayersComponents(portrait, portrait.CharacterIndex, portrait.AltSelect);
            PortraitLayersManager.ShowAllLayersFromType(
                joined ? PortraitLayersAttachType.Joined : PortraitLayersAttachType.NotJoined, 
                portrait
            );
        }

        [HarmonyPostfix]
        private static void VersusPlayerMatchResults_ctor_Postfix(VersusPlayerMatchResults __instance, 
            Session session, VersusMatchResults results, int playerIndex, Vector2 from, Vector2 to, List<AwardInfo> awards)
        {
            var won = DynamicData.For(__instance).Get<bool>("won");
            var layers = CreateLayersComponents(__instance, playerIndex);
            
            if (layers != null)
            {
                PortraitLayersManager.ShowAllLayersFromType(
                    won ? PortraitLayersAttachType.Won : PortraitLayersAttachType.Lose, 
                    layers
                );
            }
        }

        public static void CreateLayersComponents(ArcherPortrait archerPortrait, int characterIndex, ArcherData.ArcherTypes altSelect)
        {
            var data = ArcherData.Get(characterIndex, altSelect);
            PortraitLayersManager.CreateSelectionLayersComponents(archerPortrait, data);
        }
        
        public static List<PortraitLayerSpriteComponent> CreateLayersComponents(Entity entity, int playerIndex)
        {
            var data = ArcherData.Get(TFGame.Characters[playerIndex], TFGame.AltSelect[playerIndex]);
            return PortraitLayersManager.CreateWonLoseLayersComponents(entity, data);
        }
    }
}