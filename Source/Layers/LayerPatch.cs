﻿using System.Collections.Generic;
using ArcherLoaderMod.Layer;
using ArcherLoaderMod.Skin;
using Monocle;
using MonoMod.Utils;
using TowerFall;
using PlayerCorpse = On.TowerFall.PlayerCorpse;

namespace ArcherLoaderMod.Layers
{
    public class LayerPatch
    {
        
        private static bool enabled = false;
        private static int headIndex;
        private static Sprite<string> headSprite;
        private static Sprite<string> bodySprite;
        private static Sprite<string> bowSprite;
        private static Sprite<string> corpseSprite;

        public static void Load()
        {
            if(FortEntrance.Settings.DisableLayers)
                return;
            On.TowerFall.Player.Added += OnPlayerOnAdded;
            On.TowerFall.PlayerCorpse.Added += OnPlayerCorpseOnAdded;
        }
        
        public static void Unload()
        {
            if(!enabled)
                return;
            On.TowerFall.Player.Added -= OnPlayerOnAdded;
            
            On.TowerFall.PlayerCorpse.Added -= OnPlayerCorpseOnAdded;
        }
        
        
        private static void OnPlayerCorpseOnAdded(PlayerCorpse.orig_Added orig, TowerFall.PlayerCorpse self)
        {
            orig(self);

            if (self.PlayerIndex == -1)
            {
                return;
            }
            
            var data = ArcherData.Get(TFGame.Characters[self.PlayerIndex], TFGame.AltSelect[self.PlayerIndex]);

            var skinData = SkinPatcher.GetSkinCharacter(self.PlayerIndex, data);
                
            var exist = Mod.ArcherCustomDataDict.TryGetValue(skinData, out var archerCustomData);
            List<LayerInfo> layerInfos = null;
            if (!exist)
            {
                var xml = Mod.FindSpriteDataXmlOnCategories("Layer", data);
                if (xml != null)
                {
                    layerInfos = LayerParser.Parse(xml);
                }
            }
            else
            {
                layerInfos = archerCustomData.LayerInfos;
            }
            if (layerInfos == null) return;
            
            foreach (var layerInfo in layerInfos)
            {
                var attachedSprite = layerInfo.AttachTo == LayerAttachType.Corpse;
                
                if(!attachedSprite)
                    continue;
                
                corpseSprite = DynamicData.For(self).Get<Sprite<string>>("sprite");
                
                var layer = new LayerSpriteComponent(layerInfo, corpseSprite, archerCustomData, data, true, true);
                self.Add(layer);
                
                // for (var i = 0; i < self.Components.Count; i++)
                // {
                    // if (self.Components[i] == headSprite)
                    // {
                        // headIndex = i;
                    // }
                // }
                
                // if (layerInfo.AttachTo == LayerAttachType.Body) continue;
                // self.Components.Remove(layer);
                // self.Components.Insert(headIndex+(layerInfo.AttachTo == LayerAttachType.Bow ? 2 : 1), layer);
            }
        }
        
        private static void OnPlayerOnAdded(On.TowerFall.Player.orig_Added orig, Player self)
        {
            orig(self);
            
            var exist = Mod.ArcherCustomDataDict.TryGetValue(self.ArcherData, out var archerCustomData);
            List<LayerInfo> layerInfos = null;
            if (!exist)
            {
                var xml = Mod.FindSpriteDataXmlOnCategories("layer", self.ArcherData);
                if (xml != null)
                {
                    layerInfos = LayerParser.Parse(xml);
                }
            }
            else
            {
                layerInfos = archerCustomData.LayerInfos;
            }
            if (layerInfos == null) return;

            headSprite = DynamicData.For(self).Get<Sprite<string>>("headSprite");
            bodySprite = DynamicData.For(self).Get<Sprite<string>>("bodySprite");
            bowSprite = DynamicData.For(self).Get<Sprite<string>>("bowSprite");
            headIndex = 0;
            
            for (var i = 0; i < self.Components.Count; i++)
            {
                if (self.Components[i] == headSprite)
                {
                    headIndex = i;
                }
            }
            
            var data = ArcherData.Get(TFGame.Characters[self.PlayerIndex], TFGame.AltSelect[self.PlayerIndex]);

            foreach (var layerInfo in layerInfos)
            {
                if(layerInfo.AttachTo == LayerAttachType.Corpse)
                    continue;
                        
                var attachedSprite = layerInfo.AttachTo == LayerAttachType.Body ? bodySprite :
                    layerInfo.AttachTo == LayerAttachType.Head ? headSprite : bowSprite;
                
                
                var layer = new LayerSpriteComponent(layerInfo, attachedSprite, archerCustomData, data, true, true);
                self.Add(layer);
                if (layerInfo.AttachTo == LayerAttachType.Body) continue;
                self.Components.Remove(layer);
                self.Components.Insert(headIndex+(layerInfo.AttachTo == LayerAttachType.Bow ? 2 : 1), layer);
            }
        }
        
        // public static void PassThroughTeam_patch(orig_Player_PlayerOnPlayer orig, Player a, Player b)
        // {
        //     var matchVariants = a.Level.Session.MatchSettings.Variants;
        //     if (matchVariants.GetCustomVariant("PassThroughTeam")[a.PlayerIndex] && a.Allegiance == b.Allegiance && a.Allegiance != Allegiance.Neutral)
        //     {
        //         return;
        //     }
        //
        //     orig(a, b);
        // }
        
    }
}
