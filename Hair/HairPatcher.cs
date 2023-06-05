using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Xna.Framework;
using Monocle;
using TowerFall;

namespace ArcherLoaderMod.Hair
{
    public class HairPatcher
    {
        //public static ManualLogSource Logger;
        private static FieldInfo _scaleField;
        private PropertyInfo _hatState;
        public static FieldInfo LinksField;
        private static FieldInfo _linkDistField;
        private static FieldInfo _offsetsField;
        private static FieldInfo _imagesField;
        private static FieldInfo _sineField;

        public static Dictionary<int, ArcherCustomData> Hairs = new();

        public void Load()
        {
            _scaleField = typeof(PlayerHair).GetField("scale", BindingFlags.NonPublic | BindingFlags.Instance);
            LinksField = typeof(PlayerHair).GetField("links", BindingFlags.NonPublic | BindingFlags.Instance);
            _linkDistField = typeof(PlayerHair).GetField("linkDist", BindingFlags.NonPublic | BindingFlags.Instance);
            _offsetsField = typeof(PlayerHair).GetField("offsets", BindingFlags.NonPublic | BindingFlags.Instance);
            _imagesField = typeof(PlayerHair).GetField("images", BindingFlags.NonPublic | BindingFlags.Instance);
            _sineField = typeof(PlayerHair).GetField("sine", BindingFlags.NonPublic | BindingFlags.Instance);
            _scaleField = typeof(PlayerHair).GetField("scale", BindingFlags.NonPublic | BindingFlags.Instance);
            _hatState = typeof(PlayerHair).GetProperty("HatState", BindingFlags.NonPublic | BindingFlags.Instance);

            On.TowerFall.Player.Added += OnPlayerOnAdded;
            On.TowerFall.PlayerHair.ctor += OnPlayerHairConstructor;
            On.TowerFall.PlayerHair.Render += OnPlayerHairOnRender;
            On.TowerFall.PlayerHair.RenderOutline += OnPlayerHairOnRenderOutline;
        }

        static public void Unload()
        {
            if (FortEntrance.Settings.DisableHairs)
                return;

            On.TowerFall.Player.Added -= OnPlayerOnAdded;
            On.TowerFall.PlayerHair.ctor -= OnPlayerHairConstructor;
            On.TowerFall.PlayerHair.Render -= OnPlayerHairOnRender;
            On.TowerFall.PlayerHair.RenderOutline -= OnPlayerHairOnRenderOutline;
        }

        
        private static void OnPlayerOnAdded(On.TowerFall.Player.orig_Added orig, Player self)
        {
            orig(self);
            if (self == null)
            {
                return;
            }

            if (!self.Hair) return;

            var exist = Mod.ArcherCustomDataDict.TryGetValue(self.ArcherData, out var archerCustomData);
            if (!exist) return;
            var hairInfo = archerCustomData.HairInfo;
            if (hairInfo != null)
            {
                self.Hair.Visible = hairInfo.VisibleWithHat;
            }
        }

        private static void OnPlayerHairConstructor(On.TowerFall.PlayerHair.orig_ctor orig, PlayerHair self, Entity follow,
            Vector2 position, float scale)
        {
            orig(self, follow, position, scale);

            HairInfo hairInfo = null;
            if (follow is PlayerCorpse corpse)
            {
                var exist = Hairs.TryGetValue(corpse.PlayerIndex, out var hair);
                if (exist)
                    hairInfo = Hairs[corpse.PlayerIndex].HairInfo;
            }

            if (follow is Player player)
            {
                var exist = Mod.ArcherCustomDataDict.TryGetValue(player.ArcherData, out var archerCustomData);
                if (exist)
                {
                    Hairs[player.PlayerIndex] = archerCustomData;
                    hairInfo = archerCustomData?.HairInfo;
                }
            }

            if (hairInfo == null)
                return;

            self.Visible = true;
            var hairSprite = hairInfo.HairSprite;
            var hairEndSprite = hairInfo.HairEndSprite;
            var sineValue = hairInfo.SineValue;
            var size = hairInfo.Size;
            var links = hairInfo.Links;
            var linksDist = hairInfo.LinksDist;
            var offsets = new Vector2[links];

            // self.Position = hairInfo.position;
            // self.Alpha = hairInfo.Alpha;
            // scaleField.SetValue(self, scale);

            LinksField.SetValue(self, links);
            _linkDistField.SetValue(self, linksDist);

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
            _sineField.SetValue(self, sine);
            _offsetsField.SetValue(self, offsets);
            _imagesField.SetValue(self, images);
        }

