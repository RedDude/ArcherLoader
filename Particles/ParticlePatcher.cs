using On.TowerFall;

namespace ArcherLoaderMod.Particles
{
    public class ParticlePatcher
    {
        public static void Load()
        {
            if(FortEntrance.Settings.DisableParticles)
                return;
            On.TowerFall.Player.Added += OnPlayerOnAdded;
        }

        public static void Unload()
        {
            if(FortEntrance.Settings.DisableParticles)
                return;
            On.TowerFall.Player.Added -= OnPlayerOnAdded;
        }
        private static void OnPlayerOnAdded(Player.orig_Added orig, TowerFall.Player self)
        {
            orig(self);
            
            var exist = Mod.ArcherCustomDataDict.TryGetValue(self.ArcherData, out var archerCustomData);
            if (!exist) return;
            var particlesInfos = archerCustomData.ParticlesInfos;
            if (particlesInfos == null) return;
            foreach (var particlesInfo in particlesInfos)
            {
                self.Add(new ArcherParticlesComponent(particlesInfo, true, true));
            }
        }

    }
}