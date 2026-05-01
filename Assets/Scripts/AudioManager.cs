using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-2000)]
public sealed class AudioManager : MonoBehaviour
{
    [Header("Mixer")]
    [SerializeField] private AudioMixer audioMixer;
    [SerializeField] private string musicGroupName = "Music";
    [SerializeField] private string sfxGroupName = "SFX";

    [Header("Sources")]
    [SerializeField] private AudioSource musicSource;
    [SerializeField] private AudioSource sfxSource;

    private static AudioManager instance;
    private AudioMixerGroup musicMixerGroup;
    private AudioMixerGroup sfxMixerGroup;

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
        if (Instance.musicSource == null)
        {
            return;
        }

        Instance.musicSource.Stop();
        Instance.musicSource.clip = null;
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
        CaptureSceneSources();
        TryAssignMixerFromExistingSources();
        TryAssignMixerFromResources();
        ResolveMixerGroups();
        EnsureSourcesExist();
        ApplyMixerGroups();
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        CaptureSceneSources();
        EnsureSourcesExist();
        ApplyMixerGroups();
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
        if (musicSource == null)
        {
            musicSource = CaptureNamedSource("Music Source", "MusicSource");
        }

        if (sfxSource == null)
        {
            sfxSource = CaptureNamedSource("SFX Source", "Sfx Source", "SFXSource", "SfxSource");
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

            DontDestroyOnLoad(source.gameObject);
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
        if (musicSource == null)
        {
            musicSource = CreateSource("Music Source", true);
        }

        if (sfxSource == null)
        {
            sfxSource = CreateSource("SFX Source", false);
        }
    }

    private AudioSource CreateSource(string sourceName, bool loop)
    {
        var sourceObject = new GameObject(sourceName);
        sourceObject.transform.SetParent(transform, false);
        DontDestroyOnLoad(sourceObject);

        var source = sourceObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = loop;
        return source;
    }

    private void ApplyMixerGroups()
    {
        if (musicSource != null)
        {
            musicSource.playOnAwake = false;
            musicSource.loop = true;
            if (musicMixerGroup != null)
            {
                musicSource.outputAudioMixerGroup = musicMixerGroup;
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
        if (musicSource == null)
        {
            return;
        }

        if (musicSource.clip == clip && musicSource.isPlaying)
        {
            musicSource.loop = loop;
            musicSource.volume = volume;
            return;
        }

        musicSource.Stop();
        musicSource.clip = clip;
        musicSource.loop = loop;
        musicSource.volume = volume;
        musicSource.Play();
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
}
