using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public sealed class UICursorController : MonoBehaviour
{
    private const string PointerCursorResourcePath = "Cursors/_Cursors_Pointer";
    private const string PressedCursorResourcePath = "Cursors/_Cursors_PointerClicked";
    private const string ClickSfxResourcePath = "Sounds/SFX/UI/button_click";

    private static UICursorController instance;

    private readonly List<RaycastResult> raycastResults = new();
    private Texture2D pointerCursor;
    private Texture2D pressedCursor;
    private CursorVisualState currentState = CursorVisualState.Normal;
    private bool wasLeftMousePressed;

    private enum CursorVisualState
    {
        Normal,
        Pointer,
        Pressed
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        if (instance != null)
        {
            return;
        }

        var root = new GameObject(nameof(UICursorController));
        DontDestroyOnLoad(root);
        instance = root.AddComponent<UICursorController>();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        pointerCursor = Resources.Load<Texture2D>(PointerCursorResourcePath);
        pressedCursor = Resources.Load<Texture2D>(PressedCursorResourcePath);
        ApplyCursorState(CursorVisualState.Normal);
    }

    private void Update()
    {
        var isPointerOverClickable = TryGetMousePosition(out var mousePosition) && IsPointerOverClickableObject(mousePosition);
        var isLeftMousePressed = Mouse.current != null && Mouse.current.leftButton.isPressed;

        if (isPointerOverClickable && isLeftMousePressed && !wasLeftMousePressed)
        {
            AudioManager.PlaySfx(ClickSfxResourcePath);
        }

        wasLeftMousePressed = isLeftMousePressed;

        var desiredState = ResolveCursorState();
        if (desiredState != currentState)
        {
            ApplyCursorState(desiredState);
        }
    }

    private void OnDisable()
    {
        if (instance == this)
        {
            ApplyCursorState(CursorVisualState.Normal);
        }

        wasLeftMousePressed = false;
    }

    private CursorVisualState ResolveCursorState()
    {
        if (!TryGetMousePosition(out var mousePosition) || !IsPointerOverClickableObject(mousePosition))
        {
            return CursorVisualState.Normal;
        }

        return Mouse.current.leftButton.isPressed
            ? CursorVisualState.Pressed
            : CursorVisualState.Pointer;
    }

    private bool TryGetMousePosition(out Vector2 mousePosition)
    {
        var mouse = Mouse.current;
        if (mouse == null)
        {
            mousePosition = default;
            return false;
        }

        mousePosition = mouse.position.ReadValue();
        return true;
    }

    private bool IsPointerOverClickableObject(Vector2 mousePosition)
    {
        var eventSystem = EventSystem.current;
        if (eventSystem == null)
        {
            return false;
        }

        var pointerData = new PointerEventData(eventSystem)
        {
            position = mousePosition
        };

        raycastResults.Clear();
        eventSystem.RaycastAll(pointerData, raycastResults);

        for (var index = 0; index < raycastResults.Count; index++)
        {
            var target = raycastResults[index].gameObject;
            if (target == null)
            {
                continue;
            }

            var selectable = target.GetComponentInParent<Selectable>();
            if (selectable != null && selectable.IsInteractable())
            {
                return true;
            }

            if (ExecuteEvents.GetEventHandler<IPointerClickHandler>(target) != null)
            {
                return true;
            }

            if (ExecuteEvents.GetEventHandler<ISubmitHandler>(target) != null)
            {
                return true;
            }

            if (ExecuteEvents.GetEventHandler<IBeginDragHandler>(target) != null)
            {
                return true;
            }

            if (ExecuteEvents.GetEventHandler<IDragHandler>(target) != null)
            {
                return true;
            }

            if (ExecuteEvents.GetEventHandler<IEndDragHandler>(target) != null)
            {
                return true;
            }

            if (ExecuteEvents.GetEventHandler<IDropHandler>(target) != null)
            {
                return true;
            }
        }

        return false;
    }

    private void ApplyCursorState(CursorVisualState state)
    {
        currentState = state;

        switch (state)
        {
            case CursorVisualState.Pointer:
                Cursor.SetCursor(pointerCursor, Vector2.zero, CursorMode.Auto);
                break;
            case CursorVisualState.Pressed:
                Cursor.SetCursor(pressedCursor != null ? pressedCursor : pointerCursor, Vector2.zero, CursorMode.Auto);
                break;
            default:
                Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
                break;
        }
    }
}
