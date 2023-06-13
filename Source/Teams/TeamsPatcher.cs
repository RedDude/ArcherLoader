using System.Collections.Generic;
using FortRise;
using Microsoft.Xna.Framework;
using Monocle;
using MonoMod.Utils;
using TowerFall;
using TeamSelector = On.TowerFall.TeamSelector;
using VersusStart = On.TowerFall.VersusStart;

namespace ArcherLoaderMod.Teams
{
    public class TeamsPatcher
    {
        public static bool enabled = false;
        private static Atlas TeamBannersAtlas;

        public static Subtexture RedTeamSubtexture;
        public static void LoadContent(FortContent fortContent)
        {
            TeamBannersAtlas = fortContent.LoadAtlas("TeamBanners/atlas.xml", "TeamBanners/atlas.png");
        }

        public static List<Color> TeamColors = new List<Color>
        {
            Calc.HexToColor("AD4126"),//"C9FFC"), // Blue
            Calc.HexToColor("AD4126"),//"D30000"),  // RED
            Calc.HexToColor("00B800"), //Green
            Calc.HexToColor("F878F8"), // PINK
            Calc.HexToColor("EF8C21"), // ORANGE 
            Calc.HexToColor("E5E5E5"), // WHITE
            Calc.HexToColor("F2FF00"),  // YELLOW
            Calc.HexToColor("00FFF6"),  // CYAN
            Calc.HexToColor("7A42FF"),  // PURPLE
            Color.Brown,
        };
        
        public static void Load()
        {
            On.TowerFall.TeamBanner.ctor += OnTeamBannerOnctor;
            On.TowerFall.TeamBanner.Render += OnTeamBannerOnRender;
            On.TowerFall.TeamSelector.Update += OnTeamSelectorOnUpdate;
            On.TowerFall.VersusStart.Render += OnVersusStartOnRender;
            
            enabled = true;
        }
        
        
        public static void Unload()
        {
            if(!enabled)
                return;
            
            On.TowerFall.TeamBanner.ctor -= OnTeamBannerOnctor;
            On.TowerFall.TeamBanner.Render -= OnTeamBannerOnRender;
            On.TowerFall.TeamSelector.Update -= OnTeamSelectorOnUpdate;
            On.TowerFall.VersusStart.Render -= OnVersusStartOnRender;
        }


        private static void ChangeColor(int teamIndex, int otherTeamIndex, bool up)
        {
            TFGame.Characters[1] = 8;
            TFGame.Characters[2] = 8;
            var index = TeamColors.IndexOf(ArcherData.Teams[teamIndex].ColorA);
            if(up)
                index++;
            else
                index--;
            
            if (index > TeamColors.Count - 1)
                index = 0;
            if (index < 0)
                index = TeamColors.Count - 1;

            if (TeamColors[index] == ArcherData.Teams[otherTeamIndex].ColorA)
            {
                if(up)
                    index++;
                else
                    index--;
            }

            if (index > TeamColors.Count - 1)
                index = 0;
            if (index < 0)
                index = TeamColors.Count - 1;

            ArcherData.Teams[teamIndex].ColorA = TeamColors[index];
            ArcherData.Teams[teamIndex].ColorB = Color.Lerp( TeamColors[index], Color.White, 0.5f);
        }

        private static void OnTeamSelectorOnUpdate(TeamSelector.orig_Update orig, TowerFall.TeamSelector self)
        {
            orig(self);
            var playerIndex = DynamicData.For(self).Get<int>("playerIndex");
            if (MainMenu.VersusMatchSettings.Teams[playerIndex] == Allegiance.Blue)
            {
                if (TFGame.PlayerInputs[playerIndex].MenuUp) ChangeColor(0, 1, true);

                if (TFGame.PlayerInputs[playerIndex].MenuDown) ChangeColor(0, 1, false);
            }
            else if (MainMenu.VersusMatchSettings.Teams[playerIndex] == Allegiance.Red && TFGame.PlayerInputs[playerIndex].MenuUp)
            {
                if (TFGame.PlayerInputs[playerIndex].MenuUp) ChangeColor(1, 0, true);

                if (TFGame.PlayerInputs[playerIndex].MenuDown) ChangeColor(1, 0, false);
            }
        }

        private static void OnVersusStartOnRender(VersusStart.orig_Render orig, TowerFall.VersusStart self)
        {
            orig(self);
            if (self.Level.Session.MatchSettings.TeamMode)
            {
                foreach (var selfComponent in self.Components)
                {
                    var teamBannerA = DynamicData.For(self).Get<Image>("teamBannerA");
                    var teamBannerB = DynamicData.For(self).Get<Image>("teamBannerB");
                    if (selfComponent is Image image)
                    {
                        if (image == teamBannerA)
                        {
                            image.SwapSubtexture(TeamBannersAtlas["teamA_color"]);
                            image.Color = ArcherData.Teams[0].ColorA;
                        }
                        if (image == teamBannerB)
                        {
                            image.SwapSubtexture(TeamBannersAtlas["teamB_color"]);
                            image.Color = ArcherData.Teams[1].ColorA;
                        }
                    }
                }
            }
        }
        
        private static void OnTeamBannerOnctor(On.TowerFall.TeamBanner.orig_ctor orig, TeamBanner self, Vector2 position, Vector2 @from, string bannerFile)
        {
            orig(self, position, from, bannerFile);

            var subtexture = bannerFile == "teamA2x"
                ? TeamBannersAtlas["teamA2x_color"]
                : TeamBannersAtlas["teamB2x_color"];

            if (bannerFile == "teamA2x")
                RedTeamSubtexture = subtexture;
            
            DynamicData.For(self).Set("subtexture", subtexture);
        }

        private static void OnTeamBannerOnRender(On.TowerFall.TeamBanner.orig_Render orig, TeamBanner self)
        {
            // orig(self);
            var subtexture = DynamicData.For(self).Get<Subtexture>("subtexture");
            Draw.TextureCentered(subtexture, self.Position, subtexture == RedTeamSubtexture ? ArcherData.Teams[0].ColorA : ArcherData.Teams[1].ColorA);
        }

        
        public static Color ChangeColorBrightness(Color color, float correctionFactor)
        {
            var red = (float)color.R;
            var green = (float)color.G;
            var blue = (float)color.B;

            if (correctionFactor < 0)
            {
                correctionFactor = 1 + correctionFactor;
                red *= correctionFactor;
                green *= correctionFactor;
                blue *= correctionFactor;
            }
            else
            {
                red = (255 - red) * correctionFactor + red;
                green = (255 - green) * correctionFactor + green;
                blue = (255 - blue) * correctionFactor + blue;
            }

            return  new Color((int)red, (int)green, (int)blue, color.A);
        }
    }
}