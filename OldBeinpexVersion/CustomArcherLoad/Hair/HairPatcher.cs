using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Xml;
using BepInEx.Logging;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Monocle;
using TowerFall;

namespace CustomArcherLoad
{
    public class HairPatcher
    {
        public static ManualLogSource Logger;
        private static FieldInfo scaleField;
        private static FieldInfo linksField;
        private static FieldInfo linkDistField;
        private static FieldInfo offsetsField;
        private static FieldInfo imagesField;
        private static FieldInfo sineField;
        private static FieldInfo shieldField;
        private static FieldInfo spriteField;

        public HairPatcher(ManualLogSource manualLogSource)
        {
            Logger = manualLogSource;

            scaleField = typeof(PlayerHair).GetField("scale", BindingFlags.NonPublic | BindingFlags.Instance);
            linksField = typeof(PlayerHair).GetField("links", BindingFlags.NonPublic | BindingFlags.Instance);
            linkDistField = typeof(PlayerHair).GetField("linkDist", BindingFlags.NonPublic | BindingFlags.Instance);
            offsetsField = typeof(PlayerHair).GetField("offsets", BindingFlags.NonPublic | BindingFlags.Instance);
            imagesField = typeof(PlayerHair).GetField("images", BindingFlags.NonPublic | BindingFlags.Instance);
            sineField = typeof(PlayerHair).GetField("sine", BindingFlags.NonPublic | BindingFlags.Instance);
            scaleField = typeof(PlayerHair).GetField("scale", BindingFlags.NonPublic | BindingFlags.Instance);
            shieldField = typeof(PlayerHair).GetField("shield", BindingFlags.NonPublic | BindingFlags.Instance);
            spriteField = typeof(PlayerHair).GetField("sprite", BindingFlags.NonPublic | BindingFlags.Instance);
        }

        public void Patch()
        {
            var instance = new Harmony("HairPatcher Patcher");
            instance.PatchAll(typeof(HairPatcher));
        }


        [HarmonyPatch(typeof(Player), nameof(Player.Added))]
        [HarmonyPostfix]
        public static void VisibleWithHatPostfix(Player __instance)
        {
            if (__instance == null)
            {
                return;
            }

            if (!__instance.Hair) return;
            var archerCustomData = Mod.ArcherCustomDataDict[__instance.ArcherData];
            var hairInfo = archerCustomData.HairInfo;
            if (hairInfo != null)
            {
                __instance.Hair.Visible = hairInfo.VisibleWithHat;
                return;
            }
        }

        // [HarmonyPatch(typeof(Player), "LoseHat")]
        // [HarmonyPostfix]
        // private void LoseHatPostfix(Player __instance, Arrow arrow = null, bool updateHead = true)
        // {
        //     if(__instance == null){
        //         return;
        //     }

        //     if(!__instance.Hair) return;
        //     var archerCustomData = Mod.ArcherCustomDataDict[__instance.ArcherData];
        //     var hairInfo = archerCustomData.HairInfo;
        //     if(hairInfo == null) 
        //     {
        //         return;
        //     }

        // 	Hat hat = Hat.CreateHat(__instance.ArcherData, __instance.TeamColor, __instance.HatState, __instance.Position + Vector2.UnitY * -7f, arrow, __instance.Facing == Facing.Left, __instance.PlayerIndex);
        // 	if (hat != null)
        // 	{
        // 		__instance.Level.Add(hat);
        // 	}
        // 	if (!__instance.Dead && updateHead)
        // 	{
        //         hatState.SetValue(__instance, HatStates.NoHat);
        // 		__instance.InitHead();
        // 	}
        // }

        [HarmonyPatch(typeof(PlayerHair), MethodType.Constructor,
            new Type[] {typeof(Entity), typeof(Vector2), typeof(float)})]
        [HarmonyFinalizer]
        static void PlayerHairPostfix(PlayerHair __instance, Entity follow, Vector2 position, float scale)
        {
            HairInfo hairInfo = null;
            if (follow is PlayerCorpse corpse)
            {
                var exist = hairs.TryGetValue(corpse.PlayerIndex, out var hair);
                if (exist)
                    hairInfo = hairs[corpse.PlayerIndex].HairInfo;
            }

            if (follow is Player player)
            {
                var archerCustomData = Mod.ArcherCustomDataDict[player.ArcherData];

                hairs[player.PlayerIndex] = archerCustomData;
                hairInfo = archerCustomData.HairInfo;
            }

            if (hairInfo == null)
                return;

            __instance.Visible = true;
            var hairSprite = hairInfo.HairSprite;
            var hairEndSprite = hairInfo.HairEndSprite;
            var sineValue = hairInfo.SineValue;
            var size = hairInfo.Size;
            var links = hairInfo.Links;
            var linksDist = hairInfo.LinksDist;
            var offsets = new Vector2[links];

            // __instance.Position = hairInfo.position;
            // __instance.Alpha = hairInfo.Alpha;
            // scaleField.SetValue(__instance, scale);

            linksField.SetValue(__instance, links);
            linkDistField.SetValue(__instance, linksDist);

            for (var i = 0; i < links; i++)
            {
                offsets[i] = new Vector2(0f, size * i);
            }

            var images = new Subtexture[links];
            for (var i = 0; i < links - 1; i++)
            {
                images[i] = TFGame.Atlas[hairSprite];
            }

            images[links - 1] = TFGame.Atlas[hairEndSprite];

            var sine = new SineWave(sineValue);
            sineField.SetValue(__instance, sine);
            offsetsField.SetValue(__instance, offsets);
            imagesField.SetValue(__instance, images);
        }

