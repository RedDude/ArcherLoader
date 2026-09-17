#nullable enable
using System;
using System.Collections.Generic;
using System.Xml;
using FortRise;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Monocle;
using TowerFall;

namespace ArcherLoaderMod.Source.Features.Particles
{
    // <Particle>...</Particle> or <Particles><Particle/><Particle/>...</Particles>
    // See ParticlesInfo for the fields, all optional.
    public sealed class ParticlesFeature : IArcherFeature
    {
        private static readonly Dictionary<ArcherData, List<ParticlesInfo>> particlesByArcher = new();

        public string Name => "Particles";

        public bool Enabled => !FortEntrance.Instance.Settings.DisableParticles;

        public void Load(IModuleContext context)
        {
            context.Harmony.Patch(
                AccessTools.Method(typeof(Player), nameof(Player.Added)),
                postfix: new HarmonyMethod(typeof(ParticlesFeature), nameof(Player_Added_Postfix)));
        }

        public bool Decorate(ArcherDecoration decoration)
        {
            var xml = decoration.Xml;
            var infos = new List<ParticlesInfo>();

            if (xml.HasChild("Particle"))
                infos.Add(HandleParticle(xml["Particle"]!));

            if (xml.HasChild("Particles"))
            {
                foreach (var node in xml["Particles"]!)
                {
                    if (node is XmlElement { Name: "Particle" } particleXml)
                        infos.Add(HandleParticle(particleXml));
                }
            }

            if (infos.Count == 0)
                return false;

            particlesByArcher[decoration.ArcherData] = infos;
            return true;
        }

        private static ParticlesInfo HandleParticle(XmlElement xml)
        {
            var source = xml.ChildText("Source");
            if (string.IsNullOrEmpty(source) || !TFGame.Atlas.Contains(source))
                throw new Exception($"particle Source '{source}' not found in the atlas");

            return new ParticlesInfo
            {
                Source = source,
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
                Direction = xml.ChildFloat("Direction", -(float)Math.PI / 2f),
                DirectionRange = xml.ChildFloat("DirectionRange", (float)Math.PI / 6f),
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

                Foreground = xml.ChildBool("Foreground", false),

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
                ReplaceJump = xml.ChildBool("ReplaceJump", false),

                IsDodgeCooldown = xml.ChildBool("IsDodgeCooldown", false)
            };
        }

        private static void Player_Added_Postfix(Player __instance)
        {
            if (!particlesByArcher.TryGetValue(__instance.ArcherData, out var infos))
                return;

            foreach (var info in infos)
                __instance.Add(new ArcherParticlesComponent(info, true, true));
        }
    }
}
