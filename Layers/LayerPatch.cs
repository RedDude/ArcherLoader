using Monocle;
using MonoMod.Utils;
using TowerFall;

namespace ArcherLoaderMod.Layers
{
    public class LayerPatch
    {
        public static void Load()
        {
            On.TowerFall.Player.Added += OnPlayerOnAdded;
        }
        
        public static void Unload()
        {
            On.TowerFall.Player.Added -= OnPlayerOnAdded;
        }
        private static void OnPlayerOnAdded(On.TowerFall.Player.orig_Added orig, Player self)
        {
            orig(self);
            
            var headSprite = DynamicData.For(self).Get<Sprite<string>>("headSprite");
            var layer = new LayerSpriteComponent(true, true);
            var headIndex = 0; 
            for (var i = 0; i < self.Components.Count; i++)
            {
                if (self.Components[i] == headSprite)
                {
                    headIndex = i;
                }
            }
            
            var layerHeadSprite = new LayerHeadSpriteComponent(true, true);
            self.Add(layerHeadSprite);
            self.Components.Remove(layerHeadSprite);
            self.Components.Insert(headIndex+1, layerHeadSprite);
            
            self.Add(layer);
            self.Components.Remove(layer);
            self.Components.Insert(headIndex, layer);
            
           
           
        }
        
        // public static void PassThroughTeam_patch(orig_Player_PlayerOnPlayer orig, Player a, Player b)
        // {
        //     var matchVariants = a.Level.Session.MatchSettings.Variants;
        //     if (matchVariants.GetCustomVariant("PassThroughTeam")[a.PlayerIndex] && a.Allegiance == b.Allegiance && a.Allegiance != Allegiance.Neutral)
        //     {
        //         return;
        //     }
        //
        //     orig(a, b);
        // }
        
    }
}
