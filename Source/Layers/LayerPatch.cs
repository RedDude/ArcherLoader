using ArcherLoaderMod.Layer;
using ArcherLoaderMod.Skin;
using HarmonyLib;
using Monocle;
using MonoMod.Utils;
using System.Collections.Generic;
using TowerFall;

namespace ArcherLoaderMod.Layers
{
    public class LayerPatch
    {
        private static bool enabled = false;
        private static Harmony harmony;

        public static void Load()
        {
            if (FortEntrance.Instance.Settings.DisableLayers)
                return;

            harmony = new Harmony("mod.archerloader.layers");
            harmony.Patch(
                typeof(Player).GetMethod("Added"),
                postfix: new HarmonyMethod(typeof(LayerPatch), nameof(Player_Added_Postfix))
            );
            
            harmony.Patch(
                typeof(PlayerCorpse).GetMethod("Added"),
                postfix: new HarmonyMethod(typeof(LayerPatch), nameof(PlayerCorpse_Added_Postfix))
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
            var archerData = __instance.ArcherData;
            var layerInfos = GetLayerInfos(archerData);
            if (layerInfos == null) return;

            // Get sprite components
            var headSprite = DynamicData.For(__instance).Get<Sprite<string>>("headSprite");
            var bodySprite = DynamicData.For(__instance).Get<Sprite<string>>("bodySprite");
            var bowSprite = DynamicData.For(__instance).Get<Sprite<string>>("bowSprite");

            // Find head component index
            int headIndex = 0;
            for (int i = 0; i < __instance.Components.Count; i++)
            {
                if (__instance.Components[i] == headSprite)
                {
                    headIndex = i;
                    break;
                }
            }

            // Add layer components
            foreach (var layerInfo in layerInfos)
            {
                if (layerInfo.AttachTo == LayerAttachType.Corpse) continue;

                var attachedSprite = GetAttachedSprite(layerInfo, headSprite, bodySprite, bowSprite);
                var layer = new LayerSpriteComponent(layerInfo, attachedSprite, 
                    ArcherLoaderMod.ArcherCustomDataDict.TryGetValue(archerData, out var customData) ? customData : null,
                    archerData, true, true);
                
                __instance.Add(layer);
                
                if (layerInfo.AttachTo != LayerAttachType.Body)
                {
                    __instance.Components.Remove(layer);
                    int insertIndex = headIndex + (layerInfo.AttachTo == LayerAttachType.Bow ? 2 : 1);
                    __instance.Components.Insert(insertIndex, layer);
                }
            }
        }

        [HarmonyPostfix]
        private static void PlayerCorpse_Added_Postfix(PlayerCorpse __instance)
        {
            if (__instance.PlayerIndex == -1) return;
            
            var archerData = ArcherData.Get(TFGame.Characters[__instance.PlayerIndex], TFGame.AltSelect[__instance.PlayerIndex]);
            var skinData = SkinPatcher.GetSkinCharacter(__instance.PlayerIndex, archerData);
            var layerInfos = GetLayerInfos(skinData);
            if (layerInfos == null) return;

            // Add layer components
            var corpseSprite = DynamicData.For(__instance).Get<Sprite<string>>("sprite");
            foreach (var layerInfo in layerInfos)
            {
                if (layerInfo.AttachTo != LayerAttachType.Corpse) continue;
                
                var layer = new LayerSpriteComponent(layerInfo, corpseSprite,
                    ArcherLoaderMod.ArcherCustomDataDict.TryGetValue(archerData, out var customData) ? customData : null,
                    archerData, true, true);
                
                __instance.Add(layer);
            }
        }

        private static List<LayerInfo> GetLayerInfos(ArcherData archerData)
        {
            if (ArcherLoaderMod.ArcherCustomDataDict.TryGetValue(archerData, out var archerCustomData))
            {
                return archerCustomData.LayerInfos;
            }

            var xml = ArcherLoaderMod.FindSpriteDataXmlOnCategories("layer", archerData);
            return xml != null ? LayerParser.Parse(xml) : null;
        }

        private static Sprite<string> GetAttachedSprite(LayerInfo layerInfo, 
            Sprite<string> headSprite, Sprite<string> bodySprite, Sprite<string> bowSprite)
        {
            return layerInfo.AttachTo switch
            {
                LayerAttachType.Body => bodySprite,
                LayerAttachType.Head => headSprite,
                LayerAttachType.Bow => bowSprite,
                _ => bodySprite
            };
        }
    }
}