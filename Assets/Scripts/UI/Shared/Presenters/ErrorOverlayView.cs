using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class ErrorOverlayView : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private string fallbackMessage = "Something went wrong.";

    private RectTransform rectTransform;
    private Image clickCatcher;

    private void Awake()
    {
        Initialize();
    }

    private void OnEnable()
    {
        Initialize();
        transform.SetAsLastSibling();
    }

    public void Show(string message)
    {
        Initialize();

        if (messageText != null)
        {
            messageText.text = string.IsNullOrWhiteSpace(message)
                ? fallbackMessage
                : message.Trim();
        }

        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
        }

        transform.SetAsLastSibling();
    }

    public void Hide()
    {
        if (gameObject.activeSelf)
        {
            gameObject.SetActive(false);
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData == null || eventData.button != PointerEventData.InputButton.Left)
        {
            return;
        }

        ErrorOverlayService.HideAll();
    }

    private void Initialize()
    {
        rectTransform ??= transform as RectTransform;
        messageText ??= transform.Find("Text")?.GetComponent<TMP_Text>();
        messageText ??= GetComponentInChildren<TMP_Text>(true);

        StretchToParent();
        EnsureClickCatcher();
        DisableChildRaycasts();
    }

    private void StretchToParent()
    {
        if (rectTransform == null || rectTransform.parent is not RectTransform)
        {
            return;
        }

        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.anchoredPosition = Vector2.zero;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
    }

    private void EnsureClickCatcher()
    {
        clickCatcher = GetComponent<Image>();
        if (clickCatcher == null)
        {
            clickCatcher = gameObject.AddComponent<Image>();
        }

        clickCatcher.color = new Color(0f, 0f, 0f, 0.001f);
        clickCatcher.raycastTarget = true;
    }

    private void DisableChildRaycasts()
    {
        var graphics = GetComponentsInChildren<Graphic>(true);
        for (var index = 0; index < graphics.Length; index++)
        {
            var graphic = graphics[index];
            if (graphic == null || graphic == clickCatcher)
            {
                continue;
            }

            graphic.raycastTarget = false;
        }
    }
}
