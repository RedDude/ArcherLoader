using System;
using System.Collections.Generic;
using ArcherLoaderMod.Skin;
using Microsoft.Xna.Framework;
using Monocle;
using MonoMod.Utils;
using TowerFall;

namespace ArcherLoaderMod.Mute
{
    public class MutePatcher
    {
        private static bool enabled = false;
        public static void Load()
        {
            // if(FortEntrance.Settings.DisableMutes)
                // return;
            On.TowerFall.Player.EnterDodge += OnEnterDodge;
            On.TowerFall.Player.Jump += OnJump;
            
            // On.TowerFall.PMuteCorpse.Added += OnPMuteCorpseOnAdded;
        }

        private static float SetVolumeAndGetOriginal(Player self)
        {
            var originalSound = Audio.MasterVolume;
            for (var i = 0; i < self.Components.Count; i++)
            {
                if (self.Components[i].GetType() == typeof(MutePlayerComponent))
                {
                    Audio.MasterVolume = 0;
                }
            }

            return originalSound;
        }


        private static void OnEnterDodge(On.TowerFall.Player.orig_EnterDodge orig, TowerFall.Player self)
        {
            float originalSound = SetVolumeAndGetOriginal(self);
            orig(self);
            Audio.MasterVolume = originalSound;
        }


        private static void OnJump(On.TowerFall.Player.orig_Jump orig, TowerFall.Player self, bool particles, bool canSuper, bool forceSuper, int ledgeDir, bool doubleJump)
        {
            float originalSound = SetVolumeAndGetOriginal(self);
            orig(self, particles, canSuper, forceSuper, ledgeDir, doubleJump);
            Audio.MasterVolume = originalSound;
        }

        public static void Unload()
        {
            On.TowerFall.Player.EnterDodge -= OnEnterDodge;
            On.TowerFall.Player.Jump -= OnJump;
        }
        
        
     
    }
}
