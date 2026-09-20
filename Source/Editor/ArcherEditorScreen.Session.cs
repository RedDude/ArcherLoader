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
    private static bool once;

    // true from StartGame until the editor level is up: the level loads over the next frames, outside OpenEditor's try
    private static bool openingEditor;

    // The level is built by the loader scene's Update, so a bad map or archer throws there and would take the game
    // down. Swallow it, tell the user, and go back to the main menu.
    private static Exception LevelLoader_Update_Finalizer(Exception __exception)
    {
        if (__exception == null || !openingEditor) return __exception;

        openingEditor = false;
        editorMode = EditorMode.Live;
        editorSession = null;
        CrashGuard.MarkClean();
        ReportError("The editor level failed to load", __exception);
        System.Console.Error.WriteLine("[ArcherEditor] The editor level failed to load: " + __exception);

        try
        {
            Engine.Instance.Scene = new MainMenu(MainMenu.MenuState.Main);
        }
        catch (Exception e)
        {
            EditorConsole.Error("Could not return to the main menu: " + e.Message);
        }

        return null;
    }

    public static void HandleQuickStart()
    {
        // a crashed last session must not start straight into the same crash: the warning asks first
        if (!FortEntrance.Instance.Settings.QuickStart || CrashGuard.Crashed != null) return;
        
        harmony.Patch(
            typeof(MainMenu).GetMethod("Update"),
            prefix: new HarmonyMethod(typeof(ArcherEditorScreen), nameof(MainMenu_Update_Prefix))
        );
    }

    [HarmonyPrefix]
    private static void MainMenu_Update_Prefix(MainMenu __instance)
    {
        if (__instance.State == MainMenu.MenuState.Loading) return;

            if (once) return;
            once = true;

            OpenEditor();
        harmony.Unpatch(typeof(MainMenu).GetMethod("Update"), HarmonyPatchType.Prefix, harmony.Id);
    }

    // settings menu: the atlas viewer is part of the editor, so open the editor with it up and unrestricted
    public static void OpenAtlasViewer()
    {
        AtlasWindow.Open = true;
        AtlasWindow.ShowAll = true;
        OpenEditor();
    }

    public static void OpenEditor() => OpenEditor(EditorMode.Live);

    // Starting a session touches a lot of game state; if anything throws, the game stays where it is and the
    // reason goes to the console instead of taking the game down.
    public static void OpenEditor(EditorMode mode)
    {
        try
        {
            OpenEditorCore(mode);
        }
        catch (Exception e)
        {
            editorMode = EditorMode.Live;
            editorSession = null;
            CrashGuard.MarkClean();
            EditorConsole.Error("Could not open the editor: " + e.Message);
            System.Console.Error.WriteLine("[ArcherEditor] Could not open the editor: " + e);
        }
    }

    private static void OpenEditorCore(EditorMode mode)
    {
            editorMode = mode;
            pendingPlaygroundToggle = false;
            editorPlayground = false;

            var player1CharacterIndex = FortEntrance.Instance.Settings.Player1CharacterIndex;
            if (player1CharacterIndex > -1)
                TFGame.Characters[0] = player1CharacterIndex >= ArcherData.Archers.Length
                    ? ArcherData.Archers.Length - 1
                    : player1CharacterIndex;

            var player2CharacterIndex = FortEntrance.Instance.Settings.Player2CharacterIndex;
            if (player2CharacterIndex > -1)
                TFGame.Characters[1] = player2CharacterIndex >= ArcherData.Archers.Length
                    ? ArcherData.Archers.Length - 1
                    : player2CharacterIndex;

            var player3CharacterIndex = FortEntrance.Instance.Settings.Player3CharacterIndex;
            if (player3CharacterIndex > -1)
                TFGame.Characters[2] = player3CharacterIndex >= ArcherData.Archers.Length
                    ? ArcherData.Archers.Length - 1
                    : player3CharacterIndex;


            // the archer picked in the Archers window overrides the quick start indexes
            var editorSettings = FortEntrance.Instance.Settings;
            var editorIndex = EditorPlayerIndex;
            if (editorSettings.EditorArcherIndex > -1)
            {
                TFGame.Characters[editorIndex] = Math.Min(editorSettings.EditorArcherIndex, ArcherData.Archers.Length - 1);
                TFGame.AltSelect[editorIndex] = (ArcherData.ArcherTypes)editorSettings.EditorArcherType;
            }

            // only the selected player exists in the editor; borrow another input device if that slot has none
            TFGame.PlayerInputs[editorIndex] ??= TFGame.PlayerInputs.FirstOrDefault(x => x != null);
            for (var i = 0; i < TFGame.PlayerInputs.Length; i++)
                TFGame.Players[i] = i == editorIndex && TFGame.PlayerInputs[i] != null;

            var (mapTower, mapLevel) = CurrentMap();
            currentMapKey = $"{mapTower}:{mapLevel}";
            var matchSettings = new MatchSettings(TowerFor(mapTower).GetLevelSystem(), Modes.LevelTest,
                MatchSettings.MatchLengths.Standard);
            // matchSettings.Mode = ModRegisters.GameModeType<ArcherEditorMode>();
            (matchSettings.LevelSystem as VersusLevelSystem).StartOnLevel(mapLevel);
            var session = new Session(matchSettings);
            editorSession = session;

            // marker for the crash detector, removed again when the session ends normally
            var archer = ArcherData.Get(TFGame.Characters[editorIndex], TFGame.AltSelect[editorIndex]);
            CrashGuard.MarkStarted(editorIndex, TFGame.Characters[editorIndex], (int)TFGame.AltSelect[editorIndex],
                mode == EditorMode.Unloaded ? "(unloaded archer editor)" : $"{archer?.Name0} {archer?.Name1}".Trim());

            openingEditor = true;
            session.StartGame();

            // var matchSettings = new MatchSettings(GameData.VersusTowers[GameData.VersusTowers.Count-1].GetLevelSystem(), Modes.LevelTest,
            //     MatchSettings.MatchLengths.Standard);
            // matchSettings.Variants.GetCustomVariant("ReaperChalice").Value = true;

            // (matchSettings.LevelSystem as VersusLevelSystem).StartOnLevel(-1);
            // new Session(matchSettings).StartGame();

    }
}
