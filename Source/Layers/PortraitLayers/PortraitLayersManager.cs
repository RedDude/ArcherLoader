using System.Collections.Generic;
using Monocle;
using MonoMod.Utils;
using TowerFall;

namespace ArcherLoaderMod.Source.Layers.PortraitLayers
{
    public class PortraitLayersManager
    {
        public static Dictionary<ArcherPortrait, Dictionary<ArcherData, List<PortraitLayerSpriteComponent>>> PortraitLayers =
            new ();

        public static void Clear()
        {
            PortraitLayers.Clear();
        }

        public static void HideAllLayers(ArcherPortrait self)
        {
            if (GetLayerByPortraitAndData(self, out var layers)) return;
            if (layers == null) return;
            foreach (var portraitLayerInfo in layers)
            {
                portraitLayerInfo.Visible = false;
            }
        }
        
        public static void HideAllLayersFromPortrait(ArcherPortrait self)
        {
            if(PortraitLayers.TryGetValue(self, out var portraits))
            foreach (var portrait in portraits)
            {
                foreach (var portraitLayerInfo in portrait.Value)
                {
                    portraitLayerInfo.Visible = false;
                }
            }
        }
        
        public static void HideAllLayers(ArcherPortrait self, ArcherData data)
        {
            if (GetLayerByPortraitAndData(self, data, out var layers)) return;
            if (layers == null) return;
            foreach (var portraitLayerInfo in layers)
            {
                portraitLayerInfo.Visible = false;
            }
        }
        
        public static void ShowAllLayersFromType(PortraitLayersAttachType type, ArcherPortrait self)
        {
            if (GetLayerByPortraitAndData(self, out var layers)) return;
            if (layers == null) return;
            foreach (var portraitLayerInfo in layers)
            {
                portraitLayerInfo.Visible = portraitLayerInfo.layerInfo.AttachTo == type;
            }
        }
        
        public static void ShowAllLayersFromType(PortraitLayersAttachType type, ArcherPortrait self, ArcherData data)
        {
            if (GetLayerByPortraitAndData(self, data, out var layers)) return;
            if (layers == null) return;
            foreach (var portraitLayerInfo in layers)
            {
                portraitLayerInfo.Visible = portraitLayerInfo.layerInfo.AttachTo == type;
            }
        }

        public static void ShowAllLayersFromType(PortraitLayersAttachType type, List<PortraitLayerSpriteComponent> layers)
        {
            if (layers == null) return;
            foreach (var portraitLayerInfo in layers)
            {
                portraitLayerInfo.Visible = portraitLayerInfo.layerInfo.AttachTo == type;
            }
        }
        public static void OnPortraitLeave(ArcherPortrait self)
        {
            if (GetLayerByPortraitAndData(self, self.ArcherData, out var layers)) return;
            if (layers == null) return;
            ShowAllLayersFromType(PortraitLayersAttachType.NotJoin, layers);
        }

        public static void OnPortraitStartJoin(ArcherPortrait self)
        {
            GetLayerByPortraitAndData(self, self.ArcherData, out var layers);
            if (layers == null) return;
            ShowAllLayersFromType(PortraitLayersAttachType.Joined, layers);
        }

        private static bool GetLayerByPortraitAndData(ArcherPortrait self, out List<PortraitLayerSpriteComponent> layers)
        {
            if (!PortraitLayers.ContainsKey(self))
            {
                layers = null;
                return true;
            }

            var data = ArcherData.Get(self.CharacterIndex, self.AltSelect);
            return !PortraitLayers[self].TryGetValue(data, out layers);
        }
        
        private static bool GetLayerByPortraitAndData(ArcherPortrait self, ArcherData data, out List<PortraitLayerSpriteComponent> layers)
        {
            if (PortraitLayers.ContainsKey(self)) 
                return !PortraitLayers[self].TryGetValue(data, out layers);
            
            layers = null;
            return true;
        }
             
        public static void CreateLayersComponents(ArcherPortrait archerPortrait, ArcherData data)
        {
            var exist = Mod.ArcherCustomDataDict.TryGetValue(data, out var archerCustomData);
            List<PortraitLayerInfo> layerInfos = null;
            if (!exist)
            {
                var xml = Mod.FindSpriteDataXmlOnCategories("portraitLayer", data);
                if (xml != null)
                {
                    layerInfos = PortraitLayerParser.Parse(xml);
                }
            }
            else
            {
                layerInfos = archerCustomData.PortraitLayerInfos;
            }

      
            if (layerInfos == null || layerInfos.Count == 0) return;
            
            if (!PortraitLayers.ContainsKey(archerPortrait))
            {
                PortraitLayers[archerPortrait] = new Dictionary<ArcherData, List<PortraitLayerSpriteComponent>>();
            }

            if (PortraitLayers[archerPortrait].ContainsKey(data)) return;
            
            var flashSprite = DynamicData.For(archerPortrait).Get<Sprite<int>>("flash");
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
                var layer = new PortraitLayerSpriteComponent(portraitLayerInfo, data, true, false);
                archerPortrait.Add(layer);
                newLayers.Add(layer);
                archerPortrait.Components.Remove(layer);
                archerPortrait.Components.Insert(flashIndex, layer);
            }

            PortraitLayers[archerPortrait][data] = newLayers;
        }
    }
}