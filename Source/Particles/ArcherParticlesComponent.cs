using System.Collections.Generic;
using System.Reflection;
using Microsoft.Xna.Framework;
using Monocle;
using TowerFall;

namespace ArcherLoaderMod.Particles
{
    public class ArcherParticlesComponent : Component
    {
        private ParticleType particles;
        
        private bool started;
        
        private ParticlesInfo info;
        private static FieldInfo canPadParticlesField;
        private bool canPadParticles;

        private static Dictionary<ParticlesInfo, ParticleType> cache = new();
        
        public ArcherParticlesComponent(ParticlesInfo info, bool active, bool visible) : base(active, visible)
        {
            this.info = info;

            Create(info);
        }

        private void Create(ParticlesInfo info)
        {
            this.info = info;
            if (!cache.TryGetValue(info, out particles))
            {
                particles = new ParticleType
                {
                    Source = TFGame.Atlas[info.Source],
                    Color = info.Color,
                    Color2 = info.Color2,
                    ColorSwitch = info.ColorSwitch,
                    ColorSwitchLoop = info.ColorSwitchLoop,
                    Speed = info.Speed,
                    SpeedRange = info.SpeedRange,
                    SpeedMultiplier = info.SpeedMultiplier,
                    Acceleration = info.Acceleration,
                    Direction = info.Direction,
                    DirectionRange = info.DirectionRange,
                    Life = info.Life,
                    LifeRange = info.LifeRange,
                    Size = info.Size,
                    SizeRange = info.SizeRange,
                    Rotated = info.Rotated,
                    RandomRotate = info.RandomRotate,
                    ScaleOut = info.ScaleOut,

                    // Source = TFGame.Atlas["fireParticle"],
                    // ColorSwitch = 12,
                    // ColorSwitchLoop = false,
                    // Size = 0.5f,
                    // SizeRange = 0.25f,
                    // Speed = 0.14f,
                    // SpeedRange = 0.1f,
                    // Direction = -(float)Math.PI / 2f,
                    // DirectionRange = (float)Math.PI / 6f,
                    // Life = 28,
                    // LifeRange = 10,
                    // ScaleOut = true,
                    // Color = (Particles.Fire.Color = Calc.HexToColor("FCF353")),
                    // Color2 = (Particles.Fire.Color2 = Calc.HexToColor("F83800"))
                };
                cache[info] = particles;
            }
            
            started = info.StartDelay == 0;
        }

        // public override void Added()
        // {
        //     var player = ((Player) Parent);
        //     if(player.Allegiance == Allegiance.Neutral)
        //     {
        //         Visible = false;
        //         Parent.Remove(this);
        //         return;
        //     }
        //     
        //     var spriteDataBody = TFGame.SpriteData.GetXML(player.ArcherData.Sprites.Body);
        //     var spriteDataBow = TFGame.SpriteData.GetXML(player.ArcherData.Sprites.Bow);
        //     var spriteDataHeadNoHat = TFGame.SpriteData.GetXML(player.ArcherData.Sprites.HeadNoHat);
        //     var spriteDataHeadCrown = TFGame.SpriteData.GetXML(player.ArcherData.Sprites.HeadCrown);
        //     var spriteDataHeadNormal = TFGame.SpriteData.GetXML(player.ArcherData.Sprites.HeadNormal);
        //
        //     CheckSprite(spriteDataBody, ref bodyOutline, ref originalBody);
        //     CheckSprite(spriteDataHeadNormal,ref headOutline, ref originalHead);
        //     CheckSprite(spriteDataHeadNoHat, ref headOutlineNotHat, ref originalNotHat);
        //     CheckSprite(spriteDataHeadCrown, ref headOutlineCrown, ref originalHeadCrown);
        //     CheckSprite(spriteDataBow, ref bowOutline, ref originalBow);
        //     
        //     bodySprite = DynamicData.For(player).Get<Sprite<string>>("bodySprite");
        //     headSprite = DynamicData.For(player).Get<Sprite<string>>("headSprite");
        //     bowSprite = DynamicData.For(player).Get<Sprite<string>>("bowSprite");
        // }

