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

    [Header("Display")]
    [SerializeField] private bool faceLeft;
    [SerializeField] private bool preserveAspect = true;
    [SerializeField] private Color tint = Color.white;

    [Header("References")]
    [SerializeField] private Image targetImage;
    [SerializeField] private UiSpriteSheetAnimator spriteAnimator;

    private void Reset()
    {
        CacheReferences();
        ApplyPresentation();
    }

    private void Awake()
    {
        CacheReferences();
        ApplyPresentation();
    }

    private void OnEnable()
    {
        CacheReferences();
        ApplyPresentation();
    }

    private void OnValidate()
    {
        CacheReferences();
        framesPerSecond = Mathf.Max(0.01f, framesPerSecond);

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
        spriteAnimator.Frames = animationFrames;
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
