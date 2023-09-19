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

        public static Dictionary<string, int> levels = new Dictionary<string, int>();

        public static void Load()
        {
            hook_AtlasGetItem = new Hook(
                typeof(Monocle.Atlas).GetMethod("get_Item", new Type[] { typeof(string) }),
                patch_Atlas
            );
        }

        public static void Unload()
        {
            hook_AtlasGetItem.Dispose();
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
            }

            return null;
        }
    }
}