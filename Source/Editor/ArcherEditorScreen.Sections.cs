using System;
using System.Collections.Generic;
using System.Linq;
using ArcherEditorMod;
using ArcherEditorMod.Editor;
using ImGuiNET;
using Microsoft.Xna.Framework;
using Monocle;
using TowerFall;

/// <summary>One mock stand set to look at something specific: the mocks it spawns, and the map it spawns them on.</summary>
public sealed record MockSection(string Name, string Hint, Action<Session, Player> Build, bool PicksMap = false, bool Checker = false);

/// <summary>A mock the user creates in the Custom section.</summary>
public sealed class CustomMock
{
    public int X = 160, Y = 120;
    public bool NoGravity = true;
    public int Hat = -1;             // -1 = the editor's hat, else Player.HatStates
    public int FacingChoice;         // 0 = the editor's facing, 1 = left, 2 = right
    public string Animation = "";    // empty = whatever the game plays
    public int Frame;
    public bool Duck, Run, Taunt;
}

public static partial class ArcherEditorScreen
{
    private static MockSection[]? sections;
    private static string currentMapKey = "0:0";
    public static readonly List<CustomMock> customMocks = new();

    private static MockSection[] Sections => sections ??= new[]
    {
        new MockSection("Poses", "Standing, walking, ducking, jumping and shooting on the map.", BuildPoses),
        new MockSection("Aim (8 directions)", "Aims the bow in each of the 8 directions for a second, fires, waits, repeats.", BuildAim, Checker: true),
        new MockSection("Air (no gravity)", "Mocks that hover: jump, fall, dodge and glide poses.", BuildAir, Checker: true),
        new MockSection("Taunt", "The archer ducking and taunting: no hat, hat and crown.", BuildTaunt, Checker: true),
        new MockSection("Death", "A corpse and a ghost.", BuildDeath, Checker: true),
        new MockSection("Custom", "Mocks you create: position, gravity, hat, animation. The only section with a map choice.", BuildCustom, PicksMap: true),
    };

    private static int CurrentSectionIndex =>
        Math.Clamp(FortEntrance.Instance.Settings.MockSectionIndex, 0, Sections.Length - 1);

    private static MockSection CurrentSection => Sections[CurrentSectionIndex];

    // ---- map of a section (tower + level of the versus towers) ----

    // tower -1 = the generated checkerboard level; only a section that picks its map can have anything but the default
    private static (int Tower, int Level) MapFor(int section)
    {
        if (Sections[section].Checker)
            return (-1, 0);

        if (!Sections[section].PicksMap)
            return (0, 0);

        var settings = FortEntrance.Instance.Settings;
        var tower = section < settings.MockSectionTowers.Length ? settings.MockSectionTowers[section] : 0;
        var level = section < settings.MockSectionLevels.Length ? settings.MockSectionLevels[section] : 0;

        if (tower < 0)
            return (-1, 0);

        tower = Math.Min(tower, GameData.VersusTowers.Count - 1);
        level = Math.Clamp(level, 0, Math.Max(0, GameData.VersusTowers[tower].Levels.Count - 1));
        return (tower, level);
    }

    private static string MapKey(int section)
    {
        var (tower, level) = MapFor(section);
        return $"{tower}:{level}";
    }

    public static (int Tower, int Level) CurrentMap() => MapFor(CurrentSectionIndex);

    // The fixed mock coordinates were laid out for the default map (tower 0, level 0).
    private static bool IsDefaultMap => CurrentMap() == (0, 0);

    private static void SetMap(int section, int tower, int level)
    {
        var settings = FortEntrance.Instance.Settings;
        var size = Math.Max(Sections.Length, Math.Max(settings.MockSectionTowers.Length, settings.MockSectionLevels.Length));
        var towers = new int[size];
        var levels = new int[size];
        settings.MockSectionTowers.CopyTo(towers, 0);
        settings.MockSectionLevels.CopyTo(levels, 0);
        towers[section] = tower;
        levels[section] = level;
        settings.MockSectionTowers = towers;
        settings.MockSectionLevels = levels;
        SaveSettings();
    }

    // ---- switching ----

    private static void SelectSection(int index)
    {
        index = (index % Sections.Length + Sections.Length) % Sections.Length;
        FortEntrance.Instance.Settings.MockSectionIndex = index;
        SaveSettings();

        // a different map needs a new level; the same one only needs its mocks rebuilt
        if (MapKey(index) != currentMapKey)
            pendingReopen = true;
        else
            HotRefresh();
    }

    private static void ShowTauntSection() => SelectSection(Array.FindIndex(Sections, s => s.Name == "Taunt"));

