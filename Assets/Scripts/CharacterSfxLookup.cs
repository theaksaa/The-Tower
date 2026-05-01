using System;
using System.Collections.Generic;
using UnityEngine;

public enum CharacterSfxType
{
    Revive
}

public static class CharacterSfxLookup
{
    private const string CharacterSfxRoot = "Sounds/SFX/Characters";
    private const string DefaultSpriteKey = "default";

    private static readonly Dictionary<string, AudioClip> ClipCache = new(StringComparer.OrdinalIgnoreCase);

    public static AudioClip LoadCharacterSfxOrDefault(string spriteKey, CharacterSfxType sfxType)
    {
        var normalizedSpriteKey = string.IsNullOrWhiteSpace(spriteKey) ? DefaultSpriteKey : spriteKey.Trim();
        var resourceName = GetResourceName(sfxType);
        var cacheKey = $"{normalizedSpriteKey}:{resourceName}";

        if (ClipCache.TryGetValue(cacheKey, out var cachedClip))
        {
            return cachedClip;
        }

        var clip = LoadClip($"{CharacterSfxRoot}/{normalizedSpriteKey}/{resourceName}");
        if (clip == null && !string.Equals(normalizedSpriteKey, DefaultSpriteKey, StringComparison.OrdinalIgnoreCase))
        {
            clip = LoadClip($"{CharacterSfxRoot}/{DefaultSpriteKey}/{resourceName}");
        }

        ClipCache[cacheKey] = clip;
        return clip;
    }

    private static string GetResourceName(CharacterSfxType sfxType)
    {
        return sfxType switch
        {
            CharacterSfxType.Revive => "revive",
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
