using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using System.Numerics;
using System.Xml;
using ArcherEditorMod.Editor.ImGuiSupport;
using ArcherEditorMod.Source.Features;
using ArcherEditorMod.Source.Features.Ghost;
using ArcherEditorMod.Source.Features.Hair;
using ArcherEditorMod.Source.Features.Particles;
using ArcherEditorMod.Source.Features.Wings;
using ImGuiNET;
using Rectangle = Microsoft.Xna.Framework.Rectangle;
using Monocle;
using TowerFall;

namespace ArcherEditorMod.Editor;

public enum OverlayMode
{
    Off,
    Hover,
    Always
}

/// <summary>
/// Shows the texture pages of a loaded atlas with the subtexture bounding boxes and ids overlaid, either all the
/// time or only under the mouse. Inside the archer editor it only lists the atlases, pages and textures the archer
/// uses (sprites, portraits, corpse, hair, wings, ghost, particles...); "All atlases" lifts that, and the settings
/// menu button opens it unrestricted.
/// </summary>
public static partial class AtlasWindow
{
    public static bool Open;

    /// <summary>True lists every atlas / page / texture instead of only the archer's.</summary>
    public static bool ShowAll;

    private sealed class Page
    {
        public Texture Texture = null!;
        public List<(string Name, Subtexture Sub)> Subs = new();
    }

    private static readonly string[] atlasNames = { "Atlas", "MenuAtlas", "BGAtlas", "BossAtlas" };
    private static readonly string[] overlayNames = { "Off", "Hover", "Always" };

    private static int atlasIndex;
    private static int pageIndex;
    private static int boxMode = (int)OverlayMode.Always;
    private static int idMode = (int)OverlayMode.Hover;
    private static int spriteMode = (int)OverlayMode.Hover;
    private static float zoom = 1f;
    private static string filter = "";
    private static ArcherData? lastArcher;
    private static bool wantArcherPage;

    // pages are rebuilt when the atlas changes size (mods register textures over time)
    private static List<Page> pages = new();
    private static Atlas? pagesAtlas;
    private static int pagesCount = -1;

    private static Atlas? AtlasAt(int index) => index switch
    {
        1 => TFGame.MenuAtlas,
        2 => TFGame.BGAtlas,
        3 => TFGame.BossAtlas,
        _ => TFGame.Atlas
    };

    private static void RebuildPages(Atlas atlas)
    {
        if (ReferenceEquals(pagesAtlas, atlas) && pagesCount == atlas.SubTextures.Count) return;

        pagesAtlas = atlas;
        pagesCount = atlas.SubTextures.Count;

        // a mod's textures live in their own texture, the base game's in the atlas image itself
        pages = atlas.SubTextures
            .GroupBy(p => p.Value.Texture)
            .Select(g => new Page { Texture = g.Key, Subs = g.Select(p => (p.Key, p.Value)).ToList() })
            .OrderByDescending(p => ReferenceEquals(p.Texture, atlas))
            .ThenByDescending(p => p.Subs.Count)
            .ToList();
    }

    // ---- sprite data: which sprites use a texture, and how it is cut into frames ----

    private sealed record SpriteRef(string Source, string Id, XmlElement Xml);

    private static readonly string[] SpriteTextureTags = { "Texture", "RedTexture", "BlueTexture", "RedTeam", "BlueTeam", "Flash" };

    private static Dictionary<string, List<SpriteRef>> spriteRefs = new();
    private static int spriteRefsAtlas = -1;
    private static int spriteRefsCount = -1;

    private static IEnumerable<(string Source, SpriteData? Data)> SpriteDataFor(int atlas) => atlas switch
    {
        1 => new[] { ("MenuSpriteData", TFGame.MenuSpriteData) },
        2 => new[] { ("BGSpriteData", TFGame.BGSpriteData) },
        3 => new[] { ("BossSpriteData", TFGame.BossSpriteData) },
        _ => new[] { ("SpriteData", TFGame.SpriteData), ("CorpseSpriteData", TFGame.CorpseSpriteData) }
    };

