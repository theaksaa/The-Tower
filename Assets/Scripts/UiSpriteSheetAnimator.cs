using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
[RequireComponent(typeof(Image))]
public class UiSpriteSheetAnimator : MonoBehaviour
{
    [SerializeField] private Image targetImage;
    [SerializeField] private Sprite[] frames;
    [SerializeField] private float framesPerSecond = 10f;
    [SerializeField] private bool loop = true;

    private int currentFrameIndex;
    private float elapsedTime;

    public Sprite[] Frames
    {
        get => frames;
        set
        {
            frames = value;
            Restart();
        }
    }

    public float FramesPerSecond
    {
        get => framesPerSecond;
        set => framesPerSecond = Mathf.Max(0.01f, value);
    }

    public bool Loop
    {
        get => loop;
        set => loop = value;
    }

    private void Reset()
    {
        targetImage = GetComponent<Image>();
    }

    private void Awake()
    {
        EnsureReferences();
    }

    private void OnEnable()
    {
        EnsureReferences();
        ApplyCurrentFrame();
    }

    private void OnValidate()
    {
        EnsureReferences();
        framesPerSecond = Mathf.Max(0.01f, framesPerSecond);
        ApplyCurrentFrame();
    }

    private void Update()
    {
        if (!Application.isPlaying || frames == null || frames.Length <= 1)
        {
            return;
        }

        elapsedTime += Time.unscaledDeltaTime;
        var frameDuration = 1f / framesPerSecond;

        while (elapsedTime >= frameDuration)
        {
            elapsedTime -= frameDuration;
            if (loop)
            {
                currentFrameIndex = (currentFrameIndex + 1) % frames.Length;
            }
            else if (currentFrameIndex < frames.Length - 1)
            {
                currentFrameIndex++;
            }

            ApplyCurrentFrame();

            if (!loop && currentFrameIndex >= frames.Length - 1)
            {
                elapsedTime = 0f;
                break;
            }
        }
    }

    public void Restart()
    {
        currentFrameIndex = 0;
        elapsedTime = 0f;
        ApplyCurrentFrame();
    }

    private void EnsureReferences()
    {
        if (targetImage == null)
        {
            targetImage = GetComponent<Image>();
        }
    }

    private void ApplyCurrentFrame()
    {
        if (targetImage == null || frames == null || frames.Length == 0)
        {
            return;
        }

        currentFrameIndex = Mathf.Clamp(currentFrameIndex, 0, frames.Length - 1);
        targetImage.sprite = frames[currentFrameIndex];
    }
}
