using System.Reflection;
using System.Xml;
using FortRise;
using FortRise.IO;
using Microsoft.Xna.Framework.Input;
using Monocle;
using MonoMod.RuntimeDetour;
using MonoMod.Utils;
using TowerFall;
using ArrowHUD = TowerFall.ArrowHUD;

namespace ArcherLoaderMod.Taunt
{
    public class TauntVariant
    {
        private static Hook hook_UpdateHead;
        private static Hook hook_LeaveDucking;
        private static Hook hook_UpdateAnimation;
        
        public static Atlas MyAtlas;
        
        // private static Dictionary<Player, Sprite<string>> tauntCharacters = new ();
        private static Dictionary<Player, TauntState> tauntStates = new ();
        
        private static Variant variantInfo;
        private static Sprite<string> originalSprite;
        private static PropertyInfo drawSelfPropertyInfo;

        private static Dictionary<ArcherData, TauntInfo> tauntInfos = new();
        public static MethodInfo _loseHat;

        public static bool enabled = false;

        // private static CharacterSounds _sounds = new();
        public static void OnVariantsRegister(VariantManager variants, bool noPerPlayer = false)
        {
            var info = new CustomVariantInfo("Taunt", MyAtlas["variants/taunt"], CustomVariantFlags.PerPlayer)
            {
                Description = "HUMILIATE YOUR FOES (DUCK + RIGHT STICK DOWN or V key)"
                // , Header = "RULES"
            };
            variants.AddVariant(info, noPerPlayer);
        }

        public static void LoadContent(FortContent fortContent)
        {
            drawSelfPropertyInfo = typeof(Player).GetProperty( "DrawSelf",BindingFlags.Public | BindingFlags.Instance);

            MyAtlas = fortContent.LoadAtlas("Atlas/atlas.xml", "Atlas/atlas.png");
        }

        public static void Load()
        {
            enabled = true;
            _loseHat = typeof(Player).GetMethod("LoseHat", BindingFlags.NonPublic | BindingFlags.Instance);
        
            On.TowerFall.Player.Update += OnPlayerOnUpdate;
            On.TowerFall.Player.DoWrapRender += OnPlayerOnDoWrapRender;
            
            Action<orig_Player_UpdateHead, Player> updateHeadPatch = UpdateHead_patch;
            Action<orig_Player_LeaveDucking, Player> leaveDuckingPatch = LeaveDucking_patch;
            Action<orig_Player_UpdateAnimation, Player> updateAnimationPatch = UpdateAnimation_patch;
            
            var methodInfo = typeof(Player).GetMethod("UpdateHead", BindingFlags.NonPublic | BindingFlags.Instance);
            hook_UpdateHead = new Hook(
                methodInfo,
                updateHeadPatch);

            hook_LeaveDucking = new Hook(
                typeof(Player).GetMethod("LeaveDucking", BindingFlags.NonPublic | BindingFlags.Instance),
                leaveDuckingPatch);

            hook_UpdateAnimation = new Hook(
                typeof(Player).GetMethod("UpdateAnimation", BindingFlags.NonPublic | BindingFlags.Instance),
                updateAnimationPatch);
            
            Cache.Init<SelfExplosion>();
            
            On.TowerFall.ArrowHUD.Render += OnArrowHudOnRender;
        }