    // "<  Section  >": the arrows step through the sections, the name opens a list to pick one
    private static void DrawSectionSelector()
    {
        var index = CurrentSectionIndex;

        if (ImGui.ArrowButton("##sectionPrev", ImGuiDir.Left))
            SelectSection(index - 1);

        ImGui.SameLine();
        ImGui.SetNextItemWidth(170);
        if (ImGui.BeginCombo("##section", Sections[index].Name))
        {
            for (var i = 0; i < Sections.Length; i++)
            {
                if (ImGui.Selectable(Sections[i].Name, i == index))
                    SelectSection(i);
            }
            ImGui.EndCombo();
        }

        ImGui.SameLine();
        if (ImGui.ArrowButton("##sectionNext", ImGuiDir.Right))
            SelectSection(index + 1);

        ImGui.TextDisabled(Sections[index].Hint);

        // ---- map: only the Custom section chooses one, the others use the default map ----
        if (!Sections[index].PicksMap)
            return;

        var (tower, level) = MapFor(index);
        var towers = GameData.VersusTowers;
        ImGui.SetNextItemWidth(220);
        if (ImGui.BeginCombo("Map", TowerLabel(tower)))
        {
            if (ImGui.Selectable(TowerLabel(-1), tower < 0))
            {
                SetMap(index, -1, 0);
                pendingReopen = true;
            }

            for (var i = 0; i < towers.Count; i++)
            {
                if (ImGui.Selectable(TowerLabel(i), i == tower))
                {
                    SetMap(index, i, 0);
                    pendingReopen = true;
                }
            }
            ImGui.EndCombo();
        }

        if (tower < 0)
        {
            ImGui.TextDisabled("A checkerboard of 2x2 tile blocks (not solid). Mocks float, one per empty block.");
            return;
        }

        var levelCount = towers[tower].Levels.Count;
        var shownLevel = level + 1;
        ImGui.SetNextItemWidth(120);
        ImGui.SliderInt("Level", ref shownLevel, 1, Math.Max(1, levelCount));
        if (ImGui.IsItemDeactivatedAfterEdit() && shownLevel - 1 != level)
        {
            SetMap(index, tower, shownLevel - 1);
            pendingReopen = true;
        }

        if (!IsDefaultMap)
            ImGui.TextDisabled("Other maps place the mocks on their spawn points.");
    }

    // the tower a session is started on; the generated level falls back to the first map if it can't be built
    private static VersusTowerData TowerFor(int tower)
    {
        if (tower >= 0)
            return GameData.VersusTowers[tower];

        try
        {
            return CheckerLevel.CreateTower();
        }
        catch (Exception e)
        {
            EditorConsole.Error("Could not build the checkerboard level, using the first map instead: " + e.Message);
            return GameData.VersusTowers[0];
        }
    }

    private static string TowerLabel(int index)
    {
        if (index < 0)
            return "Checkerboard (2x2 tiles)";

        var tower = GameData.VersusTowers[index];
        return $"{index + 1}: {tower.Theme?.Name ?? "tower"} ({tower.Levels.Count} levels)";
    }

    // ---- spawn positions ----

    // fixed coordinates on the default map, spawn points anywhere else
    private static Vector2 Slot(Session session, int index, Vector2 fixedPosition)
    {
        if (IsDefaultMap) return fixedPosition;

        // the checkerboard has one cell per mock
        if (CurrentMap().Tower < 0)
        {
            var (blockX, blockY) = CheckerLevel.Cells[index % CheckerLevel.Cells.Length];
            return CheckerLevel.CellPosition(blockX, blockY);
        }

        var spawns = session.CurrentLevel.GetXMLPositions("PlayerSpawn");
        if (spawns.Count == 0)
        {
            spawns.AddRange(session.CurrentLevel.GetXMLPositions("TeamSpawnA"));
            spawns.AddRange(session.CurrentLevel.GetXMLPositions("TeamSpawnB"));
        }

        return spawns.Count > 0 ? spawns[index % spawns.Count] : fixedPosition;
    }

    // ---- the sections ----

