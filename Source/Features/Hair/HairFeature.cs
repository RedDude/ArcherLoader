#nullable enable
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Xml;
using ArcherEditorMod.Rainbow;
using FortRise;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Monocle;
using TowerFall;

namespace ArcherEditorMod.Source.Features.Hair
{
    // One hair: <HairInfo>...</HairInfo>
    // Several independent hairs on the same archer (e.g. a ponytail plus a fringe, each with its own
    // sprite/color/physics): <HairInfos><HairInfo/><HairInfo/>...</HairInfos>
    //
    //   <HairSprite>player/hair</HairSprite>       optional
    //   <HairEndSprite>player/hairEnd</HairEndSprite> optional
    //   <Links>2</Links> <Size>1</Size> <LinksDist>1</LinksDist> <SineValue>30</SineValue>
    //   <Color>FFFFFF</Color> <EndColor>...</EndColor> <OutlineColor>000000</OutlineColor>
    //   <Alpha>1</Alpha> <Gradient>false</Gradient> <GradientOffset>0</GradientOffset>
    //   <Prismatic>false</Prismatic> <PrismaticEnd>false</PrismaticEnd> <PrismaticTime>1</PrismaticTime>
    //   <Rainbow>false</Rainbow> <VisibleWithHat>true</VisibleWithHat>
    //   <X>0</X> <Y>0</Y> <DuckingOffset x="0" y="0"/> <WithHatOffset x="0" y="0"/>
    public sealed class HairFeature : IArcherFeature
    {
        private static readonly FieldInfo ScaleField = AccessTools.Field(typeof(PlayerHair), "scale");
        private static readonly FieldInfo LinksField = AccessTools.Field(typeof(PlayerHair), "links");
        private static readonly FieldInfo LinkDistField = AccessTools.Field(typeof(PlayerHair), "linkDist");
        private static readonly FieldInfo OffsetsField = AccessTools.Field(typeof(PlayerHair), "offsets");
        private static readonly FieldInfo ImagesField = AccessTools.Field(typeof(PlayerHair), "images");
        private static readonly FieldInfo SineField = AccessTools.Field(typeof(PlayerHair), "sine");

        // The archer's configured hairs, first one is the "primary" (the PlayerHair the game itself
        // creates); the rest are extra PlayerHair components we construct and attach ourselves.
        private static readonly Dictionary<ArcherData, List<HairInfo>> hairByArcher = new();

        // Cached per player index so a PlayerCorpse (which gets a fresh PlayerHair of its own) can
        // reconstruct the same set of hairs the living player had.
        private sealed record PlayerHairs(List<HairInfo> Own, List<HairInfo> Extras);

        private static readonly Dictionary<int, PlayerHairs> hairsByPlayerIndex = new();

        // Hairs created from the archer editor. They are always extra hairs on top of the archer's own one
        // (its HairInfo, or the game's default hair when it has none).
        private static readonly Dictionary<ArcherData, List<HairInfo>> editorHairs = new();

        private static readonly FieldInfo HeadYOriginsField = AccessTools.Field(typeof(Player), "headYOrigins");
        private static readonly FieldInfo BodySpriteField = AccessTools.Field(typeof(Player), "bodySprite");

        // Extra hairs we are in the middle of constructing for a Follow entity, consumed in order by
        // PlayerHair_ctor_Postfix so each new instance gets the right HairInfo instead of the primary's.
        private static readonly Dictionary<Entity, Queue<HairInfo>> pendingExtraHair = new();

        // Which HairInfo a given PlayerHair instance renders. A ConditionalWeakTable so entries are
        // collected automatically once the component itself is gone (players/corpses come and go a lot
        // over a long session).
        private static readonly ConditionalWeakTable<PlayerHair, HairInfo> infoByInstance = new();

        public string Name => "Hair";

        public bool Enabled => !FortEntrance.Instance.Settings.DisableHairs;

