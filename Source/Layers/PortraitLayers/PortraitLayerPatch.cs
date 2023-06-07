using System.Collections.Generic;
using ArcherLoaderMod.Layers;
using Monocle;
using MonoMod.Utils;
using TowerFall;
using ArcherPortrait = On.TowerFall.ArcherPortrait;

namespace ArcherLoaderMod.Source.Layers.PortraitLayers
{
    public class PortraitLayerPatch
    {
        private static bool enabled = false;

        private static Dictionary<TowerFall.ArcherPortrait, List<PortraitLayerSpriteComponent>> portraitLayers =
            new (); 
        
        public static void Load()
        {
            if(FortEntrance.Settings.DisableLayers)
                return;
            ArcherPortrait.SetCharacter += OnSetCharacter;
            ArcherPortrait.StartJoined += OnArcherPortraitOnStartJoined;
            ArcherPortrait.Leave += OnArcherPortraitOnLeave;
            ArcherPortrait.Update += OnArcherPortraitOnUpdate;
            enabled = true;
        }

        public static void Unload()
        {
            if(!enabled)
                return;
            ArcherPortrait.SetCharacter -= OnSetCharacter;
            ArcherPortrait.StartJoined -= OnArcherPortraitOnStartJoined;
            ArcherPortrait.Leave -= OnArcherPortraitOnLeave;
            ArcherPortrait.Update -= OnArcherPortraitOnUpdate;
        }
        
        
        private static void OnArcherPortraitOnUpdate(ArcherPortrait.orig_Update orig, TowerFall.ArcherPortrait self)
        {
            orig(self);
        }

        private static void OnArcherPortraitOnLeave(ArcherPortrait.orig_Leave orig, TowerFall.ArcherPortrait self)
        {
            orig(self);
            if (!portraitLayers.ContainsKey(self)) return;
            var layers = portraitLayers[self];
            if (layers == null) return;
            foreach (var portraitLayerInfo in layers)
            {
                portraitLayerInfo.Visible = portraitLayerInfo.layerInfo.AttachTo == PortraitLayersAttachType.NotJoin;
            }
        }

        private static void OnArcherPortraitOnStartJoined(ArcherPortrait.orig_StartJoined orig, TowerFall.ArcherPortrait self)
        {
            orig(self);
            if (!portraitLayers.ContainsKey(self)) return;
            var layers = portraitLayers[self];
            if (layers == null) return;
            foreach (var portraitLayerInfo in layers)
            {
                portraitLayerInfo.Visible = portraitLayerInfo.layerInfo.AttachTo == PortraitLayersAttachType.Join;
            }
        }

        private static void OnSetCharacter(ArcherPortrait.orig_SetCharacter origSetCharacter, TowerFall.ArcherPortrait archerPortrait, int characterIndex, ArcherData.ArcherTypes altSelect, int moveDir)
        {
            if (portraitLayers.ContainsKey(archerPortrait))
            {
                var currentLayers = portraitLayers[archerPortrait];
                if (currentLayers == null) return;
                foreach (var portraitLayerInfo in currentLayers)
                {
                    portraitLayerInfo.Visible = false;
                }
            }
        
            origSetCharacter(archerPortrait, characterIndex, altSelect, moveDir);
            var data = ArcherData.Get(characterIndex, altSelect);

            var exist = Mod.ArcherCustomDataDict.TryGetValue(data, out var archerCustomData);
            if (!exist) return;
            
            var layerInfos = archerCustomData.PortraitLayerInfos;
            if (layerInfos == null) return;

            if (!portraitLayers.ContainsKey(archerPortrait))
            {
                var flashSprite = DynamicData.For(archerPortrait).Get<Sprite<string>>("flash");
                var flashIndex = -1;
                for (var i = 0; i < archerPortrait.Components.Count; i++)
                {
                    if (archerPortrait.Components[i] == flashSprite)
                    {
                        flashIndex = i;
                    }
                }
                
                var newLayers = new List<PortraitLayerSpriteComponent>(layerInfos.Count);
                foreach (var portraitLayerInfo in layerInfos)
                {
                    var layer = new PortraitLayerSpriteComponent(portraitLayerInfo,true, false);
                    archerPortrait.Add(layer);
                    newLayers.Add(layer);
                    archerPortrait.Components.Remove(layer);
                    archerPortrait.Components.Insert(flashIndex, layer);
                }
                portraitLayers[archerPortrait] = newLayers;
            }
            
            var layers = portraitLayers[archerPortrait];
            foreach (var portraitLayerInfo in layers)
            {
                if (portraitLayerInfo.layerInfo.AttachTo == PortraitLayersAttachType.Join)
                    portraitLayerInfo.Visible = false;
                else if (portraitLayerInfo.layerInfo.AttachTo == PortraitLayersAttachType.NotJoin)
                {
                    portraitLayerInfo.Visible = true;
                }
                   
                else
                    portraitLayerInfo.Visible = portraitLayerInfo.Visible;
            }
        }
    }
}
