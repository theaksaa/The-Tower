using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasGroup))]
public class ItemSlotDragItem : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private static readonly Color EmptyItemSlotTint = new(1f, 1f, 1f, 0.28f);

    private IItemLoadoutController controller;
    private RectTransform dragRoot;
    private RectTransform rectTransform;
    private CanvasGroup canvasGroup;
    private Canvas canvas;
    private Image sourceBackground;
    private Image sourceIcon;
    private Sprite originalBackgroundSprite;
    private Sprite originalIconSprite;
    private Color originalBackgroundColor;
    private Color originalIconColor;
    private bool originalBackgroundPreserveAspect;
    private bool originalIconPreserveAspect;
    private Sprite emptyBackgroundSprite;
    private RectTransform dragProxyRectTransform;
    private bool dropHandled;

    public string ItemId { get; private set; }
    public int SourceIndex { get; private set; }
    public bool SourceWasEquipped { get; private set; }

    public void Initialize(
        IItemLoadoutController owner,
        string itemId,
        int sourceIndex,
        bool sourceWasEquipped,
        RectTransform root,
        Canvas parentCanvas,
        Sprite emptySlotBackgroundSprite = null,
        bool destroyOnDrop = true)
    {
        controller = owner;
        ItemId = itemId;
        SourceIndex = sourceIndex;
        SourceWasEquipped = sourceWasEquipped;
        dragRoot = root;
        canvas = parentCanvas;
        emptyBackgroundSprite = emptySlotBackgroundSprite;
        rectTransform = GetComponent<RectTransform>();
        canvasGroup = GetComponent<CanvasGroup>();
        sourceBackground = rectTransform.Find("Item Background")?.GetComponent<Image>()
            ?? rectTransform.Find("Move Background")?.GetComponent<Image>();
        sourceIcon = rectTransform.Find("Item Icon")?.GetComponent<Image>()
            ?? rectTransform.Find("Move Icon")?.GetComponent<Image>();
    }

    public void MarkDropHandled()
    {
        dropHandled = true;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (controller == null || dragRoot == null)
        {
            return;
        }

        dropHandled = false;
        controller.NotifyItemDragStateChanged(true);
        CreateDragProxy();
        ApplyEmptySlotPreview();
        canvasGroup.blocksRaycasts = false;
        UpdatePosition(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        UpdatePosition(eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        DestroyDragProxy();

        if (canvasGroup != null)
        {
            canvasGroup.blocksRaycasts = true;
        }

        controller.NotifyItemDragStateChanged(false);

        if (dropHandled)
        {
            return;
        }

        RestoreSourceVisuals();
    }

    private void UpdatePosition(PointerEventData eventData)
    {
        if (dragProxyRectTransform == null || dragRoot == null)
        {
            return;
        }

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                dragRoot,
                eventData.position,
                canvas != null ? canvas.worldCamera : null,
                out var localPoint))
        {
            dragProxyRectTransform.localPosition = localPoint;
        }
    }

    private void CreateDragProxy()
    {
        var proxyObject = Instantiate(gameObject, dragRoot, true);
        proxyObject.name = $"{gameObject.name} Drag Proxy";

        foreach (var behaviour in proxyObject.GetComponents<MonoBehaviour>())
        {
            Destroy(behaviour);
        }

        var proxyCanvasGroup = proxyObject.GetComponent<CanvasGroup>();
        if (proxyCanvasGroup == null)
        {
            proxyCanvasGroup = proxyObject.AddComponent<CanvasGroup>();
        }

        proxyCanvasGroup.blocksRaycasts = false;
        proxyCanvasGroup.alpha = 0.92f;

        dragProxyRectTransform = proxyObject.GetComponent<RectTransform>();
        dragProxyRectTransform.SetAsLastSibling();
        dragProxyRectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        dragProxyRectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        dragProxyRectTransform.pivot = new Vector2(0.5f, 0.5f);
    }

    private void DestroyDragProxy()
    {
        if (dragProxyRectTransform == null)
        {
            return;
        }

        Destroy(dragProxyRectTransform.gameObject);
        dragProxyRectTransform = null;
    }

    private void ApplyEmptySlotPreview()
    {
        if (sourceBackground != null)
        {
            originalBackgroundSprite = sourceBackground.sprite;
            originalBackgroundColor = sourceBackground.color;
            originalBackgroundPreserveAspect = sourceBackground.preserveAspect;
            if (emptyBackgroundSprite != null)
            {
                sourceBackground.sprite = emptyBackgroundSprite;
            }

            sourceBackground.color = EmptyItemSlotTint;
            sourceBackground.preserveAspect = true;
        }

        if (sourceIcon != null)
        {
            originalIconSprite = sourceIcon.sprite;
            originalIconColor = sourceIcon.color;
            originalIconPreserveAspect = sourceIcon.preserveAspect;
            sourceIcon.color = new Color(1f, 1f, 1f, 0f);
        }
    }

    private void RestoreSourceVisuals()
    {
        if (sourceBackground != null)
        {
            sourceBackground.sprite = originalBackgroundSprite;
            sourceBackground.color = originalBackgroundColor;
            sourceBackground.preserveAspect = originalBackgroundPreserveAspect;
        }

        if (sourceIcon != null)
        {
            sourceIcon.sprite = originalIconSprite;
            sourceIcon.color = originalIconColor;
            sourceIcon.preserveAspect = originalIconPreserveAspect;
        }
    }
}
