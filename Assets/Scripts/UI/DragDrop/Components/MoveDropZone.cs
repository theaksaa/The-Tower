using UnityEngine;
using UnityEngine.EventSystems;

public interface IMoveLoadoutController
{
    bool TryApplyMoveDrop(string moveId, bool sourceWasEquipped, int sourceEquippedIndex, int targetEquippedIndex);
    void QueueDropRefresh();
    void NotifyDragStateChanged(bool isDragging);
}

public class MoveDropZone : MonoBehaviour, IDropHandler
{
    private IMoveLoadoutController controller;

    public int EquippedIndex { get; private set; }

    public void Initialize(IMoveLoadoutController owner, int equippedIndex)
    {
        controller = owner;
        EquippedIndex = equippedIndex;
    }

    public void OnDrop(PointerEventData eventData)
    {
        var dragItem = eventData.pointerDrag != null
            ? eventData.pointerDrag.GetComponent<MoveSlotDragItem>()
            : null;

        if (dragItem == null || controller == null)
        {
            return;
        }

        if (controller.TryApplyMoveDrop(
                dragItem.MoveId,
                dragItem.SourceWasEquipped,
                dragItem.SourceEquippedIndex,
                EquippedIndex))
        {
            dragItem.MarkDropHandled();
            controller.QueueDropRefresh();
        }
    }
}
