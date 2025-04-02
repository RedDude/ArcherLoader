using System;
using System.Collections.Generic;
using ArcherLoaderMod;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Monocle;
using MonoMod.Utils;
using TowerFall;
using TowerFall.Editor;
using Level = TowerFall.Level;

public static class ArcherEditor
{
    // public static void Init(){
    //         MethodInfo targetMethod = typeof(MockPlayer).GetMethod("Update", new[] { typeof(string) });
    //         MethodInfo hookMethod = typeof(ContentLoaderPatcher).GetMethod(nameof(AtlasGetItemHook), BindingFlags.Static | BindingFlags.NonPublic);
    //         _atlasGetItemHook = new Hook(targetMethod, hookMethod);
    // }
    public static List<MockPlayer> allCurrentMocks = new List<MockPlayer>();

    private static EditorUI lastMoused;
    public static Allegiance editorAllegiance = Allegiance.Neutral;
    private static ArcherLoader.Editor.OverlayTextButton editorAllegianceButton;
    private static RollcallElement rollcall;
    private static RollcallElement rollcallPreview;
    private static RollcallElement rollcallNotJoinedPreview;

    public static void Load()
    {
        On.TowerFall.RollcallElement.EnterJoined += OnForceStart;
        // On.TowerFall.RollcallElement.HandleControlChange += OnHandleControlChange;
        On.TowerFall.RollcallElement.NotJoinedUpdate += OnNotJoinedUpdate;
        On.TowerFall.RollcallElement.JoinedUpdate += OnJoinedUpdate;
        On.TowerFall.RollcallElement.Render += OnRender;
        
        
    }

    private static void OnRender(On.TowerFall.RollcallElement.orig_Render orig, RollcallElement self)
    {
       if(rollcallPreview != null){
            DynamicData.For(rollcallPreview).Set("input", null);
       }
       if(rollcallNotJoinedPreview != null){
            DynamicData.For(rollcallNotJoinedPreview).Set("input", null);
       }
       orig(self);
    }

    public static void Unload()
    {
        On.TowerFall.RollcallElement.EnterJoined -= OnForceStart;
        On.TowerFall.RollcallElement.NotJoinedUpdate -= OnNotJoinedUpdate;
         On.TowerFall.RollcallElement.JoinedUpdate -= OnJoinedUpdate;
         On.TowerFall.RollcallElement.Render -= OnRender;
    }

    private static int OnJoinedUpdate(On.TowerFall.RollcallElement.orig_JoinedUpdate orig, RollcallElement self)
    {
        return rollcallPreview != null ? 1 : orig(self);
    }

    private static int OnNotJoinedUpdate(On.TowerFall.RollcallElement.orig_NotJoinedUpdate orig, RollcallElement self)
    {
        return rollcallNotJoinedPreview != null ? 0 : orig(self);
    }

    private static void OnForceStart(On.TowerFall.RollcallElement.orig_EnterJoined orig, RollcallElement self)
    {
        if (MainMenu.RollcallMode < 0)
        {
            if(rollcall){
                rollcall.RemoveSelf();
                HotRefresh();
                return;
            }
        }

        orig(self);
    }

