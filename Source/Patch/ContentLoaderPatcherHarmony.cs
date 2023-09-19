using System;
using System.Collections.Generic;
using System.Reflection;
using System.Xml;
using Microsoft.Xna.Framework;
using Monocle;
using MonoMod.RuntimeDetour;
using TowerFall;

namespace ArcherLoaderMod.Patch
{
    public class ContentLoaderPatcherHarmony
    {
        private static Hook hook_AtlasGetItem;
        public delegate Subtexture orig_get_Item(Atlas self, string name);

        public static void Load()
        {
            On.Monocle.SpriteData.GetSpriteString += SpriteDataPostFix;
            On.Monocle.SpriteData.GetSpriteInt += SpriteDataGetSpriteIntPrefix;
            On.Monocle.SpriteData.GetXML += SpriteDataGetSpriteXMLPostfix;
            hook_AtlasGetItem = new Hook(
                typeof(Monocle.Atlas).GetMethod("get_Item", new Type[] { typeof(string) }),
                patch_Atlas
            );
        }

        public static void Unload()
        {
            On.Monocle.SpriteData.GetSpriteString -= SpriteDataPostFix;
            On.Monocle.SpriteData.GetSpriteInt -= SpriteDataGetSpriteIntPrefix;
            On.Monocle.SpriteData.GetXML -= SpriteDataGetSpriteXMLPostfix;
            hook_AtlasGetItem.Dispose();
        }


        private static Sprite<string> SpriteDataPostFix(On.Monocle.SpriteData.orig_GetSpriteString orig, SpriteData self, string id)
        {
            try 
            {
                return orig(self, id);
            }
            catch (Exception) 
            {
                if (self.Contains(id))
                {
                    // var spritesField = typeof(SpriteData).GetField("sprites",
                    //     BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);
                    // var atlasField = typeof(SpriteData).GetField("atlas",
                    //     BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);
                    // var sprites = (Dictionary<string, XmlElement>) spritesField.GetValue(__instance);
                    // var atlas = (Atlas) atlasField.GetValue(__instance);
                    return self.GetSpriteString(id);//GetSpriteString(id, sprites, atlas);
                }

                // Console.WriteLine("not found trying other spritesData");
                foreach (var spriteData in Mod.cachedCustomSpriteDataList)
                {
                    if (!spriteData.Contains(id))
                        continue;

                    // var spritesField = typeof(SpriteData).GetField("sprites",BindingFlags.NonPublic | BindingFlags.Instance);
                    // var atlasField = typeof(SpriteData).GetField("atlas",BindingFlags.NonPublic | BindingFlags.Instance);
                    // var sprites = (Dictionary<string, XmlElement>) spritesField.GetValue(spriteData);
                    // var atlas = (Atlas) atlasField.GetValue(spriteData);
                    // __result = GetSpriteString(id, sprites, atlas);
                    return spriteData.GetSpriteString(id);
                }

                foreach (var spriteData in Mod.customSpriteDataList)
                {
                    if (!spriteData.Contains(id))
                        continue;
                    if (!Mod.cachedCustomSpriteDataList.Contains(spriteData))
                        Mod.cachedCustomSpriteDataList.Add(spriteData);

                    return spriteData.GetSpriteString(id);
                }
            }
            return null;
        }


        private static Sprite<int> SpriteDataGetSpriteIntPrefix(On.Monocle.SpriteData.orig_GetSpriteInt orig, SpriteData self, string id)
        {
            if (self.Contains(id))
                return orig(self, id);
            
            
            // Console.WriteLine("GetSpriteInt: " + id + " not found trying other spritesData");
            foreach (var spriteData in Mod.cachedCustomSpriteDataList)
            {
                if (!spriteData.Contains(id))
                    continue;
                return spriteData.GetSpriteInt(id);
            }

            foreach (var spriteData in Mod.customSpriteDataList)
            {
                if (!spriteData.Contains(id))
                    continue;
                if (!Mod.cachedCustomSpriteDataList.Contains(spriteData))
                    Mod.cachedCustomSpriteDataList.Add(spriteData);
                // Console.WriteLine("GetSpriteInt found!");
                return spriteData.GetSpriteInt(id);
            }
            return orig(self, id);
        }

        private static XmlElement SpriteDataGetSpriteXMLPostfix(On.Monocle.SpriteData.orig_GetXML orig, SpriteData self, string id)
        {
            if (self.Contains(id))
                return orig(self, id);

            foreach (var spriteData in Mod.cachedCustomSpriteDataList)
            {
                if (!spriteData.Contains(id))
                    continue;
                return spriteData.GetXML(id);
            }

            // Console.WriteLine("GetXML: not found trying other spritesData");
            foreach (var spriteData in Mod.customSpriteDataList)
            {
                if (!spriteData.Contains(id))
                    continue;
                if (!Mod.cachedCustomSpriteDataList.Contains(spriteData))
                    Mod.cachedCustomSpriteDataList.Add(spriteData);
                // Console.WriteLine("GetXML found!");
                return spriteData.GetXML(id);
            }

            return orig(self, id);
        }

        private static Subtexture patch_Atlas(orig_get_Item orig, Atlas self, string name)
        {
            try 
            {
                return orig(self, name);
            }
            catch (Exception) 
            {
                if (self.Contains(name))
                    return null;
            }

            // Console.WriteLine("Atlas: " + name + " not found trying other spritesData");
            foreach (var atlas in Mod.cachedCustomAtlasList)
            {
                if (!atlas.Contains(name))
                    continue;
                return atlas[name];
            }

            foreach (var atlas in Mod.customAtlasList)
            {
                if (!atlas.Contains(name))
                    continue;
                if (!Mod.cachedCustomAtlasList.Contains(atlas))
                    Mod.cachedCustomAtlasList.Add(atlas);
                // Console.WriteLine("Atlas: " + "found!");
                return atlas[name];
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

            // [HarmonyPatch(typeof(QuestSpawnPortal), "FinishSpawn")]
        // [HarmonyPostfix]
        // static void QuestSpawnPortal(QuestSpawnPortal __instance)
        // {
        //     __instance.Level.Add<Skeleton>(new Skeleton(__instance.Position + new Vector2(0.0f, 2f), Facing.Left, ArrowTypes.Normal, false, false, true, false, false));
        // }
        
        //private static void InitPatch(On.TowerFall.ArcherData.hook_Initialize orig, TriggerArrow self, LevelEntity owner, Vector2 position, float direction)
        // public static void InitPatch(On.TowerFall.ArcherData.hook_Initialize orig, ArcherData self)
        // {
        //     Mod.Start();
        // }

        //public static void FixSFX(On.TowerFall.Sounds.hook_Load orig)
        //{
        //Mod.FixSFX();
        //}
}