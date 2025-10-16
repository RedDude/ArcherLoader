using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Xml;
using FortRise;
using HarmonyLib;
using Microsoft.Xna.Framework.Input;
using Monocle;
using MonoMod.Utils;
using TowerFall;
using ArrowHUD = TowerFall.ArrowHUD;

namespace ArcherLoaderMod.Taunt
{
    public class TauntVariant
    {
        public static Atlas MyAtlas;
        private static Dictionary<Player, TauntState> tauntStates = new();
        private static Variant variantInfo;
        private static Sprite<string> originalSprite;
        private static PropertyInfo drawSelfPropertyInfo;
        private static Dictionary<ArcherData, TauntInfo> tauntInfos = new();
        public static MethodInfo _loseHat;
        public static bool enabled = false;
        private static ISubtextureEntry TauntImage;
        private static Harmony harmony;
        private static IVariantEntry info;


        public static ArcherLoaderSettings settings { get; set; }
        
        public static void OnVariantsRegister(IModuleContext context)
        {
            info = context.Registry.Variants.RegisterVariant("TauntVariantBatata", new()
            {
                Title = "Taunt",
                Description = "PROVOKE YOUR FOES (DUCK + RIGHT STICK DOWN or V key)",
                Icon = TauntImage,
                Flags = CustomVariantFlags.PerPlayer
            });

            settings = FortEntrance.Instance.Settings;
        }


        public static void LoadContent(IModContent content, IModuleContext context)
        {
            drawSelfPropertyInfo = typeof(Player).GetProperty("DrawSelf", BindingFlags.Public | BindingFlags.Instance);
            TauntImage = context.Registry.Subtextures.RegisterTexture("Taunt", content.Root.GetRelativePath("taunt.png"));
        }

        public static void Load()
        {
            enabled = true;
            _loseHat = typeof(Player).GetMethod("LoseHat", BindingFlags.NonPublic | BindingFlags.Instance);
            harmony = new Harmony("mod.archerloader.taunt");
            
            // Patch methods
            harmony.Patch(
                typeof(Player).GetMethod("Update"),
                postfix: new HarmonyMethod(typeof(TauntVariant), nameof(Player_Update_Postfix))
            );
            
            harmony.Patch(
                typeof(Player).GetMethod("DoWrapRender"),
                prefix: new HarmonyMethod(typeof(TauntVariant), nameof(Player_DoWrapRender_Prefix))
            );
            
            harmony.Patch(
                typeof(Player).GetMethod("LeaveDucking", BindingFlags.NonPublic | BindingFlags.Instance),
                prefix: new HarmonyMethod(typeof(TauntVariant), nameof(Player_LeaveDucking_Prefix))
            );
            
            harmony.Patch(
                typeof(Player).GetMethod("UpdateHead", BindingFlags.NonPublic | BindingFlags.Instance),
                postfix: new HarmonyMethod(typeof(TauntVariant), nameof(Player_UpdateHead_Postfix))
            );
            
            harmony.Patch(
                typeof(Player).GetMethod("UpdateAnimation", BindingFlags.NonPublic | BindingFlags.Instance),
                prefix: new HarmonyMethod(typeof(TauntVariant), nameof(Player_UpdateAnimation_Prefix))
            );
            
            harmony.Patch(
                typeof(ArrowHUD).GetMethod("Render"),
                prefix: new HarmonyMethod(typeof(TauntVariant), nameof(ArrowHUD_Render_Prefix))
            );

            Cache.Init<SelfExplosion>();
        }

 [HarmonyPrefix]
        private static void Player_DoWrapRender_Prefix(Player __instance)
        {
            if (tauntStates.ContainsKey(__instance))
            {
                var tauntState = tauntStates[__instance];
                var tauntCharacter = tauntState?.sprite;
                if (tauntState != null && tauntCharacter != null && tauntState.animation != null)
                    tauntCharacter.DrawOutline();
            }
        }

