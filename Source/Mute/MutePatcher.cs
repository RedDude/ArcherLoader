using HarmonyLib;
using Microsoft.Xna.Framework;
using Monocle;
using TowerFall;

namespace ArcherLoaderMod.Mute
{
    public class MutePatcher
    {
        private static bool enabled = false;
        private static Harmony harmony;

        public static void Load()
        {
            // if(FortEntrance.Settings.DisableMutes) return;
            
            harmony = new Harmony("mod.archerloader.mute");
            enabled = true;
            
            // Patch Player.EnterDodge
            harmony.Patch(
                typeof(Player).GetMethod("EnterDodge"),
                prefix: new HarmonyMethod(typeof(MutePatcher), nameof(EnterDodge_Prefix)),
                postfix: new HarmonyMethod(typeof(MutePatcher), nameof(EnterDodge_Postfix))
            );
            
            // Patch Player.Jump
            harmony.Patch(
                typeof(Player).GetMethod("Jump"),
                prefix: new HarmonyMethod(typeof(MutePatcher), nameof(Jump_Prefix)),
                postfix: new HarmonyMethod(typeof(MutePatcher), nameof(Jump_Postfix))
            );
        }

        private static void SetVolumeAndGetOriginal(Player self, out float originalVolume)
        {
            originalVolume = Audio.MasterVolume;
            foreach (var component in self.Components)
            {
                if (component is MutePlayerComponent)
                {
                    Audio.MasterVolume = 0;
                    return;
                }
            }
        }

        [HarmonyPrefix]
        private static void EnterDodge_Prefix(Player __instance, out float __state)
        {
            SetVolumeAndGetOriginal(__instance, out __state);
        }

        [HarmonyPostfix]
        private static void EnterDodge_Postfix(float __state)
        {
            Audio.MasterVolume = __state;
        }

        [HarmonyPrefix]
        private static void Jump_Prefix(Player __instance, out float __state)
        {
            SetVolumeAndGetOriginal(__instance, out __state);
        }

        [HarmonyPostfix]
        private static void Jump_Postfix(float __state)
        {
            Audio.MasterVolume = __state;
        }

        public static void Unload()
        {
            if (!enabled) return;
            harmony.UnpatchAll();
        }
    }
}