        public void Load(IModuleContext context)
        {
            context.Harmony.Patch(
                AccessTools.Method(typeof(Player), nameof(Player.Added)),
                postfix: new HarmonyMethod(typeof(HairFeature), nameof(Player_Added_Postfix)));

            context.Harmony.Patch(
                AccessTools.Method(typeof(PlayerCorpse), nameof(PlayerCorpse.Added)),
                postfix: new HarmonyMethod(typeof(HairFeature), nameof(PlayerCorpse_Added_Postfix)));

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

        // ---- Editor access ----
        // Hair 1 is the archer's own: its configured HairInfo, or the game's default hair (not editable) when it
        // has none. Every hair after that is created by the editor.

        // A new hair that looks like the game's own one until it is customized.
        public static HairInfo NewHairInfo(string? name = null) =>
            new() { Name = name, Links = 5, LinksDist = 3, Size = 3 };

        /// <summary>Puts back the archer's own and editor hairs (undo); null / empty removes them.</summary>
        public static void SetLists(ArcherData archer, List<HairInfo>? own, List<HairInfo>? editor)
        {
            if (own == null) hairByArcher.Remove(archer); else hairByArcher[archer] = own;
            if (editor == null || editor.Count == 0) editorHairs.Remove(archer); else editorHairs[archer] = editor;
        }

        public static List<HairInfo>? GetOwnHairs(ArcherData archer) =>
            hairByArcher.TryGetValue(archer, out var infos) ? infos : null;

        public static IReadOnlyList<HairInfo> GetEditorHairs(ArcherData archer) =>
            editorHairs.TryGetValue(archer, out var infos) ? infos : System.Array.Empty<HairInfo>();

        // Own + editor hairs, for validation
        public static List<HairInfo>? GetHairs(ArcherData archer)
        {
            var all = new List<HairInfo>();
            if (hairByArcher.TryGetValue(archer, out var own)) all.AddRange(own);
            if (editorHairs.TryGetValue(archer, out var editor)) all.AddRange(editor);
            return all.Count > 0 ? all : null;
        }

        public static void OverrideDefaultHair(ArcherData archer)
        {
            if (!hairByArcher.ContainsKey(archer))
                hairByArcher[archer] = new List<HairInfo> { NewHairInfo() };
        }

        public static void RemoveOwnHair(ArcherData archer, int index)
        {
            if (!hairByArcher.TryGetValue(archer, out var infos) || index < 0 || index >= infos.Count)
                return;
            infos.RemoveAt(index);
            if (infos.Count == 0)
                hairByArcher.Remove(archer);
        }

        public static void AddEditorHair(ArcherData archer, string? name)
        {
            if (!editorHairs.TryGetValue(archer, out var infos))
                editorHairs[archer] = infos = new List<HairInfo>();
            infos.Add(NewHairInfo(name));
        }

        public static void RemoveEditorHair(ArcherData archer, int index)
        {
            if (!editorHairs.TryGetValue(archer, out var infos) || index < 0 || index >= infos.Count)
                return;
            infos.RemoveAt(index);
            if (infos.Count == 0)
                editorHairs.Remove(archer);
        }

        // Structural settings (links, size, sprites, sine) are baked into each PlayerHair when it is
        // created; push the current HairInfo values into every live hair.
        public static void ReapplyAll()
        {
            foreach (var (hair, info) in infoByInstance)
                ApplyHairCustomization(hair, info);
        }

        public bool Decorate(ArcherDecoration decoration)
        {
            var xml = decoration.Xml;
            var infos = new List<HairInfo>();

            if (xml.HasChild("HairInfo"))
                infos.Add(ParseHairInfo(xml["HairInfo"]!));

            if (xml.HasChild("HairInfos"))
            {
                foreach (var node in xml["HairInfos"]!)
                {
                    if (node is XmlElement { Name: "HairInfo" } hairXml)
                        infos.Add(ParseHairInfo(hairXml));
                }
            }

            if (infos.Count == 0)
                return false;

            hairByArcher[decoration.ArcherData] = infos;
            return true;
        }

        private static HairInfo ParseHairInfo(XmlElement element)
        {
            var info = new HairInfo
            {
                Name = element.ChildText("Name", null),
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

            return info;
        }

        private static void Player_Added_Postfix(Player __instance)
        {
            var archer = __instance.ArcherData;
            hairByArcher.TryGetValue(archer, out var own);
            editorHairs.TryGetValue(archer, out var editor);

            // the primary hair is the one the game creates for archers with Hair on; only then does the
            // archer's own HairInfo apply. Without one, only the editor hairs are attached.
            var hasPrimary = __instance.Hair != null;
            var extras = new List<HairInfo>();
            if (hasPrimary && own != null)
                extras.AddRange(own.Skip(1));
            if (editor != null)
                extras.AddRange(editor);

            var primaryInfos = hasPrimary && own != null ? own : new List<HairInfo>();
            if (primaryInfos.Count == 0 && extras.Count == 0)
                return;

            hairsByPlayerIndex[__instance.PlayerIndex] = new PlayerHairs(primaryInfos, extras);

            // The primary hair is picked up by PlayerHair_ctor_Postfix when the game constructs it; here
            // we only need to add the extra ones ourselves, reusing the same Follow/Position/Scale.
            AddExtraHairs(__instance, __instance.Hair, extras);
        }

        private static void PlayerCorpse_Added_Postfix(PlayerCorpse __instance)
        {
            if (__instance.PlayerIndex == -1)
                return;

            if (!hairsByPlayerIndex.TryGetValue(__instance.PlayerIndex, out var hairs) || hairs.Extras.Count == 0)
                return;

            AddExtraHairs(__instance, FindCorpseHair(__instance), hairs.Extras);
        }

        private static PlayerHair? FindCorpseHair(PlayerCorpse corpse)
        {
            foreach (var component in corpse.Components)
            {
                if (component is PlayerHair hair)
                    return hair;
            }
            return null;
        }

        private static void AddExtraHairs(Entity follow, PlayerHair? primary, List<HairInfo> hairInfos)
        {
            if (hairInfos.Count == 0)
                return;

            var position = primary?.Position ?? new Vector2(0f, -6f);
            var scale = primary != null ? (float)ScaleField.GetValue(primary)! : 1f;

            pendingExtraHair[follow] = new Queue<HairInfo>(hairInfos);
            try
            {
                for (var i = 0; i < hairInfos.Count; i++)
                {
                    var extra = new PlayerHair(follow, position, scale);
                    follow.Add(extra);
                }
            }
            finally
            {
                pendingExtraHair.Remove(follow);
            }
        }

        private static void PlayerHair_ctor_Postfix(PlayerHair __instance, Entity follow)
        {
            HairInfo? hairInfo = null;

            if (pendingExtraHair.TryGetValue(follow, out var queue) && queue.Count > 0)
            {
                hairInfo = queue.Dequeue();
            }
            else if (follow is PlayerCorpse corpse && hairsByPlayerIndex.TryGetValue(corpse.PlayerIndex, out var corpseHairs)
                     && corpseHairs.Own.Count > 0)
            {
                hairInfo = corpseHairs.Own[0];
            }
            else if (follow is Player player && hairByArcher.TryGetValue(player.ArcherData, out var playerHairs)
                     && playerHairs.Count > 0)
            {
                hairInfo = playerHairs[0];
            }

            if (hairInfo == null)
                return;

            infoByInstance.AddOrUpdate(__instance, hairInfo);
            __instance.Visible = hairInfo.VisibleWithHat;
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
            if (!infoByInstance.TryGetValue(self, out var hairInfo))
                return true;

            var follow = self.Follow;
            if (follow == null)
                return true;

            var duckingOffset = Vector2.Zero;
            var withHatOffset = Vector2.Zero;
            var facing = Facing.Right;

            if (follow is Player player)
            {
                facing = player.Facing;

                if (player.State == Player.PlayerStates.Ducking)
                    duckingOffset = hairInfo.DuckingOffset;

                if (player.HatState == Player.HatStates.Normal)
                    withHatOffset = hairInfo.WithHatOffset;
            }

            var links = (int)LinksField.GetValue(self)!;
            var images = (Subtexture[])ImagesField.GetValue(self)!;
            var offsets = (Vector2[])OffsetsField.GetValue(self)!;
            var scale = (float)ScaleField.GetValue(self)!;

            // the game only moves its own hair with the head; extra hairs follow it here
            var basePosition = follow is Player owner && owner.Hair != self ? HeadHairPosition(owner, self.Position) : self.Position;

            var actionsOffsets = duckingOffset.X + hairInfo.Position.X + withHatOffset.X;
            var positionEntity = new Vector2(
                basePosition.X + (facing == Facing.Right ? actionsOffsets : actionsOffsets * -1),
                basePosition.Y + duckingOffset.Y + hairInfo.Position.Y + withHatOffset.Y
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

        // where the game puts its own hair for the current body frame (Player.UpdateHead)
        private static Vector2 HeadHairPosition(Player player, Vector2 fallback)
        {
            if (player.Hair != null)
                return player.Hair.Position;

            var origins = HeadYOriginsField.GetValue(player) as int[];
            var body = BodySpriteField.GetValue(player) as Sprite<string>;
            if (origins == null || body == null || body.CurrentFrame >= origins.Length)
                return fallback;

            return new Vector2(-(int)player.Facing * 3, 12f - origins[body.CurrentFrame] * body.Scale.Y);
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
