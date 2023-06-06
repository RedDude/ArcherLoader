using Microsoft.Xna.Framework;

namespace ArcherLoaderMod.Layers
{
    public class LayerInfo
    {
        public LayerAttachType AttachTo;
        
        public string Sprite;
        public Vector2 Position;
        public Color Color;
        public int ColorSwitch;
        public bool ColorSwitchLoop;
        public bool ToScale;
        
        public bool IsRainbowColor;
          
        public bool IsOnInvisible;
        
        public bool IsOnGround;
        public bool IsOnAir;
        
        public bool IsAiming;
        
        public bool IsNeutral;
        public bool IsTeamBlue;
        public bool IsTeamRed;
        
        public bool IsHat;
        public bool IsNotHat;
        public bool IsCrown;
        
        public bool IsDucking;
        public bool IsDodging;
        public bool IsLedgeGrab;
        public bool IsNormal;
        public bool IsDying;
        
        public bool OnJump;
        public bool ReplaceJump;
        
        public bool IsShoot;
        
        public Vector2 DuckingOffset;
        public Vector2 HatOffset;
        public Vector2 CrownOffset;
    
        public LayerInfo()
        {
        }

    }
}
