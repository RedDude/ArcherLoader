using System;
using System.Collections.Generic;
using ArcherLoaderMod.Rainbow;
using Microsoft.Xna.Framework;
using Monocle;
using MonoMod.Utils;
using TowerFall;
using ArcherPortrait = On.TowerFall.ArcherPortrait;
using MainMenu = On.TowerFall.MainMenu;
using RollcallElement = On.TowerFall.RollcallElement;
using VersusPlayerMatchResults = On.TowerFall.VersusPlayerMatchResults;

namespace ArcherLoaderMod.Source.Layers.PortraitLayers
{
    public class PortraitLayerPatch
    {
        public static bool Enabled { get; set; }

        public static void Load()
        {
            if(FortEntrance.Settings.DisableLayers)
                return;

            ArcherPortrait.SetCharacter += OnSetCharacter;
            ArcherPortrait.StartJoined += OnArcherPortraitOnStartJoined;
            ArcherPortrait.Leave += OnArcherPortraitOnLeave;
            ArcherPortrait.Update += OnArcherPortraitOnUpdate;
            MainMenu.DestroyRollcall += OnMainMenuOnDestroyRollcall;
            RollcallElement.ctor += OnRollcallElementConstructor;
            On.TowerFall.VersusPlayerMatchResults.ctor += OnVersusPlayerMatchResultsOnctor;
        }

        public static void Unload()
        {
            if(!Enabled)
                return;

            ArcherPortrait.SetCharacter -= OnSetCharacter;
            ArcherPortrait.StartJoined -= OnArcherPortraitOnStartJoined;
            ArcherPortrait.Leave -= OnArcherPortraitOnLeave;
            ArcherPortrait.Update -= OnArcherPortraitOnUpdate;
            MainMenu.DestroyRollcall -= OnMainMenuOnDestroyRollcall;
            RollcallElement.ctor -= OnRollcallElementConstructor;
            VersusPlayerMatchResults.ctor -= OnVersusPlayerMatchResultsOnctor;
        }

        private static void OnRollcallElementConstructor(RollcallElement.orig_ctor orig, TowerFall.RollcallElement self, int index)
        {
            orig(self, index);
            var portrait = DynamicData.For(self).Get<TowerFall.ArcherPortrait>("portrait");
            var joined = DynamicData.For(portrait).Get<bool>("joined");
            CreateLayersComponents(portrait, portrait.CharacterIndex, portrait.AltSelect);
            PortraitLayersManager.ShowAllLayersFromType(joined ? PortraitLayersAttachType.Joined : PortraitLayersAttachType.NotJoined, portrait);
        }

        private static void OnMainMenuOnDestroyRollcall(MainMenu.orig_DestroyRollcall orig, TowerFall.MainMenu self)
        {
            orig(self);
            PortraitLayersManager.Clear();
        }
        
        private static void OnArcherPortraitOnUpdate(ArcherPortrait.orig_Update orig, TowerFall.ArcherPortrait self)
        {
            if (Mod.ArcherCustomDataDict.TryGetValue(self.ArcherData, out var data))
            {
                if (data.IsPrismaticGem)
                {
                    var prismatic = RainbowManager.GetColor(Environment.TickCount, 0);
                    self.ArcherData.ColorA = prismatic;
                    self.ArcherData.ColorB = RainbowManager.GetColor(Environment.TickCount, 1);
                    var gem = DynamicData.For(self).Get<Sprite<string>>("gem");
                    gem.Color = prismatic;
                }
            }
            orig(self);
        }

        private static void OnArcherPortraitOnLeave(ArcherPortrait.orig_Leave orig, TowerFall.ArcherPortrait self)
        {
            orig(self);
            PortraitLayersManager.OnPortraitLeave(self);
        }

 
        private static void OnArcherPortraitOnStartJoined(ArcherPortrait.orig_StartJoined orig, TowerFall.ArcherPortrait self)
        {
            orig(self);
            PortraitLayersManager.OnPortraitStartJoin(self);
        }
        
        private static void OnSetCharacter(ArcherPortrait.orig_SetCharacter origSetCharacter, TowerFall.ArcherPortrait self, int characterIndex, ArcherData.ArcherTypes altSelect, int moveDir)
        {
            PortraitLayersManager.HideAllLayersFromPortrait(self);
            origSetCharacter(self, characterIndex, altSelect, moveDir);
            CreateLayersComponents(self, characterIndex, altSelect);
            PortraitLayersManager.ShowAllLayersFromType(PortraitLayersAttachType.NotJoined, self);
        }

        public static void CreateLayersComponents(TowerFall.ArcherPortrait archerPortrait, int characterIndex, ArcherData.ArcherTypes altSelect)
        {
            var data = ArcherData.Get(characterIndex, altSelect);
            PortraitLayersManager.CreateLayersComponents(archerPortrait, data);
        }
        
        public static List<PortraitLayerSpriteComponent> CreateLayersComponents(Entity entity, int playerIndex)
        {
            var data = ArcherData.Get(TFGame.Characters[playerIndex], TFGame.AltSelect[playerIndex]);
            return PortraitLayersManager.CreateLayersComponents(entity, data);
        }
        
        private static void OnVersusPlayerMatchResultsOnctor(VersusPlayerMatchResults.orig_ctor orig, TowerFall.VersusPlayerMatchResults self, Session session, VersusMatchResults results, int playerIndex, Vector2 @from, Vector2 to, List<AwardInfo> awards)
        {
            orig(self, session, results, playerIndex, from, to, awards);
            // var portrait = DynamicData.For(self).Get<TowerFall.ArcherPortrait>("portrait");
            var won = DynamicData.For(self).Get<bool>("won");
            var layers = CreateLayersComponents(self, playerIndex);
            if(layers != null)
                PortraitLayersManager.ShowAllLayersFromType(won ? PortraitLayersAttachType.Won : PortraitLayersAttachType.Lose, layers);
        }

    }
}
