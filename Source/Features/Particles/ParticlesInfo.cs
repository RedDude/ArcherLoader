#nullable enable
using Microsoft.Xna.Framework;

namespace ArcherLoaderMod.Source.Features.Particles
{
    public sealed class ParticlesInfo
    {
        public string Source = null!;
        public int Amount = 1;
        public Vector2 Position;
        public Color Color = Color.White;
        public Color Color2 = Color.White;
        public int ColorSwitch;
        public bool ColorSwitchLoop;
        public float Speed = 0.5f;
        public float SpeedRange = 0.1f;
        public float SpeedMultiplier;
        public Vector2 Acceleration;
        public float Direction;
        public float DirectionRange;
        public int Life = 28;
        public int LifeRange = 10;
        public float Size = 0.5f;
        public float SizeRange = 0.1f;
        public bool Rotated;
        public bool RandomRotate;
        public bool ScaleOut = true;
        public Vector2 PositionRange = new(1f, 0f);
        public int Interval = 3;
        public int StartDelay;

        public bool Foreground;

        public bool IsOnInvisible;

        public bool IsOnGround = true;
        public bool IsOnAir = true;

        public bool IsAiming = true;

        public bool IsNeutral = true;
        public bool IsTeamBlue = true;
        public bool IsTeamRed = true;

        public bool IsHat = true;
        public bool IsNotHat = true;
        public bool IsCrown = true;

        public bool IsDucking = true;
        public bool IsDodging = true;
        public bool IsLedgeGrab = true;
        public bool IsNormal = true;
        public bool IsDying = true;

        public bool OnJump;
        public bool ReplaceJump;

        public bool IsShoot = true;

        public Vector2 DuckingOffset;
        public Vector2 HatOffset;
        public Vector2 CrownOffset;
        public bool IsDodgeCooldown;
    }
}
