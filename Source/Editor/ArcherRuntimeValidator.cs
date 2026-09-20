using System.Collections.Generic;
using System.Linq;
using System.Xml;
using ArcherEditorMod.Source.Features.Hair;
using Monocle;
using TowerFall;

namespace ArcherEditorMod.Editor;

/// <summary>
/// Validates a registered archer as the game sees it (ArcherData + the sprite data / atlases FortRise merged),
/// reusing the ValidatorMessage types of the original xml validator. That one expects the old standalone
/// archer xml with a private atlas per archer; with FortRise 5 owning the archer definition, the
/// live objects are the source of truth.
/// </summary>
public static class ArcherRuntimeValidator
{
    private static readonly string[] RequiredBodyAnimations =
        { "stand", "run", "jump", "fall", "duck", "dodge", "ledge" };

    private static readonly string[] RequiredHeadAnimations = { "idle", "duck", "lookUp", "lookDown", "lookBack" };

    public static List<ValidatorMessage> Validate(ArcherData data)
    {
        var results = new List<ValidatorMessage>();
        void Error(string message) => results.Add(new ValidatorMessage(message));
        void Warn(string message) => results.Add(new ValidatorMessage(message) { type = ValidatorMessageType.WARN });

        if (string.IsNullOrWhiteSpace(data.Name0)) Error("Name0 is empty.");
        if (string.IsNullOrWhiteSpace(data.Name1)) Warn("Name1 is empty.");

        // ---- sprites ----
        var bodyId = data.Sprites.Body;
        var sprites = new (string Label, string Id, bool Required)[]
        {
            ("Body", bodyId, true),
            ("HeadNormal", data.Sprites.HeadNormal, true),
            ("HeadNoHat", data.Sprites.HeadNoHat, true),
            ("HeadCrown", data.Sprites.HeadCrown, true),
            ("Bow", data.Sprites.Bow, true),
            ("HeadBack", data.Sprites.HeadBack, false),
        };

        foreach (var (label, id, required) in sprites)
        {
            if (string.IsNullOrEmpty(id))
            {
                if (required) Error($"Sprites/{label} is not set.");
                continue;
            }

            if (!TFGame.SpriteData.Contains(id))
            {
                Error($"Sprites/{label}: '{id}' is not in the sprite data.");
                continue;
            }

            if (label.StartsWith("Head") && label != "HeadBack")
                CheckAnimations(id, label, RequiredHeadAnimations, results);
        }

        if (!string.IsNullOrEmpty(bodyId) && TFGame.SpriteData.Contains(bodyId))
            CheckBody(bodyId, results);

        // ---- corpse ----
        if (string.IsNullOrEmpty(data.Corpse))
        {
            Error("Corpse is not set.");
        }
        else if (!TFGame.CorpseSpriteData.Contains(data.Corpse))
        {
            Error($"Corpse '{data.Corpse}' is not in the corpse sprite data.");
        }
        else
        {
            var corpse = TFGame.CorpseSpriteData.GetSpriteString(data.Corpse);
            foreach (var animation in new[] { "ground", "fall", "pinned" })
            {
                if (!corpse.ContainsAnimation(animation))
                    Warn($"Corpse '{data.Corpse}' has no '{animation}' animation.");
            }
        }

        // ---- textures ----
        CheckTexture("Aimer", data.Aimer, true, results);
        CheckTexture("Portraits/NotJoined", data.Portraits.NotJoined, true, results);
        CheckTexture("Portraits/Joined", data.Portraits.Joined, true, results);
        CheckTexture("Portraits/Win", data.Portraits.Win, true, results);
        CheckTexture("Portraits/Lose", data.Portraits.Lose, true, results);
        CheckTexture("Hat/Normal", data.Hat.Normal, false, results);
        CheckTexture("Statue/Image", data.Statue.Image, false, results);
        CheckTexture("Statue/Glow", data.Statue.Glow, false, results);

        // ---- hairs ----
        var hairs = HairFeature.GetHairs(data);
        if (hairs != null)
        {
            for (var i = 0; i < hairs.Count; i++)
            {
                if (!TFGame.Atlas.Contains(hairs[i].HairSprite))
                    Error($"Hair {i + 1}: HairSprite '{hairs[i].HairSprite}' is not in the atlas.");
                if (!TFGame.Atlas.Contains(hairs[i].HairEndSprite))
                    Error($"Hair {i + 1}: HairEndSprite '{hairs[i].HairEndSprite}' is not in the atlas.");
                if (hairs[i].Links < 1)
                    Error($"Hair {i + 1}: Links must be at least 1.");
            }
        }
        else if (data.Hair)
        {
            Warn("ArcherData.Hair is on but no HairInfo is configured (the game's default hair is used).");
        }

        return results;
    }

