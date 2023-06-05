using System.IO;
using System.Linq;
using BepInEx.Configuration;
using HarmonyLib;
using Microsoft.Xna.Framework.Audio;
using Monocle;
using TowerFall;

namespace CustomArcherLoad
{
    public class VictoryMusicPatcher
    {
        public void Patch()
        {
            var instance = new Harmony("Victory Music Patcher");
            instance.PatchAll(typeof(VictoryMusicPatcher));
        }

        [HarmonyPatch(typeof(ArcherData), "PlayVictoryMusic")]
        [HarmonyPrefix]
        static bool VictoryMusicPatcherPrefix(ArcherData __instance)
        {
            Mod.ArcherCustomDataDict.TryGetValue(__instance, out var custom);

            var victory = custom?.victory;
            if (victory == null)
                return true;
            var masterVolume = Audio.MasterVolume;
            if (Music.MasterVolume > 0 && masterVolume == 0)
                Audio.MasterVolume = 1;
            victory.Play(160, 10);
            Audio.MasterVolume = masterVolume;
            return false;
        }
    }
}