        private static void OnPlayerOnDoWrapRender(On.TowerFall.Player.orig_DoWrapRender orig, Player self)
        {
            if (tauntStates.ContainsKey(self))
            {
                var tauntState = tauntStates[self];
                var tauntCharacter = tauntState?.sprite;
                if (tauntState != null && tauntCharacter && tauntState.animation != null)
                    tauntCharacter?.DrawOutline();
            }
            orig(self);
        }
        
        
        private static void OnArrowHudOnRender(On.TowerFall.ArrowHUD.orig_Render orig, ArrowHUD self)
        {
            if (!FortEntrance.Settings.TauntAlwaysOn && variantInfo == null)
            {
                orig(self);
                return;
            }
            
            if (!FortEntrance.Settings.HideArrowsWhileTaunt)
            {
                orig(self);
                return;
            }

            var player = DynamicData.For(self).Get<Player>("player");
            
            if (tauntStates.ContainsKey(player))
            {
                return;
            }
            orig(self);
        }

        
        private static void Explode(Player self, Tween t = null)
        {
            // if (BombPickup.SFXNewest == this)
            // {
            //     Sounds.sfx_bombChestLoop.Stop();
            // }
            Sounds.pu_bombArrowExplode.Play(self.X);
            // self.Collidable = false;
            if(FortEntrance.Settings.TauntTooExplode)
            {
                Explosion.Spawn(self.Level, self.Position, self.PlayerIndex, plusOneKill: false, false, bombTrap: false);
            }
            else
            {
                SelfExplosion.Spawn(self.Level, self.Position, self.PlayerIndex, plusOneKill: false, !tauntInfos[self.ArcherData].SelfDestruction);
            }
           
            // ArrowCushion.ReleaseArrows(Speed);
            TFGame.PlayerInputs[self.PlayerIndex].Rumble(1f, 30);
            // RemoveSelf();
        }

