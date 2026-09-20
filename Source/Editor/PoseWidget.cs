using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Xml;
using ArcherEditorMod.Editor.ImGuiSupport;
using ImGuiNET;
using Monocle;
using TowerFall;

namespace ArcherEditorMod.Editor;

public enum Pose
{
    Stand,
    Duck,
    Hat,
    Crown
}

/// <summary>What an overlay needs to draw on top of a pose preview.</summary>
public sealed class PoseContext
{
    public ImGuiRenderer Renderer = null!;
    public ImDrawListPtr DrawList;
    public Vector2 Anchor;               // screen position of the player's Position
    public float Scale;
    public Vector2 HeadHairBase;         // where the game puts its own hair for this pose (game pixels, from Position)
    public Pose Pose;

    public Vector2 ToScreen(Vector2 gamePixels) => Anchor + gamePixels * Scale;
}

/// <summary>
/// A small preview of the archer in one pose (standing, ducking, with hat, with crown) with arrow buttons on every
/// side to nudge an offset, like the frames window does for head origins. Used by the hair and particle windows.
/// </summary>
public static class PoseWidget
{
    private const float Scale = 4f;
    private const float CanvasW = 32, CanvasH = 40;
    private const float AnchorX = 16, AnchorY = 30;
    private const float ArrowSize = 22f;

    private static readonly Dictionary<string, Sprite<string>> spriteCache = new();

    public static float CellWidth => CanvasW * Scale + 2 * ArrowSize + 12;

    /// <returns>True when the offset was changed.</returns>
    public static bool Draw(ImGuiRenderer renderer, ArcherData data, string id, string label, Pose pose,
        ref Microsoft.Xna.Framework.Vector2 offset, Action<PoseContext>? overlay)
    {
        var bodyId = data.Sprites.Body;
        var headId = pose switch
        {
            Pose.Hat => data.Sprites.HeadNormal,
            Pose.Crown => data.Sprites.HeadCrown,
            _ => data.Sprites.HeadNoHat
        };

        var changed = false;
        ImGui.BeginGroup();
        ImGui.PushID(id);

        var start = ImGui.GetCursorScreenPos();
        var canvasSize = new Vector2(CanvasW * Scale, CanvasH * Scale);
        var canvasMin = start + new Vector2(ArrowSize + 4, ArrowSize + 2);
        var canvasMax = canvasMin + canvasSize;
        var drawList = ImGui.GetWindowDrawList();

        ImGui.Dummy(new Vector2(canvasSize.X + 2 * ArrowSize + 8, canvasSize.Y + 2 * ArrowSize + 24));
        drawList.AddRectFilled(canvasMin, canvasMax, ImGui.GetColorU32(new Vector4(0.12f, 0.12f, 0.14f, 1)));
        drawList.AddRect(canvasMin, canvasMax, ImGui.GetColorU32(new Vector4(0.6f, 0.6f, 0.6f, 1)));

        var anchor = canvasMin + new Vector2(AnchorX, AnchorY) * Scale;
        drawList.AddLine(anchor - new Vector2(5, 0), anchor + new Vector2(5, 0), 0x8000FFFF);
        drawList.AddLine(anchor - new Vector2(0, 5), anchor + new Vector2(0, 5), 0x8000FFFF);

        var headHairBase = new Vector2(-3, -6);
        if (TFGame.SpriteData.Contains(bodyId) && TFGame.SpriteData.Contains(headId))
            headHairBase = DrawArcher(renderer, drawList, anchor, bodyId, headId, pose);

        overlay?.Invoke(new PoseContext
        {
            Renderer = renderer, DrawList = drawList, Anchor = anchor, Scale = Scale,
            HeadHairBase = headHairBase, Pose = pose
        });

        // arrows on every side; screen deltas map straight onto the offset (right = +x, down = +y)
        var midX = canvasMin.X + (canvasSize.X - ArrowSize) / 2;
        var midY = canvasMin.Y + (canvasSize.Y - ArrowSize) / 2;
        var dx = 0f;
        var dy = 0f;
        if (Arrow("up", ImGuiDir.Up, new Vector2(midX, start.Y))) dy = -1;
        if (Arrow("down", ImGuiDir.Down, new Vector2(midX, canvasMax.Y + 2))) dy = 1;
        if (Arrow("left", ImGuiDir.Left, new Vector2(start.X, midY))) dx = -1;
        if (Arrow("right", ImGuiDir.Right, new Vector2(canvasMax.X + 4, midY))) dx = 1;

        ImGui.SetCursorScreenPos(canvasMin + new Vector2(2, 2));
        if (ImGui.SmallButton("reset"))
        {
            changed |= offset != Microsoft.Xna.Framework.Vector2.Zero;
            offset = Microsoft.Xna.Framework.Vector2.Zero;
        }

        if (dx != 0 || dy != 0)
        {
            offset += new Microsoft.Xna.Framework.Vector2(dx, dy);
            changed = true;
        }

        ImGui.SetCursorScreenPos(new Vector2(start.X, canvasMax.Y + ArrowSize + 4));
        ImGui.Text($"{label}  ({offset.X:0.##}, {offset.Y:0.##})");

        ImGui.PopID();
        ImGui.EndGroup();
        return changed;
    }

