using System;
using System.Collections.Generic;
using ArcherEditorMod;
using ArcherEditorMod.Source.ModImport;
using FortRise;
using Microsoft.Extensions.Logging;
// using HarmonyLib;
using Monocle;
using MonoMod.ModInterop;
using TowerFall;

namespace ArcherEditorMod
{

    // [Fort("com.reddude.archerEditor", "archer Editor")]
    public class FortEntrance : Mod
    {
        public static FortEntrance Instance;
        public static IModContent content;
        public static IModContent context;
        public static IModContent logger;
        
        public FortEntrance(IModContent content, IModuleContext context, ILogger logger) : base(content, context, logger)
        {
            Instance = this;
            Source.Features.ArcherDecorationRegistry.Load(context, logger,
                new Source.Features.Wings.WingsFeature(),
                new Source.Features.Hair.HairFeature(),
                new Source.Features.Ghost.GhostFeature(),
                new Source.Features.Particles.ParticlesFeature(),
                new Source.Features.Layers.LayerFeature(),
                new Source.Features.PortraitLayers.PortraitLayersFeature(),
                new Source.Features.Taunt.TauntFeature(content));
            Source.Features.ArcherDecorationRegistry.LoadSelfContent(content, context.Registry);


            OnInitialize += moduleContext =>
            {
                ArcherEditorMod.Load();
            };
            OnUnload += moduleContext =>
            {
                ArcherEditorMod.Unload();
            };
            //Settings.FlightTest = () => { Music.Play("Flight"); };

            //PinkSlime.LoadPatch();
            //TriggerBrambleArrow.Load();
            //PatchEnemyBramble.Load();

            typeof(ModExports).ModInterop();
            // FortRise.RiseCore.Events.OnPreInitialize += OnPreInitialize;
            
            // ArcherEditorMod.OnVariantsRegister(context);
            // This is where you register a lot of features such as custom arrows, variants, pickups, etc..
            // use context.Registry for adding new feature to the game.
            // This is also where you hook methods from the game.
            // use context.Harmony for hooks.
        }
        
        public override ModuleSettings CreateSettings() => new ArcherEditorSettings();

        public ArcherEditorSettings Settings => GetSettings<ArcherEditorSettings>()!;

        // public override void LoadContent()
        // {
        //     ArcherEditorMod.LoadContent(Content);
        // }

        // private void OnPreInitialize()
        // {
        //     TfExAPIModImport.MarkModuleAsSafe?.Invoke(this);
        // }
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

    [ModExportName("CustomArcherEditorModExport")]
    public static class ModExports
    {
        public static Dictionary<ArcherData, ArcherCustomData> GetArcherCustomDataDict() => ArcherEditorMod.ArcherCustomDataDict;
        public static List<Atlas> GetCustomAtlasList() => ArcherEditorMod.customAtlasList;
        public static List<SpriteData> GetCustomSpriteDataList() => ArcherEditorMod.customSpriteDataList;
        public static List<CharacterSounds> GetCustomSFXList() => ArcherEditorMod.customSFXList;
    }

}
