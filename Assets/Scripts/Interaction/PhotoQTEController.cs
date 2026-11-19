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
    [SerializeField] private Image timeBar;
    [SerializeField] private Slider timeSlider;
    [SerializeField] private TMP_Text feedbackText;
    [SerializeField] private TMP_Text hudPhotoHint;

    [Header("Detection")]
    [SerializeField, Min(0f)]
    private float extraHorizontalRadius = 2f;

    [SerializeField, Min(0f)]
    private float maxVerticalDifference = 3f;

    [SerializeField, Min(0f)]
    private float playerPivotHeight = 1f;

    [Header("Feedback")]
    [SerializeField, Min(0.1f)]
    private float feedbackDuration = 2f;

    private CityProblem currentProblem;
    private float initialDistance;
    private float feedbackRemaining;

    private readonly HashSet<CityProblem> failedThisRun =
        new HashSet<CityProblem>();

    private void Awake()
    {
        if (gameplayCamera == null)
            gameplayCamera = Camera.main;

        if (qtePanel == gameObject)
        {
            Debug.LogError(
                "[ROport Photo] O controlador nao pode " +
                "estar no proprio QTE Panel!", this);
            return;
        }

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

        if (runManager.Phase != RunPhase.Executing)
        {
            HideQTE();
            return;
        }

        Keyboard keyboard = Keyboard.current;
        bool pressedE =
            keyboard != null &&
            keyboard.eKey.wasPressedThisFrame;

        if (currentProblem != null)
        {
            if (!currentProblem.CanPhotograph)
            {
                HideQTE();
            }
            else if (!IsWithinPhotoRange(currentProblem))
            {
                FailQTE();
            }
        }

        if (currentProblem == null)
        {
            CityProblem nearby = FindNearbyProblem();

            if (nearby != null)
                StartQTE(nearby);
        }

        if (currentProblem == null)
        {
            if (pressedE)
            {
                Debug.LogWarning(
                    "[ROport Photo] E pressionado, " +
                    "mas nenhum problema fotografavel " +
                    "foi encontrado. Confira o alcance, " +
                    "o estado e as referencias.");
            }

            return;
        }

        UpdateQTEWorldPosition();
        UpdatePhotoBar();

        if (pressedE)
            TryTakePhoto();
    }

    private bool IsWithinPhotoRange(CityProblem problem)
    {
        if (problem == null || player == null)
            return false;

        if (problem.IsPlayerNear(player.position))
            return true;

        Vector3 feet =
            player.position -
            Vector3.up * playerPivotHeight;

        Vector3 center = problem.transform.position;

        if (Mathf.Abs(feet.y - center.y) >
            maxVerticalDifference)
            return false;

        float dx = feet.x - center.x;
        float dz = feet.z - center.z;

        float radius =
            problem.InteractionRadius +
            extraHorizontalRadius;

        return dx * dx + dz * dz <= radius * radius;
    }

    private CityProblem FindNearbyProblem()
    {
        if (cityProgressManager.Problems == null)
            return null;

        CityProblem nearest = null;
        float bestDistance = float.MaxValue;

        foreach (CityProblem problem in
                 cityProgressManager.Problems)
        {
            if (problem == null ||
                !problem.CanPhotograph ||
                failedThisRun.Contains(problem) ||
                !IsWithinPhotoRange(problem))
            {
                continue;
            }

            Vector3 difference =
                player.position - problem.transform.position;

            difference.y = 0f;

            float distance = difference.sqrMagnitude;

            if (distance < bestDistance)
            {
                bestDistance = distance;
                nearest = problem;
            }
        }

        return nearest;
    }

    private void StartQTE(CityProblem problem)
    {
        if (problem == null)
            return;

        currentProblem = problem;

        initialDistance = Mathf.Max(
            0.001f,
            routeFollower.GetRemainingDistanceInsideProblem(
                problem));

        SetBarProgress(1f);

        if (qtePanel != null)
            qtePanel.SetActive(true);

        if (qteText != null)
        {
            qteText.text =
                "FOTOGRAFAR\n" +
                problem.DisplayName +
                "\n[E]";
        }

        if (hudPhotoHint != null)
        {
            hudPhotoHint.gameObject.SetActive(true);
            hudPhotoHint.text =
                "[E] FOTOGRAFAR\n" +
                problem.DisplayName;
        }

        UpdateQTEWorldPosition();

        Debug.Log(
            "[ROport Photo] QTE iniciado: " +
            problem.DisplayName);
    }

    private void UpdatePhotoBar()
    {
        if (currentProblem == null)
            return;

        float remainingDistance =
            routeFollower.GetRemainingDistanceInsideProblem(
                currentProblem);

        // O calculo da rota nao pode cancelar a foto.
        float progress = remainingDistance > 0.001f
            ? Mathf.Clamp01(
                remainingDistance / initialDistance)
            : 1f;

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
            return;

        Transform canvasTransform =
            worldQTECanvas.transform;

        canvasTransform.position =
            currentProblem.transform.position +
            Vector3.up * worldHeight +
            gameplayCamera.transform.up * screenUpOffset;

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

            Debug.Log(
                "[ROport Photo] Foto registrada: " +
                currentProblem.DisplayName);

            // O CityProgressManager dispara PhotoRecorded
            // para a animacao ROPhoto.
            HideQTE();
        }
        else
        {
            Debug.LogWarning(
                "[ROport Photo] RegisterPhoto recusou " +
                "a fotografia de " +
                currentProblem.DisplayName);
        }
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

        if (hudPhotoHint != null)
        {
            hudPhotoHint.text = "";
            hudPhotoHint.gameObject.SetActive(false);
        }
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