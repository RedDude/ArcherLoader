using System;
using Microsoft.Xna.Framework;
using Monocle;
using MonoMod.Utils;
using TowerFall;

public class MockPlayer : Player
{
    public Action<MockPlayer> callback;
    public InputState fakeInput;
    // public Vector2 moveAxis;

    public Sprite<string> myBodySprite;

    public MockPlayer(Action<MockPlayer> callback, int playerIndex, Vector2 position, Allegiance allegiance, Allegiance teamColor, PlayerInventory inventory, HatStates hatState, bool frozen, bool flash, bool indicator) : base(playerIndex, position, allegiance, teamColor, inventory, hatState, frozen, flash, indicator)
    {
        fakeInput = new InputState();
        // newInput.MoveX = -1;
        // newInput.MoveY = -1;
        DynamicData.For(this).Set("input", fakeInput);
        Collidable = false;
        Arrows.SetMaxArrows(0);
        myBodySprite = DynamicData.For(this).Get<Sprite<string>>("bodySprite");
        // moveAxis = DynamicData.For(this).Get<Vector2>("moveAxis");
        // direction = DynamicData.For(this).Get<InputState>("input");
        this.callback = callback;
        Add(new MutePlayerComponent(this, true, true));
        this.Add(new MockPlayerState(this, true, true));
    }
    
    public void UpdateInput(){
        DynamicData.For(this).Set("input", fakeInput);
    }

    public override void Update(){
        ArrowHUD.Visible = false;
        ArrowHUD.Active = false;
        // DynamicData.For(this).Set("showCounter", '');
        callback.Invoke(this);
        // var newInput = new InputState();
        // newInput.MoveX = -1;
        // newInput.MoveY = -1;
        // DynamicData.For((this as Player)).Set("input", newInput);
        var fake = TFGame.PlayerInputs[PlayerIndex];
        TFGame.PlayerInputs[PlayerIndex] = null;
        base.Update();
        TFGame.PlayerInputs[PlayerIndex] = fake;

        ArrowHUD.Visible = false;
        ArrowHUD.Active = false;
        //  DynamicData.For(this).Set("input", newInput);
        //   base.Update();
    }
}