using System;
using System.Collections.Generic;
using System.Reflection;
using ArcherLoaderMod.Rainbow;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Monocle;
using TowerFall;

namespace ArcherLoaderMod.Hair
{
    public class HairPatcher
    {
        public static bool enabled = false;
        public static Dictionary<int, ArcherCustomData> Hairs = new();
        private static Harmony harmony;

        // Cached reflection fields
        private static readonly FieldInfo _scaleField = typeof(PlayerHair).GetField("scale", BindingFlags.NonPublic | BindingFlags.Instance);
        public static readonly FieldInfo LinksField = typeof(PlayerHair).GetField("links", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo _linkDistField = typeof(PlayerHair).GetField("linkDist", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo _offsetsField = typeof(PlayerHair).GetField("offsets", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo _imagesField = typeof(PlayerHair).GetField("images", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo _sineField = typeof(PlayerHair).GetField("sine", BindingFlags.NonPublic | BindingFlags.Instance);

        public static void Load()
        {
            harmony = new Harmony("mod.archerloader.hair");
            
            // Patch methods
            harmony.Patch(
                typeof(Player).GetMethod("Added"),
                postfix: new HarmonyMethod(typeof(HairPatcher), nameof(Player_Added_Postfix))
            );
            
            harmony.Patch(
                typeof(PlayerHair).GetConstructor(new[] { typeof(Entity), typeof(Vector2), typeof(float) }),
                postfix: new HarmonyMethod(typeof(HairPatcher), nameof(PlayerHair_ctor_Postfix))
            );
            
            harmony.Patch(
                typeof(PlayerHair).GetMethod("Render"),
                prefix: new HarmonyMethod(typeof(HairPatcher), nameof(PlayerHair_Render_Prefix))
            );
            
            harmony.Patch(
                typeof(PlayerHair).GetMethod("RenderOutline"),
                prefix: new HarmonyMethod(typeof(HairPatcher), nameof(PlayerHair_RenderOutline_Prefix))
            );
            
            enabled = true;
        }

        public static void Unload()
        {
            if (!enabled) return;
            harmony?.UnpatchAll();
        }

        [HarmonyPostfix]
        private static void Player_Added_Postfix(Player __instance)
        {
            if (__instance.Hair == null) return;
            
            if (!ArcherLoaderMod.ArcherCustomDataDict.TryGetValue(__instance.ArcherData, out var archerCustomData)) 
                return;
                
            __instance.Hair.Visible = archerCustomData.HairInfo?.VisibleWithHat ?? true;
        }

        [HarmonyPostfix]
        private static void PlayerHair_ctor_Postfix(PlayerHair __instance, Entity follow, Vector2 position, float scale)
        {
            HairInfo hairInfo = null;
            
            if (follow is PlayerCorpse corpse && Hairs.TryGetValue(corpse.PlayerIndex, out var corpseData))
            {
                hairInfo = corpseData.HairInfo;
            }
            else if (follow is Player player && 
                     ArcherLoaderMod.ArcherCustomDataDict.TryGetValue(player.ArcherData, out var playerData))
            {
                Hairs[player.PlayerIndex] = playerData;
                hairInfo = playerData.HairInfo;
            }

            if (hairInfo == null) return;
            
            __instance.Visible = true;
            ApplyHairCustomization(__instance, hairInfo);
        }

        private static void ApplyHairCustomization(PlayerHair hair, HairInfo hairInfo)
        {
            var links = hairInfo.Links;
            LinksField.SetValue(hair, links);
            _linkDistField.SetValue(hair, hairInfo.LinksDist);

            // Create offsets
            var offsets = new Vector2[links];
            for (var i = 0; i < links; i++)
            {
                offsets[i] = new Vector2(0f, hairInfo.Size * i);
            }
            _offsetsField.SetValue(hair, offsets);

            // Create images
            var images = new Subtexture[links];
            for (var i = 0; i < links - 1; i++)
            {
                images[i] = TFGame.Atlas[hairInfo.HairSprite];
            }
            images[links - 1] = TFGame.Atlas[hairInfo.HairEndSprite];
            _imagesField.SetValue(hair, images);

            // Set sine wave
            _sineField.SetValue(hair, new SineWave(hairInfo.SineValue));
        }

        [HarmonyPrefix]
        private static bool PlayerHair_Render_Prefix(PlayerHair __instance)
        {
            return HandleHairRendering(__instance, isOutline: false);
        }

        [HarmonyPrefix]
        private static bool PlayerHair_RenderOutline_Prefix(PlayerHair __instance)
        {
            return HandleHairRendering(__instance, isOutline: true);
        }

        private static bool HandleHairRendering(PlayerHair self, bool isOutline)
        {
            var follow = self.Follow;
            if (follow == null) return true;
            
            HairInfo hairInfo = null;
            Vector2 duckingOffset = Vector2.Zero;
            Vector2 withHatOffset = Vector2.Zero;
            var facing = Facing.Right;

            if (follow is PlayerCorpse corpse && Hairs.TryGetValue(corpse.PlayerIndex, out var corpseData))
            {
                hairInfo = corpseData.HairInfo;
            }
            else if (follow is Player player)
            {
                if (!Hairs.TryGetValue(player.PlayerIndex, out var playerData)) 
                    return true;
                    
                hairInfo = playerData.HairInfo;
                if (hairInfo == null) return true;
                
                facing = player.Facing;
                
                if (player.State == Player.PlayerStates.Ducking)
                    duckingOffset = hairInfo.DuckingOffset;
                
                if (player.HatState == Player.HatStates.Normal)
                    withHatOffset = hairInfo.WithHatOffset;
            }

            if (hairInfo == null) return true;

            var links = (int)LinksField.GetValue(self);
            var images = (Subtexture[])_imagesField.GetValue(self);
            var offsets = (Vector2[])_offsetsField.GetValue(self);
            var scale = (float)_scaleField.GetValue(self);

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

            return false; // Skip original rendering
        }

        private static Color GetHairColor(int index, int links, HairInfo hairInfo)
        {
            // Handle prismatic/rainbow effects
            if (hairInfo.Prismatic || hairInfo.Rainbow)
                return RainbowManager.CurrentColor;

            // Handle end color without prismatic
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

            // Handle prismatic end
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
                    {
                        Draw.TextureCentered(texture, position + new Vector2(x, y), color, scale, rotation);
                    }
                }
            }
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