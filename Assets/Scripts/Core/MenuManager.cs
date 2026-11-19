using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MenuManager : MonoBehaviour
{
    [Header("Menus (Canvas ou Painéis)")]
    [SerializeField] private GameObject mainMenu;
    [SerializeField] private GameObject optionsMenu;

    [Header("Configurações de Câmera")]
    [SerializeField] private Camera mainCamera;
    [SerializeField] private Transform camera1Target;
    [SerializeField] private Transform camera2Target;
    [SerializeField] private float transitionDuration = 1.0f;

    [Header("Configurações de Cena")]
    [SerializeField] private string gameSceneName = "SampleScene";

    private Coroutine cameraTransitionCoroutine;

    private void Start()
    {
        if (mainMenu != null) mainMenu.SetActive(true);
        if (optionsMenu != null) optionsMenu.SetActive(false);

        if (mainCamera != null && camera1Target != null)
        {
            mainCamera.transform.position = camera1Target.position;
            mainCamera.transform.rotation = camera1Target.rotation;
        }
    }

    public void Jogar()
    {
        if (!string.IsNullOrEmpty(gameSceneName))
        {
            SceneManager.LoadScene(gameSceneName);
        }
        else
        {
            Debug.LogWarning("Que cena é essa q eu nao achei");
        }
    }

    public void AbrirOpcoes()
    {
        if (mainMenu != null) mainMenu.SetActive(false);
        if (optionsMenu != null) optionsMenu.SetActive(true);

        IniciarTransicaoCamera(camera2Target);
    }

    public void VoltarParaMenuPrincipal()
    {
        if (optionsMenu != null) optionsMenu.SetActive(false);
        if (mainMenu != null) mainMenu.SetActive(true);

        IniciarTransicaoCamera(camera1Target);
    }

    public void Sair()
    {
        Debug.Log("saindo");
        Application.Quit();

    }

    private void IniciarTransicaoCamera(Transform alvo)
    {
        if (mainCamera == null || alvo == null) return;

        if (cameraTransitionCoroutine != null)
        {
            StopCoroutine(cameraTransitionCoroutine);
        }

        cameraTransitionCoroutine = StartCoroutine(CameraTransicao(alvo));
    }

    private IEnumerator CameraTransicao(Transform alvo)
    {
        Vector3 posicaoInicial = mainCamera.transform.position;
        Quaternion rotacaoInicial = mainCamera.transform.rotation;
        float tempoPassado = 0f;

        while (tempoPassado < transitionDuration)
        {
            tempoPassado += Time.deltaTime;
            float t = tempoPassado / transitionDuration;

            t = t * t * (3f - 2f * t);

            mainCamera.transform.position = Vector3.Lerp(posicaoInicial, alvo.position, t);
            mainCamera.transform.rotation = Quaternion.Slerp(rotacaoInicial, alvo.rotation, t);

            yield return null;
        }

        mainCamera.transform.position = alvo.position;
        mainCamera.transform.rotation = alvo.rotation;
    }
}
