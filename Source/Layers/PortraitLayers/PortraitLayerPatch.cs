using System;
using ArcherLoaderMod.Rainbow;
using Monocle;
using MonoMod.Utils;
using TowerFall;
using ArcherPortrait = On.TowerFall.ArcherPortrait;
using MainMenu = On.TowerFall.MainMenu;
using RollcallElement = On.TowerFall.RollcallElement;

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
            Enabled = true;
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
        }

        private static void OnRollcallElementConstructor(RollcallElement.orig_ctor orig, TowerFall.RollcallElement self, int index)
        {
            orig(self, index);
            var portrait = DynamicData.For(self).Get<TowerFall.ArcherPortrait>("portrait");
            CreateLayersComponents(portrait, portrait.CharacterIndex, portrait.AltSelect);
            PortraitLayersManager.ShowAllLayersFromType(PortraitLayersAttachType.NotJoin, portrait);
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
            PortraitLayersManager.ShowAllLayersFromType(PortraitLayersAttachType.NotJoin, self);
        }

        public static void CreateLayersComponents(TowerFall.ArcherPortrait archerPortrait, int characterIndex, ArcherData.ArcherTypes altSelect)
        {
            var data = ArcherData.Get(characterIndex, altSelect);
            PortraitLayersManager.CreateLayersComponents(archerPortrait, data);
        }
    }
}