        private static void OnPlayerOnUpdate(On.TowerFall.Player.orig_Update orig, Player self)
        {
            orig(self);
            
            var matchVariants = self.Level.Session.MatchSettings.Variants;
            variantInfo = matchVariants.GetCustomVariant("Taunt");
            var variantEnabled = variantInfo[self.PlayerIndex] || FortEntrance.Settings.TauntAlwaysOn;
            if (!variantEnabled) return;
                
            if (self.State == Player.PlayerStates.Frozen)
            {
                return;
            }

            var input = DynamicData.For(self).Get<InputState>("input");
            var playerInput = TFGame.PlayerInputs[self.PlayerIndex];

            SelfKill(self, playerInput, input);
            LoseHat(self, playerInput, input);

            var tauntButton = playerInput switch
            {
                XGamepadInput xGamepadInput => xGamepadInput.XGamepad.RightStickDownPressed(0),
                // NewGamepadInput newGamepadInput => 
                KeyboardInput => MInput.Keyboard.Check((Keys) Keys.V),
                _ => input.ArrowsPressed
            };

            if (!tauntButton || self.State != Player.PlayerStates.Ducking)
            {
                return;
            }

            if (tauntStates.TryGetValue(self, out var tauntCharacter))
            {
                tauntInfos.TryGetValue(self.ArcherData, out var currentTauntInfo);
                if (currentTauntInfo != null && (currentTauntInfo.hasTauntNoHat || currentTauntInfo.hasTauntCrown || currentTauntInfo.hasTaunt))
                {
                    CheckTauntAnimation(self, tauntCharacter.sprite, currentTauntInfo);
                }
                return;
            }

            var tauntInfo = InitTauntInfo(self);
            if (tauntInfo.id != null)
            {
                self.Add(tauntInfo.spriteData);
                tauntStates[self].sprite = tauntInfo.spriteData;
                CheckTauntAnimation(self, tauntStates[self].sprite, tauntInfo);
                
                if (FortEntrance.Settings.TauntTooExplode || tauntInfo.SelfDestruction)
                {
                    tauntInfo.spriteData.OnAnimationComplete += sprite =>
                    {
                        Explode(self);
                        tauntInfo.Sound.Stop();
                        Music.Stop();
                    };
                }
            }
            
            if(!self.Dead)
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
            if (!dropHatButton || !FortEntrance.Settings.DropHat) return;
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

            if (!killHatButton || !FortEntrance.Settings.SelfKill) return;
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
                var customExist = Mod.ArcherCustomDataDict.TryGetValue(self.ArcherData, out var archerCustomData);
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
                    if (Mod.customSpriteDataCategoryDict.ContainsKey("taunt"))
                    {
                        foreach (var customSpriteData in Mod.customSpriteDataCategoryDict["taunt"])
                        {
                            var xmlElement = customSpriteData.Element;

                            var forAttribute = Mod.GetForAttribute(xmlElement);
                            if (string.IsNullOrEmpty(forAttribute)) continue;
                            Mod.BaseArcherByNameDict.TryGetValue(xmlElement.GetAttribute(forAttribute).ToLower(),
                                out var searchArcherData);
                            if (searchArcherData == null)
                            {
                                foreach (var customData in Mod.ArcherCustomDataDict)
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

                        foreach (var customSpriteData in Mod.customSpriteDataCategoryDict["taunt"])
                        {
                            var xmlElement = customSpriteData.Element;

                            var forAttribute = Mod.GetForAttribute(xmlElement);
                            if (string.IsNullOrEmpty(forAttribute)) continue;
                            Mod.BaseArcherByNameDict.TryGetValue(xmlElement.GetAttribute(forAttribute).ToLower(),
                                out var searchArcherData);
                            if (searchArcherData == null)
                            {
                                foreach (var customData in Mod.ArcherCustomDataDict)
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
            tauntInfo.NoHatTexture ??= xmlElement["Normal"].ChildText("NoHat", null);
            tauntInfo.NoHatTexture ??= xmlElement["NoHat"].ChildText("Normal", null);

            tauntInfo.CrownTexture = xmlElement.ChildText("CrownTexture", null);
            tauntInfo.CrownTexture ??= xmlElement["Normal"].ChildText("Crown", null);
            tauntInfo.CrownTexture ??= xmlElement["Crown"].ChildText("Normal", null);

            tauntInfo.TauntTexture = xmlElement.ChildText("Texture", null);
            tauntInfo.TauntTexture ??= xmlElement["Normal"].ChildText("Hat", null);
            tauntInfo.TauntTexture ??= xmlElement["Hat"].ChildText("Normal", null);

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

        public static void LeaveDucking_patch(orig_Player_LeaveDucking orig, Player self)
        {
            var variantEnabled = FortEntrance.Settings.TauntAlwaysOn || variantInfo[self.PlayerIndex];
            if (!variantEnabled)
            {
                orig(self);
                return;
            }
            
            if (tauntStates.ContainsKey(self))
            {
                var bodySprite = DynamicData.For(self).Get<Sprite<string>>("bodySprite");
                tauntInfos.TryGetValue(self.ArcherData, out var tauntInfo);
                var tauntState = tauntStates[self];
                if (tauntState?.animation != null)
                {
                    tauntState.sprite.Visible = false;
                    tauntState.sprite.Stop();
                    drawSelfPropertyInfo.SetValue(self, true);
                    bodySprite.Visible = true;
                    self.Remove(tauntState.sprite);
                }
                tauntInfo?.Sound.Stop();
                tauntStates.Remove(self);
            }
            orig(self);
        }

        public static void UpdateHead_patch(orig_Player_UpdateHead orig, Player self)
        {
            orig(self);
            var variantEnabled = FortEntrance.Settings.TauntAlwaysOn || variantInfo != null && variantInfo[self.PlayerIndex];;
            if (!variantEnabled) return;

            if (!tauntStates.TryGetValue(self, out var state)) return;
            if (state?.animation != null)
                state.headSprite.Visible = false;
        }

        public static void UpdateAnimation_patch(orig_Player_UpdateAnimation orig, Player self)
        {
            if (!FortEntrance.Settings.TauntAlwaysOn && variantInfo == null)
            {
                orig(self);
                return;
            }
            var variantEnabled = FortEntrance.Settings.TauntAlwaysOn || variantInfo != null && variantInfo[self.PlayerIndex];
            if (variantEnabled && tauntStates.ContainsKey(self))
            {
                var tauntState = tauntStates[self];
                if (tauntState == null) return;
                PlayTauntAnimation(self, tauntState);
                return;
            }
        
            orig(self);
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
            if(!enabled)
                return;

            hook_UpdateHead.Dispose();
            hook_LeaveDucking.Dispose();
            hook_UpdateAnimation.Dispose();
        }
        
        public delegate void orig_Player_UpdateAnimation(Player self);
        
        public delegate void orig_Player_UpdateHead(Player self);

        public delegate void orig_Player_LeaveDucking(Player self);

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

    private static bool Exists(string name) => ModIO.IsFileExists(Audio.LOAD_PREFIX +  name + ".wav");

    private static string VariedSuffix(int num)
    {
      ++num;
      return num < 10 ? "_0" + (object) num : "_" + (object) num;
    }

    }
}