        [HarmonyPrefix]
        private static bool ArrowHUD_Render_Prefix(ArrowHUD __instance)
        {
            if (!settings.TauntAlwaysOn && variantInfo == null)
                return true;
            
            if (!settings.HideArrowsWhileTaunt)
                return true;

            var player = DynamicData.For(__instance).Get<Player>("player");
            return !tauntStates.ContainsKey(player);
        }
        
        private static void Explode(Player self, Tween t = null)
        {
            Sounds.pu_bombArrowExplode.Play(self.X);
            if (settings.TauntTooExplode)
                Explosion.Spawn(self.Level, self.Position, self.PlayerIndex, plusOneKill: false, false, bombTrap: false);
            else
                SelfExplosion.Spawn(self.Level, self.Position, self.PlayerIndex, plusOneKill: false, !tauntInfos[self.ArcherData].SelfDestruction);
            
            TFGame.PlayerInputs[self.PlayerIndex].Rumble(1f, 30);
        }

        [HarmonyPostfix]
        private static void Player_Update_Postfix(Player __instance)
        {
            var matchVariants = __instance.Level.Session.MatchSettings.Variants;
            var variantEnabled = settings.TauntAlwaysOn;
                        
            if (!variantEnabled)
            {
                variantInfo = matchVariants.GetCustomVariant("ArcherLoader/Taunt");
                variantEnabled = variantInfo?[__instance.PlayerIndex] ?? false;
            }

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

        private static void HandleTaunt(Player self)
        {
            if (tauntStates.TryGetValue(self, out var tauntCharacter))
            {
                tauntInfos.TryGetValue(self.ArcherData, out var currentTauntInfo);
                if (currentTauntInfo != null && (currentTauntInfo.hasTauntNoHat || currentTauntInfo.hasTauntCrown || currentTauntInfo.hasTaunt))
                    CheckTauntAnimation(self, tauntCharacter.sprite, currentTauntInfo);
                return;
            }

            var tauntInfo = InitTauntInfo(self);
            if (tauntInfo.id != null)
            {
                self.Add(tauntInfo.spriteData);
                tauntStates[self].sprite = tauntInfo.spriteData;
                CheckTauntAnimation(self, tauntStates[self].sprite, tauntInfo);
                
                if (settings.TauntTooExplode || tauntInfo.SelfDestruction)
                {
                    tauntInfo.spriteData.OnAnimationComplete += sprite =>
                    {
                        Explode(self);
                        tauntInfo.Sound?.Stop();
                        Music.Stop();
                    };
                }
            }
            
            if (!self.Dead)
                tauntInfo.Sound?.Play();
        }

       
        private static void LoseHat(Player self, PlayerInput playerInput, InputState input)
        {
            var dropHatButton = playerInput switch
            {
                XGamepadInput xGamepadInput => xGamepadInput.XGamepad.RightStickLeftReleased(0),
                // NewGamepadInput newGamepadInput => 
                KeyboardInput => MInput.Keyboard.Check((Keys) Keys.L),
                _ => input.ArrowsPressed
            };
            if (!dropHatButton || !settings.DropHat) return;
            if (self.HatState != Player.HatStates.NoHat && self.State == Player.PlayerStates.Normal)
            {
                _loseHat.Invoke(self, new object[] {null, true});
            }
        }

        private static void SelfKill(Player self, PlayerInput playerInput, InputState input)
        {
            var killHatButton = playerInput switch
            {
                XGamepadInput xGamepadInput => xGamepadInput.XGamepad.RightStickRightReleased(0) &&
                                               playerInput.GetState().ShootCheck,
                // NewGamepadInput newGamepadInput => 
                KeyboardInput => MInput.Keyboard.Check((Keys) Keys.K),
                _ => input.ArrowsPressed
            };

            if (!killHatButton || !settings.SelfKill) return;
            if (self.State != Player.PlayerStates.Dying)
            {
                self.Die(DeathCause.Curse, self.PlayerIndex);
            }
        }

        private static TauntInfo InitTauntInfo(Player self)
        {
            tauntInfos.TryGetValue(self.ArcherData, out var tauntInfo);
            if (tauntInfo == null)
            {
                var customExist = ArcherLoaderMod.ArcherCustomDataDict.TryGetValue(self.ArcherData, out var archerCustomData);
                if (customExist && archerCustomData.Taunt != null)
                {
                    var xmlElement = TFGame.SpriteData.GetXML(archerCustomData.Taunt);
                    tauntInfo = new TauntInfo
                    {
                        id = archerCustomData.Taunt,
                        hasTauntCrown = false,
                        spriteData = archerCustomData.TauntSpriteData ??
                                     TFGame.SpriteData.GetSpriteString(archerCustomData.Taunt),
                        SelfDestruction = xmlElement.ChildBool("SelfDestruction", false)
                    };

                    archerCustomData.TauntSpriteData = tauntInfo.spriteData;

                    var originalPath = HandleSFX(self, archerCustomData.FolderPath, xmlElement, out var sound);

                    tauntInfo.Sound = sound;

                    HandleTexturesAndAnimations(tauntInfo, xmlElement);

                    Audio.LOAD_PREFIX = originalPath;
                    tauntInfos[self.ArcherData] = tauntInfo;
                }
                else
                {
                    if (ArcherLoaderMod.customSpriteDataCategoryDict.ContainsKey("taunt"))
                    {
                        foreach (var customSpriteData in ArcherLoaderMod.customSpriteDataCategoryDict["taunt"])
                        {
                            var xmlElement = customSpriteData.Element;

                            var forAttribute = ArcherLoaderMod.GetForAttribute(xmlElement);
                            if (string.IsNullOrEmpty(forAttribute)) continue;
                            ArcherLoaderMod.BaseArcherByNameDict.TryGetValue(xmlElement.GetAttribute(forAttribute).ToLower(),
                                out var searchArcherData);
                            if (searchArcherData == null)
                            {
                                foreach (var customData in ArcherLoaderMod.ArcherCustomDataDict)
                                {
                                    if (customData.Value.ID == xmlElement.GetAttribute(forAttribute))
                                    {
                                        searchArcherData = customData.Key;
                                    }
                                }
                            }

                            if (self.ArcherData != searchArcherData)
                            {
                                continue;
                            }

                            var spritedata = TFGame.SpriteData.GetSpriteString(customSpriteData.id);
                            tauntInfo = new TauntInfo
                            {
                                id = customSpriteData.id,
                                hasTauntCrown = false,
                                spriteData = spritedata,
                                SelfDestruction = xmlElement.ChildBool("SelfDestruction", false)
                            };

                            var originalPath = HandleSFX(self, customSpriteData, xmlElement, out var sound);

                            tauntInfo.Sound = sound;
                            HandleTexturesAndAnimations(tauntInfo, xmlElement);

                            Audio.LOAD_PREFIX = originalPath;
                            tauntInfos[self.ArcherData] = tauntInfo;

                            break;
                        }

                        foreach (var customSpriteData in ArcherLoaderMod.customSpriteDataCategoryDict["taunt"])
                        {
                            var xmlElement = customSpriteData.Element;

                            var forAttribute = ArcherLoaderMod.GetForAttribute(xmlElement);
                            if (string.IsNullOrEmpty(forAttribute)) continue;
                            ArcherLoaderMod.BaseArcherByNameDict.TryGetValue(xmlElement.GetAttribute(forAttribute).ToLower(),
                                out var searchArcherData);
                            if (searchArcherData == null)
                            {
                                foreach (var customData in ArcherLoaderMod.ArcherCustomDataDict)
                                {
                                    if (customData.Value.ID == xmlElement.GetAttribute(forAttribute))
                                    {
                                        searchArcherData = customData.Key;
                                    }

                                    ;
                                }
                            }

                            if (self.ArcherData != searchArcherData)
                            {
                                continue;
                            }

                            var spritedata = TFGame.SpriteData.GetSpriteString(customSpriteData.id);
                            tauntInfo = new TauntInfo
                            {
                                id = customSpriteData.id,
                                hasTauntCrown = false,
                                spriteData = spritedata,
                                SelfDestruction = xmlElement.ChildBool("SelfDestruction", false)
                            };

                            var originalPath = HandleSFX(self, customSpriteData, xmlElement, out var sound);

                            tauntInfo.Sound = sound;
                            HandleTexturesAndAnimations(tauntInfo, xmlElement);

                            Audio.LOAD_PREFIX = originalPath;
                            tauntInfos[self.ArcherData] = tauntInfo;

                            break;
                        }
                    }
                }
            }

            tauntInfo ??= new TauntInfo
            {
                Sound = self.ArcherData.SFX.Ready,
            };

            if (!tauntStates.ContainsKey(self))
            {
                tauntStates[self] = new TauntState()
                {
                    bodySprite = DynamicData.For(self).Get<Sprite<string>>("bodySprite"),
                    headSprite = DynamicData.For(self).Get<Sprite<string>>("headSprite"),
                    bowSprite = DynamicData.For(self).Get<Sprite<string>>("bowSprite"),
                    arrowHud = self.ArrowHUD
                };
            }

            return tauntInfo;
        }

        private static void HandleTexturesAndAnimations(TauntInfo tauntInfo, XmlElement xmlElement)
        {
            HandleNormal(tauntInfo, xmlElement);
            HandleRed(tauntInfo, xmlElement);
            HandleBlue(tauntInfo, xmlElement);
        }

        private static void HandleRed(TauntInfo tauntInfo, XmlElement xmlElement)
        {
            tauntInfo.NoHatTextureRed = xmlElement.ChildText("NoHatTextureRed", null);
            tauntInfo.NoHatTextureRed ??= xmlElement.ChildText("RedNoHatTexture", null);
            tauntInfo.NoHatTextureRed ??= xmlElement.ChildText("NoHatRedTexture", null);
            tauntInfo.NoHatTextureRed ??= xmlElement["Red"].ChildText("NoHat", null);
            tauntInfo.NoHatTextureRed ??= xmlElement["NoHat"].ChildText("Red", null);

            tauntInfo.CrownTextureRed = xmlElement.ChildText("CrownTextureRed", null);
            tauntInfo.CrownTextureRed ??= xmlElement.ChildText("RedCrownTexture", null);
            tauntInfo.CrownTextureRed ??= xmlElement.ChildText("CrownRedTexture", null);
            tauntInfo.CrownTextureRed ??= xmlElement["Red"].ChildText("Crown", null);
            tauntInfo.CrownTextureRed ??= xmlElement["Crown"].ChildText("Red", null);

            tauntInfo.TauntTextureRed = xmlElement.ChildText("TextureRed", null);
            tauntInfo.TauntTextureRed ??= xmlElement.ChildText("RedTexture", null);
            tauntInfo.TauntTextureRed ??= xmlElement["Red"].ChildText("Normal", null);
            tauntInfo.TauntTextureRed ??= xmlElement["Normal"].ChildText("Red", null);
            tauntInfo.TauntTextureRed ??= xmlElement["Red"].ChildText("Hat", null);
            tauntInfo.TauntTextureRed ??= xmlElement["Hat"].ChildText("Red", null);

            tauntInfo.hasTauntNoHatRed = tauntInfo.spriteData.ContainsAnimation("tauntNoHat") &&
                                         !string.IsNullOrEmpty(tauntInfo.NoHatTextureRed);
            tauntInfo.hasTauntCrownRed = tauntInfo.spriteData.ContainsAnimation("tauntCrown") &&
                                         !string.IsNullOrEmpty(tauntInfo.CrownTextureRed);
            tauntInfo.hasTauntRed = tauntInfo.spriteData.ContainsAnimation("taunt") &&
                                    !string.IsNullOrEmpty(tauntInfo.TauntTextureRed);
        }

        private static void HandleBlue(TauntInfo tauntInfo, XmlElement xmlElement)
        {
            tauntInfo.NoHatTextureBlue = xmlElement.ChildText("NoHatTextureBlue", null);
            tauntInfo.NoHatTextureBlue ??= xmlElement.ChildText("BlueNoHatTexture", null);
            tauntInfo.NoHatTextureBlue ??= xmlElement.ChildText("NoHatBlueTexture", null);
            tauntInfo.NoHatTextureBlue ??= xmlElement["Blue"].ChildText("NoHat", null);
            tauntInfo.NoHatTextureBlue ??= xmlElement["NoHat"].ChildText("Blue", null);

            tauntInfo.CrownTextureBlue = xmlElement.ChildText("CrownTextureBlue", null);
            tauntInfo.CrownTextureBlue ??= xmlElement.ChildText("BlueCrownTexture", null);
            tauntInfo.CrownTextureBlue ??= xmlElement.ChildText("CrownBlueTexture", null);
            tauntInfo.CrownTextureBlue ??= xmlElement["Blue"].ChildText("Crown", null);
            tauntInfo.CrownTextureBlue ??= xmlElement["Crown"].ChildText("Blue", null);

            tauntInfo.TauntTextureBlue = xmlElement.ChildText("TextureBlue", null);
            tauntInfo.TauntTextureBlue ??= xmlElement.ChildText("BlueTexture", null);
            tauntInfo.TauntTextureBlue ??= xmlElement["Blue"].ChildText("Normal", null);
            tauntInfo.TauntTextureBlue ??= xmlElement["Normal"].ChildText("Blue", null);
            tauntInfo.TauntTextureBlue ??= xmlElement["Blue"].ChildText("Hat", null);
            tauntInfo.TauntTextureBlue ??= xmlElement["Hat"].ChildText("Blue", null);

            tauntInfo.hasTauntNoHatBlue = tauntInfo.spriteData.ContainsAnimation("tauntNoHat") &&
                                         !string.IsNullOrEmpty(tauntInfo.NoHatTextureBlue);
            tauntInfo.hasTauntCrownBlue = tauntInfo.spriteData.ContainsAnimation("tauntCrown") &&
                                         !string.IsNullOrEmpty(tauntInfo.CrownTextureBlue);
            tauntInfo.hasTauntBlue = tauntInfo.spriteData.ContainsAnimation("taunt") &&
                                    !string.IsNullOrEmpty(tauntInfo.TauntTextureBlue);
        }
               
        private static void HandleNormal(TauntInfo tauntInfo, XmlElement xmlElement)
        {
            tauntInfo.NoHatTexture = xmlElement.ChildText("NoHatTexture", null);
            tauntInfo.NoHatTexture ??= xmlElement["Normal"]?.ChildText("NoHat", null);
            tauntInfo.NoHatTexture ??= xmlElement["NoHat"]?.ChildText("Normal", null);

            tauntInfo.CrownTexture = xmlElement.ChildText("CrownTexture", null);
            tauntInfo.CrownTexture ??= xmlElement["Normal"]?.ChildText("Crown", null);
            tauntInfo.CrownTexture ??= xmlElement["Crown"]?.ChildText("Normal", null);

            tauntInfo.TauntTexture = xmlElement.ChildText("Texture", null);
            tauntInfo.TauntTexture ??= xmlElement["Normal"]?.ChildText("Hat", null);
            tauntInfo.TauntTexture ??= xmlElement["Hat"]?.ChildText("Normal", null);

            tauntInfo.hasTauntNoHat = tauntInfo.spriteData.ContainsAnimation("tauntNoHat") &&
                                      !string.IsNullOrEmpty(tauntInfo.NoHatTexture);
            tauntInfo.hasTauntCrown = tauntInfo.spriteData.ContainsAnimation("tauntCrown") &&
                                      !string.IsNullOrEmpty(tauntInfo.CrownTexture);
            tauntInfo.hasTaunt =
                tauntInfo.spriteData.ContainsAnimation("taunt") && !string.IsNullOrEmpty(tauntInfo.TauntTexture);
        }

        private static string HandleSFX(Player self, CustomSpriteDataInfo customSpriteData, XmlElement xmlElement,
            out SFX sound)
        {
            return HandleSFX(self, customSpriteData.PathName, xmlElement, out sound);
        }

        private static string HandleSFX(Player self, string pathName, XmlElement xmlElement, out SFX sound)
        {
            var originalPath = Audio.LOAD_PREFIX;
            Audio.LOAD_PREFIX = pathName;
            var soundPath = xmlElement.ChildText("SFX", null);
            sound = null;
            if (soundPath != null)
            {
                if (Exists(soundPath))
                {
                    var looped = LoadLooped(soundPath);
                    looped.Instance.IsLooped = false;
                    sound = looped;
                }
            }

            if (sound == null)
            {
                soundPath = xmlElement.ChildText("SFXLooped", null);
                if (Exists(soundPath))
                {
                    sound = LoadLooped(soundPath);
                }
            }

            if (sound == null)
            {
                soundPath = xmlElement.ChildText("SFXVaried", null);
                if (Exists(soundPath))
                {
                    sound = LoadVaried(soundPath);
                }
            }

            sound ??= self.ArcherData.SFX.Ready;
            return originalPath;
        }

     
        [HarmonyPrefix]
        private static bool Player_LeaveDucking_Prefix(Player __instance)
        {
            // var variantEnabled = settings.TauntAlwaysOn || variantInfo?[__instance.PlayerIndex] ?? false;
            // if (!variantEnabled)
            //     return true;
            //
            // if (tauntStates.ContainsKey(__instance))
            // {
            //     var bodySprite = DynamicData.For(__instance).Get<Sprite<string>>("bodySprite");
            //     tauntInfos.TryGetValue(__instance.ArcherData, out var tauntInfo);
            //     var tauntState = tauntStates[__instance];
            //     if (tauntState?.animation != null)
            //     {
            //         tauntState.sprite.Visible = false;
            //         tauntState.sprite.Stop();
            //         drawSelfPropertyInfo.SetValue(__instance, true);
            //         bodySprite.Visible = true;
            //         __instance.Remove(tauntState.sprite);
            //     }
            //     tauntInfo?.Sound.Stop();
            //     tauntStates.Remove(__instance);
            // }
            return true;
        }

        [HarmonyPostfix]
        private static void Player_UpdateHead_Postfix(Player __instance)
        {
            // var variantEnabled = settings.TauntAlwaysOn || variantInfo?[__instance.PlayerIndex] ?? false;
            // if (!variantEnabled || !tauntStates.TryGetValue(__instance, out var state)) 
            //     return;
            //
            // if (state?.animation != null)
            //     state.headSprite.Visible = false;
        }

        [HarmonyPrefix]
        private static bool Player_UpdateAnimation_Prefix(Player __instance)
        {
            // if (!settings.TauntAlwaysOn && variantInfo == null)
            //     return true;
            //
            // var variantEnabled = settings.TauntAlwaysOn || variantInfo?[__instance.PlayerIndex] ?? false;
            // if (variantEnabled && tauntStates.ContainsKey(__instance))
            // {
            //     var tauntState = tauntStates[__instance];
            //     if (tauntState == null) 
            //         return true;
            //         
            //     PlayTauntAnimation(__instance, tauntState);
            //     return false;
            // }
            return true;
        }

        
        private static bool CheckTauntAnimation(Player self, Sprite<string> tauntCharacter, TauntInfo tauntInfo)
        {
            string animation = null;
            if (self.HatState == Player.HatStates.Normal && tauntInfo.hasTaunt)
            {
                animation = "taunt";
            }

            if (self.HatState == Player.HatStates.NoHat && tauntInfo.hasTauntNoHat)
            {
                animation = "tauntNoHat";
            }

            if (self.HatState == Player.HatStates.Crown && tauntInfo.hasTauntCrown)
            {
                animation = "tauntCrown";
            }
            // if (self.HatState == Player.HatStates.Normal)
            // {
            //     animation = tauntInfo.hasTaunt ? "tauntHat" :
            //         tauntInfo.hasTauntNoHat ? "taunt" :
            //         tauntInfo.hasTauntCrown ? "tauntCrown" : null;
            // }
            // if (self.HatState == Player.HatStates.Crown)
            // {
            //     animation = tauntInfo.hasTauntCrown ? "tauntCrown" :
            //         tauntInfo.hasTaunt ? "tauntHat" :
            //         tauntInfo.hasTauntNoHat ? "taunt" : null;
            // }
            // if (self.HatState == Player.HatStates.NoHat)
            // {
            //     animation = tauntInfo.hasTaunt? "taunt" :
            //         tauntInfo.hasTauntNoHat ? "tauntHat" :
            //         tauntInfo.hasTauntCrown  ? "tauntCrown" : null;
            // }

            if (animation == null) return false;
            var texture = (GetTexture(self, animation, "taunt", 
                               tauntInfo.TauntTexture, tauntInfo.TauntTextureBlue, tauntInfo.TauntTextureRed) ??
                           GetTexture(self, animation, "tauntNoHat", 
                               tauntInfo.NoHatTexture, tauntInfo.NoHatTextureBlue, tauntInfo.NoHatTextureRed)) ??
                          GetTexture(self, animation, "tauntCrown", 
                              tauntInfo.CrownTexture, tauntInfo.CrownTextureBlue, tauntInfo.CrownTextureRed);

            if (texture == null) return false;

            if (tauntStates.TryGetValue(self, out var state))
            {
                state.textureName = texture;
                state.animation = animation;
                return true;
            }

            return true;

        }

        private static void PlayTauntAnimation(Player self, TauntState state)
        {
            if (state.animation == null)
            {
                state.bodySprite.Visible = true;
                state.headSprite.Visible = true;
                if(state.sprite != null)
                    state.sprite.Visible = false;
                drawSelfPropertyInfo.SetValue(self, true);
                state.bodySprite.Play("run");
                return;
            }
            state.sprite.FlipX = self.Facing != Facing.Right;

            drawSelfPropertyInfo.SetValue(self, false);
            state.sprite.SwapSubtexture(TFGame.Atlas[state.textureName]);
            state.bodySprite.Visible = false;
            state.sprite.Visible = true;
            state.sprite.Play(state.animation);
        }

        private static string GetTexture(Player self, string animation, string compare, string normal, string blue, string red)
        {
            if (animation != compare) return null;
            switch (self.TeamColor)
            {
                case Allegiance.Neutral:
                    return normal;
                case Allegiance.Blue:
                    return blue;
                case Allegiance.Red:
                    return red;
            }
            return null;
        }

        public static void Unload()
        {
            if (!enabled)
                return;
            harmony.UnpatchAll();
        }
        
        // public static SFX LoadSFX(string name) => Exists(name) ? new SFX(name) : (SFX) null;

    public static SFX LoadWithVariedBackup(string name) => Exists(name) ? new SFX(name) : (SFX) LoadVaried(name);

    public static SFXVaried LoadVaried(string name)
    {
      var num = 0;
      while (Exists(name + VariedSuffix(num)))
        ++num;
      return num > 0 ? new SFXVaried(name, num) : (SFXVaried) null;
    }

    public static SFXLooped LoadLooped(string name) => Exists(name) ? new SFXLooped(name) : (SFXLooped) null;

    private static bool Exists(string name) => File.Exists(Audio.LOAD_PREFIX +  name + ".wav");

    private static string VariedSuffix(int num)
    {
      ++num;
      return num < 10 ? "_0" + (object) num : "_" + (object) num;
    }

    }
}
