using Microsoft.Xna.Framework;
using TowerFall;

/// <summary>A PlayerCorpse that stays where it was placed, so the corpse sprite/hair can be inspected.</summary>
public class MockCorpse : PlayerCorpse
{
    private readonly Vector2 anchor;

    public MockCorpse(Vector2 position, ArcherData archerData, Allegiance teamColor, Facing facing, int playerIndex)
        : base(position, archerData, teamColor, facing, playerIndex, -1)
    {
        anchor = position;
    }

    public override void Update()
    {
        base.Update();
        Position = anchor;
        Speed = Vector2.Zero;
    }
}
