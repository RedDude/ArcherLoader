using System;
using System.Collections.Generic;
using ArcherEditorMod.Source.Layers.PortraitLayers;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Monocle;
using MonoMod.Utils;
using TowerFall;

namespace ArcherEditorMod.Rainbow
{
    public class PrismaticPatcher
    {
        public static Dictionary<ArcherData, Color> originalColorA = new();
        public static Dictionary<ArcherData, Color> originalColorB = new();
        private static Harmony harmony;

        public static void Load()
        {
            harmony = new Harmony("mod.archereditor.prismatic");
            
            // Patch methods
            harmony.Patch(
                typeof(ArcherPortrait).GetMethod("Update"),
                prefix: new HarmonyMethod(typeof(PrismaticPatcher), nameof(ArcherPortrait_Update_Prefix))
            );
            
            harmony.Patch(
                typeof(Player).GetMethod("Added"),
                postfix: new HarmonyMethod(typeof(PrismaticPatcher), nameof(Player_Added_Postfix))
            );
            
            harmony.Patch(
                typeof(VersusPlayerMatchResults).GetMethod("Render"),
                prefix: new HarmonyMethod(typeof(PrismaticPatcher), nameof(VersusPlayerMatchResults_Render_Prefix))
            );
            
            harmony.Patch(
                typeof(VersusPlayerMatchResults).GetMethod("ctor"),
                postfix: new HarmonyMethod(typeof(PrismaticPatcher), nameof(VersusPlayerMatchResults_ctor_Postfix))
            );
            
            harmony.Patch(
                typeof(TFGame).GetMethod("Update"),
                postfix: new HarmonyMethod(typeof(PrismaticPatcher), nameof(TFGame_Update_Postfix))
            );
        }

        public static void Unload()
        {
            harmony?.UnpatchAll();
        }

        [HarmonyPostfix]
        private static void TFGame_Update_Postfix(GameTime time)
        {
            RainbowManager.CurrentColor = RainbowManager.GetColor();
        }

        [HarmonyPostfix]
        private static void Player_Added_Postfix(Player __instance)
        {
            if (!ArcherEditorMod.ArcherCustomDataDict.TryGetValue(__instance.ArcherData, out var archerCustomData)) 
                return;

            __instance.Add(new PrismaticMainColorsComponent(__instance.ArcherData, archerCustomData, true, true));
            
            if ((Engine.Instance.Scene as Level)?.Session.RoundLogic is not QuestRoundLogic questRoundLogic) 
                return;
            
            var hud = questRoundLogic.PlayerHUDs[__instance.PlayerIndex];
            var gems = DynamicData.For(hud).Get<List<Sprite<int>>>("gems");
            
            foreach (var gem in gems)
            {
                if (archerCustomData.PrismaticArcher)
                    gem.Color = RainbowManager.CurrentColor;
                else if (archerCustomData.IsGemColorA)
                    gem.Color = archerCustomData.ColorA;
                else if (archerCustomData.IsGemColorB)
                    gem.Color = archerCustomData.ColorB;
            }
            
            if (!archerCustomData.PrismaticArcher) 
                return;

            __instance.Add(new PrismaticQuestGemColorsComponent(hud, __instance.ArcherData, archerCustomData, true, true));
        }

        [HarmonyPrefix]
        private static void VersusPlayerMatchResults_Render_Prefix(VersusPlayerMatchResults __instance)
        {
            var playerIndex = DynamicData.For(__instance).Get<int>("playerIndex");
            var archerData = ArcherData.Get(TFGame.Characters[playerIndex], TFGame.AltSelect[playerIndex]);
            
            if (!ArcherEditorMod.ArcherCustomDataDict.TryGetValue(archerData, out var archerCustomData) || 
                !archerCustomData.IsPrismaticGem) 
                return;

            var gem = DynamicData.For(__instance).Get<Sprite<string>>("gem");
            gem.Color = RainbowManager.CurrentColor;
        }

        [HarmonyPrefix]
        private static void ArcherPortrait_Update_Prefix(ArcherPortrait __instance)
        {
            if (!ArcherEditorMod.ArcherCustomDataDict.TryGetValue(__instance.ArcherData, out var data) || 
                !data.IsPrismaticGem) 
                return;

            __instance.ArcherData.ColorA = RainbowManager.CurrentColor;
            __instance.ArcherData.ColorB = RainbowManager.CurrentColor;
            
            var gem = DynamicData.For(__instance).Get<Sprite<string>>("gem");
            gem.Color = RainbowManager.CurrentColor;
        }

        [HarmonyPostfix]
        private static void VersusPlayerMatchResults_ctor_Postfix(VersusPlayerMatchResults __instance, 
            Session session, VersusMatchResults results, int index, Vector2 from, Vector2 to, List<AwardInfo> awards)
        {
            var playerIndex = DynamicData.For(__instance).Get<int>("playerIndex");
            var archerData = ArcherData.Get(TFGame.Characters[playerIndex], TFGame.AltSelect[playerIndex]);
            
            if (!ArcherEditorMod.ArcherCustomDataDict.TryGetValue(archerData, out var archerCustomData)) 
                return;

            var gem = DynamicData.For(__instance).Get<Sprite<string>>("gem");
            
            if (archerCustomData.IsGemColorA)
                gem.Color = archerCustomData.ColorA;
            else if (archerCustomData.IsGemColorB)
                gem.Color = archerCustomData.ColorB;
            else
                gem.Color = archerCustomData.GemColor;
        }
    }
}