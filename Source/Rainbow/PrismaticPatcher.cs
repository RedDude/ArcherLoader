using System;
using System.Collections.Generic;
using ArcherLoaderMod.Source.Layers.PortraitLayers;
using Microsoft.Xna.Framework;
using Monocle;
using MonoMod.Utils;
using TowerFall;
using VersusPlayerMatchResults = On.TowerFall.VersusPlayerMatchResults;

namespace ArcherLoaderMod.Rainbow
{
    public class PrismaticPatcher
    {

        public static Dictionary<ArcherData, Color> originalColorA = new Dictionary<ArcherData, Color>();
        public static Dictionary<ArcherData, Color> originalColorB = new Dictionary<ArcherData, Color>();
        public static bool Enabled { get; set; }

        public static void Load()
        {
            // if(FortEntrance.Settings.DisableLayers)
                // return;

            On.TowerFall.ArcherPortrait.Update += OnArcherPortraitOnUpdate;
            On.TowerFall.Player.Added += OnPlayerOnAdded;
            On.TowerFall.VersusPlayerMatchResults.Render += OnVersusPlayerMatchResultsOnRender;
            On.TowerFall.VersusPlayerMatchResults.ctor += OnVersusPlayerMatchResultsOnctor  ;
            On.TowerFall.TFGame.Update += OnTfGameOnUpdate;
            // Enabled = true;
        }

        public static void Unload()
        {
            // if(!Enabled)
                // return;

            On.TowerFall.ArcherPortrait.Update -= OnArcherPortraitOnUpdate;
            On.TowerFall.Player.Added -= OnPlayerOnAdded;
            VersusPlayerMatchResults.Render -= OnVersusPlayerMatchResultsOnRender;
            On.TowerFall.TFGame.Update -= OnTfGameOnUpdate;
        }

        
        private static void OnTfGameOnUpdate(On.TowerFall.TFGame.orig_Update orig, TFGame self, GameTime time)
        {
            orig(self, time);
            RainbowManager.CurrentColor = RainbowManager.GetColor(Environment.TickCount);
        }

        private static void OnPlayerOnAdded(On.TowerFall.Player.orig_Added orig, Player self)
        {
            orig(self);
            
            var exist = Mod.ArcherCustomDataDict.TryGetValue(self.ArcherData, out var archerCustomData);
            if (!exist) return;

            self.Add(new PrismaticMainColorsComponent(self.ArcherData, archerCustomData, true, true));
            if ((Engine.Instance.Scene as Level)?.Session.RoundLogic is not QuestRoundLogic questRoundLogic) return;
            
            var hud = questRoundLogic.PlayerHUDs[self.PlayerIndex];
            var gems = DynamicData.For(hud).Get<List<Sprite<int>>>("gems");
            
            foreach (var gem in gems)
            {
                if (archerCustomData.PrismaticArcher)
                {
                    gem.Color = RainbowManager.CurrentColor;
                }
                if (archerCustomData.IsGemColorA)
                {
                    gem.Color = archerCustomData.ColorA;
                }
                if (archerCustomData.IsGemColorB)
                {
                    gem.Color = archerCustomData.ColorB;
                }
            }
            
            var isPrismaticArcher = archerCustomData.PrismaticArcher;
            if (!isPrismaticArcher) return;

            self.Add(new PrismaticQuestGemColorsComponent(hud, self.ArcherData, archerCustomData, true, true));
        }

        
        private static void OnVersusPlayerMatchResultsOnRender(VersusPlayerMatchResults.orig_Render orig, TowerFall.VersusPlayerMatchResults self)
        {
            var playerIndex = DynamicData.For(self).Get<int>("playerIndex");
            var archerData = ArcherData.Get(TFGame.Characters[playerIndex], TFGame.AltSelect[playerIndex]);
            var exist = Mod.ArcherCustomDataDict.TryGetValue(archerData, out var archerCustomData);
            if (exist && archerCustomData.IsPrismaticGem)
            {
                var gem = DynamicData.For(self).Get<Sprite<string>>("gem");
                gem.Color = RainbowManager.CurrentColor;
            }
            
            orig(self);
        }
        private static void OnArcherPortraitOnUpdate(On.TowerFall.ArcherPortrait.orig_Update orig, TowerFall.ArcherPortrait self)
        {
            if (Mod.ArcherCustomDataDict.TryGetValue(self.ArcherData, out var data))
            {
                // if (data.IsPrismaticGem || data.PrismaticArcher)
                // {
                //     if (!originalColorA.ContainsKey(self.ArcherData))
                //     {
                //         originalColorA[self.ArcherData] = new Color(self.ArcherData.ColorA.R, self.ArcherData.ColorA.G, self.ArcherData.ColorA.B);
                //         originalColorB[self.ArcherData] = new Color(self.ArcherData.ColorB.R, self.ArcherData.ColorB.G, self.ArcherData.ColorB.B);
                //     }
                // }
                // else
                // {
                //     if (originalColorA.ContainsKey(self.ArcherData))
                //     {
                //         ArcherData.Archers[self.CharacterIndex].ColorA = originalColorA[self.ArcherData];
                //         ArcherData.Archers[self.CharacterIndex].ColorB = originalColorB[self.ArcherData];
                //         self.ArcherData.ColorA = originalColorA[self.ArcherData];
                //         self.ArcherData.ColorB = originalColorB[self.ArcherData];
                //     }
                // }

                if (data.IsPrismaticGem)
                {
                    self.ArcherData.ColorA = RainbowManager.CurrentColor;
                    self.ArcherData.ColorB = RainbowManager.CurrentColor;// RainbowManager.GetColor(Environment.TickCount, 1);
                    var gem = DynamicData.For(self).Get<Sprite<string>>("gem");
                    gem.Color = RainbowManager.CurrentColor;
                }

                // if (data.PrismaticArcher)
                // {
                //     ArcherData.Archers[self.CharacterIndex].ColorA = RainbowManager.currentColor;
                //     ArcherData.Archers[self.CharacterIndex].ColorB = RainbowManager.currentColor;
                // }
            }
            orig(self);
        }
        
        private static void OnVersusPlayerMatchResultsOnctor(VersusPlayerMatchResults.orig_ctor orig, TowerFall.VersusPlayerMatchResults self, Session session, VersusMatchResults results, int index, Vector2 @from, Vector2 to, List<AwardInfo> awards)
        {
            orig(self, session, results, index, from, to, awards);
            var playerIndex = DynamicData.For(self).Get<int>("playerIndex");
            var archerData = ArcherData.Get(TFGame.Characters[playerIndex], TFGame.AltSelect[playerIndex]);
            var exist = Mod.ArcherCustomDataDict.TryGetValue(archerData, out var archerCustomData);
            if(!exist) return;
            
            var gem = DynamicData.For(self).Get<Sprite<string>>("gem");
            gem.Color = archerCustomData.GemColor;
            if (archerCustomData.IsGemColorA)
            {
                gem.Color = archerCustomData.ColorA;
            }
            if (archerCustomData.IsGemColorB)
            {
                gem.Color = archerCustomData.ColorB;
            }
        }

    }
}