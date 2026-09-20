using Microsoft.Xna.Framework;
using TowerFall;

/// <summary>A PlayerGhost that hovers in place and can't be hit, so its sprite/colors can be inspected.</summary>
public class MockGhost : PlayerGhost
{
    private readonly Vector2 anchor;

    public MockGhost(PlayerCorpse corpse) : base(corpse)
    {
        anchor = Position;
        Collidable = false;
    }

    public override void Update()
    {
        base.Update();
        Position = anchor;
        Speed = Vector2.Zero;
    }
}
