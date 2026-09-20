using System;
using System.Collections.Generic;
using System.Linq;
using ArcherEditorMod.Source.Features.Ghost;
using ArcherEditorMod.Source.Features.Hair;
using ArcherEditorMod.Source.Features.Layers;
using ArcherEditorMod.Source.Features.Particles;
using ArcherEditorMod.Source.Features.PortraitLayers;
using ArcherEditorMod.Source.Features.Taunt;
using ArcherEditorMod.Source.Features.Wings;
using ImGuiNET;
using Monocle;
using TowerFall;

namespace ArcherEditorMod.Editor;

/// <summary>What the archer refers to, in its archer data and its custom data: sprite data entries and textures.</summary>
public static partial class AtlasWindow
{
    private sealed record TextureRef(string Label, int Atlas, string Name);
    private sealed record SpriteReference(string Source, string Id, string Label);

    // ---- sprite data entries ----

    // body, heads, bow, corpse, gems (archer data) + taunt and portrait layers (custom data)
    private static List<SpriteReference> ReferencedSprites(ArcherData a)
    {
        var result = new List<SpriteReference>();
        void Add(string source, string? id, string label)
        {
            if (!string.IsNullOrWhiteSpace(id) && !result.Any(r => r.Source == source && r.Id == id))
                result.Add(new SpriteReference(source, id, label));
        }

        Add("SpriteData", a.Sprites.Body, "Body");
        Add("SpriteData", a.Sprites.HeadNormal, "Head");
        Add("SpriteData", a.Sprites.HeadNoHat, "Head, no hat");
        Add("SpriteData", a.Sprites.HeadCrown, "Head, crown");
        Add("SpriteData", a.Sprites.HeadBack, "Head, back");
        Add("SpriteData", a.Sprites.Bow, "Bow");
        Add("SpriteData", a.Gems.Gameplay, "Gem");
        Add("CorpseSpriteData", a.Corpse, "Corpse");
        Add("MenuSpriteData", a.Gems.Menu, "Menu gem");

        if (TauntFeature.TryGet(a, out var taunt))
            Add("SpriteData", taunt.SpriteId, "Taunt");

        foreach (var layer in PortraitLayersFeature.GetLayers(a))
            Add(layer.IsMenuSprite ? "MenuSpriteData" : "SpriteData", layer.Sprite, "Portrait layer");

        return result;
    }

    // ---- textures ----

    private static Dictionary<Subtexture, (int Atlas, string Name)> subLookup = new();
    private static int subLookupCount = -1;

    private static (int Atlas, string Name)? NameOf(Subtexture? sub)
    {
        if (sub == null) return null;

        var count = Enumerable.Range(0, 4).Sum(i => AtlasAt(i)?.SubTextures.Count ?? 0);
        if (count != subLookupCount)
        {
            subLookupCount = count;
            subLookup = new Dictionary<Subtexture, (int, string)>();
            for (var i = 0; i < 4; i++)
            {
                if (AtlasAt(i) is not { } atlas) continue;
                foreach (var (name, texture) in atlas.SubTextures)
                    subLookup.TryAdd(texture, (i, name));
            }
        }

        return subLookup.TryGetValue(sub, out var found) ? found : null;
    }

    // portraits (menu atlas), aimer, hat, statue, wings, ghost, and the textures hair / particles / layers name
    private static List<TextureRef> ReferencedTextures(ArcherData a)
    {
        var result = new List<TextureRef>();
        void Add(string label, int atlas, string? name)
        {
            if (!string.IsNullOrEmpty(name) && !result.Any(r => r.Atlas == atlas && r.Name == name))
                result.Add(new TextureRef(label, atlas, name));
        }

        void Sub(string label, Subtexture? texture)
        {
            if (NameOf(texture) is { } found) Add(label, found.Atlas, found.Name);
        }

        void Named(string label, string? text)
        {
            if (ResolveName(0, text) is { } name) Add(label, 0, name);
        }

        Sub("Portrait: not joined", a.Portraits.NotJoined);
        Sub("Portrait: joined", a.Portraits.Joined);
        Sub("Portrait: win", a.Portraits.Win);
        Sub("Portrait: lose", a.Portraits.Lose);
        Sub("Aimer", a.Aimer);
        Sub("Hat", a.Hat.Normal);
        Sub("Hat (blue)", a.Hat.Blue);
        Sub("Hat (red)", a.Hat.Red);
        Sub("Statue", a.Statue.Image);
        Sub("Statue glow", a.Statue.Glow);

        if (WingsFeature.TryGet(a, out var wings, out _)) Sub("Wings", wings);
        if (GhostFeature.TryGet(a, out var ghost, out _)) Sub("Ghost", ghost);

        foreach (var hair in HairFeature.GetHairs(a) ?? new List<HairInfo>())
        {
            Named("Hair", hair.HairSprite);
            Named("Hair end", hair.HairEndSprite);
        }

        foreach (var particles in ParticlesFeature.GetAllParticles(a))
            Named("Particles", particles.Source);

        foreach (var layer in LayerFeature.GetLayers(a))
            Named("Layer", layer.Sprite);

        return result;
    }

    // ---- a texture picked on its own (no sprite data): its rectangle can still be moved and resized ----

    private const string TexturesSource = "Textures";
    private static int selTextureAtlas;

    private static void SelectTexture(int atlas, string name)
    {
        selSource = TexturesSource;
        selId = name;
        selTextureAtlas = atlas;
        activeTag = "Texture";
        focusTexture = name;
        atlasIndex = atlas;
        pageIndex = -1;
        focusPending = true;
    }

    private static int SelectedAtlas() => selSource == TexturesSource ? selTextureAtlas : selSource == null ? 0 : AtlasOf(selSource);

    private static void DrawTextureList(ArcherData? archer, string filterText)
    {
        if (archer == null) return;

        var textures = ReferencedTextures(archer)
            .Where(t => filterText.Length == 0 || t.Name.Contains(filterText, StringComparison.OrdinalIgnoreCase) ||
                        t.Label.Contains(filterText, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (textures.Count == 0) return;

        ImGui.SeparatorText("Textures");
        foreach (var texture in textures)
        {
            var selected = selSource == TexturesSource && selId == texture.Name && selTextureAtlas == texture.Atlas;
            if (ImGui.Selectable($"{texture.Label}##tex{texture.Atlas}{texture.Name}", selected))
                SelectTexture(texture.Atlas, texture.Name);
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip($"{texture.Name}  ({atlasNames[texture.Atlas]})");
        }
    }
}
