using Microsoft.Xna.Framework;
using Monocle;
using MonoMod.Utils;
using TowerFall;
using PlayerGhost = On.TowerFall.PlayerGhost;

namespace ArcherLoaderMod.Ghost
{
    public class GhostPatcher
    {
        public static void Load()
        {
            if (FortEntrance.Settings.DisableParticles)
                return;
            On.TowerFall.PlayerGhost.Added += OnPlayerGhostOnAdded;
        }


        public static void Unload()
        {
            if (FortEntrance.Settings.DisableParticles)
                return;
            On.TowerFall.PlayerGhost.Added -= OnPlayerGhostOnAdded;
        }

        private static void OnPlayerGhostOnAdded(PlayerGhost.orig_Added orig, TowerFall.PlayerGhost self)
        {
            orig(self);

            var ghostChange = "";
            Color? ghostColor = null;
            var archerData = ArcherData.Get(TFGame.Characters[self.PlayerIndex], TFGame.AltSelect[self.PlayerIndex]);
            var exist = Mod.ArcherCustomDataDict.TryGetValue(archerData, out var archerCustomData);
            var sprite = DynamicData.For(self).Get<Sprite<string>>("sprite");
            
            if (exist)
            {
                ghostChange = archerCustomData.Ghost;
                if (!string.IsNullOrWhiteSpace(ghostChange))
                {
                    sprite.SwapSubtexture(TFGame.Atlas[ghostChange]);
                }
                if (archerCustomData.GhostColor.HasValue)
                {
                    DynamicData.For(self).Set("blendColor", archerCustomData.GhostColor.Value); 
                }
            }
           
            
            Mod.customSpriteDataCategoryDict.TryGetValue("ghost", out var category);
            if(category == null)
                return;
            foreach (var customSpriteData in category)
            {
                var xmlElement = customSpriteData.Element;
                var forAttribute = Mod.GetForAttribute(xmlElement);
                if(string.IsNullOrEmpty(forAttribute)) continue;
                Mod.BaseArcherByNameDict.TryGetValue(xmlElement.GetAttribute(forAttribute).ToLower(),
                    out var searchArcherData);
                if (searchArcherData == null)
                {
                    foreach (var customData in Mod.ArcherCustomDataDict)
                    {
                        if (customData.Value.ID == xmlElement.GetAttribute(forAttribute))
                        {
                            searchArcherData = customData.Key;
                        }
                    }
                }

                if (archerData != searchArcherData) continue;
                ghostChange = xmlElement.ChildText("Texture", "");
                ghostColor = xmlElement.HasChild("Color") ? xmlElement.ChildHexColor("Color") : null;
                break;
            }

            if (!string.IsNullOrWhiteSpace(ghostChange))
            {
                sprite.SwapSubtexture(TFGame.Atlas[ghostChange]);
            }
            if (ghostColor.HasValue)
            {
                DynamicData.For(self).Set("blendColor",  ghostColor.Value); 
            }
        }
    }
}