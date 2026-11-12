using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

public class PhotoQTEController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform player;
    [SerializeField] private RouteFollower routeFollower;
    [SerializeField] private ROportRunManager runManager;
    [SerializeField] private CityProgressManager cityProgressManager;

    [Header("World Space UI")]
    [SerializeField] private Canvas worldQTECanvas;
    [SerializeField] private Camera gameplayCamera;

    [SerializeField] private float worldHeight = 1f;
    [SerializeField] private float screenUpOffset = 3f;

    [Header("QTE UI")]
    [SerializeField] private GameObject qtePanel;
    [SerializeField] private TMP_Text qteText;

    // Use Image OU Slider, dependendo da sua UI.
    [SerializeField] private Image timeBar;
    [SerializeField] private Slider timeSlider;

    [SerializeField] private TMP_Text feedbackText;

    [Header("Feedback")]
    [SerializeField, Min(0.1f)]
    private float feedbackDuration = 2f;

    private CityProblem currentProblem;

    // Tempo total disponível ao iniciar o QTE.
    private float initialTime;

    private float feedbackRemaining;

    private readonly HashSet<CityProblem> failedThisRun =
        new HashSet<CityProblem>();

    private void Awake()
    {
        if (gameplayCamera == null)
            gameplayCamera = Camera.main;

        HideQTE();
        SetBarProgress(1f);

        if (feedbackText != null)
            feedbackText.text = "";
    }

    private void OnEnable()
    {
        if (runManager != null)
            runManager.PhaseChanged += OnPhaseChanged;
    }

    private void OnDisable()
    {
        if (runManager != null)
            runManager.PhaseChanged -= OnPhaseChanged;

        HideQTE();
    }

    private void Update()
    {
        UpdateFeedback();

        if (player == null ||
            routeFollower == null ||
            runManager == null ||
            cityProgressManager == null)
        {
            HideQTE();
            return;
        }

        // O QTE só existe durante a execução.
        if (runManager.Phase != RunPhase.Executing)
        {
            HideQTE();
            return;
        }

        if (currentProblem != null)
        {
            if (!currentProblem.CanPhotograph)
            {
                HideQTE();
                return;
            }

            // Sair do raio encerra a oportunidade.
            if (!currentProblem.IsPlayerNear(player.position))
            {
                FailQTE();
                return;
            }
        }

        // Procura um problema se não houver QTE ativo.
        if (currentProblem == null)
        {
            CityProblem nearby = FindNearbyProblem();

            if (nearby != null)
                StartQTE(nearby);
        }

        if (currentProblem == null)
            return;

        UpdateQTEWorldPosition();

        // Calcula o percurso restante dentro da área.
        float remainingDistance =
            routeFollower.GetRemainingDistanceInsideProblem(
                currentProblem);

        if (remainingDistance <= 0.001f)
        {
            FailQTE();
            return;
        }

        float remainingTime =
            remainingDistance / routeFollower.MovementSpeed;

        // A barra representa a fração do tempo restante.
        float progress = Mathf.Clamp01(
            remainingTime / initialTime);

        // Se o personagem estiver bloqueado,
        // a barra representa a ultima oportunidade.
        if (routeFollower.IsBlocked)
        {
            progress = Mathf.Clamp01(
                routeFollower.BlockTimeRemaining /
                routeFollower.BlockWaitDuration);
        }

        SetBarProgress(progress);

        SetBarProgress(progress);

        // Fotografa somente enquanto ainda há tempo.
        Keyboard keyboard = Keyboard.current;

        if (keyboard != null &&
            keyboard.eKey.wasPressedThisFrame)
        {
            TryTakePhoto();
        }
    }

    private CityProblem FindNearbyProblem()
    {
        CityProblem nearest = null;
        float nearestDistance = float.MaxValue;

        foreach (CityProblem problem in cityProgressManager.Problems)
        {
            if (problem == null ||
                !problem.CanPhotograph ||
                failedThisRun.Contains(problem) ||
                !problem.IsPlayerNear(player.position))
            {
                continue;
            }

            Vector3 difference =
                player.position - problem.transform.position;

            difference.y = 0f;

            float distance = difference.sqrMagnitude;

            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearest = problem;
            }
        }

        return nearest;
    }

    private void StartQTE(CityProblem problem)
    {
        float distance =
            routeFollower.GetRemainingDistanceInsideProblem(
                problem);

        // Não inicia se não houver tempo útil na rota.
        if (distance <= 0.001f)
        {
            failedThisRun.Add(problem);
            return;
        }

        currentProblem = problem;

        initialTime =
            distance / routeFollower.MovementSpeed;

        SetBarProgress(1f);

        UpdateQTEWorldPosition();

        if (qtePanel != null)
            qtePanel.SetActive(true);

        if (qteText != null)
        {
            qteText.text =
                "FOTOGRAFAR\n" +
                problem.DisplayName +
                "\n[E]";
        }

        Debug.Log(
            "[ROport] QTE iniciado. Tempo disponível: " +
            initialTime.ToString("F2") + "s");
    }

    
    private void SetBarProgress(float progress)
    {
        progress = Mathf.Clamp01(progress);

        if (timeBar != null)
            timeBar.fillAmount = progress;

        if (timeSlider != null)
        {
            timeSlider.minValue = 0f;
            timeSlider.maxValue = 1f;
            timeSlider.SetValueWithoutNotify(progress);
        }
    }

    private void UpdateQTEWorldPosition()
    {
        if (currentProblem == null ||
            worldQTECanvas == null ||
            gameplayCamera == null)
        {
            return;
        }

        Transform canvasTransform = worldQTECanvas.transform;

        Vector3 heightOffset =
            Vector3.up * worldHeight;

        Vector3 screenOffset =
            gameplayCamera.transform.up * screenUpOffset;

        canvasTransform.position =
            currentProblem.transform.position +
            heightOffset +
            screenOffset;

        canvasTransform.rotation =
            gameplayCamera.transform.rotation;
    }

    private void TryTakePhoto()
    {
        if (currentProblem == null)
            return;

        if (cityProgressManager.RegisterPhoto(currentProblem))
        {
            ShowFeedback(
                "FOTO REGISTRADA!\n" +
                currentProblem.DisplayName);

            Debug.Log("[ROport] Foto registrada!");

            HideQTE();
        }
    }

    private void FailQTE()
    {
        if (currentProblem == null)
            return;

        failedThisRun.Add(currentProblem);

        ShowFeedback(
            "FOTO PERDIDA!\nTente na próxima run.");

        Debug.Log("[ROport] QTE falhou!");

        HideQTE();
    }

    private void HideQTE()
    {
        currentProblem = null;
        initialTime = 0f;

        if (qtePanel != null)
            qtePanel.SetActive(false);
    }

    private void ShowFeedback(string message)
    {
        feedbackRemaining = feedbackDuration;

        if (feedbackText != null)
            feedbackText.text = message;
    }

    private void UpdateFeedback()
    {
        if (feedbackRemaining <= 0f)
            return;

        feedbackRemaining -= Time.deltaTime;

        if (feedbackRemaining <= 0f &&
            feedbackText != null)
        {
            feedbackText.text = "";
        }
    }

    private void OnPhaseChanged(RunPhase phase)
    {
        HideQTE();

        if (phase == RunPhase.Planning)
            failedThisRun.Clear();
    }
}