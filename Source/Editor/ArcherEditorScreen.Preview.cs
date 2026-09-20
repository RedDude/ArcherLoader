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
    [HarmonyPrefix]
    private static bool RollcallElement_EnterJoined_Prefix(RollcallElement __instance)
    {
        if (rollcall != null)
        {
            LeaveRollcall();
            return false; // Skip original
        }
        return true; // Continue to original
    }

    // The game's own confirm does not always reach the element (its input is the borrowed device of another slot),
    // so the rollcall can also be left from the keyboard.
    private static void LeaveRollcall()
    {
        rollcall?.RemoveSelf();
        rollcall = null;
        TFGame.Players[EditorPlayerIndex] = TFGame.PlayerInputs[EditorPlayerIndex] != null;
        HotRefresh();
    }

    private static void HandleRollcallExit()
    {
        if (rollcall == null) return;

        if (MInput.Keyboard.Pressed(Keys.Enter) || MInput.Keyboard.Pressed(Keys.Escape) || MInput.Keyboard.Pressed(Keys.Space))
            LeaveRollcall();
    }

    [HarmonyPrefix]
    private static bool RollcallElement_NotJoinedUpdate_Prefix(RollcallElement __instance, ref int __result)
    {
        if (rollcallNotJoinedPreview != null)
        {
            __result = 0; // Stay in NotJoined state
            return false; // Skip original
        }
        return true; // Continue to original
    }

    [HarmonyPrefix]
    private static bool RollcallElement_JoinedUpdate_Prefix(RollcallElement __instance, ref int __result)
    {
        if (rollcallPreview != null)
        {
            __result = 1; // Stay in Joined state
            return false; // Skip original
        }
        return true; // Continue to original
    }

    [HarmonyPrefix]
    private static void RollcallElement_Render_Prefix(RollcallElement __instance)
    {
        if (rollcallPreview != null)
        {
            DynamicData.For(rollcallPreview).Set("input", null);
        }
        if (rollcallNotJoinedPreview != null)
        {
            DynamicData.For(rollcallNotJoinedPreview).Set("input", null);
        }
    }

    // the joined-state rollcall portrait of the archer; toggling again (or Enter / Escape, or the game's own confirm)
    // takes it away and refreshes the editor
    private static void ToggleRollcall(Session session)
    {
        if (rollcall != null)
        {
            LeaveRollcall();
            return;
        }

        var level = session.CurrentLevel;
        if (!level.Layers.ContainsKey(-1))
            level.Layers.Add(-1, new Monocle.Layer());

        ClearMocks();
        var p = EditorPlayer(level);
        p.Visible = false;
        p.Active = false;
        MainMenu.RollcallMode = (MainMenu.RollcallModes)(-1);
        TFGame.Players[EditorPlayerIndex] = false;
        rollcall = new RollcallElement(EditorPlayerIndex);
        rollcall.Position = new Vector2(200, 100);
        rollcall.LayerIndex = 3;
        level.Add(rollcall);
    }

    private static void TogglePortraits(Session session)
    {
            if(rollcallPreview != null){
                rollcallPreview.RemoveSelf();
                rollcallPreview = null;
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

                return;
            }

            var scene = (Engine.Instance.Scene as Level);
            if(!scene.Layers.ContainsKey(-1)){
                scene.Layers.Add(-1, new Monocle.Layer());
            }
            
            MainMenu.RollcallMode = (MainMenu.RollcallModes)(-2);
            rollcallPreview = new RollcallElement(EditorPlayerIndex);
            rollcallPreview.Position = new Vector2(200, 100);
            rollcallPreview.LayerIndex = 3;
            session.CurrentLevel.Add(rollcallPreview);
            // DynamicData.For(rollcallPreview).Set("input", null);
            // DynamicData.For(rollcallPreview).Set("input", new );

            TFGame.Players[EditorPlayerIndex] = false;
            rollcallNotJoinedPreview = new RollcallElement(EditorPlayerIndex);
            TFGame.Players[EditorPlayerIndex] = true;
            rollcallNotJoinedPreview.Position = new Vector2(100, 100);
            rollcallNotJoinedPreview.LayerIndex = 3;
            DynamicData.For(rollcallNotJoinedPreview).Set("altButton", null);
            DynamicData.For(rollcallNotJoinedPreview).Set("confirmButton", null);
            var rightArrow = DynamicData.For(rollcallNotJoinedPreview).Get<Image>("rightArrow");
            if(rightArrow != null){
                rightArrow.Visible = false;
            }
            var leftArrow = DynamicData.For(rollcallNotJoinedPreview).Get<Image>("leftArrow");
            if(leftArrow != null){
                leftArrow.Visible = false;
            }
            // DynamicData.For(rollcall).Set("MainMenu", new MainMenu(MainMenu.MenuState.Rollcall));
            session.CurrentLevel.Add(rollcallNotJoinedPreview);
            // DynamicData.For(rollcallNotJoinedPreview).Set("input", new PlayerInput());

            var p = EditorPlayer(session.CurrentLevel);
            resultsPortraitWin = new MatchResultsPortraitPreview(p.PlayerIndex, true, new Vector2(200, 200));
            resultsPortraitWin.LayerIndex = 3;
            session.CurrentLevel.Add(resultsPortraitWin);

            resultsPortraitLose = new MatchResultsPortraitPreview(p.PlayerIndex, false, new Vector2(100, 200));
            resultsPortraitLose.LayerIndex = 3;
            session.CurrentLevel.Add(resultsPortraitLose);
    }
}
