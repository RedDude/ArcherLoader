using System;
using System.Globalization;
using System.IO;
using System.Xml;
using Microsoft.Xna.Framework;
using ArcherEditorMod.Source.Features;
using TowerFall;

namespace ArcherEditorMod.Editor.Tools;

/// <summary>
/// Writes the archer's own data (name, subname, colors, gender, start-without-hat) from memory to the archerData.xml
/// of the mod it came from. Base game archers and mods that are not a folder on disk are refused: copy them first.
/// </summary>
public static class ArcherDataSaver
{
    /// <summary>Why this archer's data can't be written, or null when it can.</summary>
    public static string? CannotSaveReason(ArcherData data)
    {
        var source = ArcherDecorationRegistry.FindArcherSource(data);
        if (source == null)
            return "This is a base game archer: its data can't be saved. Create a copy first (Archer Tools > Create copy).";
        if (source.ArcherDataFile == null)
            return "This archer's mod is not a folder on disk (zipped?): its data can't be saved. Create a copy first (Archer Tools > Create copy).";
        return null;
    }

    /// <summary>Empty names and clashes with another archer of the same kind.</summary>
    public static string? Problem(ArcherData data)
    {
        if (string.IsNullOrWhiteSpace(data.Name0)) return "Name is empty.";
        if (string.IsNullOrWhiteSpace(data.Name1)) return "Subname is empty.";

        var clash = ArcherNames.FindCollision(ArcherNames.GroupOf(data), data, data.Name0, data.Name1);
        return clash != null ? $"'{ArcherNames.Full(data.Name0, data.Name1)}' is already used by another archer." : null;
    }

    /// <returns>The file that was written.</returns>
    public static string Save(ArcherData data)
    {
        var reason = CannotSaveReason(data);
        if (reason != null) throw new InvalidOperationException(reason);

        var problem = Problem(data);
        if (problem != null) throw new InvalidOperationException(problem);

        var source = ArcherDecorationRegistry.FindArcherSource(data)!;
        var path = source.ArcherDataFile!;

        var doc = ArcherXml.Load(path);
        var element = ArcherXml.FindById(doc, source.LocalId)
                      ?? throw new InvalidDataException($"'{source.LocalId}' not found in {path}");

        ArcherXml.SetChild(element, "Name0", data.Name0.Trim());
        ArcherXml.SetChild(element, "Name1", data.Name1.Trim());
        ArcherXml.SetChild(element, "ColorA", ArcherXml.Hex(data.ColorA));
        ArcherXml.SetChild(element, "ColorB", ArcherXml.Hex(data.ColorB));
        ArcherXml.SetChild(element, "LightbarColor", ArcherXml.Hex(data.LightbarColor));
        ArcherXml.SetChild(element, "Genders", data.Gender.ToString());
        ArcherXml.SetChild(element, "StartNoHat", data.StartNoHat.ToString().ToLowerInvariant());

        // The rest is written when the file already has it or the value differs from the game's default, so an alt
        // or skin archer (which inherits from its base archer) doesn't get a full copy of the base data.
        Field(element, "Hair", data.Hair.ToString().ToLowerInvariant(), "false");
        Field(element, "PurpleParticles", data.PurpleParticles.ToString().ToLowerInvariant(), "false");
        Field(element, "SFX", data.SFXID.ToString(CultureInfo.InvariantCulture), "0");
        Field(element, "SleepHeadFrame", data.SleepHeadFrame.ToString(CultureInfo.InvariantCulture), "-1");
        Field(element, "VictoryMusic", data.VictoryMusic ?? "Team", "Team");
        Field(element, "Corpse", data.Corpse ?? "", "");

        if (element["Sprites"] is { } sprites)
        {
            Field(sprites, "Body", data.Sprites.Body ?? "", "");
            Field(sprites, "HeadNormal", data.Sprites.HeadNormal ?? "", "");
            Field(sprites, "HeadNoHat", data.Sprites.HeadNoHat ?? "", "");
            Field(sprites, "HeadCrown", data.Sprites.HeadCrown ?? "", "");
            Field(sprites, "HeadBack", data.Sprites.HeadBack ?? "", "");
            Field(sprites, "Bow", data.Sprites.Bow ?? "", "");
        }

        if (element["Hat"] is { } hat)
            Field(hat, "Material", data.Hat.Material.ToString(), "");

        if (element["Gems"] is { } gems)
        {
            Field(gems, "Menu", data.Gems.Menu ?? "", "");
            Field(gems, "Gameplay", data.Gems.Gameplay ?? "", "");
        }

        if (element["Breathing"] is { } breathing)
        {
            Field(breathing, "Interval", data.Breathing.Interval.ToString(CultureInfo.InvariantCulture), "");
            Field(breathing, "Offset", Position(data.Breathing.Offset), "");
            Field(breathing, "DuckingOffset", Position(data.Breathing.DuckingOffset), "");
        }

        ArcherXml.Save(doc, path);
        return path;
    }

    private static string Position(Vector2 value) =>
        string.Create(CultureInfo.InvariantCulture, $"{value.X},{value.Y}");

    private static void Field(XmlElement parent, string name, string value, string defaultValue)
    {
        if (parent[name] != null || value != defaultValue)
            ArcherXml.SetChild(parent, name, value);
    }
}
