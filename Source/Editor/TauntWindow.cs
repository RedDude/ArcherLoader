using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using ArcherEditorMod.Editor.ImGuiSupport;
using ArcherEditorMod.Source.Features.Taunt;
using ImGuiNET;
using Monocle;
using TowerFall;

namespace ArcherEditorMod.Editor;

/// <summary>
/// Configures the archer's taunt: which spriteData entry holds the taunt animations, the texture for each hat
/// and team variant, self destruction, and the sound names. Changes apply to the taunt mocks at once; saving goes
/// through "Save archer data...". The taunt mock section shows the archer taunting.
/// </summary>
public static class TauntWindow
{
    public static bool Open;

    private static readonly Vector4 Yellow = new(1f, 0.85f, 0.3f, 1);
    private static readonly Vector4 Red = new(1f, 0.4f, 0.4f, 1);

    private static List<string>? candidates;
    private static string status = "";
    private static bool rebuild;

    private static readonly (string Group, string[] Tags, string[] Labels)[] groups =
    {
        ("Neutral", new[] { "Texture", "NoHatTexture", "CrownTexture" }, new[] { "Hat", "No hat", "Crown" }),
        ("Blue team", new[] { "TextureBlue", "NoHatTextureBlue", "CrownTextureBlue" }, new[] { "Hat", "No hat", "Crown" }),
        ("Red team", new[] { "TextureRed", "NoHatTextureRed", "CrownTextureRed" }, new[] { "Hat", "No hat", "Crown" }),
    };

    // which taunt animations the chosen sprite has: Texture -> "taunt", NoHat -> "tauntNoHat", Crown -> "tauntCrown"
    private static string AnimationFor(string tag) =>
        tag.StartsWith("NoHat") ? "tauntNoHat" : tag.StartsWith("Crown") ? "tauntCrown" : "taunt";

    public static void Draw(ImGuiRenderer renderer, Player player, Action refreshEditor, Action showTauntSection)
    {
        if (!Open) return;

        ImGui.SetNextWindowSize(new Vector2(560, 520), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowPos(new Vector2(900, 120), ImGuiCond.FirstUseEver);
        if (!ImGui.Begin("Taunt", ref Open))
        {
            ImGui.End();
            return;
        }

        var data = player.ArcherData;
        // a taunt sprite has to play all three: taunt (hat), tauntNoHat and tauntCrown
        candidates ??= TauntFeature.SpriteIdsWithTaunt()
            .Where(id => AnimationsOf(id).IsSupersetOf(new[] { "taunt", "tauntNoHat", "tauntCrown" })).ToList();

        if (!TauntFeature.TryGet(data, out var info))
        {
            ImGui.TextWrapped("This archer has no taunt. A taunt reuses a spriteData entry that has taunt / tauntNoHat / " +
                              "tauntCrown animations and swaps in this archer's textures.");

            ImGui.BeginDisabled(candidates.Count == 0);
            if (ImGui.Button("Create taunt"))
            {
                var created = new TauntInfo { SpriteId = candidates[0], IdText = candidates[0] };
                TauntFeature.Set(data, created);
                refreshEditor();
            }
            ImGui.EndDisabled();

            if (candidates.Count == 0)
                ImGui.TextColored(Yellow, "No spriteData entry has all three taunt animations (taunt, tauntNoHat, tauntCrown). A mod has to register one first.");

            ImGui.End();
            return;
        }

        var refresh = false;

        // ---- which sprite ----
        ImGui.SeparatorText("Animation source");
        ImGui.SetNextItemWidth(340);
        if (ImGui.BeginCombo("Sprite id", info.SpriteId))
        {
            foreach (var candidate in candidates)
            {
                if (ImGui.Selectable(candidate, candidate == info.SpriteId))
                {
                    info.SpriteId = candidate;
                    info.IdText = candidate;
                    refresh = true;
                    rebuild = true;
                }
            }
            ImGui.EndCombo();
        }

        var animations = AnimationsOf(info.SpriteId);

        // ---- textures ----
        foreach (var (group, tags, labels) in groups)
        {
            ImGui.SeparatorText(group);
            for (var i = 0; i < tags.Length; i++)
            {
                var tag = tags[i];
                var missing = !animations.Contains(AnimationFor(tag));
                var texture = TauntFeature.GetTexture(info, tag);
                info.TextureNames.TryGetValue(tag, out var name);

                if (missing) ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.6f, 0.6f, 0.6f, 1));
                if (TexturePicker.Draw(renderer, info.GetHashCode() + tag, labels[i], ref name, ref texture))
                {
                    if (name == null) info.TextureNames.Remove(tag);
                    else info.TextureNames[tag] = name;
                    TauntFeature.SetTexture(info, tag, texture);
                    rebuild = true;
                }
                if (missing) ImGui.PopStyleColor();

                if (missing && ImGui.IsItemHovered())
                    ImGui.SetTooltip($"The sprite '{info.SpriteId}' has no '{AnimationFor(tag)}' animation, so this texture is unused.");
            }
        }

