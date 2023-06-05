using Monocle;
using TowerFall;

namespace ArcherLoaderMod.Patch
{
    public class VictoryMusicPatcher
    {
        public static void Load()
        {
            On.TowerFall.ArcherData.PlayVictoryMusic += OnArcherDataOnPlayVictoryMusic;
        }

        public static void Unload()
        {
            On.TowerFall.ArcherData.PlayVictoryMusic -= OnArcherDataOnPlayVictoryMusic;
        }

        private static void OnArcherDataOnPlayVictoryMusic(On.TowerFall.ArcherData.orig_PlayVictoryMusic orig, ArcherData self)
        {
            Mod.ArcherCustomDataDict.TryGetValue(self, out var custom);

            var victory = custom?.victory;
            if (victory == null)
            {
                orig(self);
                return;
            }
                
            var masterVolume = Audio.MasterVolume;
            if (Music.MasterVolume > 0 && masterVolume == 0)
                Audio.MasterVolume = 1;
            var volume = Music.MasterVolume * 2f;
            victory.Play(160,  volume > 1 ? 1 : volume);
            Audio.MasterVolume = masterVolume;
        }
    }
}