    private static void BuildPoses(Session session, Player p)
    {
        var walking = Slot(session, 0, new Vector2(160, 82));
        CreateMockPlayer(p, walking, session, mock =>
        {
            mock.Position = walking;
            mock.fakeInput.MoveX = FacingDir;
            mock.UpdateInput();
        });

        var edge = Slot(session, 1, new Vector2(115, 65));
        CreateMockPlayer(p, edge, session, mock =>
        {
            mock.fakeInput.MoveX = FacingDir;
            mock.UpdateInput();
        });

        var running = Slot(session, 2, new Vector2(260, 92));
        CreateMockPlayer(p, running, session, mock =>
        {
            mock.Position = running;
            if (mock.myBodySprite.CurrentAnimID != "run")
                mock.myBodySprite.Play("run");
            mock.myBodySprite.CurrentFrame = 2;
            mock.myBodySprite.Rate = 0;
        });

        var ducking = Slot(session, 3, new Vector2(60, 92));
        CreateMockPlayer(p, ducking, session, mock =>
        {
            mock.fakeInput.MoveY = 1;
            mock.UpdateInput();
        });

        var jumping = Slot(session, 4, new Vector2(60, 192));
        CreateMockPlayer(p, jumping, session, mock =>
        {
            mock.fakeInput.JumpPressed = true;
            mock.UpdateInput();
        });

        var shooting = Slot(session, 5, new Vector2(260, 192));
        CreateMockPlayer(p, shooting, session, mock =>
        {
            mock.Arrows.SetMaxArrows(1);
            mock.fakeInput.ShootCheck = true;
            mock.fakeInput.ShootPressed = true;
            mock.UpdateInput();
        });
    }

    // ---- aiming ----

    // eight directions, clockwise from up (aim axis: y grows downwards)
    private static readonly (string Name, Vector2 Axis)[] AimDirections =
    {
        ("N", new Vector2(0, -1)), ("NE", new Vector2(1, -1)), ("E", new Vector2(1, 0)), ("SE", new Vector2(1, 1)),
        ("S", new Vector2(0, 1)), ("SW", new Vector2(-1, 1)), ("W", new Vector2(-1, 0)), ("NW", new Vector2(-1, -1))
    };

    private const int AimHoldFrames = 60;   // one second aiming
    private const int AimIdleFrames = 60;   // one second idle after the shot

    // hold the bow drawn for a second, release to fire, idle a second, start over
    private static Action<MockPlayer> AimCycle(Vector2 axis)
    {
        var frame = 0;
        return mock =>
        {
            if (frame == 0)
            {
                // the arrow of the last shot is gone and a new one is in the quiver
                RemoveArrowsOf(mock);
                mock.Arrows.SetMaxArrows(1);
                if (mock.Arrows.Count == 0)
                    mock.Arrows.ForceAddArrow(ArrowTypes.Normal);
            }

            mock.fakeInput.AimAxis = axis;
            var holding = frame < AimHoldFrames;
            mock.fakeInput.ShootCheck = holding;
            mock.fakeInput.ShootPressed = frame == 0;
            mock.UpdateInput();

            frame = (frame + 1) % (AimHoldFrames + AimIdleFrames);
        };
    }

    private static void RemoveArrowsOf(MockPlayer mock)
    {
        foreach (var arrow in mock.Level[GameTags.Arrow].OfType<Arrow>().Where(a => a.Owner == mock).ToList())
            arrow.RemoveSelf();
    }

    private static void BuildAim(Session session, Player p)
    {
        for (var i = 0; i < AimDirections.Length; i++)
        {
            var position = Slot(session, i, new Vector2(40 + i * 34, 192));
            CreateMockPlayer(p, position, session, AimCycle(AimDirections[i].Axis));
        }
    }

    // no gravity: they stay where they are put, so any pose can be held in mid air
    private static void BuildAir(Session session, Player p)
    {
        var poses = new (string Animation, int Frame, float X)[]
        {
            ("jump", 0, 80), ("fall", 0, 140), ("dodge", 0, 200), ("glide", 0, 260)
        };

        foreach (var (animation, frame, x) in poses)
        {
            var position = new Vector2(x, 110);
            CreateMockPlayer(p, position, session, mock => mock.UpdateInput(), noGravity: true,
                late: mock => mock.ForceAnimation(animation, frame));
        }
    }

    // ducking with the taunt button held, in each hat state
    private static void BuildTaunt(Session session, Player p)
    {
        var hats = new[] { Player.HatStates.NoHat, Player.HatStates.Normal, Player.HatStates.Crown };
        for (var i = 0; i < hats.Length; i++)
        {
            var position = Slot(session, i, new Vector2(100 + i * 60, 192));
            CreateMockPlayer(p, position, session, mock =>
            {
                mock.fakeInput.MoveY = 1;
                mock.fakeInput.ArrowsPressed = true;
                mock.UpdateInput();
            }, hat: hats[i]);
        }
    }

    private static void BuildDeath(Session session, Player p)
    {
        var corpse = new MockCorpse(Slot(session, 0, new Vector2(200, 192)), p.ArcherData, p.TeamColor, editorFacing, p.PlayerIndex);
        session.CurrentLevel.Add(corpse);
        allCurrentCorpses.Add(corpse);

        // the ghost only reads position/facing/index from the corpse, which is never added to the level
        var ghostSource = new MockCorpse(new Vector2(200, 130), p.ArcherData, p.TeamColor, editorFacing, p.PlayerIndex);
        var ghost = new MockGhost(ghostSource);
        session.CurrentLevel.Add(ghost);
        allCurrentGhosts.Add(ghost);
    }

