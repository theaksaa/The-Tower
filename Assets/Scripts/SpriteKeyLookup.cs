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

public enum CharacterSpriteKind
{
    Unknown,
    Hero,
    Monster
}

public static class SpriteKeyLookup
{
    private const string CharacterResourceRoot = "Sprites/Characters";
    private const string MoveResourceRoot = "Sprites/Moves";
    private const string DefaultSpriteKey = "default";
    private const string DefaultHeroResourceRoot = CharacterResourceRoot + "/" + DefaultSpriteKey + "/hero";
    private const string DefaultMonsterResourceRoot = CharacterResourceRoot + "/" + DefaultSpriteKey + "/monster";

    private static readonly Dictionary<string, Sprite[]> CharacterAnimationCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, Sprite> MoveSpriteCache = new(StringComparer.OrdinalIgnoreCase);

    public static Sprite[] LoadCharacterAnimation(string spriteKey, BattleAnimationState state)
    {
        return LoadCharacterAnimationInternal(spriteKey, state, CharacterSpriteKind.Unknown, fallbackToDefault: false);
    }

    public static Sprite[] LoadCharacterAnimationOrDefault(string spriteKey, BattleAnimationState state, CharacterSpriteKind kind)
    {
        return LoadCharacterAnimationInternal(spriteKey, state, kind, fallbackToDefault: true);
    }

    public static bool HasCharacterAnimation(string spriteKey, BattleAnimationState state = BattleAnimationState.Idle)
    {
        return LoadCharacterAnimation(spriteKey, state).Length > 0;
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
            // Some characters have a single portrait sprite at <key>.png
            ?? LoadSingleSprite($"{CharacterResourceRoot}/{spriteKey}/{spriteKey}")
            // Fallback to their idle sheet if available.
            ?? LoadSingleSprite(BuildCharacterResourcePath(spriteKey, "Idle"));

        MoveSpriteCache[spriteKey] = sprite;
        return sprite;
    }

    private static string GetCharacterAnimationName(BattleAnimationState state)
    {
        // User requirement: Use Attack01 for all attacks.
        if (state == BattleAnimationState.Attack)
        {
            return "Attack01";
        }

        return state.ToString();
    }

    private static string BuildCharacterResourcePath(string spriteKey, string animName)
    {
        // Resource paths never include file extensions.
        return $"{CharacterResourceRoot}/{spriteKey}/{spriteKey}-{animName}";
    }

    private static Sprite[] LoadCharacterAnimationInternal(string spriteKey, BattleAnimationState state, CharacterSpriteKind kind, bool fallbackToDefault)
    {
        var normalizedKey = string.IsNullOrWhiteSpace(spriteKey) ? null : spriteKey.Trim();
        var cacheKey = $"{normalizedKey ?? "<null>"}:{kind}:{state}:{(fallbackToDefault ? 1 : 0)}";
        if (CharacterAnimationCache.TryGetValue(cacheKey, out var cachedFrames))
        {
            return cachedFrames;
        }

        Sprite[] frames;
        if (string.IsNullOrWhiteSpace(normalizedKey))
        {
            frames = fallbackToDefault && kind != CharacterSpriteKind.Unknown
                ? LoadDefaultCharacterAnimation(kind, state)
                : Array.Empty<Sprite>();
        }
        else
        {
            frames = LoadCharacterAnimationWithoutDefault(normalizedKey, state);
            if (frames.Length == 0 && fallbackToDefault && kind != CharacterSpriteKind.Unknown)
            {
                frames = LoadDefaultCharacterAnimation(kind, state);
            }
        }

        CharacterAnimationCache[cacheKey] = frames;
        return frames;
    }

    private static Sprite[] LoadCharacterAnimationWithoutDefault(string spriteKey, BattleAnimationState state)
    {
        // New structure:
        // Assets/Resources/Sprites/Characters/<spriteKey>/<spriteKey>-Idle.png
        // Assets/Resources/Sprites/Characters/<spriteKey>/<spriteKey>-Attack01.png
        // etc.
        var animName = GetCharacterAnimationName(state);
        var frames = LoadSpritesAtPath(BuildCharacterResourcePath(spriteKey, animName));

        // Special-case: Defend is often named "Block" in the source art.
        if (frames.Length == 0 && state == BattleAnimationState.Defend)
        {
            frames = LoadSpritesAtPath(BuildCharacterResourcePath(spriteKey, "Block"));
        }

        if (frames.Length == 0 && state != BattleAnimationState.Idle)
        {
            frames = LoadSpritesAtPath(BuildCharacterResourcePath(spriteKey, "Idle"));
        }

        return frames;
    }

    private static Sprite[] LoadDefaultCharacterAnimation(CharacterSpriteKind kind, BattleAnimationState state)
    {
        var root = kind == CharacterSpriteKind.Hero ? DefaultHeroResourceRoot : DefaultMonsterResourceRoot;

        var animName = GetCharacterAnimationName(state);
        var frames = LoadSpritesAtPath($"{root}/{DefaultSpriteKey}-{animName}");

        if (frames.Length == 0 && state == BattleAnimationState.Defend)
        {
            frames = LoadSpritesAtPath($"{root}/{DefaultSpriteKey}-Block");
        }

        if (frames.Length == 0 && state != BattleAnimationState.Idle)
        {
            frames = LoadSpritesAtPath($"{root}/{DefaultSpriteKey}-Idle");
        }

        return frames;
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
