
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MenuManager : MonoBehaviour
{
    [Header("Menus")]
    [SerializeField] private GameObject mainMenu;
    [SerializeField] private GameObject optionsMenu;

    [Header("Camera")]
    [SerializeField] private Camera mainCamera;
    [SerializeField] private Transform camera1Target;
    [SerializeField] private Transform camera2Target;
    [SerializeField] private float transitionDuration = 1f;

    [Header("Scene")]
    [SerializeField] private string gameSceneName = "SampleScene";

    [Header("Audio UI")]
    [SerializeField] private Slider volumeSlider;
    [SerializeField] private Toggle musicToggle;

    private Coroutine cameraTransitionCoroutine;

    private void Start()
    {
        if (mainMenu != null)
            mainMenu.SetActive(true);

        if (optionsMenu != null)
            optionsMenu.SetActive(false);

        if (mainCamera != null && camera1Target != null)
        {
            mainCamera.transform.SetPositionAndRotation(
                camera1Target.position,
                camera1Target.rotation);
        }

        ConfigureAudioControls();
    }

    private void ConfigureAudioControls()
    {
        ROportAudioManager audio =
            ROportAudioManager.Instance;

        if (audio == null)
        {
            Debug.LogWarning(
                "[ROport Menu] AudioManager nao encontrado!");
            return;
        }

        // SLIDER
        if (volumeSlider != null)
        {
            volumeSlider.minValue = 0f;
            volumeSlider.maxValue = 1f;
            volumeSlider.wholeNumbers = false;

            volumeSlider.SetValueWithoutNotify(
                audio.MasterVolume);

            // Evita adicionar o evento duplicado.
            volumeSlider.onValueChanged.RemoveListener(
                AjustarVolume);

            volumeSlider.onValueChanged.AddListener(
                AjustarVolume);
        }
        else
        {
            Debug.LogWarning(
                "[ROport Menu] Volume Slider nao atribuido!");
        }

        // TOGGLE
        if (musicToggle != null)
        {
            // E um Toggle independente.
            // Nao deve pertencer a um ToggleGroup.
            musicToggle.group = null;
            musicToggle.interactable = true;

            musicToggle.SetIsOnWithoutNotify(
                !audio.MusicMuted);

            musicToggle.onValueChanged.RemoveListener(
                AjustarToggleMusica);

            musicToggle.onValueChanged.AddListener(
                AjustarToggleMusica);
        }
        else
        {
            Debug.LogWarning(
                "[ROport Menu] Music Toggle nao atribuido!");
        }

    }

    // ==========================================
    // AUDIO
    // ==========================================

    public void AjustarVolume(float volume)
    {
        ROportAudioManager audio =
            ROportAudioManager.Instance;

        if (audio == null)
            return;

        audio.SetMasterVolume(volume);
    }

    public void AjustarToggleMusica(bool musicaLigada)
    {
        ROportAudioManager audio =
            ROportAudioManager.Instance;

        if (audio == null)
            return;

        // TRUE = musica ligada
        // FALSE = musica desligada
        audio.SetMusicMuted(!musicaLigada);

        audio.PlayButton();


    }

    private void UpdateAudioUI()
    {
        ROportAudioManager audio =
            ROportAudioManager.Instance;

        if (audio == null)
            return;

        if (volumeSlider != null)
        {
            volumeSlider.SetValueWithoutNotify(
                audio.MasterVolume);
        }

        if (musicToggle != null)
        {
            musicToggle.SetIsOnWithoutNotify(
                !audio.MusicMuted);
        }

    }

    private void PlayButtonSound()
    {
        ROportAudioManager.Instance?.PlayButton();
    }

    // ==========================================
    // MENU
    // ==========================================

    public void Jogar()
    {
        PlayButtonSound();

        if (!string.IsNullOrEmpty(gameSceneName))
            SceneManager.LoadScene(gameSceneName);
        else
            Debug.LogWarning(
                "[ROport Menu] Cena nao configurada.");
    }

    public void AbrirOpcoes()
    {
        PlayButtonSound();

        if (mainMenu != null)
            mainMenu.SetActive(false);

        if (optionsMenu != null)
            optionsMenu.SetActive(true);

        UpdateAudioUI();
        IniciarTransicaoCamera(camera2Target);
    }

    public void VoltarParaMenuPrincipal()
    {
        PlayButtonSound();

        if (optionsMenu != null)
            optionsMenu.SetActive(false);

        if (mainMenu != null)
            mainMenu.SetActive(true);

        IniciarTransicaoCamera(camera1Target);
    }

    public void Sair()
    {
        PlayButtonSound();
        Application.Quit();
    }

    // ==========================================
    // CAMERA
    // ==========================================

    private void IniciarTransicaoCamera(Transform alvo)
    {
        if (mainCamera == null || alvo == null)
            return;

        if (cameraTransitionCoroutine != null)
            StopCoroutine(cameraTransitionCoroutine);

        cameraTransitionCoroutine =
            StartCoroutine(CameraTransicao(alvo));
    }

    private IEnumerator CameraTransicao(Transform alvo)
    {
        Vector3 posicaoInicial =
            mainCamera.transform.position;

        Quaternion rotacaoInicial =
            mainCamera.transform.rotation;

        float duracao = Mathf.Max(
            0.001f, transitionDuration);

        float tempoPassado = 0f;

        while (tempoPassado < duracao)
        {
            tempoPassado += Time.deltaTime;

            float t = Mathf.Clamp01(
                tempoPassado / duracao);

            t = t * t * (3f - 2f * t);

            mainCamera.transform.position =
                Vector3.Lerp(
                    posicaoInicial,
                    alvo.position,
                    t);

            mainCamera.transform.rotation =
                Quaternion.Slerp(
                    rotacaoInicial,
                    alvo.rotation,
                    t);

            yield return null;
        }

        mainCamera.transform.SetPositionAndRotation(
            alvo.position,
            alvo.rotation);

        cameraTransitionCoroutine = null;
    }

    private void OnDestroy()
    {
        if (volumeSlider != null)
        {
            volumeSlider.onValueChanged.RemoveListener(
                AjustarVolume);
        }

        if (musicToggle != null)
        {
            musicToggle.onValueChanged.RemoveListener(
                AjustarToggleMusica);
        }
    }
}