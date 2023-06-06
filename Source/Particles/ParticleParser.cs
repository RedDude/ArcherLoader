using System;
using System.Xml;
using Microsoft.Xna.Framework;
using Monocle;

namespace ArcherLoaderMod.Particles
{
    public class ParticleParser
    {
        public static void ParseParticles(ArcherCustomData data, XmlElement xml)
        {
            if (xml.HasChild("Particle"))
            {
                var info = HandleParticle(xml["Particle"]);
                data.ParticlesInfos.Add(info);
            }

            if (!xml.HasChild("Particles")) return;

            foreach (var o in xml["Particles"])
            {
                if (o is not XmlElement {Name: "Particle"}) continue;
                var info = HandleParticle(xml["Particle"]);
                data.ParticlesInfos.Add(info);
            }
        }

        private static ParticlesInfo HandleParticle(XmlElement xml)
        {
            if (FortEntrance.Settings.DisableParticles)
                return null;

            var particlesInfo = new ParticlesInfo
            {
                Source = xml.ChildText("Source"),
                Position = xml.ChildPosition("Position", Vector2.Zero),
                Amount = xml.ChildInt("Amount", 1),
                Color = Calc.HexToColor(xml.ChildText("Color", "FFFFFF")),
                Color2 = Calc.HexToColor(xml.ChildText("Color2", "FFFFFF")),
                ColorSwitch = xml.ChildInt("ColorSwitch", 0),
                ColorSwitchLoop = xml.ChildBool("ColorSwitchLoop", false),
                Speed = xml.ChildFloat("Speed", 0.5f),
                SpeedRange = xml.ChildFloat("SpeedRange", 0.1f),
                SpeedMultiplier = xml.ChildFloat("SpeedMultiplier", 0f),
                Acceleration = xml.ChildPosition("Acceleration", Vector2.Zero),
                Direction = xml.ChildFloat("Direction", -(float) Math.PI / 2f),
                DirectionRange = xml.ChildFloat("DirectionRange", (float) Math.PI / 6f),
                Life = xml.ChildInt("Life", 28),
                LifeRange = xml.ChildInt("LifeRange", 10),
                Size = xml.ChildFloat("Size", 0.5f),
                SizeRange = xml.ChildFloat("SizeRange", 0.1f),
                Rotated = xml.ChildBool("Rotated", false),
                RandomRotate = xml.ChildBool("RandomRotate", false),
                ScaleOut = xml.ChildBool("ScaleOut", true),
                PositionRange = xml.ChildPosition("PositionRange", new Vector2(1f, 0f)),
                Interval = xml.ChildInt("Interval", 3),
                StartDelay = xml.ChildInt("StartDelay", 0),

                IsOnInvisible = xml.ChildBool("IsOnInvisible", false),

                IsAiming = xml.ChildBool("IsAiming", true),
                IsNeutral = xml.ChildBool("IsNeutral", true),
                IsTeamBlue = xml.ChildBool("IsTeamBlue", true),
                IsTeamRed = xml.ChildBool("IsTeamRed", true),

                IsHat = xml.ChildBool("IsHat", true),
                IsNotHat = xml.ChildBool("IsNotHat", true),
                IsCrown = xml.ChildBool("IsCrown", true),

                IsOnGround = xml.ChildBool("IsOnGround", true),
                IsOnAir = xml.ChildBool("IsOnAir", true),
                IsDucking = xml.ChildBool("IsDucking", true),
                IsDodging = xml.ChildBool("IsDodging", true),
                IsLedgeGrab = xml.ChildBool("IsLedgeGrab", true),
                IsNormal = xml.ChildBool("IsNormal", true),
                IsDying = xml.ChildBool("IsDying", true),
                IsShoot = xml.ChildBool("IsShoot", true),

                DuckingOffset = xml.ChildPosition("DuckingOffset", new Vector2(0f, 0f)),
                HatOffset = xml.ChildPosition("HatOffset", new Vector2(0f, 0f)),
                CrownOffset = xml.ChildPosition("CrownOffset", new Vector2(0f, 0f)),

                OnJump = xml.ChildBool("OnJump", false),
                ReplaceJump = xml.ChildBool("ReplaceJump", false)
            };

            return particlesInfo;
        }
    }
}