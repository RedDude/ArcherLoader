#nullable enable
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Xml;
using ArcherLoaderMod.Taunt;
using FortRise;
using HarmonyLib;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework.Input;
using Monocle;
using MonoMod.Utils;
using TowerFall;

namespace ArcherLoaderMod.Source.Features.Taunt
{
    // <Taunt>
    //   <Id>myArcherTaunt</Id>                  required: a spriteData id the mod registers itself, with
    //                                            "taunt"/"tauntNoHat"/"tauntCrown" animations on it
    //   <Texture>...</Texture>                  at least one of these three is required. Resolved the same
    //                                            way Wings/Ghost textures are: "{mod}/{name}" first, then
    //                                            the raw name, so a plain atlas region name is enough.
    //   <TextureBlue>...</TextureBlue>
    //   <TextureRed>...</TextureRed>
    //   <NoHatTexture>...</NoHatTexture> / NoHatTextureBlue / NoHatTextureRed
    //   <CrownTexture>...</CrownTexture> / CrownTextureBlue / CrownTextureRed
    //   <SFX>...</SFX> or <SFXLooped>...</SFXLooped> or <SFXVaried>...</SFXVaried>   optional, path
    //                                            relative to the mod's own folder
    //   <SelfDestruction>false</SelfDestruction> optional, explode (without killing others) when the
    //                                            taunt animation completes
    // </Taunt>
    //
    // Pressing Duck + right stick down (or V) while ducking plays the archer's taunt when it has one.
    public sealed class TauntFeature : IArcherFeature
    {
        private readonly IModContent content;
        private static readonly Dictionary<ArcherData, TauntInfo> tauntByArcher = new();
        private static readonly Dictionary<Player, TauntState> tauntStates = new();

        private static MethodInfo loseHatMethod = null!;
        private static PropertyInfo drawSelfProperty = null!;
        private static IVariantEntry? variant;
        private static ILogger? logger;

        public TauntFeature(IModContent content)
        {
            this.content = content;
        }

        public string Name => "Taunt";

        public bool Enabled => true;

        public void Load(IModuleContext context)
        {
            logger = context.Logger;
            loseHatMethod = AccessTools.Method(typeof(Player), "LoseHat");
            drawSelfProperty = AccessTools.Property(typeof(Player), "DrawSelf");

            var icon = context.Registry.Subtextures.RegisterTexture("Taunt", content.Root.GetRelativePath("Content/Atlas/taunt.png"));
            variant = context.Registry.Variants.RegisterVariant("Taunt", new()
            {
                Title = "Taunt",
                Description = "PROVOKE YOUR FOES (DUCK + RIGHT STICK DOWN or V key)",
                Icon = icon,
                Flags = CustomVariantFlags.PerPlayer
            });

            Cache.Init<SelfExplosion>();

            context.Harmony.Patch(
                AccessTools.Method(typeof(Player), nameof(Player.Update)),
                postfix: new HarmonyMethod(typeof(TauntFeature), nameof(Player_Update_Postfix)));

            context.Harmony.Patch(
                AccessTools.Method(typeof(Player), "UpdateAnimation"),
                prefix: new HarmonyMethod(typeof(TauntFeature), nameof(Player_UpdateAnimation_Prefix)));

            context.Harmony.Patch(
                AccessTools.Method(typeof(Player), "LeaveDucking"),
                prefix: new HarmonyMethod(typeof(TauntFeature), nameof(Player_LeaveDucking_Prefix)));

            context.Harmony.Patch(
                AccessTools.Method(typeof(Player), nameof(Player.DoWrapRender)),
                prefix: new HarmonyMethod(typeof(TauntFeature), nameof(Player_DoWrapRender_Prefix)));

            context.Harmony.Patch(
                AccessTools.Method(typeof(ArrowHUD), nameof(ArrowHUD.Render)),
                prefix: new HarmonyMethod(typeof(TauntFeature), nameof(ArrowHUD_Render_Prefix)));
        }

