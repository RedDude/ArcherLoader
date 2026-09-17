#nullable enable
using System.Collections.Generic;
using System.Reflection;
using ArcherLoaderMod.Rainbow;
using FortRise;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Monocle;
using TowerFall;

namespace ArcherLoaderMod.Source.Features.Hair
{
    // <HairInfo>
    //   <HairSprite>player/hair</HairSprite>       optional
    //   <HairEndSprite>player/hairEnd</HairEndSprite> optional
    //   <Links>2</Links> <Size>1</Size> <LinksDist>1</LinksDist> <SineValue>30</SineValue>
    //   <Color>FFFFFF</Color> <EndColor>...</EndColor> <OutlineColor>000000</OutlineColor>
    //   <Alpha>1</Alpha> <Gradient>false</Gradient> <GradientOffset>0</GradientOffset>
    //   <Prismatic>false</Prismatic> <PrismaticEnd>false</PrismaticEnd> <PrismaticTime>1</PrismaticTime>
    //   <Rainbow>false</Rainbow> <VisibleWithHat>true</VisibleWithHat>
    //   <X>0</X> <Y>0</Y> <DuckingOffset x="0" y="0"/> <WithHatOffset x="0" y="0"/>
    // </HairInfo>
    public sealed class HairFeature : IArcherFeature
    {
        private static readonly FieldInfo ScaleField = typeof(PlayerHair).GetField("scale", BindingFlags.NonPublic | BindingFlags.Instance)!;
        private static readonly FieldInfo LinksField = typeof(PlayerHair).GetField("links", BindingFlags.NonPublic | BindingFlags.Instance)!;
        private static readonly FieldInfo LinkDistField = typeof(PlayerHair).GetField("linkDist", BindingFlags.NonPublic | BindingFlags.Instance)!;
        private static readonly FieldInfo OffsetsField = typeof(PlayerHair).GetField("offsets", BindingFlags.NonPublic | BindingFlags.Instance)!;
        private static readonly FieldInfo ImagesField = typeof(PlayerHair).GetField("images", BindingFlags.NonPublic | BindingFlags.Instance)!;
        private static readonly FieldInfo SineField = typeof(PlayerHair).GetField("sine", BindingFlags.NonPublic | BindingFlags.Instance)!;

        // Keyed by ArcherData for the initial lookup, and cached per player index so corpses (which
        // re-parent a PlayerHair away from the Player) keep using the same info.
        private static readonly Dictionary<ArcherData, HairInfo> hairByArcher = new();
        private static readonly Dictionary<int, HairInfo> hairByPlayerIndex = new();

        public string Name => "Hair";

        public bool Enabled => !FortEntrance.Instance.Settings.DisableHairs;

        public void Load(IModuleContext context)
        {
            context.Harmony.Patch(
                AccessTools.Method(typeof(Player), nameof(Player.Added)),
                postfix: new HarmonyMethod(typeof(HairFeature), nameof(Player_Added_Postfix)));

            context.Harmony.Patch(
                AccessTools.Constructor(typeof(PlayerHair), [typeof(Entity), typeof(Vector2), typeof(float)]),
                postfix: new HarmonyMethod(typeof(HairFeature), nameof(PlayerHair_ctor_Postfix)));

            context.Harmony.Patch(
                AccessTools.Method(typeof(PlayerHair), nameof(PlayerHair.Render)),
                prefix: new HarmonyMethod(typeof(HairFeature), nameof(PlayerHair_Render_Prefix)));

            context.Harmony.Patch(
                AccessTools.Method(typeof(PlayerHair), nameof(PlayerHair.RenderOutline)),
                prefix: new HarmonyMethod(typeof(HairFeature), nameof(PlayerHair_RenderOutline_Prefix)));
        }

