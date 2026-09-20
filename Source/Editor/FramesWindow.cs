using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Xml;
using ArcherEditorMod.Editor.ImGuiSupport;
using ArcherEditorMod.Source.Features;
using ImGuiNET;
using Monocle;
using MonoMod.Utils;
using TowerFall;

namespace ArcherEditorMod.Editor;

public enum FramesMode
{
    Head,
    Bow,
    Corpse,
    Sprite
}

/// <summary>
/// ImGui window that shows every frame of an animation and lets you nudge the per-frame offsets stored in the
/// body sprite xml: the head origin (HeadYOrigins / HeadXOrigins) or the bow offset (BowXOffsets / BowYOffsets).
/// Corpse sprites are shown too (view only). Edits go straight into the in-memory sprite XML and the live
/// editor players; the Save archer data window writes them back to the mod's spriteData.xml, "Copy XML" puts them on
/// the clipboard.
/// </summary>
public static class FramesWindow
{
    public static bool Open;

    private const float Scale = 5f;
    private const float CanvasW = 32, CanvasH = 40;   // game pixels
    private const float AnchorX = 16, AnchorY = 30;   // where the player's Position lands inside the canvas
    private const float ArrowSize = 22f;

    // One editable table. Screen deltas are converted with the sign: origins move the opposite way to offsets.
    private sealed class Table
    {
        public string YName = "", XName = "", YField = "", XField = "";
        public int YSign, XSign;          // value change per screen step (down/right = +1)
        public bool YRequired;            // head Y must already exist, bow tables are created on demand
        public int[]? Y, X;
    }

    private sealed record Snapshot(int[]? Y, int[]? X);

    private sealed class Ctx
    {
        public ImGuiRenderer Renderer = null!;
        public string BodyId = "";
        public XmlElement BodyXml = null!, HeadXml = null!, BowXml = null!;
        public Sprite<string> Body = null!, Head = null!, Bow = null!;
        public int HeadFrame, BowFrame;
        public bool HideBow;
        public Table HeadTable = null!, BowTable = null!, Active = null!;
        public bool Changed;
    }

    private static readonly Dictionary<string, Snapshot> originals = new();
    private static readonly Dictionary<string, Sprite<string>> spriteCache = new();
    private static readonly Dictionary<string, (string? Path, string LocalId)> files = new();
    private static FramesMode mode;
    private static string? animation;
    private static string status = "";
    private static bool repeatConfigured;

    /// <summary>Opens the window on the given body animation (ignored at draw time if the archer lacks it).</summary>
    public static void Show(string animationId)
    {
        animation = animationId;
        if (mode is FramesMode.Corpse or FramesMode.Sprite)
            mode = FramesMode.Head;
        Open = true;
    }

    private static string? spriteSource, spriteId;

    /// <summary>Opens the window on the animations of any sprite data entry (from the atlas window's sprite fields).</summary>
    public static void ShowSprite(string source, string id)
    {
        spriteSource = source;
        spriteId = id;
        animation = null;
        mode = FramesMode.Sprite;
        Open = true;
    }

    /// <summary>The atlas rectangles moved: the cut frames of every sprite have to be read again.</summary>
    public static void ClearCache() => spriteCache.Clear();

