using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasGroup))]
public class MoveSlotDragItem : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private MapRunOverviewController controller;
    private RectTransform dragRoot;
    private RectTransform rectTransform;
    private CanvasGroup canvasGroup;
    private Canvas canvas;
    private LayoutElement layoutElement;
    private Transform originalParent;
    private int originalSiblingIndex;
    private Vector2 originalAnchorMin;
    private Vector2 originalAnchorMax;
    private Vector2 originalPivot;
    private Vector2 originalOffsetMin;
    private Vector2 originalOffsetMax;
    private Vector2 originalSizeDelta;
    private bool dropHandled;

    public string MoveId { get; private set; }
    public int SourceEquippedIndex { get; private set; }
    public bool SourceWasEquipped { get; private set; }

    public void Initialize(
        MapRunOverviewController owner,
        string moveId,
        int sourceEquippedIndex,
        bool sourceWasEquipped,
        RectTransform root,
        Canvas parentCanvas)
    {
        controller = owner;
        MoveId = moveId;
        SourceEquippedIndex = sourceEquippedIndex;
        SourceWasEquipped = sourceWasEquipped;
        dragRoot = root;
        canvas = parentCanvas;
        rectTransform = GetComponent<RectTransform>();
        canvasGroup = GetComponent<CanvasGroup>();
        layoutElement = GetComponent<LayoutElement>();
    }

    public void MarkDropHandled()
    {
        dropHandled = true;
        if (canvasGroup != null)
        {
            canvasGroup.blocksRaycasts = false;
            canvasGroup.alpha = 0f;
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (controller == null || dragRoot == null)
        {
            return;
        }

        dropHandled = false;
        controller.NotifyDragStateChanged(true);
        originalParent = rectTransform.parent;
        originalSiblingIndex = rectTransform.GetSiblingIndex();
        originalAnchorMin = rectTransform.anchorMin;
        originalAnchorMax = rectTransform.anchorMax;
        originalPivot = rectTransform.pivot;
        originalOffsetMin = rectTransform.offsetMin;
        originalOffsetMax = rectTransform.offsetMax;
        originalSizeDelta = rectTransform.sizeDelta;

        if (layoutElement != null)
        {
            layoutElement.ignoreLayout = true;
        }

        var worldCorners = new Vector3[4];
        rectTransform.GetWorldCorners(worldCorners);
        var width = worldCorners[3].x - worldCorners[0].x;
        var height = worldCorners[1].y - worldCorners[0].y;

        rectTransform.SetParent(dragRoot, true);
        rectTransform.SetAsLastSibling();
        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.sizeDelta = new Vector2(width, height);
        canvasGroup.blocksRaycasts = false;
        canvasGroup.alpha = 0.92f;
        UpdatePosition(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        UpdatePosition(eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (canvasGroup != null)
        {
            canvasGroup.blocksRaycasts = true;
            canvasGroup.alpha = 1f;
        }

        controller.NotifyDragStateChanged(false);

        if (dropHandled)
        {
            if (layoutElement != null)
            {
                layoutElement.ignoreLayout = false;
            }

            if (gameObject != null)
            {
                Destroy(gameObject);
            }

            return;
        }

        if (rectTransform == null || originalParent == null)
        {
            return;
        }

        rectTransform.SetParent(originalParent, false);
        rectTransform.SetSiblingIndex(originalSiblingIndex);
        rectTransform.anchorMin = originalAnchorMin;
        rectTransform.anchorMax = originalAnchorMax;
        rectTransform.pivot = originalPivot;
        rectTransform.offsetMin = originalOffsetMin;
        rectTransform.offsetMax = originalOffsetMax;
        rectTransform.sizeDelta = originalSizeDelta;

        if (layoutElement != null)
        {
            layoutElement.ignoreLayout = false;
        }
    }

    private void UpdatePosition(PointerEventData eventData)
    {
        if (rectTransform == null || dragRoot == null)
        {
            return;
        }

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                dragRoot,
                eventData.position,
                canvas != null ? canvas.worldCamera : null,
                out var localPoint))
        {
            rectTransform.localPosition = localPoint;
        }
    }
}