        public override void Update()
        {
            var player = ((Player) Parent);
            if (!started & info.StartDelay > 0)
            {
                if(!player.Level.OnInterval(info.StartDelay))
                {
                    return;
                }
                
                started = true;
            }

            if(!player.Level.OnInterval(info.Interval))
            {
                return;
            }
            
            if(player.Invisible && !info.IsOnInvisible)
            {
                return;
            }

            if (!info.IsOnGround && player.OnGround)
            {
                return;
            }
            if (!info.IsOnAir && !player.OnGround)
            {
                return;
            }
            if (!info.IsAiming && player.Aiming)
            {
                return;
            }
         
            switch (player.Allegiance)
            {
                case Allegiance.Neutral when !info.IsNeutral:
                case Allegiance.Blue when !info.IsTeamBlue:
                case Allegiance.Red when !info.IsTeamRed:
                    return;
            }
            //
            // switch (player.State)
            // {
            //     case Player.PlayerStates.Ducking when !info.IsDucking:
            //     case Player.PlayerStates.Dodging when !info.IsDodging:
            //     case Player.PlayerStates.LedgeGrab when !info.IsLedgeGrab:
            //     case Player.PlayerStates.Normal when !info.IsNormal:
            //     case Player.PlayerStates.Dying when !info.IsDying:
            //         return;
            // }

            var duckingOffset = Vector2.Zero;
            var withHatOffset = Vector2.Zero;
            var withCrownOffset = Vector2.Zero;
            
            var facing = player.Facing;

            switch (player.HatState)
            {
                //     case Player.HatStates.Normal when !info.IsHat:
                //     case Player.HatStates.Crown when !info.IsCrown:
                //     case Player.HatStates.NoHat when !info.IsNotHat:
                //         return;
                case Player.HatStates.Normal:
                    withHatOffset = info.HatOffset;
                    break;
                case Player.HatStates.Crown:
                    withCrownOffset = info.CrownOffset;
                    break;
            }
            //
            // switch (player.State)
            // {
            //     case Player.PlayerStates.Ducking when !info.IsDucking:
            //     case Player.PlayerStates.Dodging when !info.IsDodging:
            //     case Player.PlayerStates.LedgeGrab when !info.IsLedgeGrab:
            //     case Player.PlayerStates.Normal when !info.IsNormal:
            //     case Player.PlayerStates.Dying when !info.IsDying:
            //         return;
            // }
            
            if (player.State == Player.PlayerStates.Ducking)
            {
                duckingOffset = info.DuckingOffset;
            }

            var actionsOffsets = new Vector2(
                info.Position.X + duckingOffset.X + info.Position.X + withHatOffset.X + withCrownOffset.X,
                info.Position.Y + duckingOffset.Y + info.Position.Y + withHatOffset.Y + withCrownOffset.Y);
            var positionEntity = new Vector2(
                player.Position.X + (facing == Facing.Right ? actionsOffsets.X : actionsOffsets.X * -1),
                player.Position.Y + actionsOffsets.Y);

            if (info.ReplaceJump || info.OnJump)
            {
                if (player.Speed.Y < -3.2)
                {
                    if (info.ReplaceJump)
                    {
                        // if(canPadParticlesField == null)
                        //     canPadParticlesField = typeof(SpriteData).GetField("canPadParticles",
                        //         BindingFlags.NonPublic | BindingFlags.Instance);
                        // canPadParticlesField?.SetValue(player, false);
                        //
                        // if (canPadParticles && player.Level.OnInterval(1))
                        if (info.Foreground)
                        {
                            player.Level.ParticlesFG.Emit(particles, info.Amount, positionEntity,
                                new Vector2(info.PositionRange.X, info.PositionRange.Y));
                        }
                        else
                        {
                            player.Level.Particles.Emit(particles, info.Amount, positionEntity,
                                new Vector2(info.PositionRange.X, info.PositionRange.Y));
                        }
                   
                        // player.Level.Particles.Emit(Particles.JumpPadTrail, Calc.Random.Range(this.Position, Vector2.One * info.PositionRange.Y));
                        return;
                    }

                    if (info.OnJump)
                    {
                        // if (canPadParticles && player.Level.OnInterval(1))
                        if (info.Foreground)
                        {
                            player.Level.ParticlesFG.Emit(particles, info.Amount, positionEntity,
                                new Vector2(info.PositionRange.X, info.PositionRange.Y));
                        }
                        else
                        {
                            player.Level.Particles.Emit(particles, info.Amount, positionEntity,
                                new Vector2(info.PositionRange.X, info.PositionRange.Y));
                        }

                    
                        return;
                    }
                }
                return;
            }
            // else
            // {
            //     // canPadParticles = false;
            // }
            if (info.Foreground)
            {
                player.Level.ParticlesFG.Emit(particles, info.Amount, positionEntity, new Vector2(info.PositionRange.X, info.PositionRange.Y)); 
            }
            else
            {
                player.Level.Particles.Emit(particles, info.Amount, positionEntity, new Vector2(info.PositionRange.X, info.PositionRange.Y)); 
            }

            base.Update();
        }

        public void Refresh()
        {
            Create(info);
        }
        
    }
}
