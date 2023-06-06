// using Microsoft.Xna.Framework;
// using Monocle;
// using TowerFall;
//
// namespace ArcherLoaderMod
// {
//     public class PlayerTaunt : Component
//     {
//         private readonly Sprite<string> _body;
//         private Sprite<string> sprite;
//
//         public PlayerTaunt(Entity entity, Sprite<string> body, bool active, bool visible) : base(active, visible)
//         {
//             _body = body;
//             //             sprite = TFGame.SpriteData.GetSpriteString("taunt/pink");
//             entity.Add(this);
//             entity.UpdateComponentList();
//         }
//
//         public override void Update()
//         {
//             if(!sprite.Visible)
//                 return;
//             
//             if (sprite.ContainsAnimation("taunt"))
//             {
//                 sprite.Play("taunt");
//             }
//         }
//
//         public override void Render()
//         {
//             sprite.Render();
//         }
//
//         public void Stop(Player player)
//         {
//             sprite.Visible = false;
//             player.Remove(this);
//         }
//     }
// }