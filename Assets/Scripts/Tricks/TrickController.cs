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
    [SerializeField] private RouteFollower routeFollower;
    [SerializeField] private SpriteBillboard spriteBillboard;
    [SerializeField] private ROportRunManager runManager;
    [SerializeField] private TrickScoreManager scoreManager;

    [Header("Trick Zones")]
    [SerializeField] private TrickZone[] zones;

    [Header("Ramp Speed")]
    [SerializeField, Range(0.1f, 1f)]
    private float rampSlowMultiplier = 0.5f;

    [SerializeField, Range(1f, 3f)]
    private float rampBoostMultiplier = 1.6f;

    [Header("Player")]
    [SerializeField] private float playerPivotHeight = 1f;

    [Header("Success Animation")]
    [SerializeField, Min(0.1f)]
    private float animationDuration = 0.65f;

    [SerializeField, Min(0f)]
    private float jumpHeight = 0.8f;

    [Header("Failure")]
    [SerializeField, Range(0.1f, 1f)]
    private float missSpeedMultiplier = 0.4f;

    [SerializeField, Min(0.1f)]
    private float missSlowdownDuration = 2f;

    [SerializeField, Min(0.1f)]
    private float fallAnimationDuration = 0.65f;

    [SerializeField] private float fallTiltAngle = 60f;

    [Header("UI")]
    [SerializeField] private TMP_Text hintText;
    [SerializeField] private TMP_Text feedbackText;

    [SerializeField, Min(0.1f)]
    private float feedbackDuration = 2f;

    // Zonas em que o jogador ja tentou uma manobra.
    private readonly HashSet<TrickZone> usedZones =
        new HashSet<TrickZone>();

    // Zonas em que a manobra foi bem-sucedida.
    private readonly HashSet<TrickZone> successfulZones =
        new HashSet<TrickZone>();

    private Vector3 visualStartPosition;
    private Vector3 visualStartScale;
    private Quaternion visualStartRotation;

    private Coroutine activeAnimation;
    private float feedbackTimer;

    private void Awake()
    {
        if (character == null)
            character = transform;

        if (routeFollower == null)
            routeFollower = GetComponent<RouteFollower>();

        if (spriteBillboard == null && playerVisual != null)
        {
            spriteBillboard =
                playerVisual.GetComponentInChildren<SpriteBillboard>(true);
        }

        if (playerVisual != null)
        {
            visualStartPosition = playerVisual.localPosition;
            visualStartRotation = playerVisual.localRotation;
            visualStartScale = playerVisual.localScale;
        }
    }

    private void Start()
    {
        SetHint(string.Empty);

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

        if (routeFollower != null)
            routeFollower.SetRampSpeedMultiplier(1f);

        StopAnimation();
    }

    private void Update()
    {
        UpdateFeedback();

        if (character == null ||
            routeFollower == null ||
            runManager == null ||
            scoreManager == null)
        {
            SetHint(string.Empty);
            return;
        }

        // Fora da execucao, a rampa nao altera a velocidade.
        if (runManager.Phase != RunPhase.Executing ||
            !routeFollower.IsFollowing ||
            routeFollower.IsBlocked)
        {
            routeFollower.SetRampSpeedMultiplier(1f);
            SetHint(string.Empty);
            return;
        }

        // Detecta a rampa independentemente de
        // o jogador ja ter tentado uma manobra.
        TrickZone currentZone = FindCurrentZone();

        UpdateRampSpeed(currentZone);

        // Nao permite repetir a manobra na mesma zona.
        if (currentZone == null ||
            usedZones.Contains(currentZone) ||
            activeAnimation != null)
        {
            SetHint(string.Empty);
            return;
        }

        SetHint("[F] " + currentZone.TrickName);

        Keyboard keyboard = Keyboard.current;

        if (keyboard != null &&
            keyboard.fKey.wasPressedThisFrame)
        {
            PerformTrick(currentZone);
        }
    }

    // Encontra a zona de manobra onde
    // o personagem esta neste momento.
    private TrickZone FindCurrentZone()
    {
        if (zones == null)
            return null;

        Vector3 feetPosition = GetFeetPosition();

        Vector3 direction =
            routeFollower.CurrentMoveDirection;

        TrickZone nearest = null;
        float bestDistance = float.MaxValue;

        foreach (TrickZone zone in zones)
        {
            if (zone == null ||
                !zone.ContainsPoint(feetPosition) ||
                !zone.AllowsDirection(direction))
            {
                continue;
            }

            float distance =
                (zone.transform.position - feetPosition)
                .sqrMagnitude;

            if (distance >= bestDistance)
                continue;

            bestDistance = distance;
            nearest = zone;
        }

        return nearest;
    }

    // Aplica a velocidade de acordo com o
    // resultado da manobra nesta rampa.
    private void UpdateRampSpeed(TrickZone zone)
    {
        float multiplier = 1f;

        if (zone != null)
        {
            multiplier = successfulZones.Contains(zone)
                ? rampBoostMultiplier
                : rampSlowMultiplier;
        }

        routeFollower.SetRampSpeedMultiplier(multiplier);
    }

    private Vector3 GetFeetPosition()
    {
        return character.position -
               Vector3.up * playerPivotHeight;
    }

    private void PerformTrick(TrickZone zone)
    {
        bool success = zone.EvaluateTiming(
            GetFeetPosition(),
            out int points,
            out string rating);

        usedZones.Add(zone);

        if (success)
        {
            HandleSuccessfulTrick(zone, points, rating);
        }
        else
        {
            HandleFailedTrick();
        }

        SetHint(string.Empty);
    }

    private void HandleSuccessfulTrick(
        TrickZone zone,
        int points,
        string rating)
    {
        if (!scoreManager.RegisterTrick(points))
            return;

        successfulZones.Add(zone);

        // Libera o bonus imediatamente.
        routeFollower.SetRampSpeedMultiplier(
            rampBoostMultiplier);

        ShowFeedback(
            rating + "\n+" + points + " PONTOS!");

        StartVisualAnimation(
            AnimateTrick(zone.Animation));
    }

    private void HandleFailedTrick()
    {
        // Mantem a rampa lenta e aplica
        // uma penalidade temporaria.
        routeFollower.SetRampSpeedMultiplier(
            rampSlowMultiplier);

        routeFollower.ApplySlowdown(
            missSpeedMultiplier,
            missSlowdownDuration);

        ShowFeedback(
            "ERROU A MANOBRA!\nVELOCIDADE REDUZIDA");

        StartVisualAnimation(AnimateFall());
    }

    private void StartVisualAnimation(IEnumerator animation)
    {
        if (playerVisual == null)
            return;

        StopAnimation();
        activeAnimation = StartCoroutine(animation);
    }

    private IEnumerator AnimateTrick(
        TrickAnimation animationType)
    {
        bool jump =
            animationType == TrickAnimation.Jump ||
            animationType == TrickAnimation.JumpAndSpin;

        bool spin =
            animationType == TrickAnimation.Spin ||
            animationType == TrickAnimation.JumpAndSpin;

        float elapsed = 0f;

        while (elapsed < animationDuration)
        {
            elapsed += Time.deltaTime;

            float progress =
                Mathf.Clamp01(elapsed / animationDuration);

            float jumpOffset = jump
                ? Mathf.Sin(progress * Mathf.PI) * jumpHeight
                : 0f;

            float rotationY = spin
                ? 360f * progress
                : 0f;

            playerVisual.localPosition =
                visualStartPosition +
                Vector3.up * jumpOffset;

            playerVisual.localRotation =
                visualStartRotation *
                Quaternion.Euler(0f, rotationY, 0f);

            yield return null;
        }

        RestoreVisual();
        activeAnimation = null;
    }

    private IEnumerator AnimateFall()
    {
        float elapsed = 0f;

        while (elapsed < fallAnimationDuration)
        {
            elapsed += Time.deltaTime;

            float progress =
                Mathf.Clamp01(elapsed / fallAnimationDuration);

            float impact =
                Mathf.Sin(progress * Mathf.PI);

            playerVisual.localPosition =
                visualStartPosition +
                Vector3.down * (0.25f * impact);

            playerVisual.localScale = new Vector3(
                visualStartScale.x * (1f + 0.1f * impact),
                visualStartScale.y * (1f - 0.3f * impact),
                visualStartScale.z);

            if (spriteBillboard != null)
            {
                spriteBillboard.SetExtraRoll(
                    fallTiltAngle * impact);
            }

            yield return null;
        }

        RestoreVisual();
        activeAnimation = null;
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

    private void RestoreVisual()
    {
        if (playerVisual != null)
        {
            playerVisual.localPosition = visualStartPosition;
            playerVisual.localRotation = visualStartRotation;
            playerVisual.localScale = visualStartScale;
        }

        if (spriteBillboard != null)
            spriteBillboard.SetExtraRoll(0f);
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

        if (feedbackTimer <= 0f &&
            feedbackText != null)
        {
            feedbackText.text = string.Empty;
        }
    }

    private void OnPhaseChanged(RunPhase phase)
    {
        if (routeFollower != null &&
            phase != RunPhase.Executing)
        {
            routeFollower.SetRampSpeedMultiplier(1f);
        }

        if (phase == RunPhase.Planning)
        {
            usedZones.Clear();
            successfulZones.Clear();

            StopAnimation();
            SetHint(string.Empty);

            feedbackTimer = 0f;

            if (feedbackText != null)
                feedbackText.text = string.Empty;
        }
        else if (phase == RunPhase.Finished ||
                 phase == RunPhase.Dead)
        {
            SetHint(string.Empty);
        }
    }
}