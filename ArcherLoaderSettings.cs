using FortRise;

namespace ArcherLoaderMod
{
    public class ArcherLoaderSettings : ModuleSettings
    {
        public bool TauntAlwaysOn;
        public bool QuickStart;
        public int Player1CharacterIndex;
        public int Player2CharacterIndex = -1;
        public int Player3CharacterIndex;
        public bool TauntTooExplode;
        public bool DropHat = true;
        public bool SelfKill = false;
        public bool HideArrowsWhileTaunt = true;
        public bool DisableParticles = false;
        public bool DisableHairs = false;
        public bool DisableLayers = false;
        public bool DisableTeamColors = true;
        public bool DisableCustomGhosts = false;
        public bool DisableCustomWings = false;
        public bool Validate = true;

        public override void Create(ISettingsCreate settings)
        {
            settings.CreateHeader("Quick Start");
            settings.CreateOnOff("Quick Start", QuickStart, v => QuickStart = v,
                "Skip the main menu and jump straight into a quick match.");
            settings.CreateNumber("P1 Quick Index", Player1CharacterIndex, v => Player1CharacterIndex = v, -1, 100, 1);
            settings.CreateNumber("P2 Quick Index", Player2CharacterIndex, v => Player2CharacterIndex = v, -1, 100, 1);
            settings.CreateNumber("P3 Quick Index", Player3CharacterIndex, v => Player3CharacterIndex = v, -1, 100, 1);

            settings.CreateHeader("Taunt");
            settings.CreateOnOff("Taunt Always On", TauntAlwaysOn, v => TauntAlwaysOn = v);
            settings.CreateOnOff("Over-Taunt Combustion", TauntTooExplode, v => TauntTooExplode = v);
            settings.CreateOnOff("Taunt Hide Arrows", HideArrowsWhileTaunt, v => HideArrowsWhileTaunt = v);

            settings.CreateHeader("Controls");
            settings.CreateOnOff("Idle R Stick + Right Drops Hat (Or L key)", DropHat, v => DropHat = v);
            settings.CreateOnOff("Aim + R Stick + Left Selfkills (Or K key)", SelfKill, v => SelfKill = v);

            settings.CreateHeader("Features");
            settings.CreateOnOff("Disable Particles", DisableParticles, v => DisableParticles = v, restartRequired: true);
            settings.CreateOnOff("Disable Hairs", DisableHairs, v => DisableHairs = v, restartRequired: true);
            settings.CreateOnOff("Disable Layers", DisableLayers, v => DisableLayers = v, restartRequired: true);
            settings.CreateOnOff("Disable Team Colors", DisableTeamColors, v => DisableTeamColors = v);
            settings.CreateOnOff("Disable Custom Ghosts", DisableCustomGhosts, v => DisableCustomGhosts = v, restartRequired: true);
            settings.CreateOnOff("Disable Custom Wings", DisableCustomWings, v => DisableCustomWings = v, restartRequired: true);
            settings.CreateOnOff("Validate", Validate, v => Validate = v,
                "Validate custom archer data on load and print errors to the console.");
        }
    }
}