        public bool Decorate(ArcherDecoration decoration)
        {
            var element = decoration.Xml["Taunt"];
            if (element == null)
                return false;

            var id = element.ChildText("Id", "");
            if (string.IsNullOrEmpty(id))
                throw new System.Exception("<Taunt> is missing the required <Id> element");

            // sprite_string entries are registered as "{mod}/{id}" (see ModSprites.RegisterSprite), so a
            // mod referencing its own spriteData entry has to be resolved the same way Wings/Ghost/Texture
            // are: mod-prefixed name first, then the raw name for vanilla-provided sprite strings.
            var resolvedId = ResolveSpriteStringId(decoration, id);
            if (resolvedId == null)
                throw new System.Exception($"<Taunt><Id> '{id}' is not a registered spriteData entry");

            var spriteEntry = TFGame.SpriteData.GetSpriteString(resolvedId);

            var info = new TauntInfo
            {
                SpriteId = resolvedId,
                SelfDestruction = element.ChildBool("SelfDestruction", false),
            };

            info.TauntTexture = FindTexture(decoration, element, "Texture");
            info.NoHatTexture = FindTexture(decoration, element, "NoHatTexture");
            info.CrownTexture = FindTexture(decoration, element, "CrownTexture");

            info.TauntTextureBlue = FindTexture(decoration, element, "TextureBlue");
            info.NoHatTextureBlue = FindTexture(decoration, element, "NoHatTextureBlue");
            info.CrownTextureBlue = FindTexture(decoration, element, "CrownTextureBlue");

            info.TauntTextureRed = FindTexture(decoration, element, "TextureRed");
            info.NoHatTextureRed = FindTexture(decoration, element, "NoHatTextureRed");
            info.CrownTextureRed = FindTexture(decoration, element, "CrownTextureRed");

            info.HasTaunt = spriteEntry.ContainsAnimation("taunt") && info.TauntTexture != null;
            info.HasTauntNoHat = spriteEntry.ContainsAnimation("tauntNoHat") && info.NoHatTexture != null;
            info.HasTauntCrown = spriteEntry.ContainsAnimation("tauntCrown") && info.CrownTexture != null;

            info.HasTauntBlue = spriteEntry.ContainsAnimation("taunt") && info.TauntTextureBlue != null;
            info.HasTauntNoHatBlue = spriteEntry.ContainsAnimation("tauntNoHat") && info.NoHatTextureBlue != null;
            info.HasTauntCrownBlue = spriteEntry.ContainsAnimation("tauntCrown") && info.CrownTextureBlue != null;

            info.HasTauntRed = spriteEntry.ContainsAnimation("taunt") && info.TauntTextureRed != null;
            info.HasTauntNoHatRed = spriteEntry.ContainsAnimation("tauntNoHat") && info.NoHatTextureRed != null;
            info.HasTauntCrownRed = spriteEntry.ContainsAnimation("tauntCrown") && info.CrownTextureRed != null;

            if (!info.HasTaunt && !info.HasTauntNoHat && !info.HasTauntCrown &&
                !info.HasTauntBlue && !info.HasTauntNoHatBlue && !info.HasTauntCrownBlue &&
                !info.HasTauntRed && !info.HasTauntNoHatRed && !info.HasTauntCrownRed)
                throw new System.Exception("<Taunt> needs at least one Texture/NoHatTexture/CrownTexture (with a matching animation on the Id sprite)");

            info.Sound = LoadSound(decoration, element);

            tauntByArcher[decoration.ArcherData] = info;
            return true;
        }

        private static Subtexture? FindTexture(ArcherDecoration decoration, XmlElement element, string childName)
        {
            var name = element.ChildText(childName, "");
            return string.IsNullOrWhiteSpace(name) ? null : decoration.FindTexture(name);
        }

        private static string? ResolveSpriteStringId(ArcherDecoration decoration, string id)
        {
            var sprites = TFGame.SpriteData.GetSprites();
            var prefixed = $"{decoration.ModContent.Metadata.Name}/{id}";
            if (sprites.ContainsKey(prefixed))
                return prefixed;
            return sprites.ContainsKey(id) ? id : null;
        }

