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

        // FortRise side (robust, fixes every mod with this pattern): change MainMenu.cs:155 from ToStartSelected = list[1]; to ToStartSelected = list.FirstOrDefault(b => b is not OptionsButtonHeader); so initial focus never lands on a header regardless of how a mod orders its items./
        public override void Create(ISettingsCreate settings)
        {
           
            settings.CreateButton("OPEN EDITOR", () => ArcherEditor.OpenEditor() );
            settings.CreateHeader("QUICK START");
            settings.CreateOnOff("QUICK START", QuickStart, v => QuickStart = v,
                "SKIP THE MAIN MENU AND JUMP STRAIGHT TO THE EDITOR.");
            settings.CreateNumber("P1 QUICK INDEX", Player1CharacterIndex, v => Player1CharacterIndex = v, -1, 100, 1);
            settings.CreateNumber("P2 QUICK INDEX", Player2CharacterIndex, v => Player2CharacterIndex = v, -1, 100, 1);
            settings.CreateNumber("P3 QUICK INDEX", Player3CharacterIndex, v => Player3CharacterIndex = v, -1, 100, 1);

            settings.CreateHeader("TAUNT");
            settings.CreateOnOff("TAUNT ALWAYS ON", TauntAlwaysOn, v => TauntAlwaysOn = v);
            settings.CreateOnOff("OVER-TAUNT COMBUSTION", TauntTooExplode, v => TauntTooExplode = v);
            settings.CreateOnOff("TAUNT HIDE ARROWS", HideArrowsWhileTaunt, v => HideArrowsWhileTaunt = v);

            settings.CreateHeader("CONTROLS");
            settings.CreateOnOff("IDLE R STICK + RIGHT DROPS HAT (OR L KEY)", DropHat, v => DropHat = v);
            settings.CreateOnOff("AIM + R STICK + LEFT SELFKILLS (OR K KEY)", SelfKill, v => SelfKill = v);

            settings.CreateHeader("FEATURES");
            settings.CreateOnOff("DISABLE HAIRS", DisableHairs, v => DisableHairs = v, restartRequired: true);
            settings.CreateOnOff("DISABLE CUSTOM WINGS", DisableCustomWings, v => DisableCustomWings = v, restartRequired: true);
            settings.CreateOnOff("DISABLE CUSTOM GHOSTS", DisableCustomGhosts, v => DisableCustomGhosts = v, restartRequired: true);
            settings.CreateOnOff("DISABLE PARTICLES", DisableParticles, v => DisableParticles = v, restartRequired: true);
            settings.CreateOnOff("DISABLE LAYERS", DisableLayers, v => DisableLayers = v, restartRequired: true);
            settings.CreateOnOff("DISABLE TEAM COLORS", DisableTeamColors, v => DisableTeamColors = v);
         
            settings.CreateOnOff("VALIDATE", Validate, v => Validate = v,
                "VALIDATE CUSTOM ARCHER DATA ON LOAD AND PRINT ERRORS TO THE CONSOLE.");
        }
    }
}
