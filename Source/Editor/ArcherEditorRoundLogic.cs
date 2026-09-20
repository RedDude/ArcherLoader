using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Monocle;
using TowerFall;

/// <summary>
/// Round logic of the archer editor session. It replaces the game's LevelTestRoundLogic, whose Space key jumps into
/// the level editor and whose F1/F2 skip or reroll the level: here only the archer being edited is spawned and
/// Space flips between the editor (mocks) and the playground (a playable archer).
/// </summary>
public class ArcherEditorRoundLogic : RoundLogic
{
    public ArcherEditorRoundLogic(Session session)
        : base(session, canHaveMiasma: false)
    {
    }

    public override void OnLevelLoadFinish()
    {
        // spawn like the game's level test does, but only the selected player
        var positions = Session.CurrentLevel.GetXMLPositions("PlayerSpawn");
        if (positions.Count == 0)
        {
            positions.AddRange(Session.CurrentLevel.GetXMLPositions("TeamSpawnA"));
            positions.AddRange(Session.CurrentLevel.GetXMLPositions("TeamSpawnB"));
        }

        var spawn = positions.Count > 0 ? positions[0] : new Vector2(160, 120);
        var player = new Player(ArcherEditorScreen.EditorPlayerIndex, spawn + Vector2.UnitY * 2f,
            Session.TestTeam, Session.TestTeam, PlayerInventory.Default, Session.TestHatState,
            frozen: false, flash: false, indicator: false);
        Session.CurrentLevel.Add(player);
        Alarm.Set(player, 30, player.RemoveIndicator);

        Session.StartRound();
    }

    public override void OnUpdate()
    {
        if (MInput.Keyboard.Pressed(Keys.Space))
            ArcherEditorScreen.RequestPlaygroundToggle();
    }
}
