// using System.Collections.Generic;
// using System.Reflection;
// using Microsoft.Xna.Framework;
// using Monocle;
// using MonoMod.RuntimeDetour;
// using MonoMod.Utils;
// using TowerFall;
//
// namespace ArcherLoaderMod
// {
// 	public class SkinPatcher
// 	{
// 		private static IDetour hook_PlayerOnPlayer;
//
// 		public static void Load()
// 		{
// 			hook_PlayerOnPlayer = new Hook(
// 				typeof(RollcallElement).GetMethod("NotJoinedUpdate", BindingFlags.NonPublic | BindingFlags.Instance),
// 				typeof(SkinPatcher).GetMethod("PassThroughTeam_patch", BindingFlags.Public | BindingFlags.Static)
// 			);
//
// 		}
//
// 		public static void Unload()
// 		{
// 			hook_PlayerOnPlayer.Dispose();
// 		}
//
//
// 		public delegate void orig_Player_PlayerOnPlayer(RollcallElement self);
//
// 		public static void PassThroughTeam_patch(orig_Player_PlayerOnPlayer orig, RollcallElement self)
// 		{
// 			// var matchVariants = a.Level.Session.MatchSettings.Variants;
// 			// if (matchVariants.GetCustomVariant("PassThroughTeam")[a.PlayerIndex] && a.Allegiance == b.Allegiance && a.Allegiance != Allegiance.Neutral)
// 			// {
// 			// return;
// 			// }
//
// 			// orig(a, b);
// 		}
// 		
// 		public void SetCharacter(ArcherPortrait portrait, int characterIndex, ArcherData.ArcherTypes altSelect, int moveDir)
// 		{
// 			var joined = DynamicData.For(portrait).Get<bool>("joined");
// 			if (!joined)
// 			{
// 				// lastMove = moveDir;
// 				// if (ShouldFlip(altSelect))
// 				// {
// 				// 	flipEase = 1f - flipEase;
// 				// }
// 				// else
// 				// {
// 				// 	gemWiggler.Start();
// 				// }
// 				// CharacterIndex = characterIndex;
// 				// AltSelect = altSelect;
// 				// ArcherData = ArcherData.Get(CharacterIndex, AltSelect);
// 				// portrait.SwapSubtexture(ArcherData.Portraits.NotJoined);
// 				// portraitAlt.SwapSubtexture(FlipSide.Portraits.NotJoined);
// 				// InitGem();
// 				// wiggler.Start();
// 			}
// 		}
//
// 		private int NotJoinedUpdate(RollcallElement self)
// 		{
// 			var input = DynamicData.For(self).Get<PlayerInput>("input");
// 			var archerType = DynamicData.For(self).Get<ArcherData.ArcherTypes>("archerType");
// 			if (input == null)
// 			{
// 				return 0;
// 			}
//
// 			if (input.MenuBack && !self.MainMenu.Transitioning)
// 			{
// 				for (int i = 0; i < 4; i++)
// 				{
// 					TFGame.Players[i] = false;
// 				}
//
// 				Sounds.ui_clickBack.Play();
// 				if (MainMenu.RollcallMode == MainMenu.RollcallModes.Versus ||
// 				    MainMenu.RollcallMode == MainMenu.RollcallModes.Trials)
// 				{
// 					self.MainMenu.State = MainMenu.MenuState.Main;
// 				}
// 				else
// 				{
// 					self.MainMenu.State = MainMenu.MenuState.CoOp;
// 				}
// 			}
//
// 			// if (input.MenuAlt2Check && archerType == ArcherData.ArcherTypes.Alt &&
// 			//     ArcherData.SecretArchers[self.CharacterIndex] != null)
// 			// {
// 			// 	archerType = ArcherData.ArcherTypes.Secret;
// 			// 	portrait.SetCharacter(CharacterIndex, archerType, 1);
// 			// }
//
//
// 			// 	else if (input.MenuLeft && selfCanChangeSelection)
// 			// 	{
// 			// 		drawDarkWorldLock = false;
// 			// 		ChangeSelectionLeft();
// 			// 		Sounds.ui_move2.Play();
// 			// 		arrowWiggle.Start();
// 			// 		rightArrowWiggle = false;
// 			// 	}
// 			// 	else if (input.MenuRight && CanChangeSelection)
// 			// 	{
// 			// 		drawDarkWorldLock = false;
// 			// 		ChangeSelectionRight();
// 			// 		Sounds.ui_move2.Play();
// 			// 		arrowWiggle.Start();
// 			// 		rightArrowWiggle = true;
// 			// 	}
// 			// 	else if (input.MenuAlt && GameData.DarkWorldDLC)
// 			// 	{
// 			// 		drawDarkWorldLock = false;
// 			// 		altWiggle.Start();
// 			// 		Sounds.ui_altCostumeShift.Play(base.X);
// 			// 		if (archerType == ArcherData.ArcherTypes.Normal)
// 			// 		{
// 			// 			archerType = ArcherData.ArcherTypes.Alt;
// 			// 		}
// 			// 		else
// 			// 		{
// 			// 			archerType = ArcherData.ArcherTypes.Normal;
// 			// 		}
// 			// 		portrait.SetCharacter(CharacterIndex, archerType, 1);
// 			// 	}
// 			// 	else if (input.MenuConfirmOrStart && !TFGame.CharacterTaken(CharacterIndex) && TFGame.PlayerAmount < MaxPlayers)
// 			// 	{
// 			// 		if (ArcherData.Get(CharacterIndex, archerType).RequiresDarkWorldDLC && !GameData.DarkWorldDLC)
// 			// 		{
// 			// 			drawDarkWorldLock = true;
// 			// 			if (darkWorldLockEase < 1f || !TFGame.OpenStoreDarkWorldDLC())
// 			// 			{
// 			// 				portrait.Shake();
// 			// 				shakeTimer = 30f;
// 			// 				Sounds.ui_invalid.Play(base.X);
// 			// 				if (TFGame.PlayerInputs[playerIndex] != null)
// 			// 				{
// 			// 					TFGame.PlayerInputs[playerIndex].Rumble(1f, 20);
// 			// 				}
// 			// 			}
// 			// 			return 0;
// 			// 		}
// 			// 		if (input.MenuAlt2Check && archerType == ArcherData.ArcherTypes.Normal && ArcherData.SecretArchers[CharacterIndex] != null)
// 			// 		{
// 			// 			archerType = ArcherData.ArcherTypes.Secret;
// 			// 			portrait.SetCharacter(CharacterIndex, archerType, 1);
// 			// 		}
// 			// 		portrait.Join(unlock: false);
// 			// 		TFGame.Players[playerIndex] = true;
// 			// 		TFGame.AltSelect[playerIndex] = archerType;
// 			// 		if (TFGame.PlayerInputs[playerIndex] != null)
// 			// 		{
// 			// 			TFGame.PlayerInputs[playerIndex].Rumble(1f, 20);
// 			// 		}
// 			// 		shakeTimer = 20f;
// 			// 		if (TFGame.PlayerAmount == MaxPlayers)
// 			// 		{
// 			// 			ForceStart();
// 			// 		}
// 			// 		return 1;
// 			// 	}
// 			// 	return 0;
// 			// }
// 			return 0;
// 		}
// 	}
// }