        private static void OnPlayerHairOnRender(On.TowerFall.PlayerHair.orig_Render orig, PlayerHair self)
        {
            var follow = self.Follow;
            HairInfo hairInfo = null;
            var duckingOffset = Vector2.Zero;
            var withHatOffset = Vector2.Zero;
            var facing = Facing.Right;

            if (follow is PlayerCorpse corpse)
            {
                var exist = Hairs.TryGetValue(corpse.PlayerIndex, out var hair);
                if (exist)
                    hairInfo = Hairs[corpse.PlayerIndex].HairInfo;
            }

            if (follow is Player player)
            {
                var exist = Mod.ArcherCustomDataDict.TryGetValue(player.ArcherData, out var archerCustomData);
                if (exist)
                {
                    facing = player.Facing;
                    Hairs[player.PlayerIndex] = archerCustomData;
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
            }

            if (hairInfo == null)
            {
                orig(self);
                return;
            }

            var links = (int) LinksField.GetValue(self);
            var images = (Subtexture[]) _imagesField.GetValue(self);
            var offsets = (Vector2[]) _offsetsField.GetValue(self);

            var actionsOffsets = duckingOffset.X + hairInfo.Position.X + withHatOffset.X;
            var positionEntity = new Vector2(
                self.Position.X + (facing == Facing.Right ? actionsOffsets : actionsOffsets * -1),
                self.Position.Y + duckingOffset.Y + hairInfo.Position.Y + withHatOffset.Y);

            var scale = (float) _scaleField.GetValue(self);

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
                Draw.TextureCentered(images[index], position, color * self.Alpha * self.Alpha,
                    scale, rotation);
            }
        }

