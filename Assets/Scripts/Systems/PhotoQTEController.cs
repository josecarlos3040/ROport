using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

public class PhotoQTEController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform player;
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
    [SerializeField] private Image timeBar;
    [SerializeField] private TMP_Text feedbackText;

    [Header("QTE Settings")]
    [SerializeField, Min(0.1f)]
    private float qteDuration = 1.8f;

    [SerializeField, Min(0.1f)]
    private float feedbackDuration = 2f;

    private CityProblem currentProblem;

    private float timeRemaining;
    private float feedbackRemaining;

    private readonly HashSet<CityProblem> failedThisRun =
        new HashSet<CityProblem>();

    private void Awake()
    {
        if (gameplayCamera == null)
            gameplayCamera = Camera.main;

        HideQTE();

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
            runManager == null ||
            cityProgressManager == null)
        {
            HideQTE();
            return;
        }

        if (runManager.Phase != RunPhase.Executing)
        {
            HideQTE();
            return;
        }

        if (currentProblem != null)
        {
            if (!currentProblem.CanPhotograph ||
                !currentProblem.IsPlayerNear(player.position))
            {
                HideQTE();
            }
        }

        if (currentProblem == null)
        {
            CityProblem nearby = FindNearbyProblem();

            if (nearby != null)
                StartQTE(nearby);
        }

        if (currentProblem == null)
            return;

        // Mantém a interface perto do problema.
        UpdateQTEWorldPosition();

        Keyboard keyboard = Keyboard.current;

        if (keyboard != null &&
            keyboard.eKey.wasPressedThisFrame)
        {
            TryTakePhoto();
            return;
        }

        timeRemaining -= Time.deltaTime;

        if (timeBar != null)
        {
            timeBar.fillAmount = Mathf.Clamp01(
                timeRemaining / qteDuration);
        }

        if (timeRemaining <= 0f)
            FailQTE();
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
        currentProblem = problem;
        timeRemaining = qteDuration;

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

        if (timeBar != null)
            timeBar.fillAmount = 1f;

        Debug.Log("[ROport] QTE iniciado: " + problem.DisplayName);
    }

    // NOVO: posiciona o QTE no mundo 3D.
    private void UpdateQTEWorldPosition()
    {
        if (currentProblem == null ||
            worldQTECanvas == null ||
            gameplayCamera == null)
        {
            return;
        }

        Transform canvasTransform = worldQTECanvas.transform;

        // Altura em relação à rua.
        Vector3 heightOffset = Vector3.up * worldHeight;

        // Direção que representa "cima" na tela.
        Vector3 screenOffset =
            gameplayCamera.transform.up * screenUpOffset;

        canvasTransform.position =
            currentProblem.transform.position +
            heightOffset +
            screenOffset;

        // O painel acompanha a orientação da câmera.
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
        timeRemaining = 0f;

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

        if (feedbackRemaining <= 0f && feedbackText != null)
            feedbackText.text = "";
    }

    private void OnPhaseChanged(RunPhase phase)
    {
        HideQTE();

        if (phase == RunPhase.Planning)
            failedThisRun.Clear();
    }
}