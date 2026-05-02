using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class HeroSelectReferences
{
    public Transform HeroSelectorRoot { get; private set; }
    public TMP_Text HeroNameText { get; private set; }
    public TMP_Text HeroDescriptionText { get; private set; }
    public Button SelectButton { get; private set; }
    public TMP_Text SelectButtonText { get; private set; }
    public List<TMP_Text> StatValueTexts { get; } = new();
    public List<TMP_Text> MoveTexts { get; } = new();
    public List<Image> MoveIconImages { get; } = new();

    public static HeroSelectReferences Create()
    {
        var references = new HeroSelectReferences
        {
            HeroSelectorRoot = FindFirstExistingTransform(
                "Canvas/Hero Selection Panel/Hero Selector",
                "Canvas/Hero Selection Panel/Hero Selection/Viewport/Content",
                "Canvas/Hero Selection Panel/Scroll View/Viewport/Content"),
            HeroNameText = FindComponent<TMP_Text>("Canvas/Hero Selection Panel/Hero Name"),
            HeroDescriptionText = FindComponent<TMP_Text>("Canvas/Hero Selection Panel/Hero Description"),
            SelectButton = FindComponent<Button>("Canvas/Hero Selection Panel/Select Button")
        };

        references.SelectButtonText = references.SelectButton != null
            ? references.SelectButton.GetComponentInChildren<TMP_Text>()
            : null;

        references.CacheStatTexts();
        references.CacheMoveReferences();
        return references;
    }

    private void CacheStatTexts()
    {
        StatValueTexts.Clear();

        foreach (var statName in new[] { "Attack Stat", "Defense Stat", "Magic Stat" })
        {
            var statRoot = GameObject.Find($"Canvas/Hero Selection Panel/Stats/{statName}");
            if (statRoot == null)
            {
                StatValueTexts.Add(null);
                continue;
            }

            var text = statRoot.GetComponentsInChildren<TMP_Text>(true)
                .FirstOrDefault(candidate => !candidate.name.Contains("Label"));
            StatValueTexts.Add(text);
        }
    }

    private void CacheMoveReferences()
    {
        MoveTexts.Clear();
        MoveIconImages.Clear();

        for (var index = 0; index < 4; index++)
        {
            var moveRoot = GameObject.Find($"Canvas/Hero Selection Panel/Moves/Move {index + 1}");
            var text = moveRoot != null ? moveRoot.GetComponentsInChildren<TMP_Text>(true).LastOrDefault() : null;
            var icon = moveRoot != null ? moveRoot.transform.Find("Move Icon")?.GetComponent<Image>() : null;
            MoveTexts.Add(text);
            MoveIconImages.Add(icon);
        }
    }

    private static T FindComponent<T>(string path) where T : Component
    {
        var target = GameObject.Find(path);
        return target != null ? target.GetComponent<T>() : null;
    }

    private static Transform FindFirstExistingTransform(params string[] paths)
    {
        if (paths == null)
        {
            return null;
        }

        for (var index = 0; index < paths.Length; index++)
        {
            var path = paths[index];
            if (string.IsNullOrWhiteSpace(path))
            {
                continue;
            }

            var target = GameObject.Find(path);
            if (target != null)
            {
                return target.transform;
            }
        }

        return null;
    }
}
