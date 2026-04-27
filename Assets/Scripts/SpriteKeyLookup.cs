using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum BattleAnimationState
{
    Idle,
    Attack,
    Hurt,
    Defend,
    Death
}

public static class SpriteKeyLookup
{
    private const string CharacterResourceRoot = "Sprites/Characters";
    private const string MoveResourceRoot = "Sprites/Moves";

    private static readonly Dictionary<string, Sprite[]> CharacterAnimationCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, Sprite> MoveSpriteCache = new(StringComparer.OrdinalIgnoreCase);

    public static Sprite[] LoadCharacterAnimation(string spriteKey, BattleAnimationState state)
    {
        if (string.IsNullOrWhiteSpace(spriteKey))
        {
            return Array.Empty<Sprite>();
        }

        var cacheKey = $"{spriteKey}:{state}";
        if (CharacterAnimationCache.TryGetValue(cacheKey, out var cachedFrames))
        {
            return cachedFrames;
        }

        var stateName = state.ToString();
        var directPath = $"{CharacterResourceRoot}/{spriteKey}_{stateName}";
        var frames = LoadSpritesAtPath(directPath);

        if (frames.Length == 0 && state != BattleAnimationState.Idle)
        {
            frames = LoadSpritesAtPath($"{CharacterResourceRoot}/{spriteKey}_Idle");
        }

        CharacterAnimationCache[cacheKey] = frames;
        return frames;
    }

    public static Sprite LoadMoveSprite(string spriteKey)
    {
        if (string.IsNullOrWhiteSpace(spriteKey))
        {
            return null;
        }

        if (MoveSpriteCache.TryGetValue(spriteKey, out var cachedSprite))
        {
            return cachedSprite;
        }

        var sprite = LoadSingleSprite($"{MoveResourceRoot}/{spriteKey}")
            ?? LoadSingleSprite($"{CharacterResourceRoot}/{spriteKey}_Idle");

        MoveSpriteCache[spriteKey] = sprite;
        return sprite;
    }

    private static Sprite[] LoadSpritesAtPath(string resourcePath)
    {
        var sprites = Resources.LoadAll<Sprite>(resourcePath)
            .OrderBy(sprite => sprite.name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (sprites.Length > 0)
        {
            return sprites;
        }

        var singleSprite = Resources.Load<Sprite>(resourcePath);
        return singleSprite != null ? new[] { singleSprite } : Array.Empty<Sprite>();
    }

    private static Sprite LoadSingleSprite(string resourcePath)
    {
        var sprites = LoadSpritesAtPath(resourcePath);
        return sprites.FirstOrDefault();
    }
}
