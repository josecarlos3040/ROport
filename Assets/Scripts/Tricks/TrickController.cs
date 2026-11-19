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


    [SerializeField] private TrickVFXController trickVFX;


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

    private readonly HashSet<TrickZone> consumedCityTricks =
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
    public bool IsPlayingFallAnimation => fallCoroutine != null;
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

        if (trickVFX == null)
            trickVFX = GetComponent<TrickVFXController>();
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

    private bool IsCityTrick(TrickZone zone)
    {
        return zone != null &&
            zone.GetComponentInParent<CityProblem>() != null;
    }

    private bool IsCityTrickConsumed(TrickZone zone)
    {
        return IsCityTrick(zone) &&
            consumedCityTricks.Contains(zone);
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
                !zone.isActiveAndEnabled ||
                attemptedZones.Contains(zone) ||
                traversedZones.Contains(zone) ||
                IsCityTrickConsumed(zone))
            {
                continue;
            }

            bool feetInside = zone.IsInApproach(feet);
            bool pivotInside =
                zone.IsInApproach(character.position);

            if (!feetInside && !pivotInside)
                continue;

            // Nao oferece a manobra se ja estiver
            // atravessando a zona principal.
            if (zone.IsInTraversal(feet) ||
                zone.IsInTraversal(character.position))
            {
                continue;
            }

            // A area de aproximacao agora e suficiente
            // para apresentar a oportunidade.
            // Nao depende mais de WillTraverseTrickZone.
            float distance =
                (zone.transform.position - feet)
                .sqrMagnitude;

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
        if (zone == null)
            return;

        bool success = zone.EvaluateApproachTiming(
            feet,
            out int points,
            out string rating);

        Debug.Log(
            "[ROport Trick] F apertado em " +
            zone.TrickName +
            " | Sucesso: " + success +
            " | Pontos: " + points +
            " | Rating: " + rating);

        attemptedZones.Add(zone);

        if (success)
        {
            preparedPoints[zone] = points;

            ShowFeedback(
                rating + "\nMANOBRA PREPARADA!");
        }
        else
        {
            ShowFeedback(
                "ERROU O TIMING!\nSEM BONUS");
        }

        SetHint("");
    }

    private TrickZone FindTraversalZone(
        Vector3 feet,
        Vector3 direction)
    {
        if (activeZone != null &&
            (activeZone.IsInTraversal(feet) ||
             activeZone.IsInTraversal(character.position)))
        {
            return activeZone;
        }

        if (zones == null || character == null)
            return null;

        foreach (TrickZone zone in zones)
        {
            if (zone == null ||
                !zone.isActiveAndEnabled ||
                IsCityTrickConsumed(zone) ||
                traversedZones.Contains(zone))
            {
                continue;
            }

            bool inside =
                zone.IsInTraversal(feet) ||
                zone.IsInTraversal(character.position);

            if (!inside)
                continue;

            if (zone.OnlyDescending &&
                direction.y >= -0.02f)
                continue;

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

        if (IsCityTrick(zone))
        {
            consumedCityTricks.Add(zone);

            Debug.Log(
                "[ROport] Manobra urbana utilizada: " +
                zone.TrickName);
        }

        activeSuccessful =
            preparedPoints.TryGetValue(zone, out int points);

        Debug.Log(
    "[ROport Trick] Entrou na travessia: " +
    zone.TrickName +
    " | Preparado: " + activeSuccessful);

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

        // Acertou e realmente comecou a manobra.
        if (activeSuccessful && trickVFX != null)
        {
            trickVFX.PlaySuccess();
        }

        if (!activeSuccessful && zone.AffectsTravelSpeed)
        {
            ShowFeedback(
                "ERROU A MANOBRA!\nVELOCIDADE REDUZIDA");

            // Problemas urbanos Trick nao executam
            // animacao de queda.
            bool isCityObstacle =
                zone.GetComponentInParent<CityProblem>() != null;

            if (!isCityObstacle)
            {
                // Mantem a queda das rampas/escadas.
                StartFallAnimation();
            }
            else
            {
                // Apenas efeito de erro no obstaculo.
                if (trickVFX != null)
                    trickVFX.PlayFailure();
            }
        }
    }

    public void ResetCityTricksForNewGame()
    {
        consumedCityTricks.Clear();
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

        if (trickVFX != null)
            trickVFX.StopSuccess();

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
            runManager.Phase != RunPhase.Executing ||
            problem == null)
        {
            return;
        }

        if (problem == null)
            return;

        // Verifica se existe um salto preparado
        // especificamente para o problema atingido.
        if (zones != null)
        {
            foreach (TrickZone zone in zones)
            {
                if (zone == null)
                    continue;

                if (zone.GetComponentInParent<CityProblem>()
                    != problem)
                    continue;

                bool jumpTrick =
                    zone.Animation == TrickAnimation.Jump ||
                    zone.Animation == TrickAnimation.JumpAndSpin;

                bool jumpPrepared =
                    preparedPoints.ContainsKey(zone) ||
                    (activeZone == zone && activeSuccessful);

                if (jumpTrick && jumpPrepared)
                {
                    Debug.Log(
                        "[ROport Trick] Salto preparado: " +
                        "nao cancelar a manobra em " +
                        problem.DisplayName);

                    return;
                }
            }
        }

        // Verifica se o jogador esta executando
        // um salto bem-sucedido sobre este problema.
        if (activeSuccessful && activeZone != null)
        {
            TrickAnimation animation = activeZone.Animation;

            bool isJump =
                animation == TrickAnimation.Jump ||
                animation == TrickAnimation.JumpAndSpin;

            CityProblem zoneProblem =
                activeZone.GetComponentInParent<CityProblem>();

            // Nao cancela um salto correto sobre
            // o problema correspondente.
            if (isJump && zoneProblem == problem)
            {
                Debug.Log(
                    "[ROport] Salto correto sobre " +
                    problem.DisplayName);

                return;
            }
        }

        if (problem != null && zones != null)
        {
            foreach (TrickZone zone in zones)
            {
                if (zone == null)
                    continue;

                if (zone.GetComponentInParent<CityProblem>()
                    == problem)
                {
                    consumedCityTricks.Add(zone);
                }
            }
        }

        TrickZone affectedZone = activeZone;

        if (affectedZone == null && zones != null)
        {
            Vector3 feet = GetFeetPosition();

            foreach (TrickZone zone in zones)
            {
                if (zone == null)
                    continue;

                if (zone.GetComponentInParent<CityProblem>()
                    != problem)
                    continue;

                if (zone.IsInTraversal(feet) ||
                    zone.IsInApproach(feet))
                {
                    affectedZone = zone;
                    break;
                }
            }
        }

        // Invalida a manobra que falhou.
        if (affectedZone != null)
        {
            attemptedZones.Add(affectedZone);
            preparedPoints.Remove(affectedZone);
        }

        activeSuccessful = false;

        if (robotAnimation != null)
            robotAnimation.EndTrick();

        if (trickVFX != null)
        {
            trickVFX.StopSuccess();
            trickVFX.PlayFailure();
        }

        RestoreVisual();

        // A penalidade de velocidade fica
        // sob responsabilidade do RouteFollower.
        ShowFeedback(
            "TROPECOU!\nVELOCIDADE REDUZIDA");

        SetHint("");

        // Nao chama StartFallAnimation().
        // Obstaculos Trick nao derrubam o robo.
    }

    private void StartFallAnimation()
    {
        StopFallAnimation();

        if (trickVFX != null)
            trickVFX.PlayFailure();

        // Reproduz a queda temporaria na rampa.
        if (robotAnimation != null)
            robotAnimation.PlayRampFall();

        // Mantem o efeito fisico/visual de tropeco
        // que ja existia no jogo.
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