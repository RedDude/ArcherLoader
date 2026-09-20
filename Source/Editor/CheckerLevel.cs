using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;
using Microsoft.Xna.Framework;
using TowerFall;

namespace ArcherEditorMod.Editor;

/// <summary>
/// A generated map: it is cut into blocks of 2x2 tiles and every other block is a (non-solid) background tile block,
/// like a checkerboard. The empty blocks are small cells of 20x20 pixels, handy to line mocks up in.
/// The level file is written from a real versus level, so its theme, tileset and settings are valid ones.
/// </summary>
public static class CheckerLevel
{
    public const int Tile = 10;
    public const int Block = 2 * Tile;          // 2x2 tiles
    public const int Columns = 32, Rows = 24;   // tiles
    public const int BlockColumns = Columns / 2, BlockRows = Rows / 2;

    /// <summary>Block (0,0) is a tile block, so a block is empty when its coordinates add up to an odd number.</summary>
    public static bool IsEmpty(int blockX, int blockY) => (blockX + blockY) % 2 == 1;

    /// <summary>Where a player stands in an empty block: its middle, on the ground of the block below.</summary>
    public static Vector2 CellPosition(int blockX, int blockY) => new(blockX * Block + Block / 2, (blockY + 1) * Block - 8);

    /// <summary>(block x, block y) of the empty blocks the sections put their mocks in.</summary>
    public static readonly (int X, int Y)[] Cells =
    {
        (4, 1), (8, 1), (12, 1), (4, 3), (8, 3), (12, 3),
        (4, 5), (8, 5), (12, 5), (4, 7), (8, 7), (12, 7)
    };

    public static VersusTowerData CreateTower()
    {
        var source = GameData.VersusTowers[0];
        var path = WriteLevel(source.Levels[0].Path);

        return new VersusTowerData
        {
            // FortRise looks the tower up as GameData.VersusTowers[ID.X] (Session.IsOfficialTowerSet), so the ID
            // must point at a real tower: borrow the source's, which also makes the level load as an official one
            ID = source.ID,
            Theme = source.Theme,
            TreasureMask = source.TreasureMask ?? TreasureSpawner.FullTreasureMask,
            SpecialArrowRate = 0.6f,
            Levels = new List<VersusLevelData> { new(path) }
        };
    }

    // a copy of a real level with the checkerboard as its background and a few spawns inside cells
    private static string WriteLevel(string sourcePath)
    {
        var doc = new XmlDocument();
        doc.Load(sourcePath);
        var level = doc["level"] ?? throw new InvalidDataException("The source level has no <level> element.");

        // the checkerboard is background tiles: nothing in it blocks a mock. Only the bottom row is solid, so an
        // archer with gravity (the playground) has ground to stand on.
        var solids = new StringBuilder();
        var background = new StringBuilder();
        for (var row = 0; row < Rows; row++)
        {
            for (var column = 0; column < Columns; column++)
            {
                solids.Append(row == Rows - 1 ? '1' : '0');
                background.Append(IsEmpty(column / 2, row / 2) ? '0' : '1');
            }

            if (row < Rows - 1)
            {
                solids.Append('\n');
                background.Append('\n');
            }
        }

        SetText(level, "Solids", solids.ToString());
        SetText(level, "BG", background.ToString());
        SetText(level, "BGTiles", "");
        SetText(level, "SolidTiles", "");

        var entities = level["Entities"] ?? level.AppendChild(doc.CreateElement("Entities")) as XmlElement;
        entities!.RemoveAll();
        var spawns = new[] { (4, 1), (8, 1), (12, 1), (4, 3) };
        for (var i = 0; i < spawns.Length; i++)
        {
            var position = CellPosition(spawns[i].Item1, spawns[i].Item2);
            var spawn = doc.CreateElement("PlayerSpawn");
            spawn.SetAttribute("id", i.ToString());
            spawn.SetAttribute("x", ((int)position.X).ToString());
            spawn.SetAttribute("y", ((int)position.Y).ToString());
            entities.AppendChild(spawn);
        }

        var directory = Path.Combine(Path.GetTempPath(), "ArcherEditor");
        Directory.CreateDirectory(directory);
        var target = Path.Combine(directory, "checkerboard.oel");
        doc.Save(target);
        return target;
    }

    private static void SetText(XmlElement level, string name, string text)
    {
        var element = level[name] ?? (XmlElement)level.AppendChild(level.OwnerDocument.CreateElement(name))!;
        element.InnerText = text;
    }
}
