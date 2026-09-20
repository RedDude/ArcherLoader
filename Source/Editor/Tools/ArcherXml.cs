using System;
using System.Linq;
using System.Xml;

namespace ArcherEditorMod.Editor.Tools;

/// <summary>Small helpers to edit archer xml files in place, keeping the file's formatting.</summary>
public static class ArcherXml
{
    public static XmlDocument Load(string path)
    {
        var doc = new XmlDocument { PreserveWhitespace = true };
        doc.Load(path);
        return doc;
    }

    /// <summary>The element with the given id attribute anywhere under the root (Archer, AltArcher, SecretArcher...).</summary>
    public static XmlElement? FindById(XmlDocument doc, string id) =>
        doc.SelectSingleNode($"//*[@id='{id}']") as XmlElement;

    public static XmlElement SetChild(XmlElement parent, string name, string value)
    {
        var element = parent[name];
        if (element == null)
        {
            element = parent.OwnerDocument.CreateElement(name);
            var last = parent.LastChild;
            // reuse the indentation of the last child so the file stays tidy
            var indent = parent.ChildNodes.OfType<XmlWhitespace>().FirstOrDefault();
            if (indent != null && last is XmlWhitespace closing)
            {
                parent.InsertBefore(parent.OwnerDocument.CreateWhitespace(indent.Value), closing);
                parent.InsertBefore(element, closing);
            }
            else
            {
                parent.AppendChild(element);
            }
        }

        element.InnerText = value;
        return element;
    }

    public static void RemoveChildren(XmlElement parent, params string[] names)
    {
        foreach (var name in names)
        {
            while (parent[name] is { } child)
                parent.RemoveChild(child);
        }
    }

    /// <summary>Renames an element by replacing it with a copy under the new name.</summary>
    public static XmlElement Rename(XmlElement element, string newName)
    {
        if (element.Name == newName)
            return element;

        var replacement = element.OwnerDocument.CreateElement(newName);
        foreach (XmlAttribute attribute in element.Attributes)
            replacement.SetAttribute(attribute.Name, attribute.Value);
        while (element.FirstChild != null)
            replacement.AppendChild(element.FirstChild);

        element.ParentNode!.ReplaceChild(replacement, element);
        return replacement;
    }

    public static string Hex(Microsoft.Xna.Framework.Color color) => $"{color.R:X2}{color.G:X2}{color.B:X2}";

    public static void Save(XmlDocument doc, string path) => doc.Save(path);
}