    private static void BuildCustom(Session session, Player p)
    {
        foreach (var custom in customMocks)
        {
            var facing = custom.FacingChoice == 1 ? Facing.Left : custom.FacingChoice == 2 ? Facing.Right : editorFacing;
            var hat = custom.Hat < 0 ? (Player.HatStates?)null : (Player.HatStates)custom.Hat;
            var animation = custom.Animation;
            var frame = custom.Frame;
            var item = custom;

            CreateMockPlayer(p, new Vector2(item.X, item.Y), session, mock =>
            {
                if (item.Duck || item.Taunt) mock.fakeInput.MoveY = 1;
                if (item.Taunt) mock.fakeInput.ArrowsPressed = true;
                if (item.Run) mock.fakeInput.MoveX = facing == Facing.Left ? -1 : 1;
                mock.UpdateInput();
            }, hat: hat, noGravity: item.NoGravity, facing: facing,
                late: animation.Length > 0 ? mock => mock.ForceAnimation(animation, frame) : null);
        }
    }

    // ---- custom mocks panel ----

    private static readonly string[] hatNames = { "Editor hat", "No hat", "Hat", "Crown" };
    private static readonly string[] facingNames = { "Editor facing", "Left", "Right" };

    private static List<string> BodyAnimations(ArcherData data)
    {
        var result = new List<string> { "" };
        if (!TFGame.SpriteData.Contains(data.Sprites.Body)) return result;

        var animations = TFGame.SpriteData.GetXML(data.Sprites.Body)["Animations"];
        if (animations == null) return result;

        foreach (System.Xml.XmlElement anim in animations.GetElementsByTagName("Anim"))
            result.Add(anim.GetAttribute("id"));
        return result;
    }

    private static void DrawCustomMocks(ArcherData data)
    {
        if (CurrentSection.Name != "Custom") return;

        ImGui.SeparatorText("Custom mocks");
        var changed = false;
        var remove = -1;
        var animations = BodyAnimations(data);

        for (var i = 0; i < customMocks.Count; i++)
        {
            var mock = customMocks[i];
            ImGui.PushID(i);
            ImGui.Text($"Mock {i + 1}");

            var position = new System.Numerics.Vector2(mock.X, mock.Y);
            ImGui.SetNextItemWidth(150);
            if (ImGui.DragFloat2("Position", ref position, 1f, 0f, 320f, "%.0f"))
            {
                mock.X = (int)position.X;
                mock.Y = (int)position.Y;
            }
            changed |= ImGui.IsItemDeactivatedAfterEdit();

            changed |= ImGui.Checkbox("No gravity", ref mock.NoGravity);
            ImGui.SameLine();
            changed |= ImGui.Checkbox("Duck", ref mock.Duck);
            ImGui.SameLine();
            changed |= ImGui.Checkbox("Run", ref mock.Run);
            ImGui.SameLine();
            changed |= ImGui.Checkbox("Taunt", ref mock.Taunt);

            var hat = mock.Hat + 1;
            ImGui.SetNextItemWidth(110);
            if (ImGui.Combo("Hat", ref hat, hatNames, hatNames.Length))
            {
                mock.Hat = hat - 1;
                changed = true;
            }

            ImGui.SetNextItemWidth(110);
            changed |= ImGui.Combo("Facing", ref mock.FacingChoice, facingNames, facingNames.Length);

            ImGui.SetNextItemWidth(160);
            if (ImGui.BeginCombo("Animation", mock.Animation.Length == 0 ? "(the game's own)" : mock.Animation))
            {
                foreach (var animation in animations)
                {
                    if (ImGui.Selectable(animation.Length == 0 ? "(the game's own)" : animation, animation == mock.Animation))
                    {
                        mock.Animation = animation;
                        changed = true;
                    }
                }
                ImGui.EndCombo();
            }

            if (mock.Animation.Length > 0)
            {
                ImGui.SetNextItemWidth(90);
                changed |= ImGui.InputInt("Frame", ref mock.Frame, 1, 1);
                mock.Frame = Math.Max(0, mock.Frame);
            }

            if (ImGui.SmallButton("Remove"))
                remove = i;

            ImGui.PopID();
            ImGui.Separator();
        }

        if (remove >= 0)
        {
            customMocks.RemoveAt(remove);
            changed = true;
        }

        if (ImGui.Button("Add mock"))
        {
            customMocks.Add(new CustomMock { X = 160 + (customMocks.Count % 5) * 20, Y = 110 });
            changed = true;
        }

        if (changed)
            HotRefresh();
    }
}
