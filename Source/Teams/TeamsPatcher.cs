using System.Collections.Generic;
using FortRise;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Monocle;
using MonoMod.Utils;
using TowerFall;

namespace ArcherLoaderMod.Teams
{
    public class TeamsPatcher
    {
        public static bool enabled = false;
        private static Harmony harmony;
        private static Atlas TeamBannersAtlas;
        public static Subtexture RedTeamSubtexture;
        
        public static List<Color> TeamColors = new List<Color>
        {
            Calc.HexToColor("AD4126"), // Red
            Calc.HexToColor("00B800"), // Green
            Calc.HexToColor("F878F8"), // Pink
            Calc.HexToColor("EF8C21"), // Orange 
            Calc.HexToColor("E5E5E5"), // White
            Calc.HexToColor("F2FF00"),  // Yellow
            Calc.HexToColor("00FFF6"),  // Cyan
            Calc.HexToColor("7A42FF"),  // Purple
            Color.Brown,
        };

        public static void LoadContent(IModContent content, IModuleContext context)
        {
            // TeamBannersAtlas = fortContent.LoadAtlas("TeamBanners/atlas.xml", "TeamBanners/atlas.png");
        }

        public static void Load()
        {
            if (FortEntrance.Instance.Settings.DisableTeamColors)
                return;

            harmony = new Harmony("mod.archerloader.teams");
            
            // Patch methods
            harmony.Patch(
                typeof(TeamBanner).GetConstructor(new[] { typeof(Vector2), typeof(Vector2), typeof(string) }),
                postfix: new HarmonyMethod(typeof(TeamsPatcher), nameof(TeamBanner_ctor_Postfix))
            );
            
            harmony.Patch(
                typeof(TeamBanner).GetMethod("Render"),
                prefix: new HarmonyMethod(typeof(TeamsPatcher), nameof(TeamBanner_Render_Prefix))
            );
            
            harmony.Patch(
                typeof(TeamSelector).GetMethod("Update"),
                postfix: new HarmonyMethod(typeof(TeamsPatcher), nameof(TeamSelector_Update_Postfix))
            );
            
            harmony.Patch(
                typeof(VersusStart).GetMethod("Render"),
                postfix: new HarmonyMethod(typeof(TeamsPatcher), nameof(VersusStart_Render_Postfix))
            );
            
            enabled = true;
        }
        
        public static void Unload()
        {
            if (!enabled) return;
            harmony?.UnpatchAll();
        }

        [HarmonyPostfix]
        private static void TeamBanner_ctor_Postfix(TeamBanner __instance, string bannerFile)
        {
            var subtexture = bannerFile switch
            {
                "teamA2x" => TeamBannersAtlas["teamA2x_color"],
                "teamB2x" => TeamBannersAtlas["teamB2x_color"],
                _ => null
            };

            if (bannerFile == "teamA2x")
                RedTeamSubtexture = subtexture;
            
            if (subtexture != null)
            {
                DynamicData.For(__instance).Set("subtexture", subtexture);
            }
        }

        [HarmonyPrefix]
        private static bool TeamBanner_Render_Prefix(TeamBanner __instance)
        {
            var subtexture = DynamicData.For(__instance).Get<Subtexture>("subtexture");
            if (subtexture == null) return true;
            
            Draw.TextureCentered(
                subtexture, 
                __instance.Position, 
                subtexture == RedTeamSubtexture ? ArcherData.Teams[0].ColorA : ArcherData.Teams[1].ColorA
            );
            return false; // Skip original render
        }

        [HarmonyPostfix]
        private static void TeamSelector_Update_Postfix(TeamSelector __instance)
        {
            var playerIndex = DynamicData.For(__instance).Get<int>("playerIndex");
            var team = MainMenu.VersusMatchSettings.Teams[playerIndex];
            
            if (team == Allegiance.Blue)
            {
                if (TFGame.PlayerInputs[playerIndex].MenuUp) ChangeTeamColor(0, 1, true);
                if (TFGame.PlayerInputs[playerIndex].MenuDown) ChangeTeamColor(0, 1, false);
            }
            else if (team == Allegiance.Red)
            {
                if (TFGame.PlayerInputs[playerIndex].MenuUp) ChangeTeamColor(1, 0, true);
                if (TFGame.PlayerInputs[playerIndex].MenuDown) ChangeTeamColor(1, 0, false);
            }
        }

        private static void ChangeTeamColor(int teamIndex, int otherTeamIndex, bool increase)
        {
            var currentColor = ArcherData.Teams[teamIndex].ColorA;
            var currentIndex = TeamColors.IndexOf(currentColor);
            
            // Find next valid color
            int newIndex;
            do
            {
                newIndex = increase 
                    ? (currentIndex + 1) % TeamColors.Count 
                    : (currentIndex - 1 + TeamColors.Count) % TeamColors.Count;
                
                if (newIndex == currentIndex) break;
                currentIndex = newIndex;
            } 
            while (TeamColors[newIndex] == ArcherData.Teams[otherTeamIndex].ColorA);

            // Apply new color
            ArcherData.Teams[teamIndex].ColorA = TeamColors[newIndex];
            ArcherData.Teams[teamIndex].ColorB = Color.Lerp(TeamColors[newIndex], Color.White, 0.5f);
        }

        [HarmonyPostfix]
        private static void VersusStart_Render_Postfix(VersusStart __instance)
        {
            if (!__instance.Level.Session.MatchSettings.TeamMode) return;
            
            var teamBannerA = DynamicData.For(__instance).Get<Image>("teamBannerA");
            var teamBannerB = DynamicData.For(__instance).Get<Image>("teamBannerB");
            
            if (teamBannerA != null)
            {
                teamBannerA.SwapSubtexture(TeamBannersAtlas["teamA_color"]);
                teamBannerA.Color = ArcherData.Teams[0].ColorA;
            }
            
            if (teamBannerB != null)
            {
                teamBannerB.SwapSubtexture(TeamBannersAtlas["teamB_color"]);
                teamBannerB.Color = ArcherData.Teams[1].ColorA;
            }
        }
        
        public static Color ChangeColorBrightness(Color color, float correctionFactor)
        {
            if (correctionFactor < 0)
            {
                correctionFactor = 1 + correctionFactor;
                return new Color(
                    (int)(color.R * correctionFactor),
                    (int)(color.G * correctionFactor),
                    (int)(color.B * correctionFactor),
                    color.A
                );
            }
            
            return new Color(
                (int)((255 - color.R) * correctionFactor + color.R),
                (int)((255 - color.G) * correctionFactor + color.G),
                (int)((255 - color.B) * correctionFactor + color.B),
                color.A
            );
        }
    }
}