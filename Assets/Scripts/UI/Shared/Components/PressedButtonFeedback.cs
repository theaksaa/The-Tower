using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class PressedButtonFeedback : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    private Button button;
    private Image backgroundImage;
    private RectTransform labelRect;
    private TMP_Text labelText;
    private readonly List<Graphic> childGraphics = new();
    private readonly List<Color> childGraphicColors = new();
    private Sprite normalSprite;
    private Sprite pressedSprite;
    private Color normalColor;
    private Color disabledColor;
    private Color labelNormalColor;
    private Color hoverColor;
    private Color pressedColor;
    private Vector2 labelBasePosition;
    private Vector2 pressedLabelOffset;
    private bool isHovered;
    private bool isPressed;
    private bool wasInteractable;
    private bool applyDisabledTint;
    private bool tintLabelWhenDisabled;
    private bool tintChildGraphicsWhenDisabled;
    private bool swapPressedSprite;

    public void Initialize(
        Button targetButton,
        Image targetImage,
        RectTransform targetLabelRect,
        Sprite targetPressedSprite,
        Color targetHoverColor,
        Color targetPressedColor,
        Vector2 targetPressedLabelOffset,
        bool shouldApplyDisabledTint = false,
        bool shouldTintLabelWhenDisabled = false,
        bool shouldTintChildGraphicsWhenDisabled = false,
        bool shouldSwapPressedSprite = true)
    {
        button = targetButton;
        backgroundImage = targetImage;
        labelRect = targetLabelRect;
        labelText = targetLabelRect != null ? targetLabelRect.GetComponent<TMP_Text>() : null;
        pressedSprite = targetPressedSprite;
        hoverColor = targetHoverColor;
        pressedColor = targetPressedColor;
        pressedLabelOffset = targetPressedLabelOffset;
        applyDisabledTint = shouldApplyDisabledTint;
        tintLabelWhenDisabled = shouldTintLabelWhenDisabled;
        tintChildGraphicsWhenDisabled = shouldTintChildGraphicsWhenDisabled;
        swapPressedSprite = shouldSwapPressedSprite;
        disabledColor = targetButton != null
            ? targetButton.colors.disabledColor
            : new Color(0.78431374f, 0.78431374f, 0.78431374f, 0.5019608f);

        if (backgroundImage != null)
        {
            normalSprite = backgroundImage.sprite;
            normalColor = backgroundImage.color;
        }

        if (labelRect != null)
        {
            labelBasePosition = labelRect.anchoredPosition;
        }

        if (labelText != null)
        {
            labelNormalColor = labelText.color;
        }

        childGraphics.Clear();
        childGraphicColors.Clear();
        if (button != null)
        {
            var graphics = button.GetComponentsInChildren<Graphic>(true);
            for (var index = 0; index < graphics.Length; index++)
            {
                var graphic = graphics[index];
                if (graphic == null || graphic == backgroundImage || graphic == labelText)
                {
                    continue;
                }

                childGraphics.Add(graphic);
                childGraphicColors.Add(graphic.color);
            }
        }

        wasInteractable = button == null || button.IsInteractable();
        ApplyVisualState();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        isHovered = true;
        ApplyVisualState();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isHovered = false;
        isPressed = false;
        ApplyVisualState();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (button == null || !button.IsInteractable())
        {
            return;
        }

        isPressed = true;
        ApplyVisualState();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        isPressed = false;
        ApplyVisualState();
    }

    private void OnDisable()
    {
        isHovered = false;
        isPressed = false;
        ApplyVisualState();
    }

    private void LateUpdate()
    {
        if (button == null)
        {
            return;
        }

        var isInteractable = button.IsInteractable();
        if (isInteractable == wasInteractable)
        {
            return;
        }

        wasInteractable = isInteractable;
        if (!isInteractable)
        {
            isHovered = false;
            isPressed = false;
        }

        ApplyVisualState();
    }

    private void ApplyVisualState()
    {
        var isInteractable = button == null || button.IsInteractable();

        if (backgroundImage != null)
        {
            backgroundImage.sprite = isInteractable && isPressed && swapPressedSprite && pressedSprite != null
                ? pressedSprite
                : normalSprite;
            backgroundImage.color = !isInteractable && applyDisabledTint
                ? disabledColor
                : isPressed
                    ? pressedColor
                    : isHovered ? hoverColor : normalColor;
        }

        if (labelRect != null)
        {
            labelRect.anchoredPosition = labelBasePosition + (isInteractable && isPressed ? pressedLabelOffset : Vector2.zero);
        }

        if (labelText != null)
        {
            labelText.color = !isInteractable && tintLabelWhenDisabled ? disabledColor : labelNormalColor;
        }

        for (var index = 0; index < childGraphics.Count; index++)
        {
            if (childGraphics[index] != null)
            {
                childGraphics[index].color = !isInteractable && tintChildGraphicsWhenDisabled
                    ? disabledColor
                    : childGraphicColors[index];
            }
        }
    }
}
