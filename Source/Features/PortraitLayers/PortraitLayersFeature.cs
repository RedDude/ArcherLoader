#nullable enable
using System.Collections.Generic;
using System.Xml;
using FortRise;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Monocle;
using MonoMod.Utils;
using TowerFall;

namespace ArcherEditorMod.Source.Features.PortraitLayers
{
    // <PortraitLayer>...</PortraitLayer> or <PortraitLayers><PortraitLayer/><PortraitLayer/>...</PortraitLayers>
    // AttachTo is one of Joined/NotJoined/Won/Lose (default Lose, matching the original behavior).
    public sealed class PortraitLayersFeature : IArcherFeature
    {
        private static readonly Dictionary<ArcherData, List<PortraitLayerInfo>> layersByArcher = new();

        public static IReadOnlyList<PortraitLayerInfo> GetLayers(ArcherData archer) =>
            layersByArcher.TryGetValue(archer, out var infos) ? infos : System.Array.Empty<PortraitLayerInfo>();

        public string Name => "PortraitLayers";

        public bool Enabled => !FortEntrance.Instance.Settings.DisableLayers;

        public void Load(IModuleContext context)
        {
            context.Harmony.Patch(
                AccessTools.Method(typeof(MainMenu), nameof(MainMenu.DestroyRollcall)),
                postfix: new HarmonyMethod(typeof(PortraitLayersFeature), nameof(MainMenu_DestroyRollcall_Postfix)));

            context.Harmony.Patch(
                AccessTools.Method(typeof(ArcherPortrait), nameof(ArcherPortrait.SetCharacter)),
                prefix: new HarmonyMethod(typeof(PortraitLayersFeature), nameof(ArcherPortrait_SetCharacter_Prefix)),
                postfix: new HarmonyMethod(typeof(PortraitLayersFeature), nameof(ArcherPortrait_SetCharacter_Postfix)));

            context.Harmony.Patch(
                AccessTools.Method(typeof(ArcherPortrait), nameof(ArcherPortrait.StartJoined)),
                postfix: new HarmonyMethod(typeof(PortraitLayersFeature), nameof(ArcherPortrait_StartJoined_Postfix)));

            context.Harmony.Patch(
                AccessTools.Method(typeof(ArcherPortrait), nameof(ArcherPortrait.Leave)),
                postfix: new HarmonyMethod(typeof(PortraitLayersFeature), nameof(ArcherPortrait_Leave_Postfix)));

            context.Harmony.Patch(
                AccessTools.Constructor(typeof(RollcallElement), [typeof(int)]),
                postfix: new HarmonyMethod(typeof(PortraitLayersFeature), nameof(RollcallElement_ctor_Postfix)));

            context.Harmony.Patch(
                AccessTools.Constructor(typeof(VersusPlayerMatchResults),
                    [typeof(Session), typeof(VersusMatchResults), typeof(int), typeof(Vector2), typeof(Vector2), typeof(List<AwardInfo>)]),
                postfix: new HarmonyMethod(typeof(PortraitLayersFeature), nameof(VersusPlayerMatchResults_ctor_Postfix)));
        }

        public bool Decorate(ArcherDecoration decoration)
        {
            var xml = decoration.Xml;
            var infos = new List<PortraitLayerInfo>();

            if (xml.HasChild("PortraitLayer"))
                infos.Add(HandleLayer(decoration, xml["PortraitLayer"]!));

            if (xml.HasChild("PortraitLayers"))
            {
                foreach (var node in xml["PortraitLayers"]!)
                {
                    if (node is XmlElement { Name: "PortraitLayer" } layerXml)
                        infos.Add(HandleLayer(decoration, layerXml));
                }
            }

            if (infos.Count == 0)
                return false;

            layersByArcher[decoration.ArcherData] = infos;
            return true;
        }

