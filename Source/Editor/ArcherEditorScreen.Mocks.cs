using System;
using System.Collections.Generic;
using System.Linq;
using ArcherEditorMod.Editor;
using ArcherEditorMod;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Monocle;
using MonoMod.Utils;
using TowerFall;
using TowerFall.Editor;
using HarmonyLib;
using ImGuiNET;
using ArcherEditorMod.Editor.ImGuiSupport;
using System.Reflection;
using Level = TowerFall.Level;

public static partial class ArcherEditorScreen
{
    // clicking a mock opens the animation it is currently playing in the Animation window
    private static void HandleMockClick()
    {
        if (editorPlayground || renderer == null || !MInput.Mouse.LeftPressed) return;
        if (ImGui.GetIO().WantCaptureMouse) return;

        var mouse = MInput.Mouse.Position;
        var hit = allCurrentMocks.LastOrDefault(m =>
            new Rectangle((int)m.Position.X - 8, (int)m.Position.Y - 14, 16, 24).Contains((int)mouse.X, (int)mouse.Y));
        if (hit != null)
            FramesWindow.Show(hit.myBodySprite.CurrentAnimID);
    }

    private static bool HotRefresh()
    {
        var session = (Engine.Instance.Scene as Level)?.Session;
        ArcherEditorCommands.ReloadArcher(null);

        if (session == null)
            return false;

        var positions = new Dictionary<int, Vector2>();
        // foreach (var currentLevelPlayer in session.CurrentLevel.Players)
        // {
        // var p = (Player)currentLevelPlayer;
        var p = EditorPlayer(session.CurrentLevel);
        try
        {
            // var data = ArcherData.Get(TFGame.Characters[p.PlayerIndex], TFGame.AltSelect[p.PlayerIndex]);
            // var exist = Mod.ArcherCustomDataDict.TryGetValue(data, out var archerCustomData);
            // if (!exist)
            // {
            //     var skinData = SkinPatcher.GetSkinCharacter(p.PlayerIndex, data);
            //     exist = Mod.ArcherCustomDataDict.TryGetValue(skinData, out archerCustomData);
            // }
            // if (!exist) continue;
            //
            // var errors = ArcherCustomManager.validator.Validate(archerCustomData.xmlData, archerCustomData.atlas,
            //     archerCustomData.menuAtlas, archerCustomData.spriteData, archerCustomData.menuSpriteData, archerCustomData.ArcherType,
            //     archerCustomData.ID, archerCustomData.FolderPath);

            // if (errors.Count == 0)
            {
                positions[p.PlayerIndex] = p.Position;

                session.CurrentLevel.Remove(p);
                var player = new Player(p.PlayerIndex, p.Position, editorAllegiance, editorAllegiance, GetInventory(session, p.PlayerIndex), editorHat, frozen: false, flash: false, indicator: false);
                DynamicData.For(player).Set("Facing", editorFacing);
                session.CurrentLevel.Add(player);
                RunValidation(player.ArcherData);

                ClearMocks();

                if(rollcallPreview != null){
                    rollcallPreview.RemoveSelf();
                    rollcallPreview = null;
                }

                if(rollcallNotJoinedPreview != null){
                    rollcallNotJoinedPreview.RemoveSelf();
                    rollcallNotJoinedPreview = null;
                }

                if(resultsPortraitWin != null){
                    resultsPortraitWin.RemoveSelf();
                    resultsPortraitWin = null;
                }

                if(resultsPortraitLose != null){
                    resultsPortraitLose.RemoveSelf();
                    resultsPortraitLose = null;
                }

                CreateMocks(session, player);
            }
        }
        catch (Exception e)
        {
            ReportError("Reloading the archer failed", e);
        }
        // }

        if (MInput.Keyboard.Check((Keys)Keys.RightShift))
        {
            var matchSettings = new MatchSettings(GameData.VersusTowers[0].GetLevelSystem(), Modes.LevelTest,
                MatchSettings.MatchLengths.Standard);
            // matchSettings.Variants.GetCustomVariant("ReaperChalice").Value = true;
            (matchSettings.LevelSystem as VersusLevelSystem).StartOnLevel(0);
            var newSession = new Session(matchSettings);

            // void OnSessionOnStartRound(On.TowerFall.Session.orig_OnUpdate round, Session self1)
            // {
            //     round(self1);
            //
            //     try
            //     {
            //         foreach (var currentLevelPlayer in newSession.CurrentLevel.Players)
            //         {
            //             var p = (Player)currentLevelPlayer;
            //             if (positions.ContainsKey(p.PlayerIndex))
            //                 currentLevelPlayer.Position = positions[p.PlayerIndex];
            //         }
            //         On.TowerFall.Session.OnUpdate -= OnSessionOnStartRound;
            //     }
            //     catch (Exception e)
            //     {
            //     }
            // }
            //
            // On.TowerFall.Session.OnUpdate += OnSessionOnStartRound;
            newSession.StartGame();

        }

        return true;
    }

    private static int FacingDir => editorFacing == Facing.Left ? -1 : 1;

    private static void ClearMocks()
    {
        allCurrentMocks.ForEach(m => m.RemoveSelf());
        allCurrentMocks.Clear();
        allCurrentCorpses.ForEach(c => c.RemoveSelf());
        allCurrentCorpses.Clear();
        allCurrentGhosts.ForEach(g => g.RemoveSelf());
        allCurrentGhosts.Clear();
    }

    private static PlayerInventory GetInventory(Session session, int playerIndex)
    {
        var inventory = session.GetPlayerInventory(playerIndex);
        inventory.Wings = editorWings;
        return inventory;
    }

    private static void CreateMocks(Session session, Player p)
    {
        var pPosition = new Vector2(140, 192);
        p.Position = pPosition;

        // playground: just the playable archer, no mocks
        p.Visible = editorPlayground;
        p.Active = editorPlayground;
        p.Collidable = editorPlayground; // arrows shot by the mocks must not find it
        if (editorPlayground) return;

        try
        {
            CurrentSection.Build(session, p);
        }
        catch (Exception e)
        {
            EditorConsole.Error($"The '{CurrentSection.Name}' mock section failed: {e.Message}");
        }
    }

    private static MockPlayer CreateMockPlayer(Player p, Vector2 mockPosition, Session session, Action<MockPlayer> callback,
        Player.HatStates? hat = null, bool noGravity = false, Facing? facing = null, Action<MockPlayer> late = null)
    {
        var mockPlayer = new MockPlayer(callback, p.PlayerIndex, mockPosition, p.Allegiance, p.TeamColor,
            GetInventory(session, p.PlayerIndex), hat ?? editorHat, frozen: false, flash: false, indicator: false)
        {
            // the checkerboard has no ground: the sections' mocks hover in their cells
            NoGravity = noGravity || (CurrentSection.Checker && CurrentMap().Tower < 0),
            lateCallback = late
        };
        DynamicData.For(mockPlayer).Set("Facing", facing ?? editorFacing);
        session.CurrentLevel.Add(mockPlayer);
        allCurrentMocks.Add(mockPlayer);
        return mockPlayer;
    }
}