        public bool Decorate(ArcherDecoration decoration)
        {
            var element = decoration.Xml["HairInfo"];
            if (element == null)
                return false;

            var info = new HairInfo
            {
                Links = element.ChildInt("Links", 2),
                LinksDist = element.ChildFloat("LinksDist", 1),
                Size = element.ChildInt("Size", 1),
                SineValue = element.ChildInt("SineValue", 30),
                HairSprite = element.ChildText("HairSprite", "player/hair"),
                HairEndSprite = element.ChildText("HairEndSprite", "player/hairEnd"),
                Alpha = element.ChildFloat("Alpha", 1),
                Color = Calc.HexToColor(element.ChildText("Color", "FFFFFF")),
                OutlineColor = Calc.HexToColor(element.ChildText("OutlineColor", "000000")),
                Rainbow = element.ChildBool("Rainbow", false),
                Gradient = element.ChildBool("Gradient", false),
                GradientOffset = element.ChildInt("GradientOffset", 0),
                Prismatic = element.ChildBool("Prismatic", false),
                PrismaticEnd = element.ChildBool("PrismaticEnd", false),
                PrismaticTime = element.ChildFloat("PrismaticTime", 1),
                VisibleWithHat = element.ChildBool("VisibleWithHat", true),
            };

            var endColor = element.ChildText("EndColor", null);
            info.EndColor = endColor != null ? Calc.HexToColor(endColor) : Color.Transparent;

            info.Position = new Vector2(element.ChildFloat("X", 0), element.ChildFloat("Y", 0));

            if (element.HasChild("DuckingOffset"))
            {
                try
                {
                    info.DuckingOffset = element.ChildPosition("DuckingOffset");
                }
                catch
                {
                    info.DuckingOffset = new Vector2(0, element.ChildInt("DuckingOffset", 0));
                }
            }

            if (element.HasChild("WithHatOffset"))
                info.WithHatOffset = element.ChildPosition("WithHatOffset");

            hairByArcher[decoration.ArcherData] = info;
            return true;
        }

        private static void Player_Added_Postfix(Player __instance)
        {
            if (__instance.Hair == null)
                return;

            if (!hairByArcher.TryGetValue(__instance.ArcherData, out var hairInfo))
                return;

            __instance.Hair.Visible = hairInfo.VisibleWithHat;
        }

        private static void PlayerHair_ctor_Postfix(PlayerHair __instance, Entity follow)
        {
            HairInfo? hairInfo = null;

            if (follow is PlayerCorpse corpse && hairByPlayerIndex.TryGetValue(corpse.PlayerIndex, out var corpseHair))
            {
                hairInfo = corpseHair;
            }
            else if (follow is Player player && hairByArcher.TryGetValue(player.ArcherData, out var playerHair))
            {
                hairByPlayerIndex[player.PlayerIndex] = playerHair;
                hairInfo = playerHair;
            }

            if (hairInfo == null)
                return;

            __instance.Visible = true;
            ApplyHairCustomization(__instance, hairInfo);
        }

        private static void ApplyHairCustomization(PlayerHair hair, HairInfo hairInfo)
        {
            var links = hairInfo.Links;
            LinksField.SetValue(hair, links);
            LinkDistField.SetValue(hair, hairInfo.LinksDist);

            var offsets = new Vector2[links];
            for (var i = 0; i < links; i++)
                offsets[i] = new Vector2(0f, hairInfo.Size * i);
            OffsetsField.SetValue(hair, offsets);

            var images = new Subtexture[links];
            for (var i = 0; i < links - 1; i++)
                images[i] = TFGame.Atlas[hairInfo.HairSprite];
            images[links - 1] = TFGame.Atlas[hairInfo.HairEndSprite];
            ImagesField.SetValue(hair, images);

            SineField.SetValue(hair, new SineWave(hairInfo.SineValue));
        }

        private static bool PlayerHair_Render_Prefix(PlayerHair __instance) => HandleHairRendering(__instance, isOutline: false);

