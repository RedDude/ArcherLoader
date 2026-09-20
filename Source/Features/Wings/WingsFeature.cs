#nullable enable
using System.Collections.Generic;
using HarmonyLib;
using FortRise;
using Microsoft.Xna.Framework;
using Monocle;
using MonoMod.Utils;
using TowerFall;

namespace ArcherEditorMod.Source.Features.Wings
{
    // <Wings>
    //   <Texture>player/wings/myWings</Texture>   optional
    //   <Color>FF00FF</Color>                     optional
    // </Wings>
    // or shorthand: <Wings>player/wings/myWings</Wings>
    public sealed class WingsFeature : IArcherFeature
    {
        private sealed record WingsInfo(Subtexture? Texture, Color? Color);

        private static readonly Dictionary<ArcherData, WingsInfo> wings = new();

        public string Name => "Wings";

        public bool Enabled => !FortEntrance.Instance.Settings.DisableCustomWings;

        public void Load(IModuleContext context)
        {
            context.Harmony.Patch(
                AccessTools.Method(typeof(Player), nameof(Player.Added)),
                postfix: new HarmonyMethod(typeof(WingsFeature), nameof(Player_Added_Postfix)));
        }

        // Editor access. Applied to players when they are created, so refresh the preview after changing.
        public static bool TryGet(ArcherData archer, out Subtexture? texture, out Color? color)
        {
            var found = wings.TryGetValue(archer, out var info);
            texture = info?.Texture;
            color = info?.Color;
            return found;
        }

        public static void Set(ArcherData archer, Subtexture? texture, Color? color) =>
            wings[archer] = new WingsInfo(texture, color);

        public static void Remove(ArcherData archer) => wings.Remove(archer);

        public bool Decorate(ArcherDecoration decoration)
        {
            var element = decoration.Xml["Wings"];
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
                    throw new System.Exception($"wings texture '{textureName}' not found in the atlas");
            }

            Color? color = element.HasChild("Color") ? element.ChildHexColor("Color") : null;
            if (texture == null && color == null)
                return false;

            wings[decoration.ArcherData] = new WingsInfo(texture, color);
            return true;
        }

        private static void Player_Added_Postfix(Player __instance)
        {
            if (!wings.TryGetValue(__instance.ArcherData, out var info))
                return;

            foreach (var component in __instance.Components)
            {
                if (component is not PlayerWings playerWings)
                    continue;

                var sprite = DynamicData.For(playerWings).Get<Sprite<string>>("sprite");
                if (sprite == null)
                    return;

                if (info.Texture != null)
                    sprite.SwapSubtexture(info.Texture);
                if (info.Color.HasValue)
                    sprite.Color = info.Color.Value;
                return;
            }
        }
    }
}
