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
        ApplyPresentation();
    }

    private void OnValidate()
    {
        CacheReferences();
        framesPerSecond = Mathf.Max(0.01f, framesPerSecond);
        currentState = initialState;

#if UNITY_EDITOR
        AutoPopulateFramesFromSheet();
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
        spriteAnimator.Frames = ResolveFramesForState(currentState);
    }

    public void SetCharacter(string newSpriteKey, BattleAnimationState state = BattleAnimationState.Idle)
    {
        spriteKey = newSpriteKey;
        PlayState(state);
    }

    public void PlayState(BattleAnimationState state)
    {
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

        if (temporaryStateRoutine != null)
        {
            StopCoroutine(temporaryStateRoutine);
        }

        temporaryStateRoutine = StartCoroutine(PlayTemporaryStateRoutine(state, extraDuration, returnState));
    }

    private System.Collections.IEnumerator PlayTemporaryStateRoutine(BattleAnimationState state, float extraDuration, BattleAnimationState returnState)
    {
        PlayState(state);

        var animationDuration = GetAnimationDuration(state);
        if (animationDuration > 0f || extraDuration > 0f)
        {
            yield return new WaitForSeconds(animationDuration + Mathf.Max(0f, extraDuration));
        }

        PlayState(returnState);
        temporaryStateRoutine = null;
    }

    private Sprite[] ResolveFramesForState(BattleAnimationState state)
    {
        var keyedFrames = SpriteKeyLookup.LoadCharacterAnimation(spriteKey, state);
        return keyedFrames.Length > 0 ? keyedFrames : animationFrames;
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

#if UNITY_EDITOR
    private void AutoPopulateFramesFromSheet()
    {
        if (spriteSheetTexture == null)
        {
            return;
        }

        var assetPath = AssetDatabase.GetAssetPath(spriteSheetTexture);
        if (string.IsNullOrEmpty(assetPath))
        {
            return;
        }

        animationFrames = AssetDatabase.LoadAllAssetsAtPath(assetPath)
            .OfType<Sprite>()
            .OrderBy(sprite => sprite.name)
            .ToArray();
    }
#endif
}