        // ---- behaviour ----
        ImGui.SeparatorText("Behaviour");
        ImGui.Checkbox("Explode when the taunt ends (SelfDestruction)", ref info.SelfDestruction);

        ImGui.TextDisabled("Sounds are loaded from the mod's folder on the next start; names are saved as written.");
        var sfx = info.SfxName ?? "";
        var looped = info.SfxLoopedName ?? "";
        var varied = info.SfxVariedName ?? "";
        ImGui.SetNextItemWidth(220);
        if (ImGui.InputText("SFX", ref sfx, 96)) info.SfxName = sfx.Length == 0 ? null : sfx;
        ImGui.SetNextItemWidth(220);
        if (ImGui.InputText("SFXLooped", ref looped, 96)) info.SfxLoopedName = looped.Length == 0 ? null : looped;
        ImGui.SetNextItemWidth(220);
        if (ImGui.InputText("SFXVaried", ref varied, 96)) info.SfxVariedName = varied.Length == 0 ? null : varied;

        // ---- apply ----
        if (rebuild)
        {
            rebuild = false;
            try
            {
                TauntFeature.Rebuild(info);
                status = "";
            }
            catch (Exception e)
            {
                status = e.Message;
            }
        }

        ImGui.Separator();
        ImGui.TextDisabled($"Plays for: hat {Flag(info.HasTaunt)}  no hat {Flag(info.HasTauntNoHat)}  crown {Flag(info.HasTauntCrown)}" +
                           $"   blue {Flag(info.HasTauntBlue || info.HasTauntNoHatBlue || info.HasTauntCrownBlue)}" +
                           $"   red {Flag(info.HasTauntRed || info.HasTauntNoHatRed || info.HasTauntCrownRed)}");
        if (!info.HasTaunt && !info.HasTauntNoHat && !info.HasTauntCrown &&
            !info.HasTauntBlue && !info.HasTauntNoHatBlue && !info.HasTauntCrownBlue &&
            !info.HasTauntRed && !info.HasTauntNoHatRed && !info.HasTauntCrownRed)
            ImGui.TextColored(Yellow, "Nothing plays yet: pick at least one texture whose animation exists.");
        if (status.Length > 0)
            ImGui.TextColored(Red, status);

        if (ImGui.Button("Show the taunt mocks"))
            showTauntSection();

        ImGui.SameLine();
        if (ImGui.Button("Copy XML"))
            ImGui.SetClipboardText(ToXml(info, info.IdText ?? info.SpriteId));

        ImGui.SameLine();
        if (ImGui.Button("Remove taunt"))
        {
            TauntFeature.Remove(data);
            refresh = true;
        }

        ImGui.TextDisabled("Use \"Save archer data...\" to write it to a mod.");

        ImGui.End();

        if (refresh)
        {
            status = "";
            refreshEditor();
        }
    }

    private static string Flag(bool value) => value ? "yes" : "no";

    private static readonly Dictionary<string, HashSet<string>> animationCache = new();

    private static HashSet<string> AnimationsOf(string spriteId)
    {
        if (animationCache.TryGetValue(spriteId, out var known))
            return known;

        var set = new HashSet<string>();
        if (TFGame.SpriteData.Contains(spriteId))
        {
            var animations = TFGame.SpriteData.GetXML(spriteId)["Animations"];
            if (animations != null)
            {
                foreach (System.Xml.XmlElement anim in animations.GetElementsByTagName("Anim"))
                    set.Add(anim.GetAttribute("id"));
            }
        }

        return animationCache[spriteId] = set;
    }

    /// <summary>The &lt;Taunt&gt; block as archerCustomData.xml expects it.</summary>
    internal static string ToXml(TauntInfo info, string idText)
    {
        var builder = new StringBuilder();
        builder.AppendLine("<Taunt>");
        void Line(string name, string value) =>
            builder.AppendLine($"  <{name}>{System.Security.SecurityElement.Escape(value)}</{name}>");

        Line("Id", idText);
        foreach (var tag in TauntFeature.TextureTags)
        {
            if (info.TextureNames.TryGetValue(tag, out var name) && !string.IsNullOrWhiteSpace(name))
                Line(tag, name);
        }

        if (info.SelfDestruction) Line("SelfDestruction", "true");
        if (!string.IsNullOrWhiteSpace(info.SfxName)) Line("SFX", info.SfxName);
        if (!string.IsNullOrWhiteSpace(info.SfxLoopedName)) Line("SFXLooped", info.SfxLoopedName);
        if (!string.IsNullOrWhiteSpace(info.SfxVariedName)) Line("SFXVaried", info.SfxVariedName);
        builder.AppendLine("</Taunt>");
        return builder.ToString();
    }
}