    private static Dictionary<string, List<SpriteRef>> SpriteRefsFor(int atlas)
    {
        var sources = SpriteDataFor(atlas).Where(s => s.Data != null)
            .Select(s => (s.Source, Entries: Monocle.SpriteDataExt.GetSprites(s.Data!))).ToList();
        var count = sources.Sum(s => s.Entries.Count);
        if (spriteRefsAtlas == atlas && spriteRefsCount == count) return spriteRefs;

        var map = new Dictionary<string, List<SpriteRef>>();
        foreach (var (source, entries) in sources)
        {
            foreach (var (id, xml) in entries)
            {
                foreach (var tag in SpriteTextureTags)
                {
                    var name = xml[tag]?.InnerText.Trim();
                    if (string.IsNullOrEmpty(name)) continue;

                    if (!map.TryGetValue(name, out var list))
                        map[name] = list = new List<SpriteRef>();
                    if (!list.Any(r => r.Id == id && r.Source == source))
                        list.Add(new SpriteRef(source, id, xml));
                }
            }
        }

        spriteRefsAtlas = atlas;
        spriteRefsCount = count;
        return spriteRefs = map;
    }

    private static int XmlInt(XmlElement xml, string name) =>
        int.TryParse(xml[name]?.InnerText.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : 0;

    private static string SpriteInfo(List<SpriteRef> refs, Subtexture sub)
    {
        var lines = new List<string>();
        foreach (var reference in refs.Take(4))
        {
            var fw = XmlInt(reference.Xml, "FrameWidth");
            var fh = XmlInt(reference.Xml, "FrameHeight");
            var frames = fw > 0 && fh > 0 ? sub.Width / fw * (sub.Height / fh) : 0;
            var animations = reference.Xml["Animations"]?.GetElementsByTagName("Anim").Cast<XmlElement>()
                .Select(a => a.GetAttribute("id")).Take(6).ToList() ?? new List<string>();
            lines.Add($"{reference.Source}: {reference.Id}  frame {fw}x{fh}, {frames} frames" +
                      (animations.Count > 0 ? $"\n    animations: {string.Join(", ", animations)}" : ""));
        }

        if (refs.Count > 4) lines.Add($"... and {refs.Count - 4} more");
        return string.Join("\n", lines);
    }

    // the frame grid a sprite cuts this texture into, drawn on top of the texture
    private static void DrawFrameGrid(ImDrawListPtr drawList, Vector2 origin, Subtexture sub, XmlElement xml)
    {
        var fw = XmlInt(xml, "FrameWidth");
        var fh = XmlInt(xml, "FrameHeight");
        if (fw <= 0 || fh <= 0) return;

        var columns = sub.Width / fw;
        var rows = sub.Height / fh;
        var color = ImGui.GetColorU32(new Vector4(0.3f, 0.9f, 1f, 0.9f));
        var labels = fw * zoom >= 14;

        for (var row = 0; row < rows; row++)
        {
            for (var column = 0; column < columns; column++)
            {
                var min = origin + new Vector2(sub.Rect.X + column * fw, sub.Rect.Y + row * fh) * zoom;
                drawList.AddRect(min, min + new Vector2(fw, fh) * zoom, color);
                if (labels)
                    drawList.AddText(min + new Vector2(2, 1), color, (row * columns + column).ToString());
            }
        }
    }

    // ---- what the archer uses ----

    private static ArcherData? relatedFor;
    private static HashSet<Subtexture> related = new();

    private static HashSet<Subtexture> RelatedTextures(ArcherData archer)
    {
        // recomputed on every refresh of the window contents that can change it: cheap, so just do it per archer
        // and whenever the atlas grows (textures registered late)
        var key = TFGame.Atlas.SubTextures.Count;
        if (ReferenceEquals(relatedFor, archer) && relatedCount == key) return related;

        relatedFor = archer;
        relatedCount = key;
        related = CollectRelated(archer);
        return related;
    }

    private static int relatedCount = -1;

    private static HashSet<Subtexture> CollectRelated(ArcherData archer)
    {
        var subs = new HashSet<Subtexture>();
        var atlases = new[] { TFGame.Atlas, TFGame.MenuAtlas, TFGame.BGAtlas, TFGame.BossAtlas };

        void Add(Subtexture? subtexture)
        {
            if (subtexture != null) subs.Add(subtexture);
        }

        void AddName(string? name)
        {
            if (string.IsNullOrWhiteSpace(name)) return;
            foreach (var atlas in atlases)
            {
                if (atlas != null && atlas.Contains(name))
                    Add(atlas[name]);
            }
        }

        void AddSprite(SpriteData? data, string? id)
        {
            if (data == null || string.IsNullOrEmpty(id) || !data.Contains(id)) return;
            var xml = data.GetXML(id);
            foreach (var tag in new[] { "Texture", "RedTexture", "BlueTexture", "RedTeam", "BlueTeam", "Flash" })
                AddName(xml[tag]?.InnerText.Trim());
        }

        Add(archer.Aimer);
        Add(archer.Portraits.NotJoined);
        Add(archer.Portraits.Joined);
        Add(archer.Portraits.Win);
        Add(archer.Portraits.Lose);
        Add(archer.Hat.Normal);
        Add(archer.Hat.Blue);
        Add(archer.Hat.Red);
        Add(archer.Statue.Image);
        Add(archer.Statue.Glow);

        foreach (var id in new[]
                 {
                     archer.Sprites.Body, archer.Sprites.HeadNormal, archer.Sprites.HeadNoHat,
                     archer.Sprites.HeadCrown, archer.Sprites.HeadBack, archer.Sprites.Bow
                 })
            AddSprite(TFGame.SpriteData, id);

        AddSprite(TFGame.CorpseSpriteData, archer.Corpse);
        AddSprite(TFGame.MenuSpriteData, archer.Gems.Menu);
        AddSprite(TFGame.SpriteData, archer.Gems.Gameplay);

        if (WingsFeature.TryGet(archer, out var wings, out _)) Add(wings);
        if (GhostFeature.TryGet(archer, out var ghost, out _)) Add(ghost);

        foreach (var hair in HairFeature.GetHairs(archer) ?? new List<HairInfo>())
        {
            AddName(hair.HairSprite);
            AddName(hair.HairEndSprite);
        }

        foreach (var particles in ParticlesFeature.GetAllParticles(archer))
            AddName(particles.Source);

        return subs;
    }

    // ---- window ----

    /// <summary>The window of the archer being created: only its sprite data, the mod's own atlas first.</summary>
    public static bool ArcherOpen;

    private static bool archerMode;

    // The two windows (general viewer, archer creation) keep their own view: the shared fields are swapped in.
    private sealed class WindowState
    {
        public int AtlasIndex, PageIndex, BoxMode = (int)OverlayMode.Always, IdMode = (int)OverlayMode.Hover,
            SpriteMode = (int)OverlayMode.Hover;
        public float Zoom = 1f;
        public string Filter = "", SpriteFilter = "", ActiveTag = "Texture";
        public ArcherData? LastArcher;
        public bool WantArcherPage, FocusPending, ShowAll, ShowPanels;
        public Vector2 Pan;
        public Rectangle? FocusRect;
        public string? FocusTexture, SelSource, SelId;

        public void Apply()
        {
            atlasIndex = AtlasIndex; pageIndex = PageIndex; boxMode = BoxMode; idMode = IdMode; spriteMode = SpriteMode;
            zoom = Zoom; filter = Filter; spriteFilter = SpriteFilter; activeTag = ActiveTag; lastArcher = LastArcher;
            wantArcherPage = WantArcherPage; focusPending = FocusPending; ShowAll = ShowAll; ShowSpritePanels = ShowPanels;
            pan = Pan; focusRect = FocusRect; focusTexture = FocusTexture; selSource = SelSource; selId = SelId;
        }

        public void Capture()
        {
            AtlasIndex = atlasIndex; PageIndex = pageIndex; BoxMode = boxMode; IdMode = idMode; SpriteMode = spriteMode;
            Zoom = zoom; Filter = filter; SpriteFilter = spriteFilter; ActiveTag = activeTag; LastArcher = lastArcher;
            WantArcherPage = wantArcherPage; FocusPending = focusPending; ShowAll = AtlasWindow.ShowAll; ShowPanels = ShowSpritePanels;
            Pan = pan; FocusRect = focusRect; FocusTexture = focusTexture; SelSource = selSource; SelId = selId;
        }
    }

    private static readonly WindowState viewerState = new();
    private static readonly WindowState archerState = new() { ShowPanels = true };

    public static void Draw(ImGuiRenderer renderer, ArcherData? archer, Action? refresh = null) =>
        Run(renderer, archer, refresh, false);

    public static void DrawArcher(ImGuiRenderer renderer, ArcherData? archer, Action? refresh = null) =>
        Run(renderer, archer, refresh, true);

    private static void Run(ImGuiRenderer renderer, ArcherData? archer, Action? refresh, bool forArcher)
    {
        var open = forArcher ? ArcherOpen : Open;
        if (!open) return;

        var state = forArcher ? archerState : viewerState;
        state.Apply();
        archerMode = forArcher;
        try
        {
            DrawCore(renderer, archer, refresh, ref open);
        }
        finally
        {
            state.Capture();
            if (forArcher) ArcherOpen = open; else Open = open;
        }
    }

    private static void DrawCore(ImGuiRenderer renderer, ArcherData? archer, Action? refresh, ref bool open)
    {


        ImGui.SetNextWindowSize(new Vector2(720, 560), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowPos(new Vector2(450, 40), ImGuiCond.FirstUseEver);
        if (!ImGui.Begin(archerMode ? "Archer Atlas" : "Atlas", ref open))
        {
            ImGui.End();
            return;
        }

        refreshEditor = refresh;
        boundArcher = archer;
        if (archerMode)
        {
            ShowAll = false;
            ShowSpritePanels = true;
        }

        var restrict = !ShowAll && archer != null && RelatedTextures(archer).Count > 0;
        var archerTextures = restrict ? RelatedTextures(archer!) : null;

        // when the archer changes, jump to its page (its atlas prefix is the default id filter)
        if (archer != null && !ReferenceEquals(archer, lastArcher))
        {
            lastArcher = archer;
            if (archerMode) { atlasIndex = -1; pageIndex = 0; } // the mod's own atlas is picked below
            wantArcherPage = true;
            var name = archer.Sprites.Body ?? "";
            filter = name.Contains('/') ? name[..name.IndexOf('/')] + "/" : "";
        }

        if (archer != null)
        {
            if (!archerMode)
            {
                ImGui.Checkbox("All atlases", ref ShowAll);
                ImGui.SameLine();
            }

            if (ImGui.Button("Archer's page"))
                wantArcherPage = true;
            ImGui.SameLine();
        }

        if (!archerMode)
        {
            ImGui.Checkbox("Sprite data editor", ref ShowSpritePanels);
            ImGui.SameLine();
        }

        ImGui.TextDisabled("wheel = zoom, hold right button = pan, Ctrl+Z / Ctrl+Y = undo / redo");


        // the archer creation window prefers what its mod loaded: that mod's atlas and pages come first
        var modPrefix = archerMode && archer != null && ArcherDecorationRegistry.FindArcherSource(archer) is { ModName.Length: > 0 } modSource
            ? modSource.ModName + "/" : null;
        bool ModAtlas(int i) => modPrefix != null && AtlasAt(i)!.SubTextures.Keys.Any(k => k.StartsWith(modPrefix, StringComparison.Ordinal));

        // ---- atlas combo: only the ones holding textures of this archer ----
        var allowed = Enumerable.Range(0, atlasNames.Length)
            .Where(i => AtlasAt(i) != null &&
                        (archerTextures == null || AtlasAt(i)!.SubTextures.Values.Any(archerTextures.Contains)))
            .ToList();

        if (allowed.Count == 0)
        {
            ImGui.TextDisabled("Nothing of this archer is in an atlas.");
            ImGui.End();
            return;
        }

        if (modPrefix != null)
            allowed = allowed.OrderByDescending(ModAtlas).ThenBy(i => i).ToList();

        if (!allowed.Contains(atlasIndex))
        {
            atlasIndex = allowed[0];
            pageIndex = 0;
        }

        ImGui.SetNextItemWidth(140);
        if (ImGui.BeginCombo("Atlas", atlasNames[atlasIndex]))
        {
            foreach (var index in allowed)
            {
                if (ImGui.Selectable(atlasNames[index] + (ModAtlas(index) ? " (mod)" : ""), index == atlasIndex))
                {
                    atlasIndex = index;
                    pageIndex = 0;
                }
            }
            ImGui.EndCombo();
        }

        var atlas = AtlasAt(atlasIndex)!;
        RebuildPages(atlas);

        var visiblePages = archerTextures == null
            ? pages
            : pages.Where(p => p.Subs.Any(s => archerTextures.Contains(s.Sub))).ToList();

        if (modPrefix != null)
            visiblePages = visiblePages.OrderByDescending(p => p.Subs.Any(s => s.Name.StartsWith(modPrefix, StringComparison.Ordinal))).ToList();

        if (wantArcherPage && archer != null)
        {
            wantArcherPage = false;
            FocusArcherPage(archer, visiblePages, ref allowed);
        }

        // a sprite picked in the list: its page, and the view moves onto its rectangle
        if (focusPending && focusTexture != null)
        {
            var sub = atlas.SubTextures.TryGetValue(focusTexture, out var focusSub) ? focusSub : null;
            var index = sub == null ? -1 : visiblePages.FindIndex(p => ReferenceEquals(p.Texture, sub.Texture));
            if (index >= 0)
            {
                pageIndex = index;
                focusRect = sub!.Rect;
                focusPending = false;
            }
            else if (sub != null && archerTextures != null)
            {
                ShowAll = true; // the sprite is not one of the archer's: lift the restriction
            }
            else
            {
                focusPending = false;
            }
        }

        pageIndex = Math.Clamp(pageIndex, 0, Math.Max(0, visiblePages.Count - 1));
        if (visiblePages.Count == 0)
        {
            ImGui.TextDisabled("This atlas has no textures.");
            ImGui.End();
            return;
        }

        ImGui.SameLine();
        ImGui.SetNextItemWidth(320);
        if (ImGui.BeginCombo("Page", PageLabel(visiblePages[pageIndex], atlas)))
        {
            for (var i = 0; i < visiblePages.Count; i++)
            {
                if (ImGui.Selectable(PageLabel(visiblePages[i], atlas), i == pageIndex))
                    pageIndex = i;
            }
            ImGui.EndCombo();
        }

        ImGui.SetNextItemWidth(110);
        ImGui.Combo("Boxes", ref boxMode, overlayNames, overlayNames.Length);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(110);
        ImGui.Combo("Ids", ref idMode, overlayNames, overlayNames.Length);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(110);
        ImGui.Combo("Sprite data", ref spriteMode, overlayNames, overlayNames.Length);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(140);
        ImGui.SliderFloat("Zoom", ref zoom, 0.25f, 6f);
        ImGui.SetNextItemWidth(260);
        ImGui.InputText("Only ids containing", ref filter, 96);

        var page = visiblePages[pageIndex];
        if (page.Texture.Texture2D == null)
        {
            ImGui.TextDisabled("This texture is not loaded yet.");
            ImGui.End();
            return;
        }

        var panelsWidth = ShowSpritePanels ? 560f : 0f;
        DrawPage(renderer, page, archerTextures, Math.Max(200f, ImGui.GetContentRegionAvail().X - panelsWidth - (ShowSpritePanels ? 8f : 0f)));

        if (ShowSpritePanels)
        {
            ImGui.SameLine();
            if (ImGui.BeginChild("sprite_list", new Vector2(220, 0), ImGuiChildFlags.Borders))
                DrawSpriteList(archer);
            ImGui.EndChild();

            ImGui.SameLine();
            if (ImGui.BeginChild("sprite_fields", new Vector2(0, 0), ImGuiChildFlags.Borders))
                DrawSpriteFields();
            ImGui.EndChild();
        }

        ImGui.End();
    }

    private static Rectangle? focusRect;

    private static string PageLabel(Page page, Atlas atlas)
    {
        var size = page.Texture.Texture2D != null
            ? $"{page.Texture.Texture2D.Width}x{page.Texture.Texture2D.Height}"
            : "not loaded";
        var owner = ReferenceEquals(page.Texture, atlas) ? "atlas image" : "own texture";
        return $"{owner}  {size}  ({page.Subs.Count} textures, e.g. {page.Subs[0].Name})";
    }

    // the page holding the archer's body sprite texture
    private static void FocusArcherPage(ArcherData archer, List<Page> visiblePages, ref List<int> allowed)
    {
        var body = archer.Sprites.Body;
        if (string.IsNullOrEmpty(body) || !TFGame.SpriteData.Contains(body)) return;

        var textureName = TFGame.SpriteData.GetXML(body)["Texture"]?.InnerText.Trim();
        if (string.IsNullOrEmpty(textureName) || !TFGame.Atlas.Contains(textureName)) return;

        if (!allowed.Contains(0)) return;
        if (atlasIndex != 0)
        {
            // switch to the main atlas; the page is picked next frame
            atlasIndex = 0;
            wantArcherPage = true;
            return;
        }

        var texture = TFGame.Atlas[textureName].Texture;
        var index = visiblePages.FindIndex(p => ReferenceEquals(p.Texture, texture));
        if (index >= 0) pageIndex = index;
    }
    private static void DrawPage(ImGuiRenderer renderer, Page page, HashSet<Subtexture>? archerTextures, float width)
    {
        var texture2D = page.Texture.Texture2D;
        var filterText = filter.Trim();
        bool Matches(string name, Subtexture sub) =>
            (archerTextures == null || archerTextures.Contains(sub)) &&
            (filterText.Length == 0 || name.Contains(filterText, StringComparison.OrdinalIgnoreCase));

        var flags = ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse;
        if (!ImGui.BeginChild("atlas_canvas", new Vector2(width, 0), ImGuiChildFlags.Borders, flags))
        {
            ImGui.EndChild();
            return;
        }

        var io = ImGui.GetIO();
        var canvasMin = ImGui.GetCursorScreenPos();
        var canvasSize = ImGui.GetContentRegionAvail();

        // one button over the whole canvas takes the mouse: left = pick / drag rectangles, right = pan
        ImGui.SetNextItemAllowOverlap();
        ImGui.InvisibleButton("atlas_surface", canvasSize, ImGuiButtonFlags.MouseButtonLeft | ImGuiButtonFlags.MouseButtonRight);
        var surfaceHovered = ImGui.IsItemHovered();
        var surfaceClicked = ImGui.IsItemClicked(ImGuiMouseButton.Left);
        var panning = ImGui.IsItemActive() && ImGui.IsMouseDown(ImGuiMouseButton.Right);

        // a sprite was picked in the list: put its rectangle in the middle of the view
        if (focusRect is { } target)
        {
            focusRect = null;
            zoom = Math.Clamp(Math.Min(canvasSize.X, canvasSize.Y) / 2f / Math.Max(1, Math.Max(target.Width, target.Height)), 1f, 8f);
            pan = canvasSize / 2 - new Vector2(target.Center.X, target.Center.Y) * zoom;
        }

        if (panning)
            pan += io.MouseDelta;

        // wheel zooms around the mouse
        if (surfaceHovered && io.MouseWheel != 0)
        {
            var old = zoom;
            zoom = Math.Clamp(zoom * MathF.Pow(1.15f, io.MouseWheel), 0.25f, 24f);
            var local = io.MousePos - canvasMin;
            pan = local - (local - pan) * (zoom / old);
        }

        var origin = canvasMin + pan;
        var size = new Vector2(texture2D.Width, texture2D.Height) * zoom;
        var drawList = ImGui.GetWindowDrawList();
        drawList.PushClipRect(canvasMin, canvasMin + canvasSize, true);

        // dark backdrop so transparent art is readable
        drawList.AddRectFilled(origin, origin + size, ImGui.GetColorU32(new Vector4(0.1f, 0.1f, 0.12f, 1)));
        drawList.AddImage(ImGuiTextures.Get(renderer, texture2D), origin, origin + size);

        var hovered = -1;
        if (surfaceHovered && drag == Drag.None && !panning)
        {
            // smallest box under the cursor wins (textures can nest)
            var mouse = (io.MousePos - origin) / zoom;
            var best = long.MaxValue;
            for (var i = 0; i < page.Subs.Count; i++)
            {
                var (name, sub) = page.Subs[i];
                if (!Matches(name, sub) || !sub.Rect.Contains((int)mouse.X, (int)mouse.Y)) continue;

                var area = (long)sub.Rect.Width * sub.Rect.Height;
                if (area < best)
                {
                    best = area;
                    hovered = i;
                }
            }
        }

        var sprites = spriteMode == (int)OverlayMode.Off ? null : SpriteRefsFor(atlasIndex);
        var boxColor = ImGui.GetColorU32(new Vector4(1f, 0.85f, 0.2f, 0.9f));
        var hoverColor = ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 1f));
        var textColor = ImGui.GetColorU32(new Vector4(0.6f, 1f, 0.6f, 1f));

        for (var i = 0; i < page.Subs.Count; i++)
        {
            var (name, sub) = page.Subs[i];
            if (!Matches(name, sub)) continue;

            var isHovered = i == hovered;
            var min = origin + new Vector2(sub.Rect.X, sub.Rect.Y) * zoom;
            var max = origin + new Vector2(sub.Rect.Right, sub.Rect.Bottom) * zoom;

            if (boxMode == (int)OverlayMode.Always || (boxMode == (int)OverlayMode.Hover && isHovered))
                drawList.AddRect(min, max, isHovered ? hoverColor : boxColor, 0, ImDrawFlags.None, isHovered ? 2f : 1f);

            if (sprites != null && sprites.TryGetValue(name, out var refs) &&
                (spriteMode == (int)OverlayMode.Always || isHovered))
                DrawFrameGrid(drawList, origin, sub, refs[0].Xml);

            // ids inside small boxes would only clutter, so labels need some room unless hovered
            var room = (max.X - min.X) > name.Length * 3.5f;
            if ((idMode == (int)OverlayMode.Always && room) || (idMode == (int)OverlayMode.Hover && isHovered))
                drawList.AddText(min + new Vector2(2, 1), textColor, name);
        }

        // the selected sprite's rectangles (movable), with the arrows around the active one
        if (ShowSpritePanels)
            DrawSpriteOverlay(drawList, origin, page, surfaceClicked, surfaceHovered);

        drawList.PopClipRect();

        if (hovered >= 0 && drag == Drag.None && !ImGui.IsAnyItemActive())
        {
            var (name, sub) = page.Subs[hovered];
            var tip = $"{name}\n{sub.Width}x{sub.Height} @ {sub.X},{sub.Y}";
            if (sprites != null && sprites.TryGetValue(name, out var hoveredRefs))
                tip += "\n" + SpriteInfo(hoveredRefs, sub);
            ImGui.SetTooltip(tip);
        }

        ImGui.EndChild();
    }
}