        private static Dictionary<int, ArcherCustomData> hairs = new Dictionary<int, ArcherCustomData>();

        [HarmonyPatch(typeof(PlayerHair), nameof(PlayerHair.Render))]
        [HarmonyPrefix]
        static bool PlayerHairPrefix(PlayerHair __instance)
        {
            var follow = __instance.Follow;
            HairInfo hairInfo = null;
            var duckingOffset = Vector2.Zero;
            var withHatOffset = Vector2.Zero;
            var facing = Facing.Right;

            if (follow is PlayerCorpse corpse)
            {
                var exist = hairs.TryGetValue(corpse.PlayerIndex, out var hair);
                if (exist)
                    hairInfo = hairs[corpse.PlayerIndex].HairInfo;
            }

            if (follow is Player player)
            {
                var archerCustomData = Mod.ArcherCustomDataDict[player.ArcherData];

                facing = player.Facing;
                hairs[player.PlayerIndex] = archerCustomData;
                hairInfo = archerCustomData.HairInfo;
                if (hairInfo != null && player.State == Player.PlayerStates.Ducking)
                {
                    duckingOffset = hairInfo.DuckingOffset;
                }

                if (hairInfo != null && player.HatState == Player.HatStates.Normal)
                {
                    withHatOffset = hairInfo.WithHatOffset;
                }
            }

            if (hairInfo == null)
            {
                return true;
            }

            var links = (int) linksField.GetValue(__instance);
            var images = (Subtexture[]) imagesField.GetValue(__instance);
            var offsets = (Vector2[]) offsetsField.GetValue(__instance);

            var actionsOffsets = duckingOffset.X + hairInfo.position.X + withHatOffset.X;
            var positionEntity = new Vector2(
                __instance.Position.X + (facing == Facing.Right ? actionsOffsets : actionsOffsets * -1),
                __instance.Position.Y + duckingOffset.Y + hairInfo.position.Y + withHatOffset.Y);

            var scale = (float) scaleField.GetValue(__instance);

            for (var index = 0; index < links; ++index)
            {
                var color = hairInfo.Color;
                if (hairInfo.Prismatic || hairInfo.Rainbow)
                {
                    color = GetPrismaticColor(Environment.TickCount, 0, hairInfo.PrismaticTime);
                }
                else if (hairInfo.EndColor.A != 0 && !hairInfo.PrismaticEnd)
                {
                    color = index == links - 1 ? hairInfo.EndColor : hairInfo.Color;
                    if (hairInfo.Gradient)
                    {
                        if (index >= hairInfo.GradientOffset)
                        {
                            var amount = (float) (index - hairInfo.GradientOffset) / links;
                            color = Color.Lerp(hairInfo.Color, hairInfo.EndColor, amount);
                        }
                    }
                }

                if (hairInfo.PrismaticEnd)
                {
                    var prismatic = GetPrismaticColor(Environment.TickCount, 0, hairInfo.PrismaticTime);
                    color = index == links - 1 ? prismatic : hairInfo.Color;
                    if (hairInfo.Gradient)
                    {
                        if (index > hairInfo.GradientOffset)
                        {
                            var amount = (float) index / links;
                            color = hairInfo.Rainbow
                                ? GetPrismaticColor(Environment.TickCount, index, hairInfo.PrismaticTime)
                                : Color.Lerp(hairInfo.Color, prismatic, amount);
                        }
                    }
                }

                var position = follow.Position + positionEntity + offsets[index];
                var rotation = index == 0 ? 0.0f : Calc.Angle(offsets[index], offsets[index - 1]);
                Draw.TextureCentered(images[index], position, color * __instance.Alpha * __instance.Alpha,
                    scale, rotation);
            }

            return false;
        }

