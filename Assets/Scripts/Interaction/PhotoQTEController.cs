using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

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
    [SerializeField] private Image timeBar;
    [SerializeField] private Slider timeSlider;
    [SerializeField] private TMP_Text feedbackText;

    [Header("Feedback")]
    [SerializeField, Min(0.1f)] private float feedbackDuration = 2f;

    private readonly HashSet<CityProblem> failedThisRun =
        new HashSet<CityProblem>();

    private CityProblem currentProblem;
    private float initialDistance;
    private float feedbackRemaining;

    private void Awake()
    {
        if (gameplayCamera == null)
            gameplayCamera = Camera.main;

        HideQTE();
        SetBarProgress(1f);

        if (feedbackText != null)
            feedbackText.text = string.Empty;
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

        if (!HasValidReferences() ||
            runManager.Phase != RunPhase.Executing)
        {
            HideQTE();
            return;
        }

        if (currentProblem != null && !IsCurrentProblemAvailable())
        {
            FailQTE();
            return;
        }

        if (currentProblem == null)
        {
            CityProblem nearby = FindNearbyProblem();

            if (nearby != null)
                StartQTE(nearby);
        }

        if (currentProblem == null)
            return;

        UpdateQTEWorldPosition();

        // Permite fotografar enquanto a janela esta ativa.
        Keyboard keyboard = Keyboard.current;

        if (keyboard != null &&
            keyboard.eKey.wasPressedThisFrame)
        {
            TryTakePhoto();
            return;
        }

        UpdateQTEProgress();
    }

    private bool HasValidReferences()
    {
        return player != null &&
               routeFollower != null &&
               runManager != null &&
               cityProgressManager != null;
    }

    private bool IsCurrentProblemAvailable()
    {
        return currentProblem.CanPhotograph &&
               currentProblem.IsPlayerNear(player.position);
    }

    private CityProblem FindNearbyProblem()
    {
        CityProblem nearest = null;
        float bestDistance = float.MaxValue;

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

            if (distance >= bestDistance)
                continue;

            bestDistance = distance;
            nearest = problem;
        }

        return nearest;
    }

    private void StartQTE(CityProblem problem)
    {
        float distance =
            routeFollower.GetRemainingDistanceInsideProblem(problem);

        // Se estiver bloqueado, a janela de espera
        // ainda permite fotografar.
        if (distance <= 0.001f && !routeFollower.IsBlocked)
        {
            failedThisRun.Add(problem);
            return;
        }

        currentProblem = problem;
        initialDistance = Mathf.Max(distance, 0.001f);

        if (qtePanel != null)
            qtePanel.SetActive(true);

        if (qteText != null)
        {
            qteText.text =
                "FOTOGRAFAR\n" +
                problem.DisplayName +
                "\n[E]";
        }

        SetBarProgress(1f);
        UpdateQTEWorldPosition();
    }

    private void UpdateQTEProgress()
    {
        float progress;

        if (routeFollower.IsBlocked)
        {
            // Ultima oportunidade antes do fim da run.
            progress = routeFollower.BlockTimeRemaining /
                       Mathf.Max(routeFollower.BlockWaitDuration, 0.01f);
        }
        else
        {
            float remainingDistance =
                routeFollower.GetRemainingDistanceInsideProblem(
                    currentProblem);

            if (remainingDistance <= 0.001f)
            {
                FailQTE();
                return;
            }

            progress = remainingDistance / initialDistance;
        }

        SetBarProgress(progress);
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

        canvasTransform.position =
            currentProblem.transform.position +
            Vector3.up * worldHeight +
            gameplayCamera.transform.up * screenUpOffset;

        canvasTransform.rotation = gameplayCamera.transform.rotation;
    }

    private void TryTakePhoto()
    {
        if (currentProblem == null)
            return;

        if (!cityProgressManager.RegisterPhoto(currentProblem))
            return;

        ShowFeedback(
            "FOTO REGISTRADA!\n" +
            currentProblem.DisplayName);

        HideQTE();
    }

    private void FailQTE()
    {
        if (currentProblem == null)
            return;

        failedThisRun.Add(currentProblem);

        ShowFeedback(
            "FOTO PERDIDA!\nTente na proxima run.");

        HideQTE();
    }

    private void HideQTE()
    {
        currentProblem = null;
        initialDistance = 0f;

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
            feedbackText.text = string.Empty;
    }

    private void OnPhaseChanged(RunPhase phase)
    {
        HideQTE();

        if (phase == RunPhase.Planning)
        {
            failedThisRun.Clear();
            feedbackRemaining = 0f;

            if (feedbackText != null)
                feedbackText.text = string.Empty;
        }
    }
}