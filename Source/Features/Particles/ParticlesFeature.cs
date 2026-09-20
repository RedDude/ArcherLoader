#nullable enable
using System;
using System.Collections.Generic;
using System.Xml;
using FortRise;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Monocle;
using TowerFall;

namespace ArcherEditorMod.Source.Features.Particles
{
    // <Particle>...</Particle> or <Particles><Particle/><Particle/>...</Particles>
    // See ParticlesInfo for the fields, all optional.
    public sealed class ParticlesFeature : IArcherFeature
    {
        private static readonly Dictionary<ArcherData, List<ParticlesInfo>> particlesByArcher = new();

        // Particles created from the archer editor, attached on top of the archer's own ones.
        private static readonly Dictionary<ArcherData, List<ParticlesInfo>> editorParticles = new();

        // ---- Editor access ----

        public static ParticlesInfo NewParticlesInfo(string? name = null) => new()
        {
            Name = name,
            Source = "fireParticle",
            Direction = -(float)Math.PI / 2f,
            DirectionRange = (float)Math.PI / 6f
        };

        /// <summary>Puts back the archer's own and editor particles (undo); null / empty removes them.</summary>
        public static void SetLists(ArcherData archer, List<ParticlesInfo>? own, List<ParticlesInfo>? editor)
        {
            if (own == null) particlesByArcher.Remove(archer); else particlesByArcher[archer] = own;
            if (editor == null || editor.Count == 0) editorParticles.Remove(archer); else editorParticles[archer] = editor;
        }

        public static List<ParticlesInfo>? GetOwnParticles(ArcherData archer) =>
            particlesByArcher.TryGetValue(archer, out var infos) ? infos : null;

        public static IReadOnlyList<ParticlesInfo> GetEditorParticles(ArcherData archer) =>
            editorParticles.TryGetValue(archer, out var infos) ? infos : Array.Empty<ParticlesInfo>();

        // Own + editor particles, for export
        public static List<ParticlesInfo> GetAllParticles(ArcherData archer)
        {
            var all = new List<ParticlesInfo>();
            if (particlesByArcher.TryGetValue(archer, out var own)) all.AddRange(own);
            if (editorParticles.TryGetValue(archer, out var editor)) all.AddRange(editor);
            return all;
        }

        public static void RemoveOwnParticles(ArcherData archer, int index)
        {
            if (!particlesByArcher.TryGetValue(archer, out var infos) || index < 0 || index >= infos.Count)
                return;
            infos.RemoveAt(index);
            if (infos.Count == 0)
                particlesByArcher.Remove(archer);
        }

        public static void AddEditorParticles(ArcherData archer, string? name)
        {
            if (!editorParticles.TryGetValue(archer, out var infos))
                editorParticles[archer] = infos = new List<ParticlesInfo>();
            infos.Add(NewParticlesInfo(name));
        }

        public static void RemoveEditorParticles(ArcherData archer, int index)
        {
            if (!editorParticles.TryGetValue(archer, out var infos) || index < 0 || index >= infos.Count)
                return;
            infos.RemoveAt(index);
            if (infos.Count == 0)
                editorParticles.Remove(archer);
        }

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
                Name = xml.ChildText("Name", null),
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
            // the archer's own particles first, then the ones created in the editor
            var infos = GetAllParticles(__instance.ArcherData);

            foreach (var info in infos)
                __instance.Add(new ArcherParticlesComponent(info, true, true));
        }
    }
}
