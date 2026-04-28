using System.Linq;
using UnityEngine;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
[RequireComponent(typeof(RectTransform))]
[RequireComponent(typeof(Image))]
[RequireComponent(typeof(UiSpriteSheetAnimator))]
public class BattleCharacterPresenter : MonoBehaviour
{
    [Header("Animation Source")]
    [SerializeField] private Texture2D spriteSheetTexture;
    [SerializeField] private Sprite[] animationFrames;
    [SerializeField] private float framesPerSecond = 10f;
    [SerializeField] private string spriteKey;
    [SerializeField] private CharacterSpriteKind spriteKind = CharacterSpriteKind.Unknown;
    [SerializeField] private BattleAnimationState initialState = BattleAnimationState.Idle;

    [Header("Display")]
    [SerializeField] private bool faceLeft;
    [SerializeField] private bool preserveAspect = true;
    [SerializeField] private Color tint = Color.white;

    [Header("References")]
    [SerializeField] private Image targetImage;
    [SerializeField] private UiSpriteSheetAnimator spriteAnimator;

    private BattleAnimationState currentState;
    private Coroutine temporaryStateRoutine;

    private void Reset()
    {
        CacheReferences();
        ApplyPresentation();
    }

    private void Awake()
    {
        CacheReferences();
        currentState = initialState;
        ApplyPresentation();
    }

    private void OnEnable()
    {
        CacheReferences();
        currentState = initialState;
#if UNITY_EDITOR
        EnsureEditorPreviewFrames();
#endif
        ApplyPresentation();
    }

    private void OnValidate()
    {
        CacheReferences();
        framesPerSecond = Mathf.Max(0.01f, framesPerSecond);
        currentState = initialState;

#if UNITY_EDITOR
        AutoPopulateFramesFromSheet();
        EnsureEditorPreviewFrames();
#endif

        ApplyPresentation();
    }

    private void CacheReferences()
    {
        if (targetImage == null)
        {
            targetImage = GetComponent<Image>();
        }

        if (spriteAnimator == null)
        {
            spriteAnimator = GetComponent<UiSpriteSheetAnimator>();
        }
    }

    private void ApplyPresentation()
    {
        if (targetImage == null || spriteAnimator == null)
        {
            return;
        }

        var rectTransform = (RectTransform)transform;
        var scale = rectTransform.localScale;
        scale.x = Mathf.Abs(scale.x) * (faceLeft ? -1f : 1f);
        scale.y = Mathf.Abs(scale.y);
        scale.z = Mathf.Abs(scale.z);
        rectTransform.localScale = scale;

        targetImage.color = tint;
        targetImage.preserveAspect = preserveAspect;
        targetImage.raycastTarget = false;

        spriteAnimator.FramesPerSecond = framesPerSecond;
        spriteAnimator.Loop = ShouldLoopState(currentState);

#if UNITY_EDITOR
        // In edit mode, avoid overwriting serialized animator frame arrays with runtime-loaded sprites.
        // This prevents the scene from "losing" sprites after domain reloads / saves.
        if (!Application.isPlaying)
        {
            var previewFrames = ResolveFramesForState(currentState);
            var previewSprite = GetFirstValidSprite(previewFrames);
            if (previewSprite != null)
            {
                targetImage.sprite = previewSprite;
                EditorUtility.SetDirty(targetImage);
            }

            return;
        }
#endif

        spriteAnimator.Frames = ResolveFramesForState(currentState);
    }

    public void SetCharacter(string newSpriteKey, BattleAnimationState state = BattleAnimationState.Idle)
    {
        spriteKey = newSpriteKey;
        PlayState(state);
    }

    public void SetCharacter(string newSpriteKey, CharacterSpriteKind kind, BattleAnimationState state = BattleAnimationState.Idle)
    {
        spriteKey = newSpriteKey;
        spriteKind = kind;
        PlayState(state);
    }

    public void PlayState(BattleAnimationState state)
    {
        CancelTemporaryState();
        currentState = state;
        ApplyPresentation();
    }

