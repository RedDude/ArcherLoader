using System;
using System.IO;
using System.Linq;
using FortRise;

namespace ArcherEditorMod.Editor.Tools;

/// <summary>Rewrites an archer's xml element as a normal, alt or secret archer. Works on files, loaded or not.</summary>
public static class ArcherTypeChange
{
    // an alt / secret inherits the missing pieces from its base; a normal archer needs all of them
    private static readonly string[] RequiredForNormal =
        { "Name0", "Name1", "Aimer", "Corpse", "Sprites", "Portraits", "Statue", "Gems" };

    /// <param name="baseReference">Alt / Secret only: the name of the archer it belongs to ("Green", "Mod/id").</param>
    /// <param name="customDataFile">The archerCustomData.xml holding this archer's entry, if any (renamed too).</param>
    /// <param name="customDataId">The id that entry uses.</param>
    public static void Apply(string archerDataFile, string localId, ArcherEntryType type, string? baseReference,
        string? customDataFile, string? customDataId)
    {
        var doc = ArcherXml.Load(archerDataFile);
        var element = ArcherXml.FindById(doc, localId)
                      ?? throw new Exception($"'{localId}' not found in {archerDataFile}");

        var elementName = type switch
        {
            ArcherEntryType.Alt => "AltArcher",
            ArcherEntryType.Secret => "SecretArcher",
            _ => "Archer"
        };

        if (type == ArcherEntryType.Normal)
        {
            var missing = RequiredForNormal.Where(n => element[n] == null).ToList();
            if (missing.Count > 0)
                throw new Exception("A normal archer needs these elements, missing here: " + string.Join(", ", missing));

            element.RemoveAttribute("Alt");
            element.RemoveAttribute("Secret");
        }
        else
        {
            if (string.IsNullOrEmpty(baseReference))
                throw new Exception("Pick the archer it belongs to first.");

            element.RemoveAttribute(type == ArcherEntryType.Alt ? "Secret" : "Alt");
            element.SetAttribute(type == ArcherEntryType.Alt ? "Alt" : "Secret", baseReference);
        }

        ArcherXml.Rename(element, elementName);
        ArcherXml.Save(doc, archerDataFile);

        // the archerCustomData.xml entry names the kind of archer it decorates
        if (customDataFile != null && customDataId != null && File.Exists(customDataFile))
        {
            var custom = ArcherXml.Load(customDataFile);
            var entry = ArcherXml.FindById(custom, customDataId);
            if (entry != null)
            {
                ArcherXml.Rename(entry, elementName);
                ArcherXml.Save(custom, customDataFile);
            }
        }
    }
}
