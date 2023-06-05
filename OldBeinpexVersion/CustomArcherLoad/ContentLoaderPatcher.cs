using System;
using System.Collections.Generic;
using System.Reflection;
using System.Xml;
using BepInEx.Logging;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Monocle;
using TowerFall;

namespace CustomArcherLoad
{
    public class ContentLoaderPatcher
    {
        public static ManualLogSource Logger;

        public ContentLoaderPatcher(ManualLogSource manualLogSource)
        {
            Logger = manualLogSource;
        }
        
        public void Patch()
        {
            var instance = new Harmony("Content Loader Patcher");
            instance.PatchAll(typeof(ContentLoaderPatcher));
        }

        [HarmonyPatch(typeof(ArcherData), "Initialize")]
        [HarmonyPostfix]
        static void ArcherDataInitializePostfix()
        {
            Mod.Start();
        }
        
        [HarmonyPatch(typeof(Sounds), "Load")]
        [HarmonyPostfix]
        static void SoundPostfix()
        {
            Mod.FixSFX();
        }
        //
        // [HarmonyPatch(typeof(QuestSpawnPortal), "FinishSpawn")]
        // [HarmonyPostfix]
        // static void QuestSpawnPortal(QuestSpawnPortal __instance)
        // {
        //     __instance.Level.Add<Skeleton>(new Skeleton(__instance.Position + new Vector2(0.0f, 2f), Facing.Left, ArrowTypes.Normal, false, false, true, false, false));
        // }
        //
        //
        [HarmonyPatch(typeof(SpriteData), "GetSpriteString", typeof(string))]
        [HarmonyFinalizer]
        static Exception SpriteDataPostfix(Exception __exception, ref SpriteData __instance,
            ref Sprite<string> __result, MethodBase __originalMethod, bool __state, string id)
        {
            if (__result != null)
            {
                return null;
            }

            if (__instance.Contains(id))
            {
                if (__exception == null) return null;
                // var spritesField = typeof(SpriteData).GetField("sprites",
                //     BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);
                // var atlasField = typeof(SpriteData).GetField("atlas",
                //     BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);
                // var sprites = (Dictionary<string, XmlElement>) spritesField.GetValue(__instance);
                // var atlas = (Atlas) atlasField.GetValue(__instance);
                __result = __instance.GetSpriteString(id);//GetSpriteString(id, sprites, atlas);
              
                return null;
            }

            // Logger.LogInfo("not found trying other spritesData");
            foreach (var spriteData in Mod.cachedCustomSpriteDataList)
            {
                if (!spriteData.Contains(id))
                    continue;

                // var spritesField = typeof(SpriteData).GetField("sprites",BindingFlags.NonPublic | BindingFlags.Instance);
                // var atlasField = typeof(SpriteData).GetField("atlas",BindingFlags.NonPublic | BindingFlags.Instance);
                // var sprites = (Dictionary<string, XmlElement>) spritesField.GetValue(spriteData);
                // var atlas = (Atlas) atlasField.GetValue(spriteData);
                // __result = GetSpriteString(id, sprites, atlas);
                __result = spriteData.GetSpriteString(id);
                return null;
            }

            foreach (var spriteData in Mod.customSpriteDataList)
            {
                if (__result != null)
                {
                    return null;
                }

                if (!spriteData.Contains(id))
                    continue;
                if (!Mod.cachedCustomSpriteDataList.Contains(spriteData))
                    Mod.cachedCustomSpriteDataList.Add(spriteData);

                __result = spriteData.GetSpriteString(id);
                return null;

            }

            return null;
        }

        [HarmonyPatch(typeof(SpriteData), "GetSpriteInt", typeof(string))]
        [HarmonyPrefix]
        static bool SpriteDataGetSpriteIntPrefix(SpriteData __instance, ref Sprite<int> __result, string id)
        {
            if (__instance.Contains(id)) return true;

            // Logger.LogInfo("GetSpriteInt: " + id + " not found trying other spritesData");
            foreach (var spriteData in Mod.cachedCustomSpriteDataList)
            {
                if (!spriteData.Contains(id))
                    continue;
                __result = spriteData.GetSpriteInt(id);
                return false;
            }

            foreach (var spriteData in Mod.customSpriteDataList)
            {
                if (!spriteData.Contains(id))
                    continue;
                if (!Mod.cachedCustomSpriteDataList.Contains(spriteData))
                    Mod.cachedCustomSpriteDataList.Add(spriteData);
                // Logger.LogInfo("GetSpriteInt found!");
                __result = spriteData.GetSpriteInt(id);
                return false;
            }

            return true;
        }

        [HarmonyPatch(typeof(SpriteData), "GetXML", typeof(string))]
        [HarmonyPrefix]
        static bool SpriteDataGetSpriteXMLPostfix(SpriteData __instance, ref XmlElement __result, string id)
        {
            if (__instance.Contains(id)) return true;

            foreach (var spriteData in Mod.cachedCustomSpriteDataList)
            {
                if (!spriteData.Contains(id))
                    continue;
                __result = spriteData.GetXML(id);
                return false;
            }

            // Logger.LogInfo("GetXML: not found trying other spritesData");
            foreach (var spriteData in Mod.customSpriteDataList)
            {
                if (!spriteData.Contains(id))
                    continue;
                if (!Mod.cachedCustomSpriteDataList.Contains(spriteData))
                    Mod.cachedCustomSpriteDataList.Add(spriteData);
                // Logger.LogInfo("GetXML found!");
                __result = spriteData.GetXML(id);
                return false;
            }

            return true;
        }

        [HarmonyPatch(typeof(Atlas), "get_Item", typeof(string))]
        [HarmonyFinalizer]
        static Exception Atlas(Exception __exception, ref Atlas __instance, ref Subtexture __result, string name)
        {
            if (__instance.Contains(name)) return null;

            // Logger.LogInfo("Atlas: " + name + " not found trying other spritesData");
            foreach (var atlas in Mod.cachedCustomAtlasList)
            {
                if (!atlas.Contains(name))
                    continue;
                __result = atlas[name];
                return null;
            }

            foreach (var atlas in Mod.customAtlasList)
            {
                if (!atlas.Contains(name))
                    continue;
                if (!Mod.cachedCustomAtlasList.Contains(atlas))
                    Mod.cachedCustomAtlasList.Add(atlas);
                // Logger.LogInfo("Atlas: " + "found!");
                __result = atlas[name];
                return null;
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