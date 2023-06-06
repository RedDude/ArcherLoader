using System.Collections.Generic;
using System.Xml;
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

        // public static Dictionary<PlayerWings, Sprite<string>> Sprites = new();
        public static void Load()
        {
            On.TowerFall.Player.Added += OnPlayerOnAdded;
            enabled = true;
            // On.TowerFall.PlayerWings.Render += OnPlayerWingsOnRender;
        }

        public static void Unload()
        {
            if(!enabled)
                return;

            On.TowerFall.Player.Added -= OnPlayerOnAdded;
            // On.TowerFall.PlayerWings.Render -= OnPlayerWingsOnRender;
        }

        
        private static void OnPlayerWingsOnRender(On.TowerFall.PlayerWings.orig_Render orig, PlayerWings self)
        {
            orig(self);
            // var owner = DynamicData.For(self).Get<Entity>("owner");
            // if (owner is not Player) return;
            // var player = owner as Player;
            //
            // var archerData = ArcherData.Get(TFGame.Characters[player.PlayerIndex], TFGame.AltSelect[player.PlayerIndex]);
            // var color = Colors[archerData];
            // if (!color.HasValue) return;
            // var sprite = Sprites[archerData];
            // sprite.Scale.X *= (float) player.Facing;
            // sprite.Color = color.Value * player.InvisOpacity;
            // self.Render();
            // sprite.Scale.X *= (float) player.Facing;
        }

        private static void OnPlayerOnAdded(On.TowerFall.Player.orig_Added orig, TowerFall.Player self)
        {
            orig(self);

            PlayerWings wings = null;
            foreach (var t in self.Components)
            {
                if (t is not PlayerWings pw) continue;
                wings = pw;
                break;
            }

            if(wings == null)
                return;
            
            var wingsChange = "";
            Color? wingsColor = null;
            
            var archerData = ArcherData.Get(TFGame.Characters[self.PlayerIndex], TFGame.AltSelect[self.PlayerIndex]);
            var exist = Mod.ArcherCustomDataDict.TryGetValue(archerData, out var archerCustomData);
            var sprite = DynamicData.For(wings).Get<Sprite<string>>("sprite");
            if (exist)
            {
                wingsChange = archerCustomData.Wings;
                if (!string.IsNullOrWhiteSpace(wingsChange))
                {
                    sprite.SwapSubtexture(TFGame.Atlas[wingsChange]);
                    Sprites[archerData] = sprite;
                }
                // if (archerCustomData.WingsColor.HasValue)
                // {
                //     Colors[archerData] = archerCustomData.WingsColor.Value;
                //     Sprites[archerData] = sprite;
                // }
    
                return;
            }

            Mod.customSpriteDataCategoryDict.TryGetValue("wings", out var wingsCategory);
            if(wingsCategory == null)
                return;
            foreach (var customSpriteData in wingsCategory)
            {
                var xmlElement = customSpriteData.Element;
                var forAttribute = Mod.GetForAttribute(xmlElement);
                if (string.IsNullOrEmpty(forAttribute)) continue;
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
                wingsChange = xmlElement.ChildText("Texture", "");
                wingsColor = xmlElement.HasChild("Color") ? xmlElement.ChildHexColor("Color") : null;
                break;
            }

            if (!string.IsNullOrWhiteSpace(wingsChange))
            {
                sprite.SwapSubtexture(TFGame.Atlas[wingsChange]);
            }
            if (wingsColor.HasValue)
            {
                Colors[archerData] = wingsColor.Value;
                Sprites[archerData] = sprite;
            }
        }
    }
}