        private static void OnPlayerHairOnRenderOutline(On.TowerFall.PlayerHair.orig_RenderOutline orig, PlayerHair self)
        {
            var follow = self.Follow;
            HairInfo hairInfo = null;
            var duckingOffset = Vector2.Zero;
            var withHatOffset = Vector2.Zero;

            var facing = Facing.Right;
            if (follow is PlayerCorpse corpse)
            {
                var exist = Hairs.TryGetValue(corpse.PlayerIndex, out var hair);
                if (exist)
                    hairInfo = Hairs[corpse.PlayerIndex].HairInfo;
            }

            if (follow is Player player)
            {
                var exist = Mod.ArcherCustomDataDict.TryGetValue(player.ArcherData, out var archerCustomData);
                if (exist)
                {
                    facing = player.Facing;
                    Hairs[player.PlayerIndex] = archerCustomData;
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
            }

            if (hairInfo == null)
            {
                orig(self);
                return;
            }

            var links = (int) LinksField.GetValue(self);
            var images = (Subtexture[]) _imagesField.GetValue(self);
            var offsets = (Vector2[]) _offsetsField.GetValue(self);

            var actionsOffsets = duckingOffset.X + hairInfo.Position.X + withHatOffset.X;
            var positionEntity = new Vector2(
                self.Position.X + (facing == Facing.Right ? actionsOffsets : actionsOffsets * -1),
                self.Position.Y + duckingOffset.Y + hairInfo.Position.Y + withHatOffset.Y);
            var scale = (float) _scaleField.GetValue(self);

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
        }


        public static Color[] PrismaticColors = new Color[6]
        {
            Color.Red,
            new Color(255, 120, 0),
            new Color(255, 217, 0),
            Color.Lime,
            Color.Cyan,
            Color.Violet
        };

        public static Color GetPrismaticColor(float time, int offset = 0, float speedMultiplier = 1f)
        {
            var interval = 1500f;
            var currentIndex = ((int) ((float) time * speedMultiplier / interval) + offset) % PrismaticColors.Length;
            var nextIndex = (currentIndex + 1) % PrismaticColors.Length;
            var position = (float) time * speedMultiplier / interval % 1f;
            var prismaticColor = default(Color);
            prismaticColor.R = (byte) (MathHelper.Lerp((float) (int) PrismaticColors[currentIndex].R / 255f,
                (float) (int) PrismaticColors[nextIndex].R / 255f, position) * 255f);
            prismaticColor.G = (byte) (MathHelper.Lerp((float) (int) PrismaticColors[currentIndex].G / 255f,
                (float) (int) PrismaticColors[nextIndex].G / 255f, position) * 255f);
            prismaticColor.B = (byte) (MathHelper.Lerp((float) (int) PrismaticColors[currentIndex].B / 255f,
                (float) (int) PrismaticColors[nextIndex].B / 255f, position) * 255f);
            prismaticColor.A = (byte) (MathHelper.Lerp((float) (int) PrismaticColors[currentIndex].A / 255f,
                (float) (int) PrismaticColors[nextIndex].A / 255f, position) * 255f);
            return prismaticColor;
        }

        // [HarmonyPatch(typeof(PlayerHair), MethodType.Constructor, 
        //     new Type[] { typeof(Entity), typeof(Vector2), typeof(float) })]
        // [HarmonyFinalizer]
        // static void PlayerHairPostfix(PlayerHair self, Entity follow, Vector2 position, float scale)
        // {
        //     HairInfo hairInfo = null;
        //     if (follow is PlayerCorpse corpse)
        //     {
        //         var exist = hairs.TryGetValue(corpse.PlayerIndex, out var hair);
        //         if(exist)
        //             hairInfo = hairs[corpse.PlayerIndex].HairInfo;
        //     }
        //     if (follow is Player player)
        //     {
        //         var exist = Mod.ArcherCustomDataDict.TryGetValue(player.ArcherData, out var archerCustomData);
        //         if (exist)
        //         {
        //             hairs[player.PlayerIndex] = archerCustomData;
        //             hairInfo = archerCustomData?.HairInfo;
        //         }
        //     }
        //
        //     if(hairInfo == null)
        //         return;
        //     
        //     self.Visible = true;
        //     var hairSprite = hairInfo.HairSprite;
        //     var hairEndSprite =  hairInfo.HairEndSprite;
        //     var sineValue = hairInfo.SineValue;
        //     var size = hairInfo.Size;
        //     var links = hairInfo.Links;
        //     var linksDist = hairInfo.LinksDist;
        //     var offsets = new Vector2[links];
        //
        //     // self.Position = hairInfo.position;
        //     // self.Alpha = hairInfo.Alpha;
        //     // scaleField.SetValue(self, scale);
        //     
        //     linksField.SetValue(self, links);
        //     linkDistField.SetValue(self, linksDist);
        //     
        //     for (var i = 0; i < links; i++)
        //     {
        //         offsets[i] = new Vector2(0f, size * i);
        //     }
        //     
        //     var images = new Subtexture[links];
        //     for (var i = 0; i < links - 1; i++)
        //     {
        //         images[i] = TFGame.Atlas[hairSprite];
        //     }
        //
        //     images[links - 1] = TFGame.Atlas[hairEndSprite];
        //     
        //     var sine = new SineWave(sineValue);
        //     sineField.SetValue(self, sine);
        //     offsetsField.SetValue(self, offsets);
        //     imagesField.SetValue(self, images);
        // }

        // [HarmonyPatch(typeof(PlayerHair), nameof(PlayerHair.Render))]
        // [HarmonyPrefix]
        // static bool PlayerHairPrefix(PlayerHair self)
        // {
        //     var follow = self.Follow;
        //     HairInfo hairInfo = null;
        //     var duckingOffset = Vector2.Zero;
        //     var withHatOffset = Vector2.Zero;
        //     var facing = Facing.Right;
        //      
        //     if (follow is PlayerCorpse corpse)
        //     {
        //         var exist = hairs.TryGetValue(corpse.PlayerIndex, out var hair);
        //         if(exist)
        //             hairInfo = hairs[corpse.PlayerIndex].HairInfo;
        //     }
        //     if (follow is Player player)
        //     {
        //         var exist = Mod.ArcherCustomDataDict.TryGetValue(player.ArcherData, out var archerCustomData);
        //         if (exist)
        //         {
        //             facing = player.Facing;
        //             hairs[player.PlayerIndex] = archerCustomData;
        //             hairInfo = archerCustomData.HairInfo;
        //             if (hairInfo != null && player.State == Player.PlayerStates.Ducking)
        //             {
        //                 duckingOffset = hairInfo.DuckingOffset;
        //             }
        //             if(hairInfo != null && player.HatState == Player.HatStates.Normal){
        //                 withHatOffset = hairInfo.WithHatOffset;
        //             }
        //         }
        //     }
        //
        //     if (hairInfo == null)
        //     {
        //         return true;
        //     }
        //        
        //     var links = (int) linksField.GetValue(self);
        //     var images = (Subtexture[]) imagesField.GetValue(self);
        //     var offsets = (Vector2[]) offsetsField.GetValue(self);
        //
        //     var actionsOffsets =  duckingOffset.X + hairInfo.Position.X + withHatOffset.X;
        //     var positionEntity = new Vector2(
        //         self.Position.X + (facing == Facing.Right ? actionsOffsets : actionsOffsets * -1),
        //         self.Position.Y + duckingOffset.Y + hairInfo.Position.Y + withHatOffset.Y);
        //
        //     var scale = (float) scaleField.GetValue(self);
        //
        //     for (var index = 0; index < links; ++index)
        //     {
        //         var color = hairInfo.Color;
        //         if (hairInfo.Prismatic || hairInfo.Rainbow)
        //         {
        //             color = GetPrismaticColor(Environment.TickCount, 0, hairInfo.PrismaticTime);
        //         }else
        //         if (hairInfo.EndColor.A != 0 && !hairInfo.PrismaticEnd)
        //         {
        //             color = index == links - 1 ? hairInfo.EndColor : hairInfo.Color;
        //             if (hairInfo.Gradient)
        //             {
        //                 if (index >= hairInfo.GradientOffset)
        //                 {
        //                     var amount = (float)(index - hairInfo.GradientOffset) / links;
        //                     color = Color.Lerp(hairInfo.Color, hairInfo.EndColor, amount);
        //                 }
        //             }
        //         }
        //         
        //         if (hairInfo.PrismaticEnd)
        //         {
        //             var prismatic = GetPrismaticColor(Environment.TickCount, 0, hairInfo.PrismaticTime);
        //             color = index == links - 1 ? prismatic : hairInfo.Color;
        //             if (hairInfo.Gradient)
        //             {
        //                 if (index > hairInfo.GradientOffset)
        //                 {
        //                     var amount = (float)index / links;
        //                     color = hairInfo.Rainbow
        //                         ? GetPrismaticColor(Environment.TickCount, index, hairInfo.PrismaticTime)
        //                         : Color.Lerp(hairInfo.Color, prismatic, amount);
        //                 }
        //             }
        //         }
        //         
        //         var position = follow.Position + positionEntity + offsets[index];
        //         var rotation = index == 0 ? 0.0f : Calc.Angle(offsets[index], offsets[index - 1]);
        //         Draw.TextureCentered(images[index], position, color * self.Alpha * self.Alpha,
        //             scale, rotation);
        //     }
        //
        //     return false;
        // }


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