    // body + head like Player.UpdateHead; returns where the game's own hair sits (from the player's Position)
    private static Vector2 DrawArcher(ImGuiRenderer renderer, ImDrawListPtr drawList, Vector2 anchor, string bodyId,
        string headId, Pose pose)
    {
        var bodyXml = TFGame.SpriteData.GetXML(bodyId);
        var headXml = TFGame.SpriteData.GetXML(headId);
        var body = GetSprite(bodyId);
        var head = GetSprite(headId);

        var bodyFrame = FirstFrame(bodyXml, pose == Pose.Duck ? "duck" : "stand");
        var headFrame = FirstFrame(headXml, pose == Pose.Duck ? "duck" : "idle");

        DrawFrame(renderer, drawList, body, bodyFrame,
            anchor + (Vec(bodyXml, "X", "Y") - Vec(bodyXml, "OriginX", "OriginY")) * Scale);

        var headOrigin = Vec(headXml, "OriginX", "OriginY");
        var yOrigins = ReadCsv(bodyXml["HeadYOrigins"]);
        var xOrigins = ReadCsv(bodyXml["HeadXOrigins"]);
        if (yOrigins != null && bodyFrame < yOrigins.Length) headOrigin.Y = yOrigins[bodyFrame];
        if (xOrigins != null && bodyFrame < xOrigins.Length) headOrigin.X = xOrigins[bodyFrame];

        DrawFrame(renderer, drawList, head, headFrame,
            anchor + (Vec(headXml, "X", "Y") - headOrigin) * Scale);

        // Player.UpdateHead: Hair.Position = (-Facing * 3, 12 - headYOrigin * scaleY), facing right
        var headY = yOrigins != null && bodyFrame < yOrigins.Length ? yOrigins[bodyFrame] : 18;
        return new Vector2(-3, 12 - headY);
    }

    private static bool Arrow(string id, ImGuiDir dir, Vector2 position)
    {
        ImGui.SetCursorScreenPos(position);
        ImGui.PushItemFlag(ImGuiItemFlags.ButtonRepeat, true);
        var pressed = ImGui.ArrowButton(id, dir);
        ImGui.PopItemFlag();
        return pressed;
    }

    public static void DrawFrame(ImGuiRenderer renderer, ImDrawListPtr drawList, Sprite<string> sprite, int frame, Vector2 topLeft)
    {
        if (frame < 0 || frame >= sprite.FrameRects.Length) return;

        var rect = sprite.FrameRects[frame];
        var (id, uv0, uv1) = ImGuiTextures.Region(renderer, sprite.Texture.Texture2D, rect);
        drawList.AddImage(id, topLeft, topLeft + new Vector2(rect.Width, rect.Height) * Scale, uv0, uv1);
    }

    private static Sprite<string> GetSprite(string id)
    {
        if (!spriteCache.TryGetValue(id, out var sprite))
            spriteCache[id] = sprite = TFGame.SpriteData.GetSpriteString(id);
        return sprite;
    }

    private static Vector2 Vec(XmlElement xml, string x, string y) => new(Num(xml, x), Num(xml, y));

    private static float Num(XmlElement xml, string name) =>
        float.TryParse(xml[name]?.InnerText, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : 0f;

    private static int[]? ReadCsv(XmlElement? element)
    {
        if (element == null) return null;
        var text = element.InnerText.Trim();
        return text.Length == 0
            ? Array.Empty<int>()
            : text.Split(',').Select(v => int.Parse(v.Trim(), CultureInfo.InvariantCulture)).ToArray();
    }

    private static int FirstFrame(XmlElement spriteXml, string animationId)
    {
        var animations = spriteXml["Animations"];
        if (animations == null) return 0;

        foreach (XmlElement anim in animations.GetElementsByTagName("Anim"))
        {
            if (anim.GetAttribute("id") != animationId) continue;
            var frames = anim.GetAttribute("frames").Split(',', StringSplitOptions.RemoveEmptyEntries);
            return frames.Length > 0 ? int.Parse(frames[0].Trim(), CultureInfo.InvariantCulture) : 0;
        }

        return 0;
    }
}