    public static void HandleHotReload()
    {

        if (FortEntrance.Settings.QuickStart) {
            var scene = (Engine.Instance.Scene as Level);
            if(scene != null)
                UpdateScene(scene);
        };


        var session = (Engine.Instance.Scene as Level)?.Session;
        
        // OverlayIconRedo
        if(!editorAllegianceButton && session != null){

            Engine.Instance.IsMouseVisible = true;


            var p = (Player)session.CurrentLevel.Players[0];


                var color = editorAllegiance == Allegiance.Neutral 
                    ? Color.White : editorAllegiance == Allegiance.Blue 
                    ? Color.Blue : Color.Red;
            editorAllegianceButton = new ArcherLoader.Editor.OverlayTextButton(Vector2.One * 30, editorAllegiance.ToString().ToUpper(), (button) =>
            {
                editorAllegiance = editorAllegiance == Allegiance.Neutral 
                    ? Allegiance.Blue : editorAllegiance == Allegiance.Blue 
                    ? Allegiance.Red : Allegiance.Neutral;
                
                var color = editorAllegiance == Allegiance.Neutral 
                    ? Color.White : editorAllegiance == Allegiance.Blue 
                    ? Color.Blue : Color.Red;
                button.text = editorAllegiance.ToString().ToUpper();
                button.Color = color;
                HotRefresh();
            }, () => true);
            editorAllegianceButton.Color = color;
            session.CurrentLevel.Add(editorAllegianceButton);

            CreateMocks(session, p);
        }
            

        if (MInput.Keyboard.Pressed((Keys)Keys.T))
        {
            var scene = (Engine.Instance.Scene as Level);
            if(!scene.Layers.ContainsKey(-1)){
                scene.Layers.Add(-1, new Monocle.Layer());
            }
            
            allCurrentMocks.ForEach(m => m.RemoveSelf());
            allCurrentMocks.Clear();
            var p = (Player)session.CurrentLevel.Players[0];
            p.Visible = false;
            p.Active = false;
            MainMenu.RollcallMode = (MainMenu.RollcallModes)(-1);
            TFGame.Players[0] = false;
            rollcall = new RollcallElement(0);
            rollcall.Position = new Vector2(200, 100);
            rollcall.LayerIndex = 3;
            // DynamicData.For(rollcall).Set("MainMenu", new MainMenu(MainMenu.MenuState.Rollcall));
            session.CurrentLevel.Add(rollcall);

            // var rollcallNotJoined = new RollcallElement(0);
            // rollcallNotJoined.Position = new Vector2(100, 100);
            // rollcallNotJoined.LayerIndex = 3;
            // // DynamicData.For(rollcall).Set("MainMenu", new MainMenu(MainMenu.MenuState.Rollcall));
            // session.CurrentLevel.Add(rollcallNotJoined);
        }
           
        if (MInput.Keyboard.Pressed((Keys)Keys.F))
        {
            var scene = (Engine.Instance.Scene as Level);
            if(!scene.Layers.ContainsKey(-1)){
                scene.Layers.Add(-1, new Monocle.Layer());
            }
            
            MainMenu.RollcallMode = (MainMenu.RollcallModes)(-2);
            rollcallPreview = new RollcallElement(0);
            rollcallPreview.Position = new Vector2(200, 100);
            rollcallPreview.LayerIndex = 3;
            session.CurrentLevel.Add(rollcallPreview);
            // DynamicData.For(rollcallPreview).Set("input", null);
            // DynamicData.For(rollcallPreview).Set("input", new );

            TFGame.Players[0] = false;
            rollcallNotJoinedPreview = new RollcallElement(0);
            TFGame.Players[0] = true;
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
          
            // var altButton = DynamicData.For(rollcallNotJoinedPreview).Get<Subtexture>("altButton");
            // if(altButton != null){
            //     altButton.Visible = false;
            // }

            
        }

        if (MInput.Keyboard.Pressed((Keys)Keys.R))
        {
            HotRefresh();
        }
    }