    private static void CheckAnimations(string spriteId, string label, string[] animations, List<ValidatorMessage> results)
    {
        var sprite = TFGame.SpriteData.GetSpriteString(spriteId);
        foreach (var animation in animations)
        {
            if (!sprite.ContainsAnimation(animation))
                results.Add(new ValidatorMessage($"{label} '{spriteId}' has no '{animation}' animation."));
        }
    }

    private static void CheckBody(string bodyId, List<ValidatorMessage> results)
    {
        var xml = TFGame.SpriteData.GetXML(bodyId);
        var sprite = TFGame.SpriteData.GetSpriteString(bodyId);
        CheckAnimations(bodyId, "Body", RequiredBodyAnimations, results);

        // highest frame any animation uses
        var maxFrame = 0;
        if (xml["Animations"] != null)
        {
            foreach (XmlElement anim in xml["Animations"]!.GetElementsByTagName("Anim"))
            {
                foreach (var frame in Calc.ReadCSVInt(anim.GetAttribute("frames")))
                {
                    maxFrame = System.Math.Max(maxFrame, frame);
                    if (frame >= sprite.FramesTotal)
                        results.Add(new ValidatorMessage(
                            $"Body '{bodyId}': animation '{anim.GetAttribute("id")}' uses frame {frame} but the sheet only has {sprite.FramesTotal}."));
                }
            }
        }

        var heads = xml["HeadYOrigins"];
        if (heads == null)
        {
            results.Add(new ValidatorMessage($"Body '{bodyId}' has no HeadYOrigins."));
        }
        else
        {
            var length = Calc.ReadCSVInt(heads.InnerText).Length;
            if (length <= maxFrame)
                results.Add(new ValidatorMessage(
                    $"Body '{bodyId}': HeadYOrigins has {length} entries but animations use frame {maxFrame}; the game will crash on that frame."));
        }

        foreach (var name in new[] { "HeadXOrigins", "BowXOffsets", "BowYOffsets" })
        {
            if (xml[name] == null) continue;
            var length = Calc.ReadCSVInt(xml[name]!.InnerText).Length;
            if (length <= maxFrame)
                results.Add(new ValidatorMessage($"Body '{bodyId}': {name} has {length} entries, fewer than the frames in use ({maxFrame + 1}).")
                    { type = ValidatorMessageType.WARN });
        }
    }

    private static void CheckTexture(string label, Subtexture texture, bool required, List<ValidatorMessage> results)
    {
        if (texture == null)
        {
            if (required) results.Add(new ValidatorMessage($"{label} is not set."));
        }
        else if (!texture.Loaded)
        {
            results.Add(new ValidatorMessage($"{label} texture is not loaded.") { type = ValidatorMessageType.WARN });
        }
    }

    public static string Summary(IReadOnlyCollection<ValidatorMessage> results) =>
        $"{results.Count(r => r.type == ValidatorMessageType.ERROR)} errors, {results.Count(r => r.type == ValidatorMessageType.WARN)} warnings";
}
