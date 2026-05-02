using System.Collections;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-2000)]
public sealed class AudioManager : MonoBehaviour
{
    private const string MusicVolumePreferenceKey = "AudioManager.MusicVolume";
    private const string SfxVolumePreferenceKey = "AudioManager.SfxVolume";
    private const float DefaultVolume = 1f;
    private const float MinDecibels = -80f;
    private const float MinLinearVolume = 0.0001f;

    [Header("Mixer")]
    [SerializeField] private AudioMixer audioMixer;
    [SerializeField] private string musicGroupName = "Music";
    [SerializeField] private string sfxGroupName = "SFX";
    [SerializeField] private string musicVolumeParameter = "MusicVolume";
    [SerializeField] private string sfxVolumeParameter = "SFXVolume";

    [Header("Sources")]
    [SerializeField] private AudioSource musicSource;
    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private float musicCrossfadeDuration = 1.25f;

    private static AudioManager instance;
    private AudioMixerGroup musicMixerGroup;
    private AudioMixerGroup sfxMixerGroup;
    private AudioSource[] musicSources;
    private int activeMusicSourceIndex;
    private Coroutine musicTransitionCoroutine;
    private Coroutine applySavedVolumesCoroutine;
    private float musicVolumeSetting = DefaultVolume;
    private float sfxVolumeSetting = DefaultVolume;

