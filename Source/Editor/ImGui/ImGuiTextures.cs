using System;
using System.Collections.Generic;
using System.Numerics;
using ImGuiNET;
using Microsoft.Xna.Framework.Graphics;
using Monocle;

namespace ArcherEditorMod.Editor.ImGuiSupport;

/// <summary>Shared texture binding + subtexture drawing helpers for the editor windows.</summary>
public static class ImGuiTextures
{
    private static readonly Dictionary<Texture2D, IntPtr> bound = new();

    public static IntPtr Get(ImGuiRenderer renderer, Texture2D texture)
    {
        if (!bound.TryGetValue(texture, out var id))
            bound[texture] = id = renderer.BindTexture(texture);
        return id;
    }

    public static (IntPtr Id, Vector2 Uv0, Vector2 Uv1) Region(ImGuiRenderer renderer, Texture2D texture, Microsoft.Xna.Framework.Rectangle rect) =>
        (Get(renderer, texture),
            new Vector2(rect.X / (float)texture.Width, rect.Y / (float)texture.Height),
            new Vector2(rect.Right / (float)texture.Width, rect.Bottom / (float)texture.Height));

    /// <summary>Image button for a subtexture; false (and nothing drawn) when the subtexture is missing.</summary>
    public static bool SubtextureButton(ImGuiRenderer renderer, string id, Subtexture? subtexture, float scale, out bool drawn)
    {
        drawn = subtexture is { Loaded: true };
        if (!drawn)
            return false;

        var (tex, uv0, uv1) = Region(renderer, subtexture!.Texture2D, subtexture.Rect);
        return ImGui.ImageButton(id, tex, new Vector2(subtexture.Width, subtexture.Height) * scale, uv0, uv1);
    }
}
