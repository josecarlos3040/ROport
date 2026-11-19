using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(100)]
public class TrickController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform character;
    [SerializeField] private Transform playerVisual;
    [SerializeField] private RouteFollower routeFollower;
    [SerializeField] private SpriteBillboard spriteBillboard;
    [SerializeField] private ROportRunManager runManager;
    [SerializeField] private TrickScoreManager scoreManager;
    [SerializeField] private RobotAnimationController robotAnimation;

    [Header("Trick Zones")]
    [SerializeField] private TrickZone[] zones;

    [Header("Ramp Speed")]
    [SerializeField, Range(0.1f, 1f)]
    private float rampSlowMultiplier = 0.5f;

    [SerializeField, Range(1f, 3f)]
    private float rampBoostMultiplier = 1.6f;

    [Header("Speed Duration After Ramp")]
    [SerializeField, Min(0f)]
    private float slowAfterRampDuration = 3f;

    [SerializeField, Min(0f)]
    private float boostAfterRampDuration = 2f;

    private float remainingSpeedDuration;
    private float extendedSpeedMultiplier = 1f;

    [Header("Player")]
    [SerializeField, Min(0f)]
    private float playerPivotHeight = 1f;

    [Header("Jump")]
    [SerializeField, Min(0f)]
    private float jumpHeight = 0.8f;

    [Header("Grind Visual")]
    [SerializeField]
    private Vector3 grindVisualOffset =
        new Vector3(0f, 0.25f, 0f);

    [SerializeField]
    private float grindTiltAngle = 8f;

    [Header("Fall Animation")]
    [SerializeField, Min(0.1f)]
    private float fallAnimationDuration = 0.65f;

    [SerializeField]
    private float fallTiltAngle = 60f;

    [Header("UI")]
    [SerializeField] private TMP_Text hintText;
    [SerializeField] private TMP_Text feedbackText;

    [SerializeField, Min(0.1f)]
    private float feedbackDuration = 2f;

    private readonly HashSet<TrickZone> attemptedZones =
        new HashSet<TrickZone>();

    private readonly HashSet<TrickZone> traversedZones =
        new HashSet<TrickZone>();

    private readonly Dictionary<TrickZone, int> preparedPoints =
        new Dictionary<TrickZone, int>();

    private TrickZone activeZone;
    private Vector3 activeTravelDirection;
    private bool activeSuccessful;

    private Vector3 visualStartPosition;
    private Quaternion visualStartRotation;
    private Vector3 visualStartScale;

    private Coroutine fallCoroutine;
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

        if (robotAnimation == null)
            robotAnimation = GetComponent<RobotAnimationController>();
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

        if (routeFollower != null)
            routeFollower.TrickObstacleHit += OnTrickObstacleHit;
    }

    private void OnDisable()
    {
        if (runManager != null)
            runManager.PhaseChanged -= OnPhaseChanged;

        if (routeFollower != null)
        {
            routeFollower.TrickObstacleHit -= OnTrickObstacleHit;
            routeFollower.SetRampSpeedMultiplier(1f);
        }

        StopFallAnimation();
        EndTraversal();
    }

    private void Update()
    {
        UpdateFeedback();

        if (!CanUpdateTricks())
        {
            SetHint("");

            if (routeFollower != null)
                routeFollower.SetRampSpeedMultiplier(1f);

            EndTraversal();
            return;
        }

        Vector3 feet = GetFeetPosition();
        Vector3 direction = routeFollower.CurrentMoveDirection;

        // Primeiro controla a manobra sobre a rampa
        // ou obstaculo.
        TrickZone traversalZone =
            FindTraversalZone(feet, direction);

        UpdateTraversal(traversalZone, direction);

        // Depois verifica se existe uma oportunidade
        // de preparar uma nova manobra.
        if (traversalZone != null || fallCoroutine != null)
        {
            SetHint("");
            return;
        }

        TrickZone approachZone =
            FindApproachZone(feet, direction);

        if (approachZone == null)
        {
            SetHint("");
            return;
        }

        SetHint("[F] " + approachZone.TrickName);

        Keyboard keyboard = Keyboard.current;

        if (keyboard != null &&
            keyboard.fKey.wasPressedThisFrame)
        {
            Vector3 timingPosition =
                approachZone.IsInApproach(feet)
                    ? feet
                    : character.position;

            PrepareTrick(approachZone, timingPosition);
        }
    }

    private bool CanUpdateTricks()
    {
        return character != null &&
               routeFollower != null &&
               runManager != null &&
               scoreManager != null &&
               runManager.Phase == RunPhase.Executing &&
               routeFollower.IsFollowing &&
               !routeFollower.IsBlocked;
    }

    private Vector3 GetFeetPosition()
    {
        return character.position -
               Vector3.up * playerPivotHeight;
    }

    private TrickZone FindApproachZone(
        Vector3 feet,
        Vector3 direction)
    {
        if (zones == null || character == null)
            return null;

        TrickZone nearest = null;
        float bestDistance = float.MaxValue;

        foreach (TrickZone zone in zones)
        {
            if (zone == null ||
                attemptedZones.Contains(zone) ||
                traversedZones.Contains(zone))
            {
                continue;
            }

            // Aceita tanto a posicao calculada dos pes
            // quanto o pivot do personagem.
            // Isso evita erros de altura nas zonas.
            bool feetInside = zone.IsInApproach(feet);
            bool pivotInside =
                zone.IsInApproach(character.position);

            if (!feetInside && !pivotInside)
                continue;

            // Nao permite preparar depois que entrou
            // na zona de execucao.
            if (zone.IsInTraversal(feet))
                continue;

            // A rota precisa passar pela zona da manobra.
            // Testamos as duas referencias de altura
            // para evitar falsos negativos.
            bool routeWillTraverse =
                routeFollower.WillTraverseTrickZone(
                    zone, playerPivotHeight, 80f) ||
                routeFollower.WillTraverseTrickZone(
                    zone, 0f, 80f);

            if (!routeWillTraverse)
                continue;

            float distance =
                (zone.transform.position - feet).sqrMagnitude;

            if (distance < bestDistance)
            {
                bestDistance = distance;
                nearest = zone;
            }
        }

        return nearest;
    }

    private void PrepareTrick(TrickZone zone, Vector3 feet)
    {
        attemptedZones.Add(zone);

        bool success = zone.EvaluateApproachTiming(
            feet,
            out int points,
            out string rating);

        if (success)
        {
            // Guarda o acerto ate chegar na manobra.
            preparedPoints[zone] = points;

            ShowFeedback(
                rating + "\nMANOBRA PREPARADA!");
        }
        else
        {
            // Sem penalidade extra neste momento.
            // Na rampa ele ira devagar.
            // No entulho, a colisao causara o tropeço.
            ShowFeedback(
                "ERROU O TIMING!\nSEM BONUS DE VELOCIDADE");
        }

        SetHint("");
    }

    private TrickZone FindTraversalZone(
        Vector3 feet,
        Vector3 direction)
    {
        // Preserva a zona atual enquanto estiver nela.
        if (activeZone != null &&
            activeZone.IsInTraversal(feet))
        {
            return activeZone;
        }

        if (zones == null)
            return null;

        foreach (TrickZone zone in zones)
        {
            if (zone == null ||
                traversedZones.Contains(zone) ||
                !zone.IsInTraversal(feet) ||
                (zone.OnlyDescending && direction.y >= -0.02f))
            {
                continue;
            }

            return zone;
        }

        return null;
    }


    private void UpdateTraversal(
        TrickZone zone,
        Vector3 direction)
    {
        // Detecta quando o personagem sai de uma zona.
        if (zone != activeZone)
        {
            if (activeZone != null &&
                activeZone.AffectsTravelSpeed)
            {
                // Guarda o resultado da manobra anterior.
                extendedSpeedMultiplier = activeSuccessful
                    ? rampBoostMultiplier
                    : rampSlowMultiplier;

                remainingSpeedDuration = activeSuccessful
                    ? boostAfterRampDuration
                    : slowAfterRampDuration;
            }

            EndTraversal();

            if (zone != null)
            {
                // Uma nova zona assume o controle da velocidade.
                remainingSpeedDuration = 0f;
                extendedSpeedMultiplier = 1f;

                BeginTraversal(zone, direction);
            }
        }

        float multiplier = 1f;

        if (zone != null)
        {
            // Enquanto esta dentro da rampa.
            if (zone.AffectsTravelSpeed)
            {
                multiplier = activeSuccessful
                    ? rampBoostMultiplier
                    : rampSlowMultiplier;
            }
        }
        else if (remainingSpeedDuration > 0f)
        {
            // Continua com a velocidade modificada
            // depois de sair da rampa.
            multiplier = extendedSpeedMultiplier;

            remainingSpeedDuration -= Time.deltaTime;

            if (remainingSpeedDuration <= 0f)
            {
                remainingSpeedDuration = 0f;
                multiplier = 1f;
            }
        }

        routeFollower.SetRampSpeedMultiplier(multiplier);

        if (activeZone != null &&
            activeSuccessful &&
            fallCoroutine == null)
        {
            UpdateTrickVisual();
        }
    }

    private void BeginTraversal(
        TrickZone zone,
        Vector3 direction)
    {
        activeZone = zone;
        activeTravelDirection = direction;

        traversedZones.Add(zone);

        activeSuccessful =
            preparedPoints.TryGetValue(zone, out int points);

        if (activeSuccessful)
        {
            // Manobra preparada corretamente.
            if (scoreManager.RegisterTrick(points))
            {
                ShowFeedback(
                    zone.TrickName + "!\n+" +
                    points + " PONTOS");
            }
            else
            {
                activeSuccessful = false;
            }
        }

        if (activeSuccessful && robotAnimation != null)
        {
            robotAnimation.BeginTrick(zone.Animation);
        }

        // Entrou na escada sem acertar a manobra.
        if (!activeSuccessful && zone.AffectsTravelSpeed)
        {
            ShowFeedback(
                "ERROU A MANOBRA!\nVELOCIDADE REDUZIDA");

            // Reutiliza a animacao de tropeço.
            StartFallAnimation();
        }
    }

    private void UpdateTrickVisual()
    {
        if (playerVisual == null || activeZone == null)
            return;

        float progress = activeZone.GetTraversalProgress(
            GetFeetPosition(),
            activeTravelDirection);

        TrickAnimation animation = activeZone.Animation;

        bool jump =
            animation == TrickAnimation.Jump ||
            animation == TrickAnimation.JumpAndSpin;

        bool spin =
            animation == TrickAnimation.Spin ||
            animation == TrickAnimation.JumpAndSpin;

        float jumpOffset = jump
            ? Mathf.Sin(progress * Mathf.PI) * jumpHeight
            : 0f;

        float rotationY = spin
            ? 360f * progress
            : 0f;

        Vector3 visualOffset = Vector3.up * jumpOffset;
        float roll = 0f;

        if (animation == TrickAnimation.Grind)
        {
            // Entrada e saida suaves do corrimao.
            float enter = Mathf.SmoothStep(
                0f, 1f,
                Mathf.Clamp01(progress * 5f));

            float exit = Mathf.SmoothStep(
                0f, 1f,
                Mathf.Clamp01((1f - progress) * 5f));

            float blend = enter * exit;

            visualOffset += grindVisualOffset * blend;
            roll = grindTiltAngle * blend;
        }

        playerVisual.localPosition =
            visualStartPosition + visualOffset;

        playerVisual.localRotation =
            visualStartRotation *
            Quaternion.Euler(0f, rotationY, 0f);

        // O Collider sobe somente nas manobras
        // que realmente possuem salto.
        routeFollower.SetJumpOffset(jumpOffset);

        if (spriteBillboard != null)
            spriteBillboard.SetExtraRoll(roll);
    }

    private void EndTraversal()
    {
        if (activeZone == null)
            return;

        if (robotAnimation != null)
            robotAnimation.EndTrick();

        activeZone = null;
        activeSuccessful = false;
        activeTravelDirection = Vector3.zero;

        if (fallCoroutine == null)
            RestoreVisual();
    }

    // Evento recebido ao atingir um problema Trick.
    private void OnTrickObstacleHit(CityProblem problem)
    {
        if (runManager == null ||
            runManager.Phase != RunPhase.Executing)
        {
            return;
        }

        TrickZone affectedZone = activeZone;

        if (affectedZone == null && zones != null)
        {
            Vector3 feet = GetFeetPosition();

            foreach (TrickZone zone in zones)
            {
                if (zone != null &&
                    (zone.IsInTraversal(feet) ||
                     zone.IsInApproach(feet)))
                {
                    affectedZone = zone;
                    break;
                }
            }
        }

        if (affectedZone != null)
        {
            attemptedZones.Add(affectedZone);
            preparedPoints.Remove(affectedZone);
        }

        // Se colidiu, a execucao da manobra falhou.
        activeSuccessful = false;

        ShowFeedback(
            "TROPECOU!\nVELOCIDADE REDUZIDA");

        StartFallAnimation();
        SetHint("");
    }

    private void StartFallAnimation()
    {
        StopFallAnimation();

        // Nova animacao de impacto do artista.
        if (robotAnimation != null)
            robotAnimation.PlayImpact();

        // Mantem o movimento de tropeço existente.
        if (playerVisual != null)
            fallCoroutine = StartCoroutine(AnimateFall());
    }

    private IEnumerator AnimateFall()
    {
        float elapsed = 0f;

        while (elapsed < fallAnimationDuration)
        {
            elapsed += Time.deltaTime;

            float progress = Mathf.Clamp01(
                elapsed / fallAnimationDuration);

            float impact = Mathf.Sin(progress * Mathf.PI);

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

        fallCoroutine = null;
        RestoreVisual();
    }

    private void StopFallAnimation()
    {
        if (fallCoroutine != null)
        {
            StopCoroutine(fallCoroutine);
            fallCoroutine = null;
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

        if (routeFollower != null)
            routeFollower.SetJumpOffset(0f);
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
            remainingSpeedDuration = 0f;
            extendedSpeedMultiplier = 1f;

            attemptedZones.Clear();
            traversedZones.Clear();
            preparedPoints.Clear();

            StopFallAnimation();
            EndTraversal();

            SetHint("");

            feedbackTimer = 0f;

            if (feedbackText != null)
                feedbackText.text = "";
        }
        else if (phase == RunPhase.Dead ||
                 phase == RunPhase.Finished)
        {
            StopFallAnimation();
            EndTraversal();
            SetHint("");
        }

        if (phase != RunPhase.Executing &&
            routeFollower != null)
        {
            routeFollower.SetRampSpeedMultiplier(1f);
        }
    }
}