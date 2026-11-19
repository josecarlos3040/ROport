using UnityEngine;

public class ROportAudioManager : MonoBehaviour
{
    public static ROportAudioManager Instance { get; private set; }

    private const string VolumeKey = "ROport_MasterVolume";
    private const string MusicMuteKey = "ROport_MusicMuted";

    [Header("Audio Sources")]
    [SerializeField] private AudioSource musicSource;
    [SerializeField] private AudioSource sfxSource;

    [Header("Audio Clips")]
    [SerializeField] private AudioClip backgroundMusic;
    [SerializeField] private AudioClip buttonSound;
    [SerializeField] private AudioClip trickSuccessSound;
    [SerializeField] private AudioClip trickFailSound;
    [SerializeField] private AudioClip photoSound;

    [Header("Audio Balance")]
    [SerializeField, Range(0f, 1f)]
    private float musicVolume = 0.6f;

    [SerializeField, Range(0f, 1f)]
    private float effectsVolume = 1f;

    public float MasterVolume { get; private set; } = 1f;
    public bool MusicMuted { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        ConfigureSources();

        MasterVolume = Mathf.Clamp01(
            PlayerPrefs.GetFloat(VolumeKey, 1f));

        MusicMuted =
            PlayerPrefs.GetInt(MusicMuteKey, 0) == 1;

        ApplyAudioSettings();

        if (musicSource.clip != null && !musicSource.isPlaying)
            musicSource.Play();
    }

    private void ConfigureSources()
    {
        AudioSource[] existingSources =
            GetComponents<AudioSource>();

        if (musicSource == null)
        {
            if (existingSources.Length > 0)
                musicSource = existingSources[0];
            else
                musicSource = gameObject.AddComponent<AudioSource>();
        }

        if (sfxSource == null || sfxSource == musicSource)
        {
            sfxSource = null;

            foreach (AudioSource source in existingSources)
            {
                if (source != musicSource)
                {
                    sfxSource = source;
                    break;
                }
            }

            if (sfxSource == null)
                sfxSource = gameObject.AddComponent<AudioSource>();
        }

        musicSource.playOnAwake = false;
        musicSource.loop = true;
        musicSource.spatialBlend = 0f;
        musicSource.ignoreListenerVolume = false;

        if (backgroundMusic != null)
            musicSource.clip = backgroundMusic;

        sfxSource.playOnAwake = false;
        sfxSource.loop = false;
        sfxSource.spatialBlend = 0f;
        sfxSource.ignoreListenerVolume = false;
    }

    private void ApplyAudioSettings()
    {
        // Volume geral de TODOS os audios do jogo.
        AudioListener.volume = MasterVolume;

        // Desliga a musica zerando o AudioSource.
        if (musicSource != null)
        {
            musicSource.mute = false;
            musicSource.volume =
                MusicMuted ? 0f : musicVolume;
        }

        // Os efeitos continuam independentes do mute.
        if (sfxSource != null)
            sfxSource.volume = effectsVolume;
    }

    public void SetMasterVolume(float value)
    {
        MasterVolume = Mathf.Clamp01(value);

        ApplyAudioSettings();

        PlayerPrefs.SetFloat(VolumeKey, MasterVolume);
        PlayerPrefs.Save();

        Debug.Log(
            "[ROport Audio] Volume geral: " +
            MasterVolume.ToString("F2"));
    }

    public void SetMusicMuted(bool muted)
    {
        MusicMuted = muted;

        ApplyAudioSettings();

        PlayerPrefs.SetInt(
            MusicMuteKey,
            MusicMuted ? 1 : 0);

        PlayerPrefs.Save();

        Debug.Log(
            "[ROport Audio] Musica: " +
            (MusicMuted ? "DESLIGADA" : "LIGADA") +
            " | Volume: " +
            musicSource.volume.ToString("F2"));
    }

    public void ToggleMusic()
    {
        SetMusicMuted(!MusicMuted);
    }

    private void PlaySFX(AudioClip clip)
    {
        if (clip == null || sfxSource == null)
            return;

        sfxSource.PlayOneShot(clip);
    }

    public void PlayButton()
    {
        PlaySFX(buttonSound);
    }

    public void PlayTrickSuccess()
    {
        PlaySFX(trickSuccessSound);
    }

    public void PlayTrickFailure()
    {
        PlaySFX(trickFailSound);
    }

    public void PlayPhoto()
    {
        PlaySFX(photoSound);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }
}