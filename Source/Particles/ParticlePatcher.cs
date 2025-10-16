using HarmonyLib;
using TowerFall;

namespace ArcherLoaderMod.Particles
{
    public class ParticlePatcher
    {
        private static bool enabled = false;
        private static Harmony harmony;

        public static void Load()
        {
            if (FortEntrance.Instance.Settings.DisableParticles)
                return;

            harmony = new Harmony("mod.archerloader.particles");
            harmony.Patch(
                typeof(Player).GetMethod("Added"),
                postfix: new HarmonyMethod(typeof(ParticlePatcher), nameof(Player_Added_Postfix))
            );
            enabled = true;
        }

        public static void Unload()
        {
            if (!enabled) return;
            harmony.UnpatchAll();
        }

        [HarmonyPostfix]
        private static void Player_Added_Postfix(Player __instance)
        {
            if (!ArcherLoaderMod.ArcherCustomDataDict.TryGetValue(__instance.ArcherData, out var archerCustomData))
                return;

            var particlesInfos = archerCustomData.ParticlesInfos;
            if (particlesInfos == null) return;
            
            foreach (var particlesInfo in particlesInfos)
            {
                __instance.Add(new ArcherParticlesComponent(particlesInfo, true, true));
            }
        }
    }
}