        [HarmonyPatch(typeof(PlayerHair), nameof(PlayerHair.RenderOutline))]
        [HarmonyPrefix]
        static bool PlayerHairRenderOutline(PlayerHair __instance)
        {
            var follow = __instance.Follow;
            HairInfo hairInfo = null;
            var duckingOffset = Vector2.Zero;
            var withHatOffset = Vector2.Zero;

            var facing = Facing.Right;
            if (follow is PlayerCorpse corpse)
            {
                var exist = hairs.TryGetValue(corpse.PlayerIndex, out var hair);
                if (exist)
                    hairInfo = hairs[corpse.PlayerIndex].HairInfo;
            }

            if (follow is Player player)
            {
                var archerCustomData = Mod.ArcherCustomDataDict[player.ArcherData];

                facing = player.Facing;
                hairs[player.PlayerIndex] = archerCustomData;
                hairInfo = archerCustomData.HairInfo;
                if (hairInfo != null && player.State == Player.PlayerStates.Ducking)
                {
                    duckingOffset = hairInfo.DuckingOffset;
                }

                if (hairInfo != null && player.HatState == Player.HatStates.Normal)
                {
                    withHatOffset = hairInfo.WithHatOffset;
                }
            }

            if (hairInfo == null)
            {
                return true;
            }

            var links = (int) linksField.GetValue(__instance);
            var images = (Subtexture[]) imagesField.GetValue(__instance);
            var offsets = (Vector2[]) offsetsField.GetValue(__instance);

            var actionsOffsets = duckingOffset.X + hairInfo.position.X + withHatOffset.X;
            var positionEntity = new Vector2(
                __instance.Position.X + (facing == Facing.Right ? actionsOffsets : actionsOffsets * -1),
                __instance.Position.Y + duckingOffset.Y + hairInfo.position.Y + withHatOffset.Y);
            var scale = (float) scaleField.GetValue(__instance);

            for (var index1 = 0; index1 < links; ++index1)
            {
                var vector2 = follow.Position + positionEntity + offsets[index1];
                var rotation = index1 == 0 ? 0.0f : Calc.Angle(offsets[index1], offsets[index1 - 1]);
                for (var index2 = -1; index2 < 2; ++index2)
                {
                    for (var index3 = -1; index3 < 2; ++index3)
                    {
                        if (index2 != 0 || index3 != 0)
                            Draw.TextureCentered(images[index1], vector2 + new Vector2((float) index2, (float) index3),
                                hairInfo.OutlineColor, scale, rotation);
                    }
                }
            }

            return false;
        }

        public static Color GetPrismaticColor(float time, int offset = 0, float speedMultiplier = 1f)
        {
            var interval = 1500f;
            var current_index = ((int) ((float) time * speedMultiplier / interval) + offset) % PRISMATIC_COLORS.Length;
            var next_index = (current_index + 1) % PRISMATIC_COLORS.Length;
            var position = (float) time * speedMultiplier / interval % 1f;
            var prismatic_color = default(Color);
            prismatic_color.R = (byte) (MathHelper.Lerp((float) (int) PRISMATIC_COLORS[current_index].R / 255f,
                (float) (int) PRISMATIC_COLORS[next_index].R / 255f, position) * 255f);
            prismatic_color.G = (byte) (MathHelper.Lerp((float) (int) PRISMATIC_COLORS[current_index].G / 255f,
                (float) (int) PRISMATIC_COLORS[next_index].G / 255f, position) * 255f);
            prismatic_color.B = (byte) (MathHelper.Lerp((float) (int) PRISMATIC_COLORS[current_index].B / 255f,
                (float) (int) PRISMATIC_COLORS[next_index].B / 255f, position) * 255f);
            prismatic_color.A = (byte) (MathHelper.Lerp((float) (int) PRISMATIC_COLORS[current_index].A / 255f,
                (float) (int) PRISMATIC_COLORS[next_index].A / 255f, position) * 255f);
            return prismatic_color;
        }

        public static Color[] PRISMATIC_COLORS = new Color[6]
        {
            Color.Red,
            new Color(255, 120, 0),
            new Color(255, 217, 0),
            Color.Lime,
            Color.Cyan,
            Color.Violet
        };

        // [HarmonyPatch(typeof(PlayerHair), nameof(PlayerHair.Update))]
        // [HarmonyTranspiler]
        // static IEnumerable<CodeInstruction> PlayerHairUpdateTranspiler(IEnumerable<CodeInstruction> instructions)
        // {
        //     var codes = new List<CodeInstruction>(instructions);
        //     // for (var i = 0; i < codes.Count; i++)
        //     // {
        //     //     if (codes[i].operand is float)
        //     //     {
        //     //         var opefloat = (float)codes[i].operand;
        //     //         if (opefloat == 0.2f)
        //     //          codes[i].operand = 0.6f;
        //     //     }
        //     //     // if (codes[i + 1].opcode == OpCodes.Ldfld && (FieldInfo)codes[i + 1].operand == AccessTools.Field(typeof(ShopMenu), "hoverText"))
        //     //     // if (codes[i].opcode != OpCodes.Call) continue;
        //     //     // {
        //     //     //     var getColor = typeof(HairPatcher).GetMethod(nameof(GetColor));
        //     //     //     if (!codes[i].operand.ToString().Contains("get_White()")) continue;
        //     //     //     codes[i] = new CodeInstruction(OpCodes.Call, getColor);
        //     //     //     // codes.Insert(i, new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(ModEntry), nameof(ModEntry.DrawAllInfo))));
        //     //     //     // codes.Insert(i, new CodeInstruction(OpCodes.Ldarg_1));
        //     //     //     break;
        //     //     // }
        //     // }
        //     
        //
        //     return codes;
        // }
    }
}