using System;
using System.Collections.Generic;
using FortRise;
using HarmonyLib;
using Monocle;
using MonoMod.ModInterop;
using TowerFall;

namespace ArcherLoaderMod
{
    
    [Fort("com.reddude.archerLoader", "archer Loader")]
    public class FortEntrance : FortModule
    {
        public static FortEntrance Instance;

        public FortEntrance()
        {
            Instance = this;
        }

        public override Type SettingsType => typeof(ArcherLoaderSettings);
        public static ArcherLoaderSettings Settings => (ArcherLoaderSettings) Instance.InternalSettings;

        public override void LoadContent()
        {
            Mod.LoadContent(Content);
        }

        public override void Load()
        {
            var harmony = new Harmony("com.reddude.archerLoader");
            harmony.PatchAll(typeof(FortEntrance).Assembly);
            Mod.Load();
            //Settings.FlightTest = () => { Music.Play("Flight"); };
     
            //PinkSlime.LoadPatch();
            //TriggerBrambleArrow.Load();
            //PatchEnemyBramble.Load();

            typeof(ModExports).ModInterop();
        }

        public override void Unload()
        {
            Mod.Unload();
        }
        public override void OnVariantsRegister(VariantManager manager, bool noPerPlayer = false)
        {
            Mod.OnVariantsRegister(manager, noPerPlayer);
        }
    }

// Harmony can be supported

//[HarmonyPatch(typeof(MainMenu), "BoolToString")]
//public class MyPatcher
//{
//    static void Postfix(ref string __result)
//    {
//        if (__result == "ON")
//        {
//            __result = "ENABLED";
//            return;
//        }

//        __result = "DISABLED";
//    }
//}


/* 
Example of interppting with libraries
Learn more: https://github.com/MonoMod/MonoMod/blob/master/README-ModInterop.md
*/

[ModExportName("CustomArcherLoaderModExport")]
public static class ModExports
{
    public static Dictionary<ArcherData, ArcherCustomData> GetArcherCustomDataDict() => Mod.ArcherCustomDataDict;
    public static List<Atlas> GetCustomAtlasList() => Mod.customAtlasList;
    public static List<SpriteData> GetCustomSpriteDataList() => Mod.customSpriteDataList;
    public static List<CharacterSounds> GetCustomSFXList() => Mod.customSFXList;
}

}
