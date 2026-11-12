
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

public class TrickController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform character;
    [SerializeField] private Transform playerVisual;
    [SerializeField] private ROportRunManager runManager;
    [SerializeField] private TrickScoreManager scoreManager;

    [Header("Trick Zones")]
    [SerializeField] private TrickZone[] zones;

    [Header("Player Settings")]
    [SerializeField, Min(0f)]
    private float playerPivotHeight = 1f;

    [Header("Animation")]
    [SerializeField, Min(0.1f)]
    private float animationDuration = 0.65f;

    [SerializeField, Min(0f)]
    private float jumpHeight = 0.8f;

    [Header("UI")]
    [SerializeField] private TMP_Text hintText;
    [SerializeField] private TMP_Text feedbackText;

    [SerializeField, Min(0.1f)]
    private float feedbackDuration = 2f;

    private readonly HashSet<TrickZone> usedZones =
        new HashSet<TrickZone>();

    private Vector3 visualStartPosition;
    private Quaternion visualStartRotation;

    private Coroutine activeAnimation;
    private float feedbackTimer;

    private void Awake()
    {
        if (character == null)
            character = transform;

        if (playerVisual != null)
        {
            visualStartPosition = playerVisual.localPosition;
            visualStartRotation = playerVisual.localRotation;
        }
    }

    private void Start()
    {
        SetHint("");

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

        StopAnimation();
    }

    private void Update()
    {
        UpdateFeedback();

        if (character == null ||
            runManager == null ||
            scoreManager == null)
        {
            SetHint("");
            return;
        }

        if (runManager.Phase != RunPhase.Executing)
        {
            SetHint("");
            return;
        }

        // Nao permite iniciar outra manobra
        // enquanto uma animacao esta acontecendo.
        if (activeAnimation != null)
        {
            SetHint("");
            return;
        }

        TrickZone availableZone = FindAvailableZone();

        if (availableZone == null)
        {
            SetHint("");
            return;
        }

        SetHint(
            "[F] " + availableZone.TrickName);

        Keyboard keyboard = Keyboard.current;

        if (keyboard != null &&
            keyboard.fKey.wasPressedThisFrame)
        {
            PerformTrick(availableZone);
        }
    }

    private TrickZone FindAvailableZone()
    {
        if (zones == null)
            return null;

        Vector3 feetPosition =
            character.position -
            Vector3.up * playerPivotHeight;

        TrickZone nearest = null;
        float shortestDistance = float.MaxValue;

        foreach (TrickZone zone in zones)
        {
            if (zone == null ||
                usedZones.Contains(zone) ||
                !zone.ContainsPoint(feetPosition))
            {
                continue;
            }

            float distance =
                (zone.transform.position - feetPosition)
                .sqrMagnitude;

            if (distance < shortestDistance)
            {
                nearest = zone;
                shortestDistance = distance;
            }
        }

        return nearest;
    }

    private void PerformTrick(TrickZone zone)
    {
        Vector3 feetPosition =
            character.position -
            Vector3.up * playerPivotHeight;

        // Calcula os pontos conforme
        // o momento em que apertou F.
        int points = zone.CalculateScore(
            feetPosition,
            out string rating);

        if (!scoreManager.RegisterTrick(points))
            return;

        // Impede repetir a mesma manobra
        // nesta zona durante a run.
        usedZones.Add(zone);

        ShowFeedback(
            rating + "\n+" + points + " PONTOS");

        SetHint("");

        if (playerVisual != null)
        {
            activeAnimation = StartCoroutine(
                AnimateTrick(zone.Animation));
        }
    }

    private IEnumerator AnimateTrick(
        TrickAnimation animationType)
    {
        bool shouldJump =
            animationType == TrickAnimation.Jump ||
            animationType == TrickAnimation.JumpAndSpin;

        bool shouldSpin =
            animationType == TrickAnimation.Spin ||
            animationType == TrickAnimation.JumpAndSpin;

        float elapsed = 0f;

        while (elapsed < animationDuration)
        {
            elapsed += Time.deltaTime;

            float progress = Mathf.Clamp01(
                elapsed / animationDuration);

            // Salto visual: sobe e desce suavemente.
            float verticalOffset = shouldJump
                ? Mathf.Sin(progress * Mathf.PI) * jumpHeight
                : 0f;

            // Giro visual de 360 graus.
            float rotationY = shouldSpin
                ? 360f * progress
                : 0f;

            playerVisual.localPosition =
                visualStartPosition +
                Vector3.up * verticalOffset;

            playerVisual.localRotation =
                visualStartRotation *
                Quaternion.Euler(0f, rotationY, 0f);

            yield return null;
        }

        RestoreVisual();

        activeAnimation = null;
    }

    private void RestoreVisual()
    {
        if (playerVisual == null)
            return;

        playerVisual.localPosition = visualStartPosition;
        playerVisual.localRotation = visualStartRotation;
    }

    private void StopAnimation()
    {
        if (activeAnimation != null)
        {
            StopCoroutine(activeAnimation);
            activeAnimation = null;
        }

        RestoreVisual();
    }

    private void SetHint(string message)
    {
        if (hintText != null)
            hintText.text = message;
    }

    private void ShowFeedback(string message)
    {
        feedbackTimer = feedbackDuration;

        if (feedbackText != null)
            feedbackText.text = message;
    }

    private void UpdateFeedback()
    {
        if (feedbackTimer <= 0f)
            return;

        feedbackTimer -= Time.deltaTime;

        if (feedbackTimer <= 0f && feedbackText != null)
            feedbackText.text = "";
    }

    private void OnPhaseChanged(RunPhase phase)
    {
        if (phase == RunPhase.Planning)
        {
            usedZones.Clear();

            StopAnimation();

            SetHint("");

            feedbackTimer = 0f;

            if (feedbackText != null)
                feedbackText.text = "";
        }

        if (phase == RunPhase.Finished)
            SetHint("");
    }
}