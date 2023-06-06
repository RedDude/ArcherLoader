using Monocle;
using MonoMod.Utils;
using TowerFall;

namespace ArcherLoaderMod.Layers
{
    public class LayerPatch
    {
        
        private static bool enabled = false;
        private static int headIndex;
        private static Sprite<string> headSprite;
        private static Sprite<string> bodySprite;
        private static Sprite<string> bowSprite;

        public static void Load()
        {
            if(FortEntrance.Settings.DisableLayers)
                return;
            On.TowerFall.Player.Added += OnPlayerOnAdded;
            enabled = true;
        }

        public static void Unload()
        {
            if(!enabled)
                return;
            On.TowerFall.Player.Added -= OnPlayerOnAdded;
        }
        
        private static void OnPlayerOnAdded(On.TowerFall.Player.orig_Added orig, Player self)
        {
            orig(self);
            
            var exist = Mod.ArcherCustomDataDict.TryGetValue(self.ArcherData, out var archerCustomData);
            if (!exist) return;
            var layerInfos = archerCustomData.LayerInfos;
            if (layerInfos == null) return;
            
            headSprite = DynamicData.For(self).Get<Sprite<string>>("headSprite");
            bodySprite = DynamicData.For(self).Get<Sprite<string>>("bodySprite");
            bowSprite = DynamicData.For(self).Get<Sprite<string>>("bowSprite");
            headIndex = 0;
            
            for (var i = 0; i < self.Components.Count; i++)
            {
                if (self.Components[i] == headSprite)
                {
                    headIndex = i;
                }
            }
            
            foreach (var layerInfo in layerInfos)
            {
                var attachedSprite = layerInfo.AttachTo == LayerAttachType.Body ? bodySprite :
                    layerInfo.AttachTo == LayerAttachType.Head ? headSprite : bowSprite;
                
                var layer = new LayerSpriteComponent(layerInfo, attachedSprite,true, true);
                self.Add(layer);
                if (layerInfo.AttachTo == LayerAttachType.Body) continue;
                self.Components.Remove(layer);
                self.Components.Insert(headIndex+(layerInfo.AttachTo == LayerAttachType.Bow ? 2 : 1), layer);
            }
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
