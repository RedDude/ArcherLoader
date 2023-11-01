using System.Collections.Generic;
using System.Xml;
using Microsoft.Xna.Framework;
using Monocle;
using Sounds = On.TowerFall.Sounds;
using SpriteData = On.Monocle.SpriteData;

namespace ArcherLoaderMod.Patch
{
    public class ContentLoaderPatcher
    {
        public static void Load()
        {
            On.TowerFall.ArcherData.Initialize += OnArcherDataOnInitialize;
            On.TowerFall.Sounds.Load += OnSoundsOnLoad;

            On.Monocle.SpriteData.GetSpriteString += OnSpriteDataOnGetSpriteString;
            On.Monocle.SpriteData.GetSpriteInt += OnSpriteDataOnGetSpriteInt;
            On.Monocle.SpriteData.GetXML += SpriteDataOnGetXML;
        }

        public static void Unload()
        {
            On.TowerFall.ArcherData.Initialize -= OnArcherDataOnInitialize;
            On.TowerFall.Sounds.Load -= OnSoundsOnLoad;
            
            On.Monocle.SpriteData.GetSpriteString -= OnSpriteDataOnGetSpriteString;
            On.Monocle.SpriteData.GetSpriteInt -= OnSpriteDataOnGetSpriteInt;
            On.Monocle.SpriteData.GetXML -= SpriteDataOnGetXML;
        }

        private static void OnArcherDataOnInitialize(On.TowerFall.ArcherData.orig_Initialize orig)
        { 
            Mod.LoadArcherContents();
            orig();
            Mod.Start();
        }
        
        private static void OnSoundsOnLoad(Sounds.orig_Load orig)
        {
            orig();
            Mod.FixSFX();
        }
        
        private static Sprite<string> OnSpriteDataOnGetSpriteString(SpriteData.orig_GetSpriteString orig, Monocle.SpriteData self, string id)
        {
            if (self.Contains(id))
            {
                return orig(self, id);
            }

            foreach (var cachedSpriteData in Mod.cachedCustomSpriteDataList)
            {
                if (!cachedSpriteData.Contains(id)) continue;
                return cachedSpriteData.GetSpriteString(id);
            }

            foreach (var customSpriteData in Mod.customSpriteDataList)
            {
                if (!customSpriteData.Contains(id)) continue;
                if (!Mod.cachedCustomSpriteDataList.Contains(customSpriteData)) Mod.cachedCustomSpriteDataList.Add(customSpriteData);
                return customSpriteData.GetSpriteString(id);
            }

            return null;
        }
        
        
        private static Sprite<int> OnSpriteDataOnGetSpriteInt(SpriteData.orig_GetSpriteInt orig, Monocle.SpriteData self, string id)
        {
            if (self.Contains(id))
            {
                return orig(self, id);
            }

            foreach (var cachedSpriteData in Mod.cachedCustomSpriteDataList)
            {
                if (!cachedSpriteData.Contains(id)) continue;
                return cachedSpriteData.GetSpriteInt(id);
            }

            foreach (var customSpriteData in Mod.customSpriteDataList)
            {
                if (!customSpriteData.Contains(id)) continue;
                if (!Mod.cachedCustomSpriteDataList.Contains(customSpriteData)) Mod.cachedCustomSpriteDataList.Add(customSpriteData);
                return customSpriteData.GetSpriteInt(id);
            }

            return null;
        }
        
        private static XmlElement SpriteDataOnGetXML(SpriteData.orig_GetXML orig, Monocle.SpriteData self, string id)
        {
            if (self.Contains(id))
            {
                return orig(self, id);
            }

            foreach (var cachedSpriteData in Mod.cachedCustomSpriteDataList)
            {
                if (!cachedSpriteData.Contains(id)) continue;
                return cachedSpriteData.GetXML(id);
            }

            foreach (var customSpriteData in Mod.customSpriteDataList)
            {
                if (!customSpriteData.Contains(id)) continue;
                if (!Mod.cachedCustomSpriteDataList.Contains(customSpriteData)) Mod.cachedCustomSpriteDataList.Add(customSpriteData);
                return customSpriteData.GetXML(id);
            }

            return null;
        }

        public static Sprite<string> GetSpriteString(string id, Dictionary<string, XmlElement> sprites, Atlas atlas)
        {
            var sprite1 = sprites[id];
            var sprite2 = new Sprite<string>(atlas[sprite1.ChildText("Texture")], sprite1.ChildInt("FrameWidth"),
                sprite1.ChildInt("FrameHeight"))
            {
                Origin = new Vector2(sprite1.ChildFloat("OriginX", 0.0f), sprite1.ChildFloat("OriginY", 0.0f)),
                Position = new Vector2(sprite1.ChildFloat("X", 0.0f), sprite1.ChildFloat("Y", 0.0f)),
                Color = sprite1.ChildHexColor("Color", Color.White)
            };
            var xmlElement = sprite1["Animations"];
            if (xmlElement != null)
            {
                foreach (XmlElement xml in xmlElement.GetElementsByTagName("Anim"))
                    sprite2.Add(xml.Attr(nameof(id)), xml.AttrFloat("delay", 0.0f), xml.AttrBool("loop", true),
                        Calc.ReadCSVInt(xml.Attr("frames")));
            }

            return sprite2;
        }

    }
}