    public static AudioManager Instance
    {
        get
        {
            EnsureInstance();
            return instance;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        EnsureInstance();
    }

    private static void EnsureInstance()
    {
        if (instance != null)
        {
            return;
        }

        instance = FindFirstObjectByType<AudioManager>();
        if (instance != null)
        {
            instance.Initialize();
            return;
        }

        var managerObject = new GameObject(nameof(AudioManager));
        instance = managerObject.AddComponent<AudioManager>();
        instance.Initialize();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        Initialize();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    public static void PlayMusic(AudioClip clip, bool loop = true, float volume = 1f)
    {
        if (clip == null)
        {
            return;
        }

        Instance.PlayMusicInternal(clip, loop, volume);
    }

    public static void PlayMusic(string resourcesPath, bool loop = true, float volume = 1f)
    {
        var clip = LoadClip(resourcesPath);
        if (clip == null)
        {
            return;
        }

        PlayMusic(clip, loop, volume);
    }

    public static void StopMusic()
    {
        Instance.StopMusicInternal();
    }

    public static void PlaySfx(AudioClip clip, float volume = 1f, float pitch = 1f)
    {
        if (clip == null)
        {
            return;
        }

        Instance.PlaySfxInternal(clip, volume, pitch);
    }

    public static void PlaySfx(string resourcesPath, float volume = 1f, float pitch = 1f)
    {
        var clip = LoadClip(resourcesPath);
        if (clip == null)
        {
            return;
        }

        PlaySfx(clip, volume, pitch);
    }

    public static void AssignSfxMixerGroup(AudioSource source)
    {
        if (source == null)
        {
            return;
        }

        Instance.AssignSfxMixerGroupInternal(source);
    }

    public static float MusicVolume => Instance.musicVolumeSetting;

    public static float SfxVolume => Instance.sfxVolumeSetting;

    public static void SetMusicVolume(float volume)
    {
        Instance.SetMusicVolumeInternal(volume);
    }

    public static void SetSfxVolume(float volume)
    {
        Instance.SetSfxVolumeInternal(volume);
    }

    private static AudioClip LoadClip(string resourcesPath)
    {
        if (string.IsNullOrWhiteSpace(resourcesPath))
        {
            return null;
        }

        var clip = Resources.Load<AudioClip>(resourcesPath);
        if (clip == null)
        {
            Debug.LogWarning($"AudioManager could not find an AudioClip at Resources path '{resourcesPath}'.");
        }

        return clip;
    }

    private void Initialize()
    {
        DontDestroyOnLoad(gameObject);
        LoadSavedVolumes();
        CaptureSceneSources();
        TryAssignMixerFromExistingSources();
        TryAssignMixerFromResources();
        ResolveMixerGroups();
        EnsureSourcesExist();
        ApplyMixerGroups();
        ScheduleApplySavedVolumes();
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        CaptureSceneSources();
        TryAssignMixerFromExistingSources();
        TryAssignMixerFromResources();
        ResolveMixerGroups();
        EnsureSourcesExist();
        ApplyMixerGroups();
        ScheduleApplySavedVolumes();
    }

    private void TryAssignMixerFromResources()
    {
        if (audioMixer != null)
        {
            return;
        }

        audioMixer = Resources.Load<AudioMixer>("GameAudioMixer");
    }

    private void TryAssignMixerFromExistingSources()
    {
        if (musicSource != null && musicSource.outputAudioMixerGroup != null)
        {
            musicMixerGroup = musicSource.outputAudioMixerGroup;
            audioMixer = musicMixerGroup.audioMixer;
        }

        if (sfxSource != null && sfxSource.outputAudioMixerGroup != null)
        {
            sfxMixerGroup = sfxSource.outputAudioMixerGroup;
            audioMixer ??= sfxMixerGroup.audioMixer;
        }
    }

    private void ResolveMixerGroups()
    {
        if (audioMixer == null)
        {
            return;
        }

        var matchingGroups = audioMixer.FindMatchingGroups(string.Empty);
        foreach (var group in matchingGroups)
        {
            if (group == null)
            {
                continue;
            }

            if (musicMixerGroup == null && group.name == musicGroupName)
            {
                musicMixerGroup = group;
            }
            else if (sfxMixerGroup == null && group.name == sfxGroupName)
            {
                sfxMixerGroup = group;
            }
        }
    }

    private void CaptureSceneSources()
    {
        if (musicSource == null || musicSource.outputAudioMixerGroup == null)
        {
            var capturedMusicSource = CaptureNamedSource("Music Source", "MusicSource");
            if (capturedMusicSource != null)
            {
                musicSource = capturedMusicSource;
                if (musicSources != null && musicSources.Length > 0)
                {
                    musicSources[0] = capturedMusicSource;
                }
            }
        }

        if (sfxSource == null || sfxSource.outputAudioMixerGroup == null)
        {
            var capturedSfxSource = CaptureNamedSource("SFX Source", "Sfx Source", "SFXSource", "SfxSource");
            if (capturedSfxSource != null)
            {
                sfxSource = capturedSfxSource;
            }
        }
    }

    private AudioSource CaptureNamedSource(params string[] objectNames)
    {
        foreach (var objectName in objectNames)
        {
            var source = FindAudioSourceInLoadedScenes(objectName);
            if (source == null)
            {
                continue;
            }

            if (source.transform.parent != transform)
            {
                source.transform.SetParent(transform, true);
            }

            return source;
        }

        return null;
    }

    private static AudioSource FindAudioSourceInLoadedScenes(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
        {
            return null;
        }

        for (var sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
        {
            var scene = SceneManager.GetSceneAt(sceneIndex);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                continue;
            }

            foreach (var rootObject in scene.GetRootGameObjects())
            {
                var source = FindAudioSourceRecursive(rootObject.transform, objectName);
                if (source != null)
                {
                    return source;
                }
            }
        }

        return null;
    }

    private static AudioSource FindAudioSourceRecursive(Transform root, string objectName)
    {
        if (root == null)
        {
            return null;
        }

        if (root.name == objectName)
        {
            return root.GetComponent<AudioSource>();
        }

        for (var childIndex = 0; childIndex < root.childCount; childIndex++)
        {
            var source = FindAudioSourceRecursive(root.GetChild(childIndex), objectName);
            if (source != null)
            {
                return source;
            }
        }

        return null;
    }

    private void EnsureSourcesExist()
    {
        EnsureMusicSourcesExist();

        if (sfxSource == null)
        {
            sfxSource = CreateSource("SFX Source", false);
        }
    }

    private void EnsureMusicSourcesExist()
    {
        if (musicSources == null || musicSources.Length != 2)
        {
            musicSources = new AudioSource[2];
        }

        if (musicSource == null)
        {
            musicSource = CreateSource("Music Source", true);
        }

        if (musicSources[0] == null)
        {
            musicSources[0] = musicSource;
        }

        if (musicSources[1] == null)
        {
            musicSources[1] = CreateSource("Music Source B", true);
        }

        activeMusicSourceIndex = Mathf.Clamp(activeMusicSourceIndex, 0, musicSources.Length - 1);
        musicSource = musicSources[activeMusicSourceIndex];
    }

    private AudioSource CreateSource(string sourceName, bool loop)
    {
        var sourceObject = new GameObject(sourceName);
        sourceObject.transform.SetParent(transform, false);

        var source = sourceObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = loop;
        return source;
    }

    private void ApplyMixerGroups()
    {
        EnsureMusicSourcesExist();

        foreach (var source in musicSources)
        {
            if (source == null)
            {
                continue;
            }

            source.playOnAwake = false;
            source.loop = true;
            if (musicMixerGroup != null)
            {
                source.outputAudioMixerGroup = musicMixerGroup;
            }
        }

        if (sfxSource != null)
        {
            sfxSource.playOnAwake = false;
            sfxSource.loop = false;
            if (sfxMixerGroup != null)
            {
                sfxSource.outputAudioMixerGroup = sfxMixerGroup;
            }
        }
    }

    private void PlayMusicInternal(AudioClip clip, bool loop, float volume)
    {
        EnsureSourcesExist();
        if (musicSources == null || musicSources.Length == 0)
        {
            return;
        }

        var currentSource = musicSources[activeMusicSourceIndex];
        if (currentSource == null)
        {
            return;
        }

        if (currentSource.clip == clip && currentSource.isPlaying)
        {
            currentSource.loop = loop;
            currentSource.volume = volume;
            return;
        }

        if (musicTransitionCoroutine != null)
        {
            StopCoroutine(musicTransitionCoroutine);
            musicTransitionCoroutine = null;
        }

        if (!currentSource.isPlaying || currentSource.clip == null || musicCrossfadeDuration <= 0f)
        {
            currentSource.Stop();
            currentSource.clip = clip;
            currentSource.loop = loop;
            currentSource.volume = volume;
            currentSource.Play();
            musicSource = currentSource;
            return;
        }

        var nextSourceIndex = (activeMusicSourceIndex + 1) % musicSources.Length;
        var nextSource = musicSources[nextSourceIndex];
        if (nextSource == null)
        {
            currentSource.Stop();
            currentSource.clip = clip;
            currentSource.loop = loop;
            currentSource.volume = volume;
            currentSource.Play();
            musicSource = currentSource;
            return;
        }

        musicTransitionCoroutine = StartCoroutine(CrossfadeMusic(currentSource, nextSource, nextSourceIndex, clip, loop, volume));
    }

    private void PlaySfxInternal(AudioClip clip, float volume, float pitch)
    {
        EnsureSourcesExist();
        if (sfxSource == null)
        {
            return;
        }

        var originalPitch = sfxSource.pitch;
        sfxSource.pitch = pitch;
        sfxSource.PlayOneShot(clip, volume);
        sfxSource.pitch = originalPitch;
    }

    private void AssignSfxMixerGroupInternal(AudioSource source)
    {
        EnsureSourcesExist();
        if (sfxMixerGroup == null)
        {
            ResolveMixerGroups();
        }

        source.playOnAwake = false;
        if (sfxMixerGroup != null)
        {
            source.outputAudioMixerGroup = sfxMixerGroup;
        }
    }

    private IEnumerator CrossfadeMusic(AudioSource fromSource, AudioSource toSource, int toSourceIndex, AudioClip clip, bool loop, float targetVolume)
    {
        var fromVolume = fromSource != null ? fromSource.volume : 0f;

        toSource.Stop();
        toSource.clip = clip;
        toSource.loop = loop;
        toSource.volume = 0f;
        toSource.Play();

        var elapsed = 0f;
        while (elapsed < musicCrossfadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            var t = Mathf.Clamp01(elapsed / musicCrossfadeDuration);

            if (fromSource != null)
            {
                fromSource.volume = Mathf.Lerp(fromVolume, 0f, t);
            }

            toSource.volume = Mathf.Lerp(0f, targetVolume, t);
            yield return null;
        }

        if (fromSource != null)
        {
            fromSource.Stop();
            fromSource.clip = null;
            fromSource.volume = fromVolume;
        }

        toSource.volume = targetVolume;
        activeMusicSourceIndex = toSourceIndex;
        musicSource = toSource;
        musicTransitionCoroutine = null;
    }

    private void StopMusicInternal()
    {
        EnsureSourcesExist();

        if (musicTransitionCoroutine != null)
        {
            StopCoroutine(musicTransitionCoroutine);
            musicTransitionCoroutine = null;
        }

        if (musicSources == null)
        {
            return;
        }

        foreach (var source in musicSources)
        {
            if (source == null)
            {
                continue;
            }

            source.Stop();
            source.clip = null;
        }
    }

    private void LoadSavedVolumes()
    {
        musicVolumeSetting = Mathf.Clamp01(PlayerPrefs.GetFloat(MusicVolumePreferenceKey, DefaultVolume));
        sfxVolumeSetting = Mathf.Clamp01(PlayerPrefs.GetFloat(SfxVolumePreferenceKey, DefaultVolume));
    }

    private void ApplySavedVolumes()
    {
        ApplyVolumeParameter(musicVolumeParameter, musicVolumeSetting);
        ApplyVolumeParameter(sfxVolumeParameter, sfxVolumeSetting);
    }

    private void ScheduleApplySavedVolumes()
    {
        if (!isActiveAndEnabled)
        {
            ApplySavedVolumes();
            return;
        }

        if (applySavedVolumesCoroutine != null)
        {
            StopCoroutine(applySavedVolumesCoroutine);
        }

        applySavedVolumesCoroutine = StartCoroutine(ApplySavedVolumesNextFrame());
    }

    private IEnumerator ApplySavedVolumesNextFrame()
    {
        yield return null;
        ApplySavedVolumes();
        applySavedVolumesCoroutine = null;
    }

    private void SetMusicVolumeInternal(float volume)
    {
        musicVolumeSetting = Mathf.Clamp01(volume);
        PlayerPrefs.SetFloat(MusicVolumePreferenceKey, musicVolumeSetting);
        PlayerPrefs.Save();
        ApplyVolumeParameter(musicVolumeParameter, musicVolumeSetting);
    }

    private void SetSfxVolumeInternal(float volume)
    {
        sfxVolumeSetting = Mathf.Clamp01(volume);
        PlayerPrefs.SetFloat(SfxVolumePreferenceKey, sfxVolumeSetting);
        PlayerPrefs.Save();
        ApplyVolumeParameter(sfxVolumeParameter, sfxVolumeSetting);
    }

    private void ApplyVolumeParameter(string parameterName, float volume)
    {
        if (audioMixer == null || string.IsNullOrWhiteSpace(parameterName))
        {
            return;
        }

        audioMixer.SetFloat(parameterName, LinearToDecibels(volume));
    }

    private static float LinearToDecibels(float volume)
    {
        var clampedVolume = Mathf.Clamp01(volume);
        if (clampedVolume <= 0f)
        {
            return MinDecibels;
        }

        return Mathf.Max(MinDecibels, Mathf.Log10(Mathf.Max(clampedVolume, MinLinearVolume)) * 20f);
    }
}
