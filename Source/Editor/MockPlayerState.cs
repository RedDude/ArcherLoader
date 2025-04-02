using Microsoft.Xna.Framework;
using Monocle;
using TowerFall;

public class MockPlayerState : Component
{
    private MockPlayer player;

    public MockPlayerState(MockPlayer player, bool active,
        bool visible) : base(active, visible)
    {
        this.player = player;
    }

    public override void Added()
    {
        base.Added();
    }

    public override void Update()
    {
        base.Update();
    }

    public override void Render()
    {
        base.Render();
        Color color = Color.White; // (base.Selected ? Color.White : ((!Hovered) ? (Color.DarkGray * 0.5f) : (Color.DarkGray * 0.8f)));
        // Draw.TextCentered(TFGame.Font, "TEST", player.Position + new Vector2(0f, -20f), color, Vector2.One, 0f);
        // Draw.Line(player.Position + new Vector2(50f, 0f), player.Position + new Vector2(390f, 0f), color);
        // Draw.TextCentered(TFGame.Font, player.moveAxis.X.ToString() + " " +  player.moveAxis.Y.ToString(), player.Position + new Vector2(0, -10f), color * 0.6f, Vector2.One, 0f);

        if(player.myBodySprite){
            Draw.TextCentered(TFGame.Font, player.myBodySprite.CurrentAnimID.ToUpper(), player.Position + new Vector2(0f, -20f), color, Vector2.One, 0f);
            // Draw.Line(player.Position + new Vector2(50f, 0f), player.Position + new Vector2(390f, 0f), color);
            Draw.TextCentered(TFGame.Font, player.myBodySprite.CurrentFrame.ToString(), player.Position + new Vector2(0, -12f), color * 0.6f, Vector2.One, 0f);
        }
    }
}


