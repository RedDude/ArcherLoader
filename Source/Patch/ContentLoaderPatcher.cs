using System;
using System.Collections.Generic;
using System.Reflection;
using System.Xml;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Monocle;
using TowerFall;

namespace ArcherEditorMod.Patch
{
    public class ContentLoaderPatcher
    {
        private static Harmony harmony;
        
        public static void Load()
        {
            harmony = new Harmony("mod.archereditor.content");
            
            // Patch methods
            harmony.Patch(
                typeof(ArcherData).GetMethod("Initialize", BindingFlags.Public | BindingFlags.Static),
                postfix: new HarmonyMethod(typeof(ContentLoaderPatcher), nameof(ArcherData_Initialize_Postfix))
            );
            
            harmony.Patch(
                typeof(Sounds).GetMethod("Load", BindingFlags.Public | BindingFlags.Static),
                postfix: new HarmonyMethod(typeof(ContentLoaderPatcher), nameof(Sounds_Load_Postfix))
            );
            
            harmony.Patch(
                typeof(Monocle.SpriteData).GetMethod("GetSpriteString", new[] { typeof(string) }),
                prefix: new HarmonyMethod(typeof(ContentLoaderPatcher), nameof(SpriteData_GetSpriteString_Prefix))
            );
            
            harmony.Patch(
                typeof(Monocle.SpriteData).GetMethod("GetSpriteInt", new[] { typeof(string) }),
                prefix: new HarmonyMethod(typeof(ContentLoaderPatcher), nameof(SpriteData_GetSpriteInt_Prefix))
            );
            
            harmony.Patch(
                typeof(Monocle.SpriteData).GetMethod("GetXML", new[] { typeof(string) }),
                prefix: new HarmonyMethod(typeof(ContentLoaderPatcher), nameof(SpriteData_GetXML_Prefix))
            );
            
            harmony.Patch(
                typeof(Atlas).GetMethod("get_Item", new[] { typeof(string) }),
                prefix: new HarmonyMethod(typeof(ContentLoaderPatcher), nameof(Atlas_GetItem_Prefix))
            );
        }

        public static void Unload()
        {
            harmony?.UnpatchAll();
        }

        [HarmonyPostfix]
        private static void ArcherData_Initialize_Postfix()
        {
            ArcherEditorMod.LoadArcherContents();
            ArcherEditorMod.Start();
        }
        
        [HarmonyPostfix]
        private static void Sounds_Load_Postfix()
        {
            ArcherEditorMod.FixSFX();
        }
        
        [HarmonyPrefix]
        private static bool SpriteData_GetSpriteString_Prefix(Monocle.SpriteData __instance, string id, ref Sprite<string> __result)
        {
            if (__instance.Contains(id)) 
                return true; // Continue to original method

            __result = FindInCustomSpriteData(id, data => data.GetSpriteString(id));
            return false; // Skip original method
        }
        
        [HarmonyPrefix]
        private static bool SpriteData_GetSpriteInt_Prefix(Monocle.SpriteData __instance, string id, ref Sprite<int> __result)
        {
            if (__instance.Contains(id)) 
                return true; // Continue to original method

            __result = FindInCustomSpriteData(id, data => data.GetSpriteInt(id));
            return false; // Skip original method
        }
        
        [HarmonyPrefix]
        private static bool SpriteData_GetXML_Prefix(Monocle.SpriteData __instance, string id, ref XmlElement __result)
        {
            if (__instance.Contains(id)) 
                return true; // Continue to original method

            __result = FindInCustomSpriteData(id, data => data.GetXML(id));
            return false; // Skip original method
        }

        private static T FindInCustomSpriteData<T>(string id, Func<Monocle.SpriteData, T> getter)
        {
            // Check cached sprite data first
            foreach (var cachedSpriteData in ArcherEditorMod.cachedCustomSpriteDataList)
            {
                if (cachedSpriteData.Contains(id))
                    return getter(cachedSpriteData);
            }

            // Check all custom sprite data
            foreach (var customSpriteData in ArcherEditorMod.customSpriteDataList)
            {
                if (!customSpriteData.Contains(id)) continue;
                
                if (!ArcherEditorMod.cachedCustomSpriteDataList.Contains(customSpriteData))
                    ArcherEditorMod.cachedCustomSpriteDataList.Add(customSpriteData);
                    
                return getter(customSpriteData);
            }

            return default;
        }
        
        [HarmonyPrefix]
        private static bool Atlas_GetItem_Prefix(Atlas __instance, string name, ref Subtexture __result)
        {
            try
            {
                // First try the original atlas
                if (__instance.Contains(name))
                {
                    __result = __instance[name]; // Let original handle it
                    return true;
                }
            }
            catch
            {
                // Ignore exception and check custom atlases
            }

            // Check cached custom atlases
            foreach (var atlas in ArcherEditorMod.cachedCustomAtlasList)
            {
                if (atlas.Contains(name))
                {
                    __result = atlas[name];
                    return false;
                }
            }

            // Check all custom atlases
            foreach (var atlas in ArcherEditorMod.customAtlasList)
            {
                if (!atlas.Contains(name)) continue;
                
                if (!ArcherEditorMod.cachedCustomAtlasList.Contains(atlas))
                    ArcherEditorMod.cachedCustomAtlasList.Add(atlas);
                    
                __result = atlas[name];
                return false;
            }

            return true; // Continue to original method (which will throw)
        }
    }
}