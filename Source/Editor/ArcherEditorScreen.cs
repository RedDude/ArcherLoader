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
    public static List<MockPlayer> allCurrentMocks = new List<MockPlayer>();
    public static List<MockCorpse> allCurrentCorpses = new List<MockCorpse>();
    public static List<MockGhost> allCurrentGhosts = new List<MockGhost>();
    public static Allegiance editorAllegiance = Allegiance.Neutral;
    private static Player.HatStates editorHat = Player.HatStates.Normal;
    private static bool editorWings;
    private static Facing editorFacing = Facing.Left;
    private static bool pendingReopen;
    private static bool pendingPlaygroundToggle;
    private static Session editorSession;
    private static bool editorPlayground;
    private static Level setupLevel;
    private static RollcallElement rollcall;
    private static RollcallElement rollcallPreview;
    private static RollcallElement rollcallNotJoinedPreview;
    private static MatchResultsPortraitPreview resultsPortraitWin;
    private static MatchResultsPortraitPreview resultsPortraitLose;
    private static Harmony harmony;
    private static ImGuiRenderer renderer;



    public static void Load()
    {
        // must run before anything can open the editor (quick start included)
        CrashGuard.Check();

        harmony = new Harmony("mod.archereditor.editor");
        
        harmony.Patch(
            AccessTools.DeclaredMethod(typeof(Engine), "Draw"),
            postfix: new HarmonyMethod(typeof(ArcherEditorScreen), nameof(Engine_Draw_Postfix))
        );

        harmony.Patch(
            AccessTools.Method(typeof(RoundLogic), nameof(RoundLogic.GetRoundLogic)),
            postfix: new HarmonyMethod(typeof(ArcherEditorScreen), nameof(GetRoundLogic_Postfix))
        );

        // Other mods (FortRise.ImGui hides the cursor every frame while its debug window is closed) fight over
        // IsMouseVisible; while the editor level is up nobody may hide it.
        harmony.Patch(
            AccessTools.PropertySetter(typeof(Game), nameof(Game.IsMouseVisible)),
            prefix: new HarmonyMethod(typeof(ArcherEditorScreen), nameof(IsMouseVisible_Prefix))
        );

        // The editor must keep running when the window loses focus (the game would pause the level)
        harmony.Patch(
            AccessTools.PropertyGetter(typeof(Game), nameof(Game.IsActive)),
            postfix: new HarmonyMethod(typeof(ArcherEditorScreen), nameof(IsActive_Postfix))
        );

        // Keys typed into an ImGui text field must not reach the game (hotkeys, player movement)
        foreach (var method in typeof(MInput.KeyboardData).GetMethods(BindingFlags.Public | BindingFlags.Instance))
        {
            if (method.ReturnType != typeof(bool) || method.Name is not ("Check" or "Pressed" or "Released"))
                continue;

            try
            {
                harmony.Patch(method, prefix: new HarmonyMethod(typeof(ArcherEditorScreen), nameof(Keyboard_Prefix)));
            }
            catch (Exception e)
            {
                EditorConsole.Warn($"Could not patch keyboard input ({method.Name}): {e.Message}");
            }
        }

        // The editor spams hits/rumble on the mocks; keep controllers quiet while it's open
        harmony.Patch(
            AccessTools.Method(typeof(MInput.XGamepadData), nameof(MInput.XGamepadData.Rumble)),
            prefix: new HarmonyMethod(typeof(ArcherEditorScreen), nameof(Rumble_Prefix))
        );

        // Patch RollcallElement methods (EnterJoined/NotJoinedUpdate/JoinedUpdate are private in the
        // current FortRise RollcallElement patch, so GetMethod needs the NonPublic flag or it returns
        // null and Harmony throws "Null method").
        harmony.Patch(
            typeof(RollcallElement).GetMethod("EnterJoined", BindingFlags.Instance | BindingFlags.NonPublic),
            prefix: new HarmonyMethod(typeof(ArcherEditorScreen), nameof(RollcallElement_EnterJoined_Prefix))
        );

        harmony.Patch(
            typeof(RollcallElement).GetMethod("NotJoinedUpdate", BindingFlags.Instance | BindingFlags.NonPublic),
            prefix: new HarmonyMethod(typeof(ArcherEditorScreen), nameof(RollcallElement_NotJoinedUpdate_Prefix))
        );

        harmony.Patch(
            typeof(RollcallElement).GetMethod("JoinedUpdate", BindingFlags.Instance | BindingFlags.NonPublic),
            prefix: new HarmonyMethod(typeof(ArcherEditorScreen), nameof(RollcallElement_JoinedUpdate_Prefix))
        );

        harmony.Patch(
            typeof(RollcallElement).GetMethod("Render"),
            prefix: new HarmonyMethod(typeof(ArcherEditorScreen), nameof(RollcallElement_Render_Prefix))
        );

        // Drives the T/F preview hotkeys every frame. This used to be wired via an `On.TowerFall.TFGame.Update`
        // hook (see git history); that hook (and this Load() call) got commented out during the standalone
        // features migration and never reconnected, which is why the editor stopped responding to input.
        harmony.Patch(
            typeof(TFGame).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public),
            postfix: new HarmonyMethod(typeof(ArcherEditorScreen), nameof(TFGame_Update_Postfix))
        );

        harmony.Patch(
            AccessTools.Method(typeof(LevelLoaderXML), "Update"),
            finalizer: new HarmonyMethod(typeof(ArcherEditorScreen), nameof(LevelLoader_Update_Finalizer))
        );

        // Own ImGui context/renderer (copied from FortRise.ImGui), so we don't depend on its debug window.
        // Load() runs from OnInitialize, i.e. after the graphics device exists.
        try
        {
            renderer = new ImGuiRenderer(Engine.Instance);
            renderer.RebuildFontAtlas();
        }
        catch (DllNotFoundException)
        {
            renderer = null;
        }
    }

    public static int EditorPlayerIndex => Math.Clamp(FortEntrance.Instance.Settings.EditorPlayerIndex, 0, 3);

    private static Player EditorPlayer(Level level) =>
        level.Players.OfType<Player>().FirstOrDefault(x => x.PlayerIndex == EditorPlayerIndex);

    // SaveData.Save persists every mod's settings (FortRise calls SaveSettings from it)
    public static void SaveSettings() => SaveData.Instance?.Save();

    public static void SelectArcher(int index, int type)
    {
        var settings = FortEntrance.Instance.Settings;
        settings.EditorArcherIndex = index;
        settings.EditorArcherType = type;
        SaveSettings();
        pendingReopen = true;
    }

    // Space (from the editor round logic) flips playground <> editor
    public static void RequestPlaygroundToggle()
    {
        if (editorMode == EditorMode.Live)
            pendingPlaygroundToggle = true;
    }

    // the editor session gets its own round logic instead of the game's LevelTestRoundLogic
    private static void GetRoundLogic_Postfix(Session session, ref RoundLogic __result)
    {
        if (session == editorSession)
            __result = new ArcherEditorRoundLogic(session);
    }

    // true = the window counts as focused while the editor is up
    private static void IsActive_Postfix(ref bool __result)
    {
        if (setupLevel != null)
            __result = true;
    }

    private static bool Keyboard_Prefix(ref bool __result)
    {
        if (!blockGameKeyboard)
            return true;

        __result = false;
        return false;
    }

    private static void IsMouseVisible_Prefix(ref bool value)
    {
        if (setupLevel != null || warningShown)
            value = true;
    }

    // false skips the original Rumble while the editor level is up
    private static bool Rumble_Prefix() => setupLevel == null;

    private static void TFGame_Update_Postfix() => HandleHotReload();

    public static void Unload()
    {
        harmony?.UnpatchAll();
    }

    public static void HandleHotReload()
    {
        HandleCrashReopen();

        // the session ended normally once we are back on the main menu
        if (CrashGuard.ShouldCloseOnMenu && Engine.Instance.Scene is MainMenu)
            CrashGuard.MarkClean();

        var level = Engine.Instance.Scene as Level;
        var session = level?.Session;

        // leaving the editor level (or restarting it) drops the editor state and the cursor
        if (setupLevel != null && level != setupLevel)
        {
            setupLevel = null;
            ArcherEditorMod.Source.Features.Taunt.TauntFeature.ForceEnabled = false;
            Engine.Instance.IsMouseVisible = false;
        }

        // Only the editor's own session gets the tooling, never regular matches or other level tests
        if (session != null && setupLevel == null && session == editorSession)
        {
            var main = EditorPlayer(level);
            if (main != null) // players spawn a moment after the level is created
            {
                setupLevel = level;
                openingEditor = false;
                CrashGuard.EditorLevelSeen();
                ArcherEditorMod.Source.Features.Taunt.TauntFeature.ForceEnabled = true;

                // like the game's level editor: show the cursor once when entering, hide it when leaving
                Engine.Instance.IsMouseVisible = true;

                if (editorMode == EditorMode.Unloaded)
                {
                    // no preview: the level is only a backdrop for the windows, nothing in it is visible or playable
                    foreach (var any in level.Players.OfType<Player>().ToList())
                    {
                        any.Visible = false;
                        any.Active = false;
                        any.Collidable = false;
                    }

                    UnloadedEditorWindow.Open = true;
                }
                else
                {
                    // only the selected archer (player 1) is shown, whatever else the session spawned
                    foreach (var other in level.Players.OfType<Player>().Where(x => x.PlayerIndex != EditorPlayerIndex).ToList())
                        other.RemoveSelf();

                    CreateMocks(session, main);
                    RunValidation(main.ArcherData);
                }
            }
        }

        if (pendingReopen)
        {
            // restart the session outside of the ImGui frame so the new archer / player index is spawned
            pendingReopen = false;
            rollcall = rollcallPreview = rollcallNotJoinedPreview = null;
            resultsPortraitWin = resultsPortraitLose = null;
            if (pendingMode.HasValue)
            {
                editorMode = pendingMode.Value;
                pendingMode = null;
            }
            OpenEditor(editorMode);
            return;
        }

        if (setupLevel == null || level != setupLevel) return;

        // the unloaded archer mode has no preview, so none of the preview logic below applies
        if (editorMode == EditorMode.Unloaded) return;

        HandleRollcallExit();
        HandleMockClick();

        if (pendingPlaygroundToggle)
        {
            pendingPlaygroundToggle = false;
            editorPlayground = !editorPlayground;
            HotRefresh();
        }

        if (MInput.Keyboard.Pressed(Keys.T))
        {
            ToggleRollcall(session);
        }

        if (MInput.Keyboard.Pressed((Keys)Keys.F))
        {
            TogglePortraits(session);
        }

        if(rollcallPreview){
            var input = DynamicData.For(rollcallPreview).Get<PlayerInput>("input");
            var control = DynamicData.For(rollcallPreview).Get<Image>("controlIcon");
            if(control != null){
                control.Visible = false;
            }
        }
        if(rollcallNotJoinedPreview){
              var control = DynamicData.For(rollcallNotJoinedPreview).Get<Image>("controlIcon");
            if(control != null){
                control.Visible = false;
            }
        }

        if (MInput.Keyboard.Pressed((Keys)Keys.R))
        {
            HotRefresh();
        }
    }
}
