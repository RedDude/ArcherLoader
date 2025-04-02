using Microsoft.Xna.Framework;
using Monocle;
using TowerFall;

public class MutePlayerComponent : Component
{
    private Player player;

    public MutePlayerComponent(Player player, bool active,
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

    // public override void Render()
    // {
    // }
}


