#nullable enable
using Microsoft.Xna.Framework;

namespace ArcherEditorMod.Source.Features.Layers
{
    public sealed class LayerInfo
    {
        public LayerAttachType AttachTo;

        public string Sprite = null!;
        public Vector2 Position;
        public Color Color = Color.White;
        public int ColorSwitch;
        public bool ColorSwitchLoop;
        public bool ToScale = true;

        public bool IsTeamColor;
        public bool IsColorA;
        public bool IsColorB;
        public bool IsRainbowColor;

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

        public int RainbowOffset;
        public float RainbowSpeed = 1f;
    }
}
