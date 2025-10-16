using HarmonyLib;
using Microsoft.Xna.Framework;
using Monocle;
using MonoMod.Utils;
using TowerFall;

namespace ArcherLoaderMod.Ghost
{
    public class GhostPatcher
    {
        private static bool enabled = false;
        private static Harmony harmony;

        public static void Load()
        {
            if (FortEntrance.Instance.Settings.DisableCustomGhosts)
                return;
                
            enabled = true;
            harmony = new Harmony("mod.archerloader.ghost");
            harmony.Patch(
                typeof(PlayerGhost).GetMethod("Added"),
                postfix: new HarmonyMethod(typeof(GhostPatcher), nameof(PlayerGhost_Added_Postfix))
            );
        }

        public static void Unload()
        {
            if (!enabled)
                return;
                
            harmony.UnpatchAll();
            harmony = null;
        }

        [HarmonyPostfix]
        private static void PlayerGhost_Added_Postfix(PlayerGhost __instance)
        {
            if (FortEntrance.Instance.Settings.DisableCustomGhosts)
                return;

            var ghostChange = "";
            Color? ghostColor = null;
            var archerData = ArcherData.Get(TFGame.Characters[__instance.PlayerIndex], TFGame.AltSelect[__instance.PlayerIndex]);
            var exist = ArcherLoaderMod.ArcherCustomDataDict.TryGetValue(archerData, out var archerCustomData);
            var sprite = DynamicData.For(__instance).Get<Sprite<string>>("sprite");
            
            if (exist)
            {
                ghostChange = archerCustomData.Ghost;
                if (!string.IsNullOrWhiteSpace(ghostChange))
                {
                    sprite.SwapSubtexture(TFGame.Atlas[ghostChange]);
                }
                if (archerCustomData.GhostColor.HasValue)
                {
                    DynamicData.For(__instance).Set("blendColor", archerCustomData.GhostColor.Value); 
                }
            }
           
            ArcherLoaderMod.customSpriteDataCategoryDict.TryGetValue("ghost", out var category);
            if(category == null)
                return;
                
            foreach (var customSpriteData in category)
            {
                var xmlElement = customSpriteData.Element;
                var forAttribute = ArcherLoaderMod.GetForAttribute(xmlElement);
                if(string.IsNullOrEmpty(forAttribute)) continue;
                
                ArcherLoaderMod.BaseArcherByNameDict.TryGetValue(
                    xmlElement.GetAttribute(forAttribute).ToLower(),
                    out var searchArcherData
                );
                
                if (searchArcherData == null)
                {
                    foreach (var customData in ArcherLoaderMod.ArcherCustomDataDict)
                    {
                        if (customData.Value.ID == xmlElement.GetAttribute(forAttribute))
                        {
                            searchArcherData = customData.Key;
                        }
                    }
                }

                if (archerData != searchArcherData) continue;
                
                ghostChange = xmlElement.ChildText("Texture", "");
                ghostColor = xmlElement.HasChild("Color") 
                    ? xmlElement.ChildHexColor("Color") 
                    : null;
                break;
            }

            if (!string.IsNullOrWhiteSpace(ghostChange))
            {
                sprite.SwapSubtexture(TFGame.Atlas[ghostChange]);
            }
            if (ghostColor.HasValue)
            {
                DynamicData.For(__instance).Set("blendColor", ghostColor.Value); 
            }
        }
    }
}