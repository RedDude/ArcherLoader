using System.Reflection;
using ArcherLoaderMod.Hair;
using FortRise;
using Monocle;
using MonoMod.Utils;
using TowerFall;

namespace ArcherLoaderMod
{
    
    public class ArcherLoaderSettings : ModuleSettings
    {
        [SettingsName("Taunt Always On")]
        public bool TauntAlwaysOn;

        [SettingsName("Quick Start")]
        public bool QuickStart;
        
        [SettingsName("P1 Quick index"), SettingsNumber(-1)]
        public int Player1CharacterIndex;
        
        [SettingsName("P2 Quick index"), SettingsNumber(-1)]
        public int Player2CharacterIndex = -1;
        
        [SettingsName("P3 Quick index"), SettingsNumber(-1)]
        public int Player3CharacterIndex;

        [SettingsName("Over-Taunt Combustion")]
        public bool TauntTooExplode;

        [SettingsName("Idle R Stick + Right Drops Hat (Or L key)")]
        public bool DropHat = true;
        
        [SettingsName("Aim + R Stick + Left selfkills (Or K key)")]
        public bool SelfKill = false;

        [SettingsName("Taunt Hide Arrows")]
        public bool HideArrowsWhileTaunt = true;
        
        [SettingsName("Disable Particles")]
        public bool DisableParticles = false;
        
        [SettingsName("Disable Hairs")]
        public bool DisableHairs = false;

        [SettingsName("Disable Layers")]
        public bool DisableLayers = false;
        
        [SettingsName("Disable Team Colors")]
        public bool DisableTeamColors = true;
        
        public bool DisableCustomGhosts = false;
        
        public bool DisableCustomWings = false;
        
        [SettingsName("Validate")]
        public bool Validate = true;

        // [SettingsNumber(0, 20, 2)]
        // public int OnStepping;

        // public Action FlightTest;
        public override void Create(ISettingsCreate settings)
        {
            // settings.
        }
    }

}