    public static void Draw(ImGuiRenderer renderer, Player player, Player.HatStates hat, IEnumerable<Player> livePlayers, Action? refresh = null)
    {
        if (!Open) return;

        DrawPicker(renderer);
        if (mode == FramesMode.Sprite)
        {
            DrawSprite(renderer, refresh);
            return;
        }

        if (!repeatConfigured)
        {
            // Held arrows: wait before repeating, then step slowly so the value can't overshoot
            var io = ImGui.GetIO();
            io.KeyRepeatDelay = 0.45f;
            io.KeyRepeatRate = 0.09f;
            repeatConfigured = true;
        }

        var data = player.ArcherData;
        var bodyId = data.Sprites.Body;
        var headId = data.Sprites[hat];
        var bowId = data.Sprites.Bow;
        if (!TFGame.SpriteData.Contains(bodyId) || !TFGame.SpriteData.Contains(headId) || !TFGame.SpriteData.Contains(bowId))
            return;

        var corpseId = data.Corpse;
        var hasCorpse = !string.IsNullOrEmpty(corpseId) && TFGame.CorpseSpriteData.Contains(corpseId);

        var ctx = new Ctx
        {
            Renderer = renderer,
            BodyId = bodyId,
            BodyXml = TFGame.SpriteData.GetXML(bodyId),
            HeadXml = TFGame.SpriteData.GetXML(headId),
            BowXml = TFGame.SpriteData.GetXML(bowId),
            Body = GetSprite(false, bodyId),
            Head = GetSprite(false, headId),
            Bow = GetSprite(false, bowId),
        };
        ctx.HideBow = ctx.BodyXml["HideBow"]?.InnerText.Trim().Equals("true", StringComparison.OrdinalIgnoreCase) ?? false;
        ctx.HeadFrame = FirstFrame(ctx.HeadXml, "idle");
        ctx.BowFrame = FirstFrame(ctx.BowXml, "idle");

        ctx.HeadTable = ReadTable(ctx.BodyXml, "HeadYOrigins", "HeadXOrigins", "headYOrigins", "headXOrigins", -1, -1, true);
        ctx.BowTable = ReadTable(ctx.BodyXml, "BowYOffsets", "BowXOffsets", "bowYOffsets", "bowXOffsets", 1, 1, false);
        ctx.Active = mode == FramesMode.Bow ? ctx.BowTable : ctx.HeadTable;
        Remember(bodyId, ctx.HeadTable);
        Remember(bodyId, ctx.BowTable);

        Dictionary<string, int[]> animations;
        if (mode == FramesMode.Corpse)
            animations = hasCorpse ? ReadAnimations(TFGame.CorpseSpriteData.GetXML(corpseId)) : new();
        else
            animations = ReadAnimations(ctx.BodyXml);
        if (animation == null || !animations.ContainsKey(animation))
            animation = animations.Keys.FirstOrDefault();

        ImGui.SetNextWindowSize(new Vector2(4 * 220, 420), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowPos(new Vector2(10, 260), ImGuiCond.FirstUseEver);
        if (!ImGui.Begin("Animation", ref Open))
        {
            ImGui.End();
            return;
        }

        if (ImGui.RadioButton("Head origin", mode == FramesMode.Head)) mode = FramesMode.Head;
        ImGui.SameLine();
        if (ImGui.RadioButton("Bow origin", mode == FramesMode.Bow)) mode = FramesMode.Bow;
        ImGui.SameLine();
        if (ImGui.RadioButton("Corpse", mode == FramesMode.Corpse)) mode = FramesMode.Corpse;

        ImGui.Text(mode == FramesMode.Corpse ? $"corpse: {corpseId}" : $"{bodyId}  /  {headId}  /  {bowId}");
        var structureChanged = false;
        if (mode != FramesMode.Corpse)
        {
            ImGui.SetNextItemWidth(180);
            if (ImGui.BeginCombo("Animation", animation ?? "-"))
            {
                foreach (var id in animations.Keys)
                {
                    if (ImGui.Selectable(id, id == animation))
                        animation = id;
                }
                ImGui.EndCombo();
            }

            DrawFileButtons(ctx);
            if (animation != null)
                structureChanged |= DrawAnimationAttributes(ctx.BodyXml, animation, "SpriteData", bodyId);
        }

        var hideBowChanged = false;
        if (mode == FramesMode.Bow)
        {
            var hide = ctx.HideBow;
            if (ImGui.Checkbox("Hide bow unless aiming (HideBow)", ref hide))
            {
                TrackXml(ctx.BodyXml, "HideBow", "SpriteData", bodyId);
                SetChild(ctx.BodyXml, "HideBow", hide ? "True" : "False", null);
                ctx.HideBow = hide;
                hideBowChanged = true;
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Archers like Red never show the bow while idle. Aiming always shows it, at the offsets edited here.");

            // the bow sprite's DownY: where the bow sits while aiming straight down (Player reads it from the bow xml)
            var down = 0;
            int.TryParse(ctx.BowXml["DownY"]?.InnerText.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out down);
            ImGui.SetNextItemWidth(110);
            if (ImGui.InputInt("Bow DownY", ref down))
            {
                TrackXml(ctx.BowXml, "Bow DownY", "SpriteData", data.Sprites.Bow);
                SetChild(ctx.BowXml, "DownY", down.ToString(CultureInfo.InvariantCulture), null);
                AtlasWindow.MarkSpriteDirty("SpriteData", data.Sprites.Bow);
            }
            structureChanged |= ImGui.IsItemDeactivatedAfterEdit();
        }
        else if (mode == FramesMode.Head)
        {
            // the body sprite's SlideHead: whether the head follows the body while sliding
            var slide = !string.Equals(ctx.BodyXml["SlideHead"]?.InnerText.Trim(), "false", StringComparison.OrdinalIgnoreCase);
            if (ImGui.Checkbox("Head follows while sliding (SlideHead)", ref slide))
            {
                TrackXml(ctx.BodyXml, "SlideHead", "SpriteData", bodyId);
                SetChild(ctx.BodyXml, "SlideHead", slide ? "True" : "False", null);
                AtlasWindow.MarkSpriteDirty("SpriteData", bodyId);
                structureChanged = true;
            }
        }

        ImGui.Separator();

        var cellWidth = CanvasW * Scale + 2 * ArrowSize + 16;
        var perRow = Math.Max(1, (int)(ImGui.GetContentRegionAvail().X / cellWidth));

        if (mode == FramesMode.Corpse)
        {
            // every corpse animation, one section each
            var corpse = hasCorpse ? GetSprite(true, corpseId) : null;
            if (corpse == null)
                ImGui.TextDisabled("This archer has no corpse sprite.");

            foreach (var (animationId, animationFrames) in animations)
            {
                if (corpse == null) break;

                ImGui.SeparatorText(animationId);
                var corpseFrames = animationFrames.Distinct().ToList();
                for (var i = 0; i < corpseFrames.Count; i++)
                {
                    if (i % perRow != 0) ImGui.SameLine();
                    ImGui.BeginGroup();
                    DrawCorpseCell(ctx, corpse, corpseFrames[i]);
                    ImGui.EndGroup();
                }
            }
        }
        else
        {
            var frames = animation != null && animations.ContainsKey(animation)
                ? animations[animation].ToList()
                : new List<int>();

            // the ALL panel comes first, in the same row as the frames
            ImGui.PushID("all");
            ImGui.BeginGroup();
            DrawAllCell(ctx);
            ImGui.EndGroup();
            ImGui.PopID();

            for (var i = 0; i < frames.Count; i++)
            {
                if ((i + 1) % perRow != 0) ImGui.SameLine();
                ImGui.PushID(i);
                ImGui.BeginGroup();
                DrawCell(ctx, frames[i]);
                ImGui.EndGroup();
                ImGui.PopID();

                if (FrameContextMenu(ctx.BodyXml, animation!, frames, i, ctx.Body, "SpriteData", bodyId))
                {
                    structureChanged = true;
                    break; // the list changed under the loop
                }
            }

            if (animation != null)
                AddFrameButton(ctx.BodyXml, animation, ctx.Body, "SpriteData", bodyId);
        }

        ImGui.End();

        if (structureChanged)
            refresh?.Invoke();

        if (hideBowChanged)
        {
            foreach (var live in livePlayers)
                DynamicData.For(live).Set("hideBow", ctx.HideBow);
        }

        if (!ctx.Changed) return;

        TrackXml(ctx.BodyXml, "Head / bow offsets", "SpriteData", bodyId);
        WriteTable(ctx.BodyXml, ctx.Active);
        foreach (var live in livePlayers)
        {
            var dynamicData = DynamicData.For(live);
            if (ctx.Active.Y != null) dynamicData.Set(ctx.Active.YField, (int[])ctx.Active.Y.Clone());
            if (ctx.Active.X != null) dynamicData.Set(ctx.Active.XField, (int[])ctx.Active.X.Clone());
        }
    }

    // ---- animations: the frame list and the attributes, edited in the sprite xml ----

    private static bool refreshRequested;

    // the sprite xml goes through the shared undo history; undo / redo mark it for saving again
    private static void TrackXml(XmlElement xml, string label, string source, string id) =>
        EditHistory.BeforeXmlEdit(xml, label, () =>
        {
            AtlasWindow.MarkSpriteDirty(source, id);
            ClearCache();
        });

    private static XmlElement? AnimXml(XmlElement spriteXml, string id) =>
        spriteXml["Animations"]?.GetElementsByTagName("Anim").Cast<XmlElement>().FirstOrDefault(a => a.GetAttribute("id") == id);

    private static bool DrawAnimationAttributes(XmlElement spriteXml, string animId, string source, string id)
    {
        var anim = AnimXml(spriteXml, animId);
        if (anim == null) return false;

        var changed = false;
        var delay = float.TryParse(anim.GetAttribute("delay"), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0f;
        ImGui.SetNextItemWidth(110);
        if (ImGui.InputFloat("Delay", ref delay, 0.01f, 0.05f, "%.3f"))
        {
            TrackXml(spriteXml, "Animation delay", source, id);
            anim.SetAttribute("delay", Math.Max(0f, delay).ToString("0.###", CultureInfo.InvariantCulture));
            AtlasWindow.MarkSpriteDirty(source, id);
        }
        changed |= ImGui.IsItemDeactivatedAfterEdit();

        var loop = !string.Equals(anim.GetAttribute("loop"), "false", StringComparison.OrdinalIgnoreCase);
        ImGui.SameLine();
        if (ImGui.Checkbox("Loop", ref loop))
        {
            TrackXml(spriteXml, "Animation loop", source, id);
            anim.SetAttribute("loop", loop ? "True" : "False");
            AtlasWindow.MarkSpriteDirty(source, id);
            changed = true;
        }

        return changed;
    }

    private static void WriteFrames(XmlElement anim, List<int> frames, string source, string id)
    {
        if (frames.Count == 0) return; // an animation keeps at least one frame

        anim.SetAttribute("frames", string.Join(",", frames));
        AtlasWindow.MarkSpriteDirty(source, id);
    }

    // right click a frame: pick another one, insert one after it, or delete it
    private static bool FrameContextMenu(XmlElement spriteXml, string animId, List<int> frames, int pos, Sprite<string> sprite,
        string source, string id)
    {
        if (!ImGui.BeginPopupContextItem($"framemenu{pos}")) return false;

        var changed = false;
        if (ImGui.MenuItem("Pick frame..."))
        {
            OpenPicker(source, id, sprite, frame => EditFrames(spriteXml, animId, source, id, list =>
            {
                if (pos < list.Count) list[pos] = frame;
            }));
        }

        if (ImGui.MenuItem("Insert frame after..."))
        {
            OpenPicker(source, id, sprite, frame => EditFrames(spriteXml, animId, source, id, list =>
                list.Insert(Math.Min(pos + 1, list.Count), frame)));
        }

        ImGui.Separator();
        if (ImGui.MenuItem("Delete", null, false, frames.Count > 1))
        {
            EditFrames(spriteXml, animId, source, id, list =>
            {
                if (pos < list.Count) list.RemoveAt(pos);
            });
            changed = true;
        }

        ImGui.EndPopup();
        return changed;
    }

    private static void AddFrameButton(XmlElement spriteXml, string animId, Sprite<string> sprite, string source, string id)
    {
        if (ImGui.Button("+ Add frame"))
            OpenPicker(source, id, sprite, frame => EditFrames(spriteXml, animId, source, id, list => list.Add(frame)));
    }

    // re-reads the list at the time of the edit: a picked frame arrives a few frames after the menu was used
    private static void EditFrames(XmlElement spriteXml, string animId, string source, string id, Action<List<int>> edit)
    {
        var anim = AnimXml(spriteXml, animId);
        if (anim == null) return;

        TrackXml(spriteXml, "Animation frames", source, id);
        var list = ParseCsv(anim.GetAttribute("frames")).ToList();
        edit(list);
        WriteFrames(anim, list, source, id);
        refreshRequested = true;
    }

    // ---- the frame picker: every frame of a sprite, closes on a click ----

    private static bool pickerOpen;
    private static Sprite<string>? pickerSprite;
    private static string pickerSource = "SpriteData", pickerId = "";
    private static string pickerFilter = "";
    private static Action<int>? pickerAction;

    private static void OpenPicker(string source, string id, Sprite<string> sprite, Action<int> onPick)
    {
        pickerSource = source;
        pickerId = id;
        pickerSprite = sprite;
        pickerAction = onPick;
        pickerOpen = true;
    }

    private static SpriteData? DataOf(string source) => source switch
    {
        "MenuSpriteData" => TFGame.MenuSpriteData,
        "CorpseSpriteData" => TFGame.CorpseSpriteData,
        "BGSpriteData" => TFGame.BGSpriteData,
        "BossSpriteData" => TFGame.BossSpriteData,
        _ => TFGame.SpriteData
    };

    private static Sprite<string>? GetSpriteOf(string source, string id)
    {
        var key = $"{source}:{id}";
        if (spriteCache.TryGetValue(key, out var cached)) return cached;

        var data = DataOf(source);
        if (data == null || !data.Contains(id)) return null;
        return spriteCache[key] = data.GetSpriteString(id);
    }

    private static void DrawPicker(ImGuiRenderer renderer)
    {
        if (!pickerOpen) return;

        ImGui.SetNextWindowSize(new Vector2(440, 380), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowPos(new Vector2(200, 200), ImGuiCond.FirstUseEver);
        if (!ImGui.Begin("Pick frame", ref pickerOpen))
        {
            ImGui.End();
            return;
        }

        try
        {
            // which sprite to take frames from: the one being edited, or any sprite of the game's sprite data
            ImGui.SetNextItemWidth(260);
            if (ImGui.BeginCombo("Sprite", pickerId))
            {
                ImGui.SetNextItemWidth(-1);
                ImGui.InputTextWithHint("##pickfilter", "filter", ref pickerFilter, 64);
                var filterText = pickerFilter.Trim();
                foreach (var source in new[] { "SpriteData", "CorpseSpriteData", "MenuSpriteData" })
                {
                    var data = DataOf(source);
                    if (data == null) continue;

                    var shown = 0;
                    foreach (var id in Monocle.SpriteDataExt.GetSprites(data).Keys.OrderBy(k => k))
                    {
                        if (filterText.Length > 0 && !id.Contains(filterText, StringComparison.OrdinalIgnoreCase)) continue;
                        if (shown++ > 200) break;

                        if (ImGui.Selectable($"{id}##{source}", id == pickerId && source == pickerSource) && GetSpriteOf(source, id) is { } sprite)
                        {
                            pickerSource = source;
                            pickerId = id;
                            pickerSprite = sprite;
                        }
                    }
                }
                ImGui.EndCombo();
            }

            var picked = pickerSprite;
            if (picked == null || picked.FrameRects.Length == 0)
            {
                ImGui.TextDisabled("This sprite has no frames.");
                return;
            }

            ImGui.TextDisabled($"{picked.FrameRects.Length} frames. Click one to use it.");
            var drawList = ImGui.GetWindowDrawList();
            var perRow = Math.Max(1, (int)(ImGui.GetContentRegionAvail().X / 84f));
            for (var frame = 0; frame < picked.FrameRects.Length; frame++)
            {
                if (frame % perRow != 0) ImGui.SameLine();

                var rect = picked.FrameRects[frame];
                var box = new Vector2(76, 76);
                var start = ImGui.GetCursorScreenPos();
                var clicked = ImGui.InvisibleButton($"pick{frame}", box);
                var hovered = ImGui.IsItemHovered();

                drawList.AddRectFilled(start, start + box, ImGui.GetColorU32(hovered ? new Vector4(0.25f, 0.3f, 0.4f, 1) : new Vector4(0.12f, 0.12f, 0.14f, 1)));
                var scale = Math.Max(1f, MathF.Floor(Math.Min(64f / Math.Max(1, rect.Width), 64f / Math.Max(1, rect.Height))));
                var size = new Vector2(rect.Width, rect.Height) * scale;
                var (id, uv0, uv1) = ImGuiTextures.Region(renderer, picked.Texture.Texture2D, rect);
                var at = start + (box - size) / 2;
                drawList.AddImage(id, at, at + size, uv0, uv1);
                drawList.AddText(start + new Vector2(3, 1), 0xFFFFFFFF, frame.ToString());

                if (clicked)
                {
                    var action = pickerAction;
                    pickerOpen = false;
                    action?.Invoke(frame);
                    break;
                }
            }
        }
        finally
        {
            ImGui.End();
        }
    }

    // ---- any sprite: the frames of its animations, edited in place ----

    private static void DrawSprite(ImGuiRenderer renderer, Action? refresh)
    {
        if (refreshRequested)
        {
            refreshRequested = false;
            refresh?.Invoke();
        }

        ImGui.SetNextWindowSize(new Vector2(4 * 190, 420), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowPos(new Vector2(10, 260), ImGuiCond.FirstUseEver);
        if (!ImGui.Begin("Animation", ref Open))
        {
            ImGui.End();
            return;
        }

        try
        {
            var source = spriteSource ?? "SpriteData";
            var id = spriteId ?? "";
            var data = DataOf(source);
            var sprite = GetSpriteOf(source, id);
            if (data == null || sprite == null)
            {
                ImGui.TextDisabled($"'{id}' is not in {source}.");
                if (ImGui.Button("Back to the archer")) mode = FramesMode.Head;
                return;
            }

            var xml = data.GetXML(id);
            ImGui.Text($"{source}: {id}");
            ImGui.SameLine();
            if (ImGui.SmallButton("Back to the archer")) mode = FramesMode.Head;

            var animations = ReadAnimations(xml);
            if (animation == null || !animations.ContainsKey(animation))
                animation = animations.Keys.FirstOrDefault();

            ImGui.SetNextItemWidth(180);
            if (ImGui.BeginCombo("Animation", animation ?? "-"))
            {
                foreach (var name in animations.Keys)
                {
                    if (ImGui.Selectable(name, name == animation))
                        animation = name;
                }
                ImGui.EndCombo();
            }

            var changed = false;
            if (animation != null)
                changed |= DrawAnimationAttributes(xml, animation, source, id);

            ImGui.Separator();

            var frames = animation != null && animations.ContainsKey(animation) ? animations[animation].ToList() : new List<int>();
            var drawList = ImGui.GetWindowDrawList();
            const float sizeScale = 4f;
            var cell = new Vector2(64, 64);
            if (sprite.FrameRects.Length > 0)
                cell = new Vector2(sprite.FrameRects[0].Width, sprite.FrameRects[0].Height) * sizeScale + new Vector2(8, 8);
            var perRow = Math.Max(1, (int)(ImGui.GetContentRegionAvail().X / (cell.X + 8)));

            for (var pos = 0; pos < frames.Count; pos++)
            {
                if (pos % perRow != 0) ImGui.SameLine();

                ImGui.PushID(pos);
                ImGui.BeginGroup();
                var start = ImGui.GetCursorScreenPos();
                ImGui.Dummy(cell);
                drawList.AddRectFilled(start, start + cell, ImGui.GetColorU32(new Vector4(0.12f, 0.12f, 0.14f, 1)));
                var frame = frames[pos];
                if (frame >= 0 && frame < sprite.FrameRects.Length)
                {
                    var rect = sprite.FrameRects[frame];
                    var (tex, uv0, uv1) = ImGuiTextures.Region(renderer, sprite.Texture.Texture2D, rect);
                    var size = new Vector2(rect.Width, rect.Height) * sizeScale;
                    var at = start + (cell - size) / 2;
                    drawList.AddImage(tex, at, at + size, uv0, uv1);
                }
                else
                {
                    drawList.AddText(start + new Vector2(4, 4), 0xFF8080FF, "no such frame");
                }

                ImGui.Text($"#{pos}  frame {frame}");
                ImGui.EndGroup();
                ImGui.PopID();

                if (FrameContextMenu(xml, animation!, frames, pos, sprite, source, id))
                {
                    changed = true;
                    break;
                }
            }

            if (animation != null)
                AddFrameButton(xml, animation, sprite, source, id);
            ImGui.TextDisabled("Right click a frame: pick, insert, delete.");

            if (changed)
                refresh?.Invoke();
        }
        finally
        {
            ImGui.End();
        }
    }

    // ---- toolbar ----

    private static void DrawFileButtons(Ctx ctx)
    {
        var tables = new[] { ctx.HeadTable, ctx.BowTable };

        ImGui.SameLine();
        if (ImGui.Button("Copy XML"))
        {
            var lines = tables.SelectMany(TableLines).ToList();
            if (ctx.BodyXml["HideBow"] != null)
                lines.Add($"<HideBow>{ctx.BodyXml["HideBow"]!.InnerText}</HideBow>");
            ImGui.SetClipboardText(string.Join("\n", lines));
        }

        ImGui.SameLine();
        ImGui.TextDisabled("(saved with Save archer data)");
    }

    // ---- saving: done by the Save archer data window ----

    private static (string? Path, string LocalId) FileOf(string bodyId)
    {
        // Backtrack the sprite id ("Mod/PlayerBody") to the spriteData.xml the mod loaded it from
        if (!files.TryGetValue(bodyId, out var file))
        {
            var path = ArcherDecorationRegistry.FindSpriteDataFile(bodyId, out var localId);
            files[bodyId] = file = (path, localId);
        }
        return file;
    }

    /// <summary>Why the head / bow offsets of this archer can't be written back, or null when they can.</summary>
    public static string? OffsetsCannotSaveReason(ArcherData data) =>
        FileOf(data.Sprites.Body).Path == null
            ? "The body sprite's spriteData.xml is not on disk (base game sprite or zipped mod)."
            : null;

    /// <summary>True when the head / bow offsets or HideBow were edited since the editor first saw them.</summary>
    public static bool OffsetsChanged(ArcherData data)
    {
        var bodyId = data.Sprites.Body;
        if (!TFGame.SpriteData.Contains(bodyId)) return false;

        var body = TFGame.SpriteData.GetXML(bodyId);
        var head = ReadTable(body, "HeadYOrigins", "HeadXOrigins", "headYOrigins", "headXOrigins", -1, -1, true);
        var bow = ReadTable(body, "BowYOffsets", "BowXOffsets", "bowYOffsets", "bowXOffsets", 1, 1, false);
        return Differs(bodyId, head) || Differs(bodyId, bow);
    }

    private static bool Differs(string bodyId, Table table)
    {
        if (!originals.TryGetValue(Key(bodyId, table), out var original)) return false;
        return !(original.Y ?? Array.Empty<int>()).SequenceEqual(table.Y ?? Array.Empty<int>())
               || !(original.X ?? Array.Empty<int>()).SequenceEqual(table.X ?? Array.Empty<int>());
    }

    /// <returns>The spriteData.xml that was written.</returns>
    public static string SaveOffsets(ArcherData data)
    {
        var bodyId = data.Sprites.Body;
        var file = FileOf(bodyId);
        if (file.Path == null)
            throw new InvalidOperationException(OffsetsCannotSaveReason(data));

        var body = TFGame.SpriteData.GetXML(bodyId);
        var tables = new[]
        {
            ReadTable(body, "HeadYOrigins", "HeadXOrigins", "headYOrigins", "headXOrigins", -1, -1, true),
            ReadTable(body, "BowYOffsets", "BowXOffsets", "bowYOffsets", "bowXOffsets", 1, 1, false)
        };
        SaveToFile(file.Path, file.LocalId, tables, body["HideBow"]?.InnerText);
        return file.Path;
    }

    private static IEnumerable<string> TableLines(Table table)
    {
        if (table.Y != null) yield return $"<{table.YName}>{string.Join(",", table.Y)}</{table.YName}>";
        if (table.X != null) yield return $"<{table.XName}>{string.Join(",", table.X)}</{table.XName}>";
    }

    // ---- cells ----

    private static bool BeginCell(out Vector2 start, out Vector2 canvasMin, out Vector2 canvasMax, out ImDrawListPtr drawList)
    {
        start = ImGui.GetCursorScreenPos();
        var canvasSize = new Vector2(CanvasW * Scale, CanvasH * Scale);
        canvasMin = start + new Vector2(ArrowSize + 4, ArrowSize + 2);
        canvasMax = canvasMin + canvasSize;
        drawList = ImGui.GetWindowDrawList();

        // reserve the whole cell (arrows on every side + label row)
        ImGui.Dummy(new Vector2(canvasSize.X + 2 * ArrowSize + 8, canvasSize.Y + 2 * ArrowSize + 22));

        drawList.AddRectFilled(canvasMin, canvasMax, ImGui.GetColorU32(new Vector4(0.12f, 0.12f, 0.14f, 1)));
        drawList.AddRect(canvasMin, canvasMax, ImGui.GetColorU32(new Vector4(0.6f, 0.6f, 0.6f, 1)));

        var anchor = canvasMin + new Vector2(AnchorX, AnchorY) * Scale;
        drawList.AddLine(anchor - new Vector2(6, 0), anchor + new Vector2(6, 0), 0x8000FFFF);
        drawList.AddLine(anchor - new Vector2(0, 6), anchor + new Vector2(0, 6), 0x8000FFFF);
        return true;
    }

    private static void DrawCorpseCell(Ctx ctx, Sprite<string> corpse, int frame)
    {
        BeginCell(out var start, out var canvasMin, out var canvasMax, out var drawList);
        var anchor = canvasMin + new Vector2(AnchorX, AnchorY) * Scale;

        // PlayerCorpse puts its sprite 8px below the hitbox top; sit the feet on the same line as the archer's
        var width = corpse.FrameRects.Length > 0 ? corpse.FrameRects[0].Width : 0;
        var height = corpse.FrameRects.Length > 0 ? corpse.FrameRects[0].Height : 0;
        DrawFrame(ctx.Renderer, drawList, corpse, frame, anchor + new Vector2(-width / 2f, 8 - height) * Scale);

        ImGui.SetCursorScreenPos(new Vector2(start.X, canvasMax.Y + ArrowSize + 4));
        ImGui.Text($"frame {frame}");
    }

    // "ALL": moves the head origin / bow offset of every frame at once
    private static void DrawAllCell(Ctx ctx)
    {
        BeginCell(out var start, out var canvasMin, out var canvasMax, out var drawList);
        var canvasSize = canvasMax - canvasMin;
        var active = ctx.Active;

        var label = "ALL";
        drawList.AddText(canvasMin + (canvasSize - ImGui.CalcTextSize(label)) / 2, 0xFFFFFFFF, label);

        var midX = canvasMin.X + (canvasSize.X - ArrowSize) / 2;
        var midY = canvasMin.Y + (canvasSize.Y - ArrowSize) / 2;
        var dx = 0;
        var dy = 0;

        if (Arrow("allup", ImGuiDir.Up, new Vector2(midX, start.Y))) dy = -1;
        if (Arrow("alldown", ImGuiDir.Down, new Vector2(midX, canvasMax.Y + 2))) dy = 1;
        if (Arrow("allleft", ImGuiDir.Left, new Vector2(start.X, midY))) dx = -1;
        if (Arrow("allright", ImGuiDir.Right, new Vector2(canvasMax.X + 4, midY))) dx = 1;

        if (dy != 0)
        {
            Ensure(ctx, active, ref active.Y, 0, YDefault(ctx, active));
            for (var i = 0; i < active.Y!.Length; i++) active.Y[i] += active.YSign * dy;
            ctx.Changed = true;
        }

        if (dx != 0)
        {
            Ensure(ctx, active, ref active.X, 0, XDefault(ctx, active));
            for (var i = 0; i < active.X!.Length; i++) active.X[i] += active.XSign * dx;
            ctx.Changed = true;
        }

        ImGui.SetCursorScreenPos(new Vector2(start.X, canvasMax.Y + ArrowSize + 4));
        ImGui.Text("every frame");
    }

    private static void DrawCell(Ctx ctx, int frame)
    {
        BeginCell(out var start, out var canvasMin, out var canvasMax, out var drawList);
        var canvasSize = canvasMax - canvasMin;
        var anchor = canvasMin + new Vector2(AnchorX, AnchorY) * Scale;
        var head = ctx.HeadTable;
        var bow = ctx.BowTable;
        var active = ctx.Active;

        var editable = active.YRequired
            ? active.Y != null && frame < active.Y.Length
            : frame < ctx.Body.FramesTotal;

        // body
        DrawFrame(ctx.Renderer, drawList, ctx.Body, frame,
            anchor + (Vec(ctx.BodyXml, "X", "Y") - Vec(ctx.BodyXml, "OriginX", "OriginY")) * Scale);

        // head, positioned like Player.UpdateHead does
        var headOrigin = Vec(ctx.HeadXml, "OriginX", "OriginY");
        if (head.Y != null && frame < head.Y.Length)
            headOrigin.Y = head.Y[frame];
        if (head.X != null && frame < head.X.Length)
            headOrigin.X = head.X[frame];
        DrawFrame(ctx.Renderer, drawList, ctx.Head, ctx.HeadFrame,
            anchor + (Vec(ctx.HeadXml, "X", "Y") - headOrigin) * Scale);

        // bow, like Player.UpdateBow (facing right, idle pose)
        if (!ctx.HideBow || mode == FramesMode.Bow)
        {
            var bowPosition = Vec(ctx.BowXml, "X", "Y");
            if (bow.X != null && frame < bow.X.Length) bowPosition.X += bow.X[frame];
            if (bow.Y != null && frame < bow.Y.Length) bowPosition.Y += bow.Y[frame];
            DrawFrame(ctx.Renderer, drawList, ctx.Bow, ctx.BowFrame,
                anchor + (bowPosition - Vec(ctx.BowXml, "OriginX", "OriginY")) * Scale);
        }

        if (!editable)
        {
            drawList.AddText(canvasMin + new Vector2(4, 4), 0xFF8080FF, "no offset entry");
            ImGui.SetCursorScreenPos(new Vector2(start.X, canvasMax.Y + ArrowSize + 4));
            ImGui.Text($"frame {frame}");
            return;
        }

        var midX = canvasMin.X + (canvasSize.X - ArrowSize) / 2;
        var midY = canvasMin.Y + (canvasSize.Y - ArrowSize) / 2;
        var dx = 0;
        var dy = 0;

        if (Arrow($"up{frame}", ImGuiDir.Up, new Vector2(midX, start.Y))) dy = -1;
        if (Arrow($"down{frame}", ImGuiDir.Down, new Vector2(midX, canvasMax.Y + 2))) dy = 1;
        if (Arrow($"left{frame}", ImGuiDir.Left, new Vector2(start.X, midY))) dx = -1;
        if (Arrow($"right{frame}", ImGuiDir.Right, new Vector2(canvasMax.X + 4, midY))) dx = 1;

        // reset, top-left corner of the frame rectangle
        ImGui.SetCursorScreenPos(canvasMin + new Vector2(2, 2));
        if (ImGui.SmallButton($"reset##{frame}"))
            ctx.Changed |= Reset(ctx, active, frame);

        if (dy != 0)
        {
            Ensure(ctx, active, ref active.Y, frame, YDefault(ctx, active));
            active.Y![frame] += active.YSign * dy;
            ctx.Changed = true;
        }

        if (dx != 0)
        {
            Ensure(ctx, active, ref active.X, frame, XDefault(ctx, active));
            active.X![frame] += active.XSign * dx;
            ctx.Changed = true;
        }

        ImGui.SetCursorScreenPos(new Vector2(start.X, canvasMax.Y + ArrowSize + 4));
        var x = active.X != null && frame < active.X.Length ? active.X[frame] : XDefault(ctx, active);
        var y = active.Y != null && frame < active.Y.Length ? active.Y[frame] : YDefault(ctx, active);
        ImGui.Text($"frame {frame}  x={x}  y={y}");
    }

    private static int XDefault(Ctx ctx, Table table) =>
        table == ctx.HeadTable ? (int)Vec(ctx.HeadXml, "OriginX", "OriginY").X : 0;

    private static int YDefault(Ctx ctx, Table table) =>
        table == ctx.HeadTable ? (int)Vec(ctx.HeadXml, "OriginX", "OriginY").Y : 0;

    // tables are created / grown lazily so a frame that has no entry yet can be edited
    private static void Ensure(Ctx ctx, Table table, ref int[]? array, int frame, int fill)
    {
        var size = table == ctx.HeadTable && table.Y != null
            ? Math.Max(table.Y.Length, frame + 1)
            : Math.Max(ctx.Body.FramesTotal, frame + 1);

        if (array == null)
        {
            array = Enumerable.Repeat(fill, size).ToArray();
        }
        else if (array.Length < size)
        {
            var grown = Enumerable.Repeat(fill, size).ToArray();
            array.CopyTo(grown, 0);
            array = grown;
        }
    }

    private static bool Reset(Ctx ctx, Table table, int frame)
    {
        var changed = false;
        originals.TryGetValue(Key(ctx.BodyId, table), out var original);

        if (table.Y != null && frame < table.Y.Length)
        {
            var value = original?.Y != null && frame < original.Y.Length ? original.Y[frame] : YDefault(ctx, table);
            changed |= table.Y[frame] != value;
            table.Y[frame] = value;
        }

        if (table.X != null && frame < table.X.Length)
        {
            var value = original?.X != null && frame < original.X.Length ? original.X[frame] : XDefault(ctx, table);
            changed |= table.X[frame] != value;
            table.X[frame] = value;
        }

        return changed;
    }

    private static bool Arrow(string id, ImGuiDir dir, Vector2 position)
    {
        ImGui.SetCursorScreenPos(position);
        ImGui.PushItemFlag(ImGuiItemFlags.ButtonRepeat, true);
        var pressed = ImGui.ArrowButton(id, dir);
        ImGui.PopItemFlag();
        return pressed;
    }

    private static void DrawFrame(ImGuiRenderer renderer, ImDrawListPtr drawList, Sprite<string> sprite, int frame,
        Vector2 topLeft)
    {
        if (frame < 0 || frame >= sprite.FrameRects.Length) return;

        var rect = sprite.FrameRects[frame];
        var (id, uv0, uv1) = ImGuiTextures.Region(renderer, sprite.Texture.Texture2D, rect);
        drawList.AddImage(id, topLeft, topLeft + new Vector2(rect.Width, rect.Height) * Scale, uv0, uv1);
    }

    // ---- xml / tables ----

    private static Table ReadTable(XmlElement bodyXml, string yName, string xName, string yField, string xField,
        int ySign, int xSign, bool yRequired) => new()
    {
        YName = yName, XName = xName, YField = yField, XField = xField,
        YSign = ySign, XSign = xSign, YRequired = yRequired,
        Y = ReadCsv(bodyXml[yName]), X = ReadCsv(bodyXml[xName])
    };

    private static string Key(string bodyId, Table table) => $"{bodyId}|{table.YName}";

    // first sighting per archer = the values Reset returns to
    private static void Remember(string bodyId, Table table)
    {
        var key = Key(bodyId, table);
        if (!originals.ContainsKey(key))
            originals[key] = new Snapshot((int[]?)table.Y?.Clone(), (int[]?)table.X?.Clone());
    }

    private static void WriteTable(XmlElement bodyXml, Table table)
    {
        if (table.Y != null) SetChild(bodyXml, table.YName, string.Join(",", table.Y), null);
        if (table.X != null) SetChild(bodyXml, table.XName, string.Join(",", table.X), null);
    }

    private static void SaveToFile(string path, string localId, IEnumerable<Table> tables, string? hideBow)
    {
        var doc = new XmlDocument { PreserveWhitespace = true };
        doc.Load(path);

        var sprite = doc.SelectSingleNode($"//*[@id='{localId}']") as XmlElement
            ?? throw new Exception($"'{localId}' not found in {path}");

        foreach (var table in tables)
        {
            var after = sprite[table.YName] ?? sprite[table.XName];
            if (table.Y != null)
                after = SetChild(sprite, table.YName, string.Join(",", table.Y), after ?? (XmlNode?)sprite.LastChild);
            if (table.X != null)
                SetChild(sprite, table.XName, string.Join(",", table.X), after ?? (XmlNode?)sprite.LastChild);
        }

        if (hideBow != null)
            SetChild(sprite, "HideBow", hideBow, sprite["HideBow"] ?? (XmlNode?)sprite.LastChild);

        doc.Save(path);
    }

    private static XmlElement SetChild(XmlElement parent, string name, string value, XmlNode? after)
    {
        var element = parent[name];
        if (element == null)
        {
            element = parent.OwnerDocument.CreateElement(name);
            if (after != null)
            {
                // copy the indentation of the neighbouring node so the file stays tidy
                var indent = after.PreviousSibling as XmlWhitespace;
                parent.InsertAfter(element, after);
                if (indent != null)
                    parent.InsertBefore(parent.OwnerDocument.CreateWhitespace(indent.Value), element);
            }
            else
            {
                parent.AppendChild(element);
            }
        }

        element.InnerText = value;
        return element;
    }

    private static Sprite<string> GetSprite(bool corpse, string id)
    {
        var key = (corpse ? "c:" : "s:") + id;
        if (!spriteCache.TryGetValue(key, out var sprite))
        {
            spriteCache[key] = sprite = corpse
                ? TFGame.CorpseSpriteData.GetSpriteString(id)
                : TFGame.SpriteData.GetSpriteString(id);
        }
        return sprite;
    }

    private static Vector2 Vec(XmlElement xml, string x, string y) => new(Num(xml, x), Num(xml, y));

    private static float Num(XmlElement xml, string name) =>
        float.TryParse(xml[name]?.InnerText, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : 0f;

    private static int[]? ReadCsv(XmlElement? element) => element == null ? null : ParseCsv(element.InnerText);

    private static int[] ParseCsv(string text)
    {
        text = text.Trim();
        return text.Length == 0
            ? Array.Empty<int>()
            : text.Split(',').Select(v => int.Parse(v.Trim(), CultureInfo.InvariantCulture)).ToArray();
    }

    private static Dictionary<string, int[]> ReadAnimations(XmlElement spriteXml)
    {
        var result = new Dictionary<string, int[]>();
        var animations = spriteXml["Animations"];
        if (animations == null) return result;

        foreach (XmlElement anim in animations.GetElementsByTagName("Anim"))
            result[anim.GetAttribute("id")] = ParseCsv(anim.GetAttribute("frames"));
        return result;
    }

    private static int FirstFrame(XmlElement spriteXml, string animationId)
    {
        var animations = ReadAnimations(spriteXml);
        return animations.TryGetValue(animationId, out var frames) && frames.Length > 0 ? frames[0] : 0;
    }
}
