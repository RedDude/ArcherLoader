using ArcherLoaderMod.Layers;
using ArcherLoaderMod.Source.Layers.PortraitLayers;
using Microsoft.Xna.Framework;
using Monocle;
using TowerFall;

public class MatchResultsPortraitPreview : Entity
{
    private Player player;
    private Image portrait;
    private Sprite<string> gem;
    private static Color WinBorder = Calc.HexToColor("FFE23F");
    private static Color LoseBorder = Calc.HexToColor("896B61");

    public MatchResultsPortraitPreview(int playerIndex, bool won, Vector2 position) 
    {
      ArcherData archerData = ArcherData.Get(TFGame.Characters[playerIndex], TFGame.AltSelect[playerIndex]);
      portrait = won ? new Image(archerData.Portraits.Win) : new Image(archerData.Portraits.Lose);
      portrait.CenterOrigin();
      portrait.Position = position ;
      Add(new DrawRectangle((float) ((double) this.portrait.X - (double) this.portrait.Width / 2.0 - 1.0), (float) ((double) this.portrait.Y - (double) this.portrait.Height / 2.0 - 1.0), this.portrait.Width + 2f, this.portrait.Height + 2f, won ? WinBorder : LoseBorder));
      Add(portrait);
      gem = TFGame.MenuSpriteData.GetSpriteString(archerData.Gems.Menu);
      gem.Play(won ? "on" : "off");
      gem.Position = -Vector2.UnitY * portrait.Height / 2f;
      gem.Visible = false;
      Add(gem);
      var layers = PortraitLayersManager.CreateWonLoseLayersComponents(this, archerData);
      if(layers != null)
          PortraitLayersManager.ShowAllLayersFromType(won ? PortraitLayersAttachType.Won : PortraitLayersAttachType.Lose, layers);
    }

    // public override void Added()
    // {
    //     base.Added();
    // }

    // public override void Update()
    // {
    //     base.Update();
    // }

    // public override void Render()
    // {
    //     base.Render();
    //     Color color = Color.White; // (base.Selected ? Color.White : ((!Hovered) ? (Color.DarkGray * 0.5f) : (Color.DarkGray * 0.8f)));
    //     // Draw.TextCentered(TFGame.Font, "TEST", player.Position + new Vector2(0f, -20f), color, Vector2.One, 0f);
    //     // Draw.Line(player.Position + new Vector2(50f, 0f), player.Position + new Vector2(390f, 0f), color);
    //     // Draw.TextCentered(TFGame.Font, player.moveAxis.X.ToString() + " " +  player.moveAxis.Y.ToString(), player.Position + new Vector2(0, -10f), color * 0.6f, Vector2.One, 0f);

    //     if(player.myBodySprite){
    //         Draw.TextCentered(TFGame.Font, player.myBodySprite.CurrentAnimID.ToUpper(), player.Position + new Vector2(0f, -20f), color, Vector2.One, 0f);
    //         // Draw.Line(player.Position + new Vector2(50f, 0f), player.Position + new Vector2(390f, 0f), color);
    //         Draw.TextCentered(TFGame.Font, player.myBodySprite.CurrentFrame.ToString(), player.Position + new Vector2(0, -12f), color * 0.6f, Vector2.One, 0f);
    //     }
    // }
}