    public void PlayTemporaryState(BattleAnimationState state, float extraDuration = 0f, BattleAnimationState returnState = BattleAnimationState.Idle)
    {
        if (!Application.isPlaying)
        {
            PlayState(state);
            return;
        }

        CancelTemporaryState();

        temporaryStateRoutine = StartCoroutine(PlayTemporaryStateRoutine(state, extraDuration, returnState));
    }

    public float GetStateDuration(BattleAnimationState state, float extraDuration = 0f)
    {
        return GetAnimationDuration(state) + Mathf.Max(0f, extraDuration);
    }

    private System.Collections.IEnumerator PlayTemporaryStateRoutine(BattleAnimationState state, float extraDuration, BattleAnimationState returnState)
    {
        currentState = state;
        ApplyPresentation();

        var animationDuration = GetAnimationDuration(state);
        if (animationDuration > 0f || extraDuration > 0f)
        {
            yield return new WaitForSeconds(animationDuration + Mathf.Max(0f, extraDuration));
        }

        currentState = returnState;
        ApplyPresentation();
        temporaryStateRoutine = null;
    }

    private Sprite[] ResolveFramesForState(BattleAnimationState state)
    {
        var keyedFrames = spriteKind == CharacterSpriteKind.Unknown
            ? SpriteKeyLookup.LoadCharacterAnimation(spriteKey, state)
            : SpriteKeyLookup.LoadCharacterAnimationOrDefault(spriteKey, state, spriteKind);
        if (HasValidFrames(keyedFrames))
        {
            return keyedFrames;
        }

        return GetFallbackFrames();
    }

    private float GetAnimationDuration(BattleAnimationState state)
    {
        var frames = ResolveFramesForState(state);
        if (frames == null || frames.Length == 0)
        {
            return 0f;
        }

        return Mathf.Max(0.01f, frames.Length / Mathf.Max(0.01f, framesPerSecond));
    }

    private void CancelTemporaryState()
    {
        if (temporaryStateRoutine == null)
        {
            return;
        }

        StopCoroutine(temporaryStateRoutine);
        temporaryStateRoutine = null;
    }

    private static bool ShouldLoopState(BattleAnimationState state)
    {
        return state == BattleAnimationState.Idle;
    }

#if UNITY_EDITOR
    private void AutoPopulateFramesFromSheet()
    {
        var sheetFrames = LoadFramesFromSheet();
        if (!HasValidFrames(sheetFrames))
        {
            return;
        }

        animationFrames = sheetFrames;
        EditorUtility.SetDirty(this);
    }

    private void EnsureEditorPreviewFrames()
    {
        if (Application.isPlaying || HasValidFrames(animationFrames))
        {
            return;
        }

        var sheetFrames = LoadFramesFromSheet();
        if (!HasValidFrames(sheetFrames))
        {
            return;
        }

        animationFrames = sheetFrames;
        EditorUtility.SetDirty(this);
    }

    private Sprite[] LoadFramesFromSheet()
    {
        if (spriteSheetTexture == null)
        {
            return System.Array.Empty<Sprite>();
        }

        var assetPath = AssetDatabase.GetAssetPath(spriteSheetTexture);
        if (string.IsNullOrEmpty(assetPath))
        {
            return System.Array.Empty<Sprite>();
        }

        return AssetDatabase.LoadAllAssetsAtPath(assetPath)
            .OfType<Sprite>()
            .OrderBy(sprite => sprite.name)
            .ToArray();
    }
#endif

    private Sprite[] GetFallbackFrames()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying && !HasValidFrames(animationFrames))
        {
            EnsureEditorPreviewFrames();
        }
#endif

        return HasValidFrames(animationFrames)
            ? animationFrames.Where(sprite => sprite != null).ToArray()
            : System.Array.Empty<Sprite>();
    }

    private static bool HasValidFrames(Sprite[] frames)
    {
        return frames != null && frames.Any(sprite => sprite != null);
    }

    private static Sprite GetFirstValidSprite(Sprite[] frames)
    {
        return frames == null ? null : frames.FirstOrDefault(sprite => sprite != null);
    }
}
