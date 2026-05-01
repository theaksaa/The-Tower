using UnityEngine;
using UnityEngine.EventSystems;

public interface IItemLoadoutController
{
    bool TryApplyItemDrop(string itemId, bool sourceWasEquipped, int sourceIndex, bool targetIsEquipped, int targetIndex);
    void QueueItemDropRefresh();
    void NotifyItemDragStateChanged(bool isDragging);
}

public class ItemDropZone : MonoBehaviour, IDropHandler
{
    private IItemLoadoutController controller;

    public bool TargetIsEquipped { get; private set; }
    public int TargetIndex { get; private set; }

    public void Initialize(IItemLoadoutController owner, bool targetIsEquipped, int targetIndex)
    {
        controller = owner;
        TargetIsEquipped = targetIsEquipped;
        TargetIndex = targetIndex;
    }

    public void OnDrop(PointerEventData eventData)
    {
        var dragItem = eventData.pointerDrag != null
            ? eventData.pointerDrag.GetComponent<ItemSlotDragItem>()
            : null;

        if (dragItem == null || controller == null)
        {
            return;
        }

        if (controller.TryApplyItemDrop(
                dragItem.ItemId,
                dragItem.SourceWasEquipped,
                dragItem.SourceIndex,
                TargetIsEquipped,
                TargetIndex))
        {
            dragItem.MarkDropHandled();
            controller.QueueItemDropRefresh();
        }
    }
}
