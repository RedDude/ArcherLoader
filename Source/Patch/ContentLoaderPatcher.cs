using Sounds = On.TowerFall.Sounds;

namespace ArcherLoaderMod.Patch
{
    public class ContentLoaderPatcher
    {
        public static void Load()
        {
            On.TowerFall.ArcherData.Initialize += OnArcherDataOnInitialize;
            On.TowerFall.Sounds.Load += OnSoundsOnLoad  ;
        }

        public static void Unload()
        {
            On.TowerFall.ArcherData.Initialize -= OnArcherDataOnInitialize;
            On.TowerFall.Sounds.Load -= OnSoundsOnLoad  ;
        }

        private static void OnArcherDataOnInitialize(On.TowerFall.ArcherData.orig_Initialize orig)
        {
            orig();
            Mod.Start();
        }
        
        private static void OnSoundsOnLoad(Sounds.orig_Load orig)
        {
            orig();
            Mod.FixSFX();
        }
    }
}