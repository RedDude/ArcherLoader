using System.Collections.Generic;
using System.Xml;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Monocle;
using MonoMod.Utils;
using TowerFall;

namespace ArcherLoaderMod.Wings
{
    public class WingsPatcher
    {
        public static Dictionary<ArcherData, Color?> Colors = new();
        public static Dictionary<ArcherData, Sprite<string>> Sprites = new();
        private static bool enabled;
        private static Harmony harmony;

        public static void Load()
        {
            if (FortEntrance.Instance.Settings.DisableCustomWings)
                return;

            harmony = new Harmony("mod.archerloader.wings");
            harmony.Patch(
                typeof(Player).GetMethod("Added"),
                postfix: new HarmonyMethod(typeof(WingsPatcher), nameof(Player_Added_Postfix))
            );
            
            enabled = true;
        }

        public static void Unload()
        {
            if (!enabled) return;
            harmony?.UnpatchAll();
        }

        [HarmonyPostfix]
        private static void Player_Added_Postfix(Player __instance)
        {
            PlayerWings wings = null;
            foreach (var component in __instance.Components)
            {
                if (component is PlayerWings pw)
                {
                    wings = pw;
                    break;
                }
            }

            if (wings == null)
                return;
            
            var archerData = ArcherData.Get(
                TFGame.Characters[__instance.PlayerIndex], 
                TFGame.AltSelect[__instance.PlayerIndex]
            );
            
            var sprite = DynamicData.For(wings).Get<Sprite<string>>("sprite");
            ApplyCustomWings(__instance, archerData, sprite);
        }

        private static void ApplyCustomWings(Player player, ArcherData archerData, Sprite<string> sprite)
        {
            string wingsChange = null;
            Color? wingsColor = null;
            
            // Check custom archer data first
            if (ArcherLoaderMod.ArcherCustomDataDict.TryGetValue(archerData, out var archerCustomData))
            {
                wingsChange = archerCustomData.Wings;
                if (!string.IsNullOrWhiteSpace(wingsChange))
                {
                    sprite.SwapSubtexture(TFGame.Atlas[wingsChange]);
                    Sprites[archerData] = sprite;
                }
                return; // Custom archer data takes priority
            }

            // Check wings category in custom sprite data
            if (!ArcherLoaderMod.customSpriteDataCategoryDict.TryGetValue("wings", out var wingsCategory) || 
                wingsCategory == null)
                return;

            foreach (var customSpriteData in wingsCategory)
            {
                var xmlElement = customSpriteData.Element;
                var forAttribute = ArcherLoaderMod.GetForAttribute(xmlElement);
                if (string.IsNullOrEmpty(forAttribute)) continue;
                
                if (!TryFindArcherData(xmlElement.GetAttribute(forAttribute), out var searchArcherData))
                    continue;

                if (archerData != searchArcherData) 
                    continue;

                wingsChange = xmlElement.ChildText("Texture", "");
                wingsColor = xmlElement.HasChild("Color") 
                    ? xmlElement.ChildHexColor("Color") 
                    : null;
                break;
            }

            if (!string.IsNullOrWhiteSpace(wingsChange))
                sprite.SwapSubtexture(TFGame.Atlas[wingsChange]);
            
            if (wingsColor.HasValue)
            {
                Colors[archerData] = wingsColor.Value;
                Sprites[archerData] = sprite;
            }
        }

        private static bool TryFindArcherData(string id, out ArcherData archerData)
        {
            if (ArcherLoaderMod.BaseArcherByNameDict.TryGetValue(id.ToLower(), out archerData))
                return true;

            foreach (var customData in ArcherLoaderMod.ArcherCustomDataDict)
            {
                if (customData.Value.ID == id)
                {
                    archerData = customData.Key;
                    return true;
                }
            }

            archerData = null;
            return false;
        }
    }
}