        private static SFX? LoadSound(ArcherDecoration decoration, XmlElement element)
        {
            var modName = decoration.ModContent.Metadata.Name;
            var folder = decoration.ModContent.Metadata.PathDirectory;

            var sfx = element.ChildText("SFX", null);
            var sfxLooped = element.ChildText("SFXLooped", null);
            var sfxVaried = element.ChildText("SFXVaried", null);
            if (sfx == null && sfxLooped == null && sfxVaried == null)
                return null; // no sound requested, silent by design

            if (string.IsNullOrEmpty(folder))
            {
                logger?.LogWarning("[{modName}] Taunt sound requested but the mod is zipped; loose .wav files can't be probed.", modName);
                return null;
            }

            var originalPrefix = Audio.LOAD_PREFIX;
            Audio.LOAD_PREFIX = folder + Path.DirectorySeparatorChar;
            try
            {
                if (sfx != null)
                {
                    if (Exists(sfx))
                    {
                        var looped = LoadLooped(sfx);
                        if (looped != null)
                        {
                            looped.Instance.IsLooped = false;
                            return looped;
                        }
                    }
                    else
                    {
                        logger?.LogWarning("[{modName}] Taunt <SFX>{sfx}</SFX> not found at '{path}'", modName, sfx, $"{Audio.LOAD_PREFIX}{sfx}.wav");
                    }
                }

                if (sfxLooped != null)
                {
                    if (Exists(sfxLooped))
                        return LoadLooped(sfxLooped);
                    logger?.LogWarning("[{modName}] Taunt <SFXLooped>{sfxLooped}</SFXLooped> not found at '{path}'", modName, sfxLooped, $"{Audio.LOAD_PREFIX}{sfxLooped}.wav");
                }

                if (sfxVaried != null)
                {
                    if (Exists(sfxVaried))
                        return LoadVaried(sfxVaried);
                    logger?.LogWarning("[{modName}] Taunt <SFXVaried>{sfxVaried}</SFXVaried> not found at '{path}'", modName, sfxVaried, $"{Audio.LOAD_PREFIX}{sfxVaried}_01.wav");
                }

                return null;
            }
            finally
            {
                Audio.LOAD_PREFIX = originalPrefix;
            }
        }

        private static void Player_Update_Postfix(Player __instance)
        {
            var matchVariants = __instance.Level.Session.MatchSettings.Variants;
            var variantEnabled = variant != null && matchVariants.GetCustomVariant(variant.Name)?[__instance.PlayerIndex] == true;

            if (!variantEnabled || __instance.State == Player.PlayerStates.Frozen)
                return;

            var input = DynamicData.For(__instance).Get<InputState>("input");
            var playerInput = TFGame.PlayerInputs[__instance.PlayerIndex];

            SelfKill(__instance, playerInput, input);
            LoseHat(__instance, playerInput, input);

            var tauntButton = playerInput switch
            {
                XGamepadInput xGamepadInput => xGamepadInput.XGamepad.RightStickDownPressed(0),
                KeyboardInput => MInput.Keyboard.Check(Keys.V),
                _ => input.ArrowsPressed
            };

            if (!tauntButton || __instance.State != Player.PlayerStates.Ducking)
                return;

            HandleTaunt(__instance);
        }

        private static void SelfKill(Player self, PlayerInput playerInput, InputState input)
        {
            if (!FortEntrance.Instance.Settings.SelfKill)
                return;

            var killButton = playerInput switch
            {
                XGamepadInput xGamepadInput => xGamepadInput.XGamepad.RightStickRightReleased(0) && playerInput.GetState().ShootCheck,
                KeyboardInput => MInput.Keyboard.Check(Keys.K),
                _ => input.ArrowsPressed
            };

            if (killButton && self.State != Player.PlayerStates.Dying)
                self.Die(DeathCause.Curse, self.PlayerIndex);
        }

        private static void LoseHat(Player self, PlayerInput playerInput, InputState input)
        {
            if (!FortEntrance.Instance.Settings.DropHat)
                return;

            var dropButton = playerInput switch
            {
                XGamepadInput xGamepadInput => xGamepadInput.XGamepad.RightStickLeftReleased(0),
                KeyboardInput => MInput.Keyboard.Check(Keys.L),
                _ => input.ArrowsPressed
            };

            if (dropButton && self.HatState != Player.HatStates.NoHat && self.State == Player.PlayerStates.Normal)
                loseHatMethod.Invoke(self, [null, true]);
        }

        private static void HandleTaunt(Player self)
        {
            if (tauntStates.TryGetValue(self, out var existing))
            {
                if (tauntByArcher.TryGetValue(self.ArcherData, out var info))
                    UpdateAnimationChoice(self, existing, info);
                return;
            }

            if (!tauntByArcher.TryGetValue(self.ArcherData, out var tauntInfo))
                return;

            var sprite = TFGame.SpriteData.GetSpriteString(tauntInfo.SpriteId);
            var state = new TauntState { BodySprite = DynamicData.For(self).Get<Sprite<string>>("bodySprite"), Sprite = sprite };
            tauntStates[self] = state;

            self.Add(sprite);
            UpdateAnimationChoice(self, state, tauntInfo);

            if (tauntInfo.SelfDestruction)
            {
                sprite.OnAnimationComplete += _ =>
                {
                    SelfExplosion.Spawn(self.Level, self.Position, self.PlayerIndex, plusOneKill: false, selfProtection: true);
                    tauntInfo.Sound?.Stop();
                    TFGame.PlayerInputs[self.PlayerIndex].Rumble(1f, 30);
                };
            }

            if (!self.Dead)
                (tauntInfo.Sound ?? self.ArcherData.SFX.Ready)?.Play();
        }