        private static bool PlayerHair_RenderOutline_Prefix(PlayerHair __instance) => HandleHairRendering(__instance, isOutline: true);

        private static bool HandleHairRendering(PlayerHair self, bool isOutline)
        {
            var follow = self.Follow;
            if (follow == null)
                return true;

            HairInfo? hairInfo = null;
            var duckingOffset = Vector2.Zero;
            var withHatOffset = Vector2.Zero;
            var facing = Facing.Right;

            if (follow is PlayerCorpse corpse && hairByPlayerIndex.TryGetValue(corpse.PlayerIndex, out var corpseHair))
            {
                hairInfo = corpseHair;
            }
            else if (follow is Player player)
            {
                if (!hairByPlayerIndex.TryGetValue(player.PlayerIndex, out var playerHair))
                    return true;

                hairInfo = playerHair;
                facing = player.Facing;

                if (player.State == Player.PlayerStates.Ducking)
                    duckingOffset = hairInfo.DuckingOffset;

                if (player.HatState == Player.HatStates.Normal)
                    withHatOffset = hairInfo.WithHatOffset;
            }

            if (hairInfo == null)
                return true;

            var links = (int)LinksField.GetValue(self)!;
            var images = (Subtexture[])ImagesField.GetValue(self)!;
            var offsets = (Vector2[])OffsetsField.GetValue(self)!;
            var scale = (float)ScaleField.GetValue(self)!;

            var actionsOffsets = duckingOffset.X + hairInfo.Position.X + withHatOffset.X;
            var positionEntity = new Vector2(
                self.Position.X + (facing == Facing.Right ? actionsOffsets : actionsOffsets * -1),
                self.Position.Y + duckingOffset.Y + hairInfo.Position.Y + withHatOffset.Y
            );

            for (var index = 0; index < links; index++)
            {
                var position = follow.Position + positionEntity + offsets[index];
                var rotation = index == 0 ? 0.0f : Calc.Angle(offsets[index], offsets[index - 1]);

                if (isOutline)
                {
                    RenderHairOutline(images[index], position, hairInfo.OutlineColor, scale, rotation);
                }
                else
                {
                    var color = GetHairColor(index, links, hairInfo);
                    Draw.TextureCentered(images[index], position, color * self.Alpha, scale, rotation);
                }
            }

            return false;
        }

        private static Color GetHairColor(int index, int links, HairInfo hairInfo)
        {
            if (hairInfo.Prismatic || hairInfo.Rainbow)
                return RainbowManager.CurrentColor;

            if (hairInfo.EndColor.A != 0 && !hairInfo.PrismaticEnd)
            {
                if (index == links - 1)
                    return hairInfo.EndColor;

                if (hairInfo.Gradient && index >= hairInfo.GradientOffset)
                {
                    var amount = (float)(index - hairInfo.GradientOffset) / links;
                    return Color.Lerp(hairInfo.Color, hairInfo.EndColor, amount);
                }
                return hairInfo.Color;
            }

            if (hairInfo.PrismaticEnd)
            {
                if (index == links - 1)
                    return RainbowManager.CurrentColor;

                if (hairInfo.Gradient && index > hairInfo.GradientOffset)
                {
                    var amount = (float)index / links;
                    return hairInfo.Rainbow
                        ? RainbowManager.GetColor(index, hairInfo.PrismaticTime)
                        : Color.Lerp(hairInfo.Color, RainbowManager.CurrentColor, amount);
                }
                return hairInfo.Color;
            }

            return hairInfo.Color;
        }

        private static void RenderHairOutline(Subtexture texture, Vector2 position, Color color, float scale, float rotation)
        {
            for (var x = -1; x < 2; x++)
            {
                for (var y = -1; y < 2; y++)
                {
                    if (x != 0 || y != 0)
                        Draw.TextureCentered(texture, position + new Vector2(x, y), color, scale, rotation);
                }
            }
        }
    }
}
