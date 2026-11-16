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

    [SerializeField]
    private float fallTiltAngle = 60f;

    [Header("UI")]
    [SerializeField] private TMP_Text hintText;
    [SerializeField] private TMP_Text feedbackText;

    [SerializeField, Min(0.1f)]
    private float feedbackDuration = 2f;

    private readonly HashSet<TrickZone> usedZones =
        new HashSet<TrickZone>();

    private Vector3 visualStartPosition;
    private Quaternion visualStartRotation;
    private Vector3 visualStartScale;

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
            routeFollower == null ||
            runManager == null ||
            scoreManager == null)
        {
            SetHint("");
            return;
        }

        if (runManager.Phase != RunPhase.Executing ||
            routeFollower.IsBlocked)
        {
            SetHint("");
            return;
        }

        if (activeAnimation != null)
        {
            SetHint("");
            return;
        }

        TrickZone zone = FindAvailableZone();

        if (zone == null)
        {
            SetHint("");
            return;
        }

        SetHint("[F] " + zone.TrickName);

        Keyboard keyboard = Keyboard.current;

        if (keyboard != null &&
            keyboard.fKey.wasPressedThisFrame)
        {
            PerformTrick(zone);
        }
    }

    private TrickZone FindAvailableZone()
    {
        if (zones == null)
            return null;

        Vector3 feetPosition =
            character.position -
            Vector3.up * playerPivotHeight;

        Vector3 movementDirection =
            routeFollower.CurrentMoveDirection;

        TrickZone nearest = null;
        float shortestDistance = float.MaxValue;

        foreach (TrickZone zone in zones)
        {
            if (zone == null ||
                usedZones.Contains(zone) ||
                !zone.ContainsPoint(feetPosition) ||
                !zone.AllowsDirection(movementDirection))
            {
                continue;
            }

            float distance =
                (zone.transform.position - feetPosition)
                .sqrMagnitude;

            if (distance < shortestDistance)
            {
                shortestDistance = distance;
                nearest = zone;
            }
        }

        return nearest;
    }

    private void PerformTrick(TrickZone zone)
    {
        Vector3 feetPosition =
            character.position -
            Vector3.up * playerPivotHeight;

        bool success = zone.EvaluateTiming(
            feetPosition,
            out int points,
            out string rating);

        if (success)
        {
            if (!scoreManager.RegisterTrick(points))
                return;

            usedZones.Add(zone);

            ShowFeedback(
                rating + "\n+" + points + " PONTOS");

            Debug.Log(
                "[ROport] Manobra concluida: " +
                zone.TrickName);

            if (playerVisual != null)
            {
                activeAnimation = StartCoroutine(
                    AnimateTrick(zone.Animation));
            }
        }
        else
        {
            // O jogador errou a janela de tempo.
            usedZones.Add(zone);

            routeFollower.ApplySlowdown(
                missSpeedMultiplier,
                missSlowdownDuration);

            ShowFeedback(
                "ERROU A MANOBRA!\nVELOCIDADE REDUZIDA");

            Debug.Log(
                "[ROport] Manobra falhou: " +
                zone.TrickName);

            if (playerVisual != null)
            {
                activeAnimation = StartCoroutine(
                    AnimateFall());
            }
        }

        SetHint("");
    }

    // Salto e giro bem-sucedidos.
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

            float verticalOffset = shouldJump
                ? Mathf.Sin(progress * Mathf.PI) * jumpHeight
                : 0f;

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

    // Animacao provisoria de tropeço / queda.
    private IEnumerator AnimateFall()
    {
        float elapsed = 0f;

        while (elapsed < fallAnimationDuration)
        {
            elapsed += Time.deltaTime;

            float progress = Mathf.Clamp01(
                elapsed / fallAnimationDuration);

            // Sobe e desce a intensidade da queda.
            float impact = Mathf.Sin(progress * Mathf.PI);

            // O corpo abaixa durante o tropeço.
            playerVisual.localPosition =
                visualStartPosition +
                Vector3.down * (0.25f * impact);

            // Pequena compressao visual.
            playerVisual.localScale = new Vector3(
                visualStartScale.x * (1f + 0.1f * impact),
                visualStartScale.y * (1f - 0.3f * impact),
                visualStartScale.z);

            // Inclina o sprite de lado.
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

        if (feedbackTimer <= 0f &&
            feedbackText != null)
        {
            feedbackText.text = "";
        }
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
        else if (phase == RunPhase.Finished)
        {
            SetHint("");
        }
    }
}