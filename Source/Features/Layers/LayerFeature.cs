#nullable enable
using System.Collections.Generic;
using System.Xml;
using FortRise;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Monocle;
using MonoMod.Utils;
using TowerFall;

namespace ArcherEditorMod.Source.Features.Layers
{
    // <Layer>...</Layer> or <Layers><Layer/><Layer/>...</Layers>
    // See LayerInfo for the fields; AttachTo is one of Head/Body/Bow/Corpse (default Body).
    public sealed class LayerFeature : IArcherFeature
    {
        private static readonly Dictionary<ArcherData, List<LayerInfo>> layersByArcher = new();

        public static IReadOnlyList<LayerInfo> GetLayers(ArcherData archer) =>
            layersByArcher.TryGetValue(archer, out var infos) ? infos : System.Array.Empty<LayerInfo>();

        public string Name => "Layers";

        public bool Enabled => !FortEntrance.Instance.Settings.DisableLayers;

        public void Load(IModuleContext context)
        {
            context.Harmony.Patch(
                AccessTools.Method(typeof(Player), nameof(Player.Added)),
                postfix: new HarmonyMethod(typeof(LayerFeature), nameof(Player_Added_Postfix)));

            context.Harmony.Patch(
                AccessTools.Method(typeof(PlayerCorpse), nameof(PlayerCorpse.Added)),
                postfix: new HarmonyMethod(typeof(LayerFeature), nameof(PlayerCorpse_Added_Postfix)));
        }

        public bool Decorate(ArcherDecoration decoration)
        {
            var xml = decoration.Xml;
            var infos = new List<LayerInfo>();

            if (xml.HasChild("Layer"))
                infos.Add(HandleLayer(xml["Layer"]!));

            if (xml.HasChild("Layers"))
            {
                foreach (var node in xml["Layers"]!)
                {
                    if (node is XmlElement { Name: "Layer" } layerXml)
                        infos.Add(HandleLayer(layerXml));
                }
            }

            if (infos.Count == 0)
                return false;

            layersByArcher[decoration.ArcherData] = infos;
            return true;
        }

        private static LayerInfo HandleLayer(XmlElement xml)
        {
            var attachTo = xml.ChildText("AttachTo", "Body").ToLowerInvariant() switch
            {
                "head" => LayerAttachType.Head,
                "bow" => LayerAttachType.Bow,
                "corpse" => LayerAttachType.Corpse,
                _ => LayerAttachType.Body
            };

            return new LayerInfo
            {
                AttachTo = attachTo,

                Sprite = xml.ChildText("Sprite"),
                Position = xml.ChildPosition("Position", Vector2.Zero),
                Color = Calc.HexToColor(xml.ChildText("Color", "FFFFFF")),
                ColorSwitch = xml.ChildInt("ColorSwitch", 0),
                ColorSwitchLoop = xml.ChildBool("ColorSwitchLoop", false),
                ToScale = xml.ChildBool("ToScale", true),

                IsColorA = xml.ChildBool("IsColorA", false),
                IsColorB = xml.ChildBool("IsColorB", false),
                IsRainbowColor = xml.ChildBool("IsRainbowColor", false),
                RainbowOffset = xml.ChildInt("RainbowOffset", 0),
                RainbowSpeed = xml.ChildFloat("RainbowSpeed", 1f),
                IsTeamColor = xml.ChildBool("IsTeamColor", false),

                IsOnInvisible = xml.ChildBool("IsOnInvisible", false),

                IsAiming = xml.ChildBool("IsAiming", true),
                IsNeutral = xml.ChildBool("IsNeutral", true),
                IsTeamBlue = xml.ChildBool("IsTeamBlue", true),
                IsTeamRed = xml.ChildBool("IsTeamRed", true),

                IsHat = xml.ChildBool("IsHat", true),
                IsNotHat = xml.ChildBool("IsNotHat", true),
                IsCrown = xml.ChildBool("IsCrown", true),

                IsOnGround = xml.ChildBool("IsOnGround", true),
                IsOnAir = xml.ChildBool("IsOnAir", true),
                IsDucking = xml.ChildBool("IsDucking", true),
                IsDodging = xml.ChildBool("IsDodging", true),
                IsLedgeGrab = xml.ChildBool("IsLedgeGrab", true),
                IsNormal = xml.ChildBool("IsNormal", true),
                IsDying = xml.ChildBool("IsDying", true),
                IsShoot = xml.ChildBool("IsShoot", true),

                DuckingOffset = xml.ChildPosition("DuckingOffset", new Vector2(0f, 0f)),
                HatOffset = xml.ChildPosition("HatOffset", new Vector2(0f, 0f)),
                CrownOffset = xml.ChildPosition("CrownOffset", new Vector2(0f, 0f)),

                OnJump = xml.ChildBool("OnJump", false),
                ReplaceJump = xml.ChildBool("ReplaceJump", false)
            };
        }

        private static void Player_Added_Postfix(Player __instance)
        {
            var archerData = __instance.ArcherData;
            if (!layersByArcher.TryGetValue(archerData, out var layerInfos))
                return;

            var headSprite = DynamicData.For(__instance).Get<Sprite<string>>("headSprite");
            var bodySprite = DynamicData.For(__instance).Get<Sprite<string>>("bodySprite");
            var bowSprite = DynamicData.For(__instance).Get<Sprite<string>>("bowSprite");

            var headIndex = 0;
            for (var i = 0; i < __instance.Components.Count; i++)
            {
                if (__instance.Components[i] == headSprite)
                {
                    headIndex = i;
                    break;
                }
            }

            foreach (var layerInfo in layerInfos)
            {
                if (layerInfo.AttachTo == LayerAttachType.Corpse)
                    continue;

                var attachedSprite = GetAttachedSprite(layerInfo, headSprite, bodySprite, bowSprite);
                var layer = new LayerSpriteComponent(layerInfo, attachedSprite, archerData, true, true);

                __instance.Add(layer);

                if (layerInfo.AttachTo != LayerAttachType.Body)
                {
                    __instance.Components.Remove(layer);
                    var insertIndex = headIndex + (layerInfo.AttachTo == LayerAttachType.Bow ? 2 : 1);
                    __instance.Components.Insert(insertIndex, layer);
                }
            }
        }

        private static void PlayerCorpse_Added_Postfix(PlayerCorpse __instance)
        {
            if (__instance.PlayerIndex == -1)
                return;

            var archerData = ArcherData.Get(TFGame.Characters[__instance.PlayerIndex], TFGame.AltSelect[__instance.PlayerIndex]);
            if (!layersByArcher.TryGetValue(archerData, out var layerInfos))
                return;

            var corpseSprite = DynamicData.For(__instance).Get<Sprite<string>>("sprite");
            foreach (var layerInfo in layerInfos)
            {
                if (layerInfo.AttachTo != LayerAttachType.Corpse)
                    continue;

                __instance.Add(new LayerSpriteComponent(layerInfo, corpseSprite, archerData, true, true));
            }
        }

        private static Sprite<string> GetAttachedSprite(LayerInfo layerInfo,
            Sprite<string> headSprite, Sprite<string> bodySprite, Sprite<string> bowSprite)
        {
            return layerInfo.AttachTo switch
            {
                LayerAttachType.Head => headSprite,
                LayerAttachType.Bow => bowSprite,
                _ => bodySprite
            };
        }
    }
}
