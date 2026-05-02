using System;
using System.Collections.Generic;
using UnityEngine;
using TheTower;

public enum MoveSfxType
{
    Attack,
    AttackBuff,
    AttackDebuff,
    DefenseBuff,
    DefenseDebuff,
    MagicBuff,
    MagicDebuff,
    Heal
}

public static class MoveSfxLookup
{
    private const string MoveSfxRoot = "Sounds/SFX/Moves";
    private const string DefaultSpriteKey = "default";

    private static readonly Dictionary<string, AudioClip> ClipCache = new(StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyList<AudioClip> LoadMoveSfx(Move move)
    {
        if (move == null)
        {
            return Array.Empty<AudioClip>();
        }

        var spriteKey = string.IsNullOrWhiteSpace(move.spriteKey) ? DefaultSpriteKey : move.spriteKey.Trim();
        var clips = new List<AudioClip>();

        var primaryEffectSfx = ResolvePrimaryEffectSfx(move.effect);
        if (primaryEffectSfx.HasValue)
        {
            AddClipIfPresent(clips, spriteKey, primaryEffectSfx.Value);
        }

        var modifierSfx = ResolveModifierSfx(move.statModifier);
        if (modifierSfx.HasValue)
        {
            AddClipIfPresent(clips, spriteKey, modifierSfx.Value);
        }

        return clips;
    }

    private static MoveSfxType? ResolvePrimaryEffectSfx(string effect)
    {
        return effect switch
        {
            "damage" => MoveSfxType.Attack,
            "damage_and_stat_modifier" => MoveSfxType.Attack,
            "drain" => MoveSfxType.Attack,
            "heal" => MoveSfxType.Heal,
            _ => null
        };
    }

    private static MoveSfxType? ResolveModifierSfx(StatModifier statModifier)
    {
        if (statModifier == null || statModifier.value == 0 || string.IsNullOrWhiteSpace(statModifier.stat))
        {
            return null;
        }

        var isBuff = statModifier.value > 0;
        return statModifier.stat.Trim().ToLowerInvariant() switch
        {
            "attack" => isBuff ? MoveSfxType.AttackBuff : MoveSfxType.AttackDebuff,
            "defense" => isBuff ? MoveSfxType.DefenseBuff : MoveSfxType.DefenseDebuff,
            "magic" => isBuff ? MoveSfxType.MagicBuff : MoveSfxType.MagicDebuff,
            _ => null
        };
    }

    private static void AddClipIfPresent(List<AudioClip> clips, string spriteKey, MoveSfxType sfxType)
    {
        var clip = LoadMoveSfxOrDefault(spriteKey, sfxType);
        if (clip != null)
        {
            clips.Add(clip);
        }
    }

    private static AudioClip LoadMoveSfxOrDefault(string spriteKey, MoveSfxType sfxType)
    {
        var resourceName = GetResourceName(sfxType);
        var cacheKey = $"{spriteKey}:{resourceName}";
        if (ClipCache.TryGetValue(cacheKey, out var cachedClip))
        {
            return cachedClip;
        }

        var clip = LoadClip($"{MoveSfxRoot}/{spriteKey}/{resourceName}");
        if (clip == null && !string.Equals(spriteKey, DefaultSpriteKey, StringComparison.OrdinalIgnoreCase))
        {
            clip = LoadClip($"{MoveSfxRoot}/{DefaultSpriteKey}/{resourceName}");
        }

        ClipCache[cacheKey] = clip;
        return clip;
    }

    private static string GetResourceName(MoveSfxType sfxType)
    {
        return sfxType switch
        {
            MoveSfxType.Attack => "attack",
            MoveSfxType.AttackBuff => "attack_buff",
            MoveSfxType.AttackDebuff => "attack_debuff",
            MoveSfxType.DefenseBuff => "defense_buff",
            MoveSfxType.DefenseDebuff => "defense_debuff",
            MoveSfxType.MagicBuff => "magic_buff",
            MoveSfxType.MagicDebuff => "magic_debuff",
            MoveSfxType.Heal => "heal",
            _ => throw new ArgumentOutOfRangeException(nameof(sfxType), sfxType, null)
        };
    }

    private static AudioClip LoadClip(string resourcePath)
    {
        return string.IsNullOrWhiteSpace(resourcePath)
            ? null
            : Resources.Load<AudioClip>(resourcePath);
    }
}