        private static void UpdateAnimationChoice(Player self, TauntState state, TauntInfo info)
        {
            var (hasNormal, hasNoHat, hasCrown, normal, noHat, crown) = self.TeamColor switch
            {
                Allegiance.Blue => (info.HasTauntBlue, info.HasTauntNoHatBlue, info.HasTauntCrownBlue, info.TauntTextureBlue, info.NoHatTextureBlue, info.CrownTextureBlue),
                Allegiance.Red => (info.HasTauntRed, info.HasTauntNoHatRed, info.HasTauntCrownRed, info.TauntTextureRed, info.NoHatTextureRed, info.CrownTextureRed),
                _ => (info.HasTaunt, info.HasTauntNoHat, info.HasTauntCrown, info.TauntTexture, info.NoHatTexture, info.CrownTexture)
            };

            var (animation, texture) = self.HatState switch
            {
                Player.HatStates.Normal when hasNormal => ("taunt", normal),
                Player.HatStates.NoHat when hasNoHat => ("tauntNoHat", noHat),
                Player.HatStates.Crown when hasCrown => ("tauntCrown", crown),
                _ => (null, null)
            };

            state.Animation = animation;
            state.Texture = texture;
        }

        private static bool Player_UpdateAnimation_Prefix(Player __instance)
        {
            if (!tauntStates.TryGetValue(__instance, out var state))
                return true;

            PlayTauntAnimation(__instance, state);
            return false;
        }

        private static void PlayTauntAnimation(Player self, TauntState state)
        {
            if (state.Animation == null || state.Sprite == null)
            {
                state.BodySprite.Visible = true;
                if (state.Sprite != null)
                    state.Sprite.Visible = false;
                drawSelfProperty.SetValue(self, true);
                state.BodySprite.Play("run");
                return;
            }

            drawSelfProperty.SetValue(self, false);
            state.Sprite.FlipX = self.Facing != Facing.Right;
            state.Sprite.SwapSubtexture(state.Texture);
            state.BodySprite.Visible = false;
            state.Sprite.Visible = true;
            state.Sprite.Play(state.Animation);
        }

        private static bool Player_LeaveDucking_Prefix(Player __instance)
        {
            if (!tauntStates.TryGetValue(__instance, out var state))
                return true;

            if (state.Sprite != null)
            {
                state.Sprite.Visible = false;
                state.Sprite.Stop();
                __instance.Remove(state.Sprite);
            }

            drawSelfProperty.SetValue(__instance, true);
            state.BodySprite.Visible = true;

            if (tauntByArcher.TryGetValue(__instance.ArcherData, out var info))
                info.Sound?.Stop();

            tauntStates.Remove(__instance);
            return true;
        }

        private static void Player_DoWrapRender_Prefix(Player __instance)
        {
            if (tauntStates.TryGetValue(__instance, out var state) && state.Animation != null)
                state.Sprite?.DrawOutline();
        }

        private static bool ArrowHUD_Render_Prefix(ArrowHUD __instance)
        {
            if (!FortEntrance.Instance.Settings.HideArrowsWhileTaunt)
                return true;

            var player = DynamicData.For(__instance).Get<Player>("player");
            return !tauntStates.ContainsKey(player);
        }

        private static SFXLooped? LoadLooped(string name) => Exists(name) ? new SFXLooped(name) : null;

        private static SFXVaried? LoadVaried(string name)
        {
            var count = 0;
            while (Exists(name + VariedSuffix(count)))
                count++;
            return count > 0 ? new SFXVaried(name, count) : null;
        }

        private static bool Exists(string name) => File.Exists(Audio.LOAD_PREFIX + name + ".wav");

        private static string VariedSuffix(int num)
        {
            num++;
            return num < 10 ? "_0" + num : "_" + num;
        }
    }
}
