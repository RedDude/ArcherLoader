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
            
        }

        private static void OnArcherPortraitOnLeave(ArcherPortrait.orig_Leave orig, TowerFall.ArcherPortrait self)
        {
            if (!portraitLayers.ContainsKey(self)) return;
            var layers = portraitLayers[self];
            if (layers == null) return;
            foreach (var portraitLayerInfo in layers)
            {
                if(portraitLayerInfo.layerInfo.AttachTo == PortraitLayersAttachType.Join) 
                    portraitLayerInfo.Visible = false;
            }
        }

        private static void OnArcherPortraitOnStartJoined(ArcherPortrait.orig_StartJoined orig, TowerFall.ArcherPortrait self)
        {
            if (!portraitLayers.ContainsKey(self)) return;
            var layers = portraitLayers[self];
            if (layers == null) return;
            foreach (var portraitLayerInfo in layers)
            {
                if(portraitLayerInfo.layerInfo.AttachTo == PortraitLayersAttachType.NotJoin) 
                    portraitLayerInfo.Visible = false;
            }
        }

        private static void OnSetCharacter(ArcherPortrait.orig_SetCharacter origSetCharacter, TowerFall.ArcherPortrait archerPortrait, int characterIndex, ArcherData.ArcherTypes altSelect, int moveDir)
        {
            origSetCharacter(archerPortrait, characterIndex, altSelect, moveDir);
            
            var data = ArcherData.Get(characterIndex, altSelect);

            var exist = Mod.ArcherCustomDataDict.TryGetValue(data, out var archerCustomData);
            if (!exist) return;
            
            var layerInfos = archerCustomData.PortraitLayerInfos;
            if (layerInfos == null) return;
            
            if (!portraitLayers.ContainsKey(archerPortrait)) return;
            var layers = portraitLayers[archerPortrait];
            if (layers == null)
            {
                layers = new List<PortraitLayerSpriteComponent>(layerInfos.Count);
                portraitLayers[archerPortrait] = layers;
                foreach (var portraitLayerInfo in layerInfos)
                {
                    var layer = new PortraitLayerSpriteComponent(portraitLayerInfo,true, false);
                    archerPortrait.Add(layer);
                    layers.Add(layer);
                }
            }

            foreach (var portraitLayerInfo in layers)
            {
                portraitLayerInfo.Visible = portraitLayerInfo.layerInfo.AttachTo switch
                {
                    PortraitLayersAttachType.Join => false,
                    PortraitLayersAttachType.NotJoin => true,
                    _ => portraitLayerInfo.Visible
                };
            }
        }
    }
}