    private static bool HotRefresh()
    {
        var session = (Engine.Instance.Scene as Level)?.Session;
        CommandList.ReloadArcher(null);

        if (session == null)
            return false;

        var positions = new Dictionary<int, Vector2>();
        // foreach (var currentLevelPlayer in session.CurrentLevel.Players)
        // {
        // var p = (Player)currentLevelPlayer;
        var p = (Player)session.CurrentLevel.Players[0];
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
                var player = new Player(p.PlayerIndex, p.Position, editorAllegiance, editorAllegiance, session.GetPlayerInventory(p.PlayerIndex), p.HatState, frozen: false, flash: false, indicator: false);
                session.CurrentLevel.Add(player);

                allCurrentMocks.ForEach(m => m.RemoveSelf());
                allCurrentMocks.Clear();

                if(rollcallPreview)
                    rollcallPreview.RemoveSelf();
                if(rollcallNotJoinedPreview)
                    rollcallNotJoinedPreview.RemoveSelf();

                CreateMocks(session, player);
            }
        }
        catch (Exception e)
        {
        }
        // }

        if (MInput.Keyboard.Check((Keys)Keys.RightShift))
        {
            var matchSettings = new MatchSettings(GameData.VersusTowers[0].GetLevelSystem(), Modes.LevelTest,
                MatchSettings.MatchLengths.Standard);
            // matchSettings.Variants.GetCustomVariant("ReaperChalice").Value = true;
            (matchSettings.LevelSystem as VersusLevelSystem).StartOnLevel(0);
            var newSession = new Session(matchSettings);
VersusPlayerMatchResults
            void OnSessionOnStartRound(On.TowerFall.Session.orig_OnUpdate round, Session self1)
            {
                round(self1);

                try
                {
                    foreach (var currentLevelPlayer in newSession.CurrentLevel.Players)
                    {
                        var p = (Player)currentLevelPlayer;
                        if (positions.ContainsKey(p.PlayerIndex))
                            currentLevelPlayer.Position = positions[p.PlayerIndex];
                    }
                    On.TowerFall.Session.OnUpdate -= OnSessionOnStartRound;
                }
                catch (Exception e)
                {
                }
            }

            On.TowerFall.Session.OnUpdate += OnSessionOnStartRound;
            newSession.StartGame();

        }

        return true;
    }

    private static void CreateMocks(Session session, Player p)
    {
        
        var pPosition = new Vector2(140, 192);
        p.Position = pPosition;

        p.Visible = false;
        p.Active = false;

        var mockPosition = p.Position;
        mockPosition.X = 160;
        mockPosition.Y = 82;
        CreateMockPlayer(p, mockPosition, session, mock =>
        {
            mock.Position = mockPosition;
            mock.fakeInput.MoveX = -1;
            mock.UpdateInput();
        });

        var mockPositionEdge = p.Position;
        mockPositionEdge.X = 115;
        mockPositionEdge.Y = 65;
        CreateMockPlayer(p, mockPositionEdge, session, mock =>
        {
            // mock.Position = mockPositionEdge;
            mock.fakeInput.MoveX = -1;
            mock.UpdateInput();
        });

        var mockPosition2 = p.Position;
        mockPosition2.X = 260;
        mockPosition2.Y = 92;
        CreateMockPlayer(p, mockPosition2, session, mock =>
        {
            mock.Position = mockPosition2;
            if (mock.myBodySprite.CurrentAnimID != "run")
            {
                mock.myBodySprite.Play("run");
            }
            mock.myBodySprite.CurrentFrame = 2;
            mock.myBodySprite.Rate = 0;
        });

        var mockPosition3 = p.Position;
        mockPosition3.X = 60;
        mockPosition3.Y = 92;
        CreateMockPlayer(p, mockPosition3, session, mock =>
        {
            // mock.Position = mockPosition3;
            mock.fakeInput.MoveY = 1;
            mock.UpdateInput();
        });

        var mockPosition4 = p.Position;
        mockPosition4.X = 60;
        mockPosition4.Y = 192;
        CreateMockPlayer(p, mockPosition4, session, mock =>
        {
            // mock.Position = mockPosition3;
            mock.fakeInput.JumpPressed = true;
            mock.UpdateInput();
        });

        var mockPosition5 = p.Position;
        mockPosition5.X = 260;
        mockPosition5.Y = 192;
        CreateMockPlayer(p, mockPosition5, session, mock =>
        {
            mock.Arrows.SetMaxArrows(1);
            // mock.Position = mockPosition3;
            mock.fakeInput.ShootCheck = true;
            mock.fakeInput.ShootPressed = true;
            mock.UpdateInput();
        });
    }

    private static void CreateMockPlayer( Player p, Vector2 mockPosition, Session session, Action<MockPlayer> callback)
    {
        var mockPlayer = new MockPlayer(callback, p.PlayerIndex, mockPosition, p.Allegiance, p.TeamColor, session.GetPlayerInventory(p.PlayerIndex), p.HatState, frozen: false, flash: false, indicator: false);
        session.CurrentLevel.Add(mockPlayer);
        allCurrentMocks.Add(mockPlayer);
    }

    public static void HandleQuickStart()
    {
        if (!FortEntrance.Settings.QuickStart) return;
        var once = false;

        void OnMainMenuOnUpdate(On.TowerFall.MainMenu.orig_Update orig, MainMenu self)
        {
            orig(self);

            //quick portrait
            if (self.State == MainMenu.MenuState.Loading) return;

            if (once) return;
            once = true;

            for (var i = 0; i < TFGame.PlayerInputs.Length; i++)
            {
                TFGame.Players[i] = TFGame.PlayerInputs[i] != null;
            }

            var player1CharacterIndex = FortEntrance.Settings.Player1CharacterIndex;
            if (player1CharacterIndex > -1)
                TFGame.Characters[0] = player1CharacterIndex >= ArcherData.Archers.Length
                    ? ArcherData.Archers.Length - 1
                    : player1CharacterIndex;

            var player2CharacterIndex = FortEntrance.Settings.Player2CharacterIndex;
            if (player2CharacterIndex > -1)
                TFGame.Characters[1] = player2CharacterIndex >= ArcherData.Archers.Length
                    ? ArcherData.Archers.Length - 1
                    : player2CharacterIndex;

            var player3CharacterIndex = FortEntrance.Settings.Player3CharacterIndex;
            if (player3CharacterIndex > -1)
                TFGame.Characters[2] = player3CharacterIndex >= ArcherData.Archers.Length
                    ? ArcherData.Archers.Length - 1
                    : player3CharacterIndex;


            // TFGame.Characters[1] = 2;

            // self.State = MainMenu.MenuState.Rollcall;
            // return;

            var matchSettings = new MatchSettings(GameData.VersusTowers[0].GetLevelSystem(), Modes.LevelTest,
                MatchSettings.MatchLengths.Standard);
            // matchSettings.Mode = ModRegisters.GameModeType<ArcherEditorMode>();
            (matchSettings.LevelSystem as VersusLevelSystem).StartOnLevel(0);
            var session = new Session(matchSettings);
            session.StartGame();

            // var matchSettings = new MatchSettings(GameData.VersusTowers[GameData.VersusTowers.Count-1].GetLevelSystem(), Modes.LevelTest,
            //     MatchSettings.MatchLengths.Standard);
            // matchSettings.Variants.GetCustomVariant("ReaperChalice").Value = true;

            // (matchSettings.LevelSystem as VersusLevelSystem).StartOnLevel(-1);
            // new Session(matchSettings).StartGame();

            On.TowerFall.MainMenu.Update -= OnMainMenuOnUpdate;
        }

        On.TowerFall.MainMenu.Update += OnMainMenuOnUpdate;
    }


    public static void UpdateScene(Scene scene)
    {

      EditorUI editorUi1 = scene.CollideFirst(MInput.Mouse.Position, GameTags.EditorUI) as EditorUI;
      Vector2 localPosition = Vector2.Zero;
      if ((bool) (Entity) editorUi1)
      {
        localPosition = MInput.Mouse.Position - editorUi1.Position;
        if (editorUi1 is LayerUI)
          localPosition /= 2f;
      }
      var lastMoused = ArcherEditor.lastMoused;

      if (editorUi1 != lastMoused)
      {
        if (lastMoused != null)
        {
          ++lastMoused.Depth;
          lastMoused.Hovered = false;
          lastMoused.OnMouseLeave();
        }
        if (editorUi1 != null)
        {
          --editorUi1.Depth;
          editorUi1.Hovered = true;
          editorUi1.OnMouseEnter();
        }
      }
      ArcherEditor.lastMoused = editorUi1;
      if (editorUi1 != null)
      {
        editorUi1.OnMouseOver(localPosition);
        if (!editorUi1.RightClicked)
        {
          if (MInput.Mouse.LeftPressed)
          {
            // if (this.FocusedTextBox != null && this.FocusedTextBox != editorUi1)
            // {
            //   this.FocusedTextBox.UnFocusTextBox();
            //   this.FocusedTextBox = (EditorUI) null;
            // }
            editorUi1.Clicked = true;
            editorUi1.OnMouseClick(localPosition);
          }
          else if (MInput.Mouse.LeftReleased)
          {
            editorUi1.Clicked = false;
            editorUi1.OnMouseUp(localPosition);
          }
        }
        if (!editorUi1.Clicked)
        {
          if (MInput.Mouse.RightPressed)
          {
            // if (this.FocusedTextBox != null && this.FocusedTextBox != editorUi1)
            // {
            //   this.FocusedTextBox.UnFocusTextBox();
            //   this.FocusedTextBox = (EditorUI) null;
            // }
            editorUi1.RightClicked = true;
            editorUi1.OnMouseRightClick(localPosition);
          }
          else if (MInput.Mouse.RightReleased)
          {
            editorUi1.RightClicked = false;
            editorUi1.OnMouseRightUp(localPosition);
          }
        }
      }
    //   else if (this.FocusedTextBox != null && (MInput.Mouse.LeftPressed || MInput.Mouse.RightPressed))
    //   {
    //     this.FocusedTextBox.UnFocusTextBox();
    //     this.FocusedTextBox = (EditorUI) null;
    //   }
      if (MInput.Mouse.LeftReleased)
      {
        foreach (EditorUI editorUi2 in scene[GameTags.EditorUI])
        {
          if (editorUi2 != editorUi1)
            editorUi2.Clicked = false;
        }
      }
      if (MInput.Mouse.RightReleased)
      {
        foreach (EditorUI editorUi2 in scene[GameTags.EditorUI])
        {
          if (editorUi2 != editorUi1)
            editorUi2.RightClicked = false;
        }
      }
    //   base.Update();
    }

}