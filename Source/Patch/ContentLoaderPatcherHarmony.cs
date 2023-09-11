using System;
using System.Collections.Generic;
using System.Reflection;
using System.Xml;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Monocle;
using TowerFall;

namespace ArcherLoaderMod.Patch
{
    [HarmonyPatch]
    public class ContentLoaderPatcherHarmony
    {
        public static Dictionary<string, int> levels = new Dictionary<string, int>();
        public void Patch()
        {
            var instance = new Harmony("Content Loader Patcher");
        }


        [HarmonyPatch(typeof(Atlas), "get_Item", typeof(string))]
        [HarmonyFinalizer]
        static Exception Atlas(Exception __exception, ref Atlas __instance, ref Subtexture __result, string name)
        {
            if (__instance.Contains(name)) return null;

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
                // Console.WriteLine("Atlas: " + "found!");
                __result = atlas[name];
                return null;
            }

            return null;
        }

    }

}