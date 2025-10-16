using HarmonyLib;
using Monocle;
using TowerFall;

namespace ArcherLoaderMod.Patch
{
    public class VictoryMusicPatcher
    {
        private static Harmony harmony;

        public static void Load()
        {
            harmony = new Harmony("mod.archerloader.victorymusic");
            harmony.Patch(
                typeof(ArcherData).GetMethod("PlayVictoryMusic"),
                prefix: new HarmonyMethod(typeof(VictoryMusicPatcher), nameof(PlayVictoryMusic_Prefix))
            );
        }

        public static void Unload()
        {
            harmony?.UnpatchAll();
        }

        [HarmonyPrefix]
        private static bool PlayVictoryMusic_Prefix(ArcherData __instance)
        {
            if (!ArcherLoaderMod.ArcherCustomDataDict.TryGetValue(__instance, out var custom) || 
                custom?.victory == null)
            {
                // Continue to original method if no custom victory music
                return true;
            }

            // Handle custom victory music
            var masterVolume = Audio.MasterVolume;
            if (Music.MasterVolume > 0 && masterVolume == 0)
                Audio.MasterVolume = 1;
                
            var volume = Music.MasterVolume * 2f;
            custom.victory.Play(160, volume > 1 ? 1 : volume);
            Audio.MasterVolume = masterVolume;
            
            // Skip original method
            return false;
        }
    }
}