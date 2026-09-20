#nullable enable
using System.Collections.Generic;
using Monocle;
using MonoMod.Utils;
using TowerFall;

namespace ArcherEditorMod.Source.Features.PortraitLayers
{
    internal static class PortraitLayersManager
    {
        private static readonly Dictionary<ArcherPortrait, Dictionary<ArcherData, List<PortraitLayerSpriteComponent>>> portraitLayers = new();

        public static void Clear() => portraitLayers.Clear();

        public static void HideAllLayersFromPortrait(ArcherPortrait self)
        {
            if (!portraitLayers.TryGetValue(self, out var portraits))
                return;

            foreach (var layers in portraits.Values)
            {
                foreach (var layer in layers)
                    layer.Visible = false;
            }
        }

        public static void ShowAllLayersFromType(PortraitLayersAttachType type, ArcherPortrait self, ArcherData data)
        {
            if (!TryGetLayers(self, data, out var layers))
                return;

            ShowAllLayersFromType(type, layers);
        }

        public static void ShowAllLayersFromType(PortraitLayersAttachType type, List<PortraitLayerSpriteComponent>? layers)
        {
            if (layers == null)
                return;

            foreach (var layer in layers)
                layer.Visible = layer.LayerInfo.AttachTo == type;
        }

        public static void OnPortraitLeave(ArcherPortrait self) =>
            ShowAllLayersFromType(PortraitLayersAttachType.NotJoined, self, self.ArcherData);

        public static void OnPortraitStartJoin(ArcherPortrait self) =>
            ShowAllLayersFromType(PortraitLayersAttachType.Joined, self, self.ArcherData);

        private static bool TryGetLayers(ArcherPortrait self, ArcherData data, out List<PortraitLayerSpriteComponent>? layers)
        {
            layers = null;
            return portraitLayers.TryGetValue(self, out var portraits) && portraits.TryGetValue(data, out layers);
        }

        public static void CreateSelectionLayersComponents(
            ArcherPortrait archerPortrait, ArcherData data, IReadOnlyList<PortraitLayerInfo>? layerInfos)
        {
            if (layerInfos == null || layerInfos.Count == 0)
                return;

            if (!portraitLayers.TryGetValue(archerPortrait, out var portraits))
            {
                portraits = new Dictionary<ArcherData, List<PortraitLayerSpriteComponent>>();
                portraitLayers[archerPortrait] = portraits;
            }

            if (portraits.ContainsKey(data))
                return;

            var flashSprite = DynamicData.For(archerPortrait).Get<Sprite<int>>("flash");
            var flashIndex = -1;
            for (var i = 0; i < archerPortrait.Components.Count; i++)
            {
                if (archerPortrait.Components[i] == flashSprite)
                    flashIndex = i;
            }

            var newLayers = new List<PortraitLayerSpriteComponent>(layerInfos.Count);
            foreach (var layerInfo in layerInfos)
            {
                if (layerInfo.AttachTo != PortraitLayersAttachType.Joined && layerInfo.AttachTo != PortraitLayersAttachType.NotJoined)
                    continue;

                var layer = new PortraitLayerSpriteComponent(layerInfo, data, true, false);
                archerPortrait.Add(layer);
                newLayers.Add(layer);
                archerPortrait.Components.Remove(layer);
                archerPortrait.Components.Insert(flashIndex, layer);
            }

            portraits[data] = newLayers;
        }

        public static List<PortraitLayerSpriteComponent>? CreateWonLoseLayersComponents(
            Entity entity, ArcherData data, IReadOnlyList<PortraitLayerInfo>? layerInfos)
        {
            if (layerInfos == null || layerInfos.Count == 0)
                return null;

            var newLayers = new List<PortraitLayerSpriteComponent>(layerInfos.Count);
            foreach (var layerInfo in layerInfos)
            {
                if (layerInfo.AttachTo != PortraitLayersAttachType.Won && layerInfo.AttachTo != PortraitLayersAttachType.Lose)
                    continue;

                var layer = new PortraitLayerSpriteComponent(layerInfo, data, true, false);
                entity.Add(layer);
                newLayers.Add(layer);
            }

            return newLayers;
        }
    }
}
