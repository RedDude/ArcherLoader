using Microsoft.Xna.Framework;

namespace ArcherLoaderMod.Particles
{
    public class ParticlesInfo
    {
        public string Source;
        public int Amount;
        public Vector2 Position;
        public Color Color;
        public Color Color2;
        public int ColorSwitch;
        public bool ColorSwitchLoop;
        public float Speed;
        public float SpeedRange;
        public float SpeedMultiplier;
        public Vector2 Acceleration;
        public float Direction;
        public float DirectionRange;
        public int Life;
        public int LifeRange;
        public float Size;
        public float SizeRange;
        public bool Rotated;
        public bool RandomRotate;
        public bool ScaleOut;
        public Vector2 PositionRange;
        public int Interval;
        public int StartDelay;
          
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
    
        public ParticlesInfo()
        {
        }

    }
}
