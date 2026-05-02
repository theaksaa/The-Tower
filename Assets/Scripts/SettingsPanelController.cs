using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class SettingsPanelController : MonoBehaviour, IPointerClickHandler
{
    private const string PrefabResourcesPath = "UI/SettingsPanel";

    [SerializeField] private Slider musicSlider;
    [SerializeField] private Slider sfxSlider;
    [SerializeField] private RectTransform contentRoot;
    [SerializeField] private Button closeButton;
    [SerializeField] private bool closeOnEscape = true;

    private bool suppressSliderCallbacks;

    public bool IsOpen => gameObject.activeSelf;

    public static SettingsPanelController FindOrCreate(Transform parent)
    {
        if (parent == null)
        {
            return null;
        }

        var existingPanel = parent.GetComponentInChildren<SettingsPanelController>(true);
        if (existingPanel != null)
        {
            return existingPanel;
        }

        var prefab = Resources.Load<SettingsPanelController>(PrefabResourcesPath);
        if (prefab == null)
        {
            Debug.LogWarning($"SettingsPanelController could not load prefab at Resources path '{PrefabResourcesPath}'.");
            return null;
        }

        return Instantiate(prefab, parent, false);
    }

    private void Awake()
    {
        AutoBind();
        BindControls();
        RefreshSliders();
    }

    private void OnEnable()
    {
        RefreshSliders();
        transform.SetAsLastSibling();
    }

    private void Update()
    {
        if (!closeOnEscape || !IsOpen)
        {
            return;
        }

        var keyboard = UnityEngine.InputSystem.Keyboard.current;
        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
        {
            Close();
        }
    }

    public void Toggle()
    {
        SetVisible(!IsOpen);
    }

    public void Open()
    {
        SetVisible(true);
    }

    public void Close()
    {
        SetVisible(false);
    }

    public void SetVisible(bool isVisible)
    {
        if (gameObject.activeSelf == isVisible)
        {
            if (isVisible)
            {
                RefreshSliders();
                transform.SetAsLastSibling();
            }

            return;
        }

        if (isVisible)
        {
            RefreshSliders();
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            return;
        }

        gameObject.SetActive(false);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData == null || eventData.button != PointerEventData.InputButton.Left)
        {
            return;
        }

        var clickedObject = eventData.pointerPressRaycast.gameObject;
        if (clickedObject == null)
        {
            clickedObject = eventData.pointerCurrentRaycast.gameObject;
        }

        if (clickedObject != gameObject)
        {
            return;
        }

        Close();
    }

    private void AutoBind()
    {
        musicSlider ??= FindSlider("Music Slider");
        sfxSlider ??= FindSlider("SFX Slider");
        contentRoot ??= transform.Find("Background") as RectTransform;
        closeButton ??= transform.Find("Back Button")?.GetComponent<Button>();
        closeButton ??= transform.Find("Close Button")?.GetComponent<Button>();
    }

    private void BindControls()
    {
        if (musicSlider != null)
        {
            musicSlider.onValueChanged.RemoveListener(HandleMusicVolumeChanged);
            musicSlider.onValueChanged.AddListener(HandleMusicVolumeChanged);
        }

        if (sfxSlider != null)
        {
            sfxSlider.onValueChanged.RemoveListener(HandleSfxVolumeChanged);
            sfxSlider.onValueChanged.AddListener(HandleSfxVolumeChanged);
        }

        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(Close);
            closeButton.onClick.AddListener(Close);
        }
    }

    private void RefreshSliders()
    {
        suppressSliderCallbacks = true;

        if (musicSlider != null)
        {
            musicSlider.SetValueWithoutNotify(AudioManager.MusicVolume);
        }

        if (sfxSlider != null)
        {
            sfxSlider.SetValueWithoutNotify(AudioManager.SfxVolume);
        }

        suppressSliderCallbacks = false;
    }

    private void HandleMusicVolumeChanged(float value)
    {
        if (suppressSliderCallbacks)
        {
            return;
        }

        AudioManager.SetMusicVolume(value);
    }

    private void HandleSfxVolumeChanged(float value)
    {
        if (suppressSliderCallbacks)
        {
            return;
        }

        AudioManager.SetSfxVolume(value);
    }

    private Slider FindSlider(string objectName)
    {
        return transform.Find(objectName)?.GetComponent<Slider>();
    }
}