        private static PortraitLayerInfo HandleLayer(ArcherDecoration decoration, XmlElement xml)
        {
            var attachTo = xml.ChildText("AttachTo", "").ToLowerInvariant() switch
            {
                "join" or "joined" => PortraitLayersAttachType.Joined,
                "notjoin" or "notjoined" => PortraitLayersAttachType.NotJoined,
                "won" => PortraitLayersAttachType.Won,
                _ => PortraitLayersAttachType.Lose
            };

            var spriteId = xml.ChildText("Sprite", xml.GetAttribute("id"));
            var (resolvedSprite, isMenuSprite) = ResolveSpriteString(decoration, spriteId);

            return new PortraitLayerInfo
            {
                AttachTo = attachTo,
                Sprite = resolvedSprite,
                IsMenuSprite = isMenuSprite,
                Position = xml.ChildPosition("Position", Vector2.Zero),
                Color = Calc.HexToColor(xml.ChildText("Color", "FFFFFF")),
                ScaleAnimation = xml.ChildPosition("ScaleAnimation", Vector2.Zero),
                RotationAnimation = xml.ChildPosition("RotationAnimation", Vector2.Zero),
                FloatAnimation = xml.ChildPosition("FloatAnimation", Vector2.Zero),
                FloatAnimationRate = xml.ChildInt("FloatAnimationRate", 0),

                IsColorA = xml.ChildBool("IsColorA", false),
                IsColorB = xml.ChildBool("IsColorB", false),
                IsRainbowColor = xml.ChildBool("IsRainbowColor", false),
                RainbowOffset = xml.ChildInt("RainbowOffset", 0),
                RainbowSpeed = xml.ChildFloat("RainbowSpeed", 1),

                ToScale = xml.ChildBool("ToScale", true),
            };
        }

        // sprite_string entries are registered as "{mod}/{id}" (see ModSprites.RegisterSprite/RegisterMenuSprite),
        // so a mod referencing its own layer sprite needs the same mod-prefixed-then-raw resolution as
        // Wings/Ghost/Texture/Taunt. Layer sprites are conventionally defined in menuSpriteData.xml, so the
        // menu container is checked first, falling back to the main container for compatibility.
        private static (string id, bool isMenuSprite) ResolveSpriteString(ArcherDecoration decoration, string id)
        {
            var prefixed = $"{decoration.ModContent.Metadata.Name}/{id}";
            var menuSprites = TFGame.MenuSpriteData.GetSprites();
            if (menuSprites.ContainsKey(prefixed))
                return (prefixed, true);
            if (menuSprites.ContainsKey(id))
                return (id, true);

            var sprites = TFGame.SpriteData.GetSprites();
            if (sprites.ContainsKey(prefixed))
                return (prefixed, false);

            return (id, false);
        }

        private static void MainMenu_DestroyRollcall_Postfix() => PortraitLayersManager.Clear();

        private static void ArcherPortrait_SetCharacter_Prefix(ArcherPortrait __instance) =>
            PortraitLayersManager.HideAllLayersFromPortrait(__instance);

        private static void ArcherPortrait_SetCharacter_Postfix(ArcherPortrait __instance, int characterIndex, ArcherData.ArcherTypes altSelect)
        {
            var data = ArcherData.Get(characterIndex, altSelect);
            layersByArcher.TryGetValue(data, out var layerInfos);
            PortraitLayersManager.CreateSelectionLayersComponents(__instance, data, layerInfos);
            PortraitLayersManager.ShowAllLayersFromType(PortraitLayersAttachType.NotJoined, __instance, data);
        }

        private static void ArcherPortrait_StartJoined_Postfix(ArcherPortrait __instance) =>
            PortraitLayersManager.OnPortraitStartJoin(__instance);

        private static void ArcherPortrait_Leave_Postfix(ArcherPortrait __instance) =>
            PortraitLayersManager.OnPortraitLeave(__instance);

        private static void RollcallElement_ctor_Postfix(RollcallElement __instance)
        {
            var portrait = DynamicData.For(__instance).Get<ArcherPortrait>("portrait");
            var joined = DynamicData.For(portrait).Get<bool>("joined");
            var data = ArcherData.Get(portrait.CharacterIndex, portrait.AltSelect);

            layersByArcher.TryGetValue(data, out var layerInfos);
            PortraitLayersManager.CreateSelectionLayersComponents(portrait, data, layerInfos);
            PortraitLayersManager.ShowAllLayersFromType(
                joined ? PortraitLayersAttachType.Joined : PortraitLayersAttachType.NotJoined,
                portrait, data);
        }

        private static void VersusPlayerMatchResults_ctor_Postfix(VersusPlayerMatchResults __instance, int playerIndex)
        {
            var won = DynamicData.For(__instance).Get<bool>("won");
            var data = ArcherData.Get(TFGame.Characters[playerIndex], TFGame.AltSelect[playerIndex]);

            layersByArcher.TryGetValue(data, out var layerInfos);
            var layers = PortraitLayersManager.CreateWonLoseLayersComponents(__instance, data, layerInfos);

            if (layers != null)
                PortraitLayersManager.ShowAllLayersFromType(won ? PortraitLayersAttachType.Won : PortraitLayersAttachType.Lose, layers);
        }
    }
}
