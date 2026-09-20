#nullable enable
using System.Collections.Generic;
using FortRise;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Monocle;
using MonoMod.Utils;
using TowerFall;

namespace ArcherEditorMod.Source.Features.Ghost
{
    // <Ghost>
    //   <Texture>player/ghost/kermit</Texture>   optional
    //   <Color>55FF55</Color>                    optional
    // </Ghost>
    // or shorthand: <Ghost>player/ghost/kermit</Ghost>
    public sealed class GhostFeature : IArcherFeature
    {
        private sealed record GhostInfo(Subtexture? Texture, Color? BlendColor);

        private static readonly Dictionary<ArcherData, GhostInfo> ghosts = new();

        public string Name => "Ghost";

        public bool Enabled => !FortEntrance.Instance.Settings.DisableCustomGhosts;

        public void Load(IModuleContext context)
        {
            context.Harmony.Patch(
                AccessTools.Method(typeof(PlayerGhost), nameof(PlayerGhost.Added)),
                postfix: new HarmonyMethod(typeof(GhostFeature), nameof(PlayerGhost_Added_Postfix)));
        }

        // Editor access. Applied to ghosts when they are created, so refresh the preview after changing.
        public static bool TryGet(ArcherData archer, out Subtexture? texture, out Color? color)
        {
            var found = ghosts.TryGetValue(archer, out var info);
            texture = info?.Texture;
            color = info?.BlendColor;
            return found;
        }

        public static void Set(ArcherData archer, Subtexture? texture, Color? color) =>
            ghosts[archer] = new GhostInfo(texture, color);

        public static void Remove(ArcherData archer) => ghosts.Remove(archer);

        public bool Decorate(ArcherDecoration decoration)
        {
            var element = decoration.Xml["Ghost"];
            if (element == null)
                return false;

            var textureName = element.HasChildNodes && element.FirstChild is System.Xml.XmlText
                ? element.InnerText.Trim()
                : element.ChildText("Texture", "");

            Subtexture? texture = null;
            if (!string.IsNullOrWhiteSpace(textureName))
            {
                texture = decoration.FindTexture(textureName);
                if (texture == null)
                    throw new System.Exception($"ghost texture '{textureName}' not found in the atlas");
            }

            Color? color = element.HasChild("Color") ? element.ChildHexColor("Color") : null;
            if (texture == null && color == null)
                return false;

            ghosts[decoration.ArcherData] = new GhostInfo(texture, color);
            return true;
        }

        private static void PlayerGhost_Added_Postfix(PlayerGhost __instance)
        {
            var archerData = ArcherData.Get(
                TFGame.Characters[__instance.PlayerIndex],
                TFGame.AltSelect[__instance.PlayerIndex]
            );

            if (!ghosts.TryGetValue(archerData, out var info))
                return;

            var dynamic = DynamicData.For(__instance);

            if (info.Texture != null)
            {
                var sprite = dynamic.Get<Sprite<string>>("sprite");
                sprite?.SwapSubtexture(info.Texture);
            }

            if (info.BlendColor.HasValue)
                dynamic.Set("blendColor", info.BlendColor.Value);
        }
    }
}
