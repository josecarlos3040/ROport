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

    [Header("City Problem Failure")]
    [SerializeField, Range(0.1f, 1f)]
    private float cityProblemMissMultiplier = 0.4f;

    [SerializeField, Min(0.1f)]
    private float cityProblemMissDuration = 3f;

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

    [Header("Repeat Trick")]
    [Tooltip("Distancia extra ao redor dos colliders. Impede repetir o F entre dois Approaches.")]
    [SerializeField, Min(0f)]
    private float rearmPadding = 2.5f;

    [Header("UI")]
    [SerializeField] private TMP_Text hintText;
    [SerializeField] private TMP_Text feedbackText;

    [SerializeField, Min(0.1f)]
    private float feedbackDuration = 2f;

    // Zonas utilizadas na rota atual.
    private readonly HashSet<TrickZone> attemptedZones =
        new HashSet<TrickZone>();

    private readonly HashSet<TrickZone> traversedZones =
        new HashSet<TrickZone>();

    // Evita aplicar a mesma falha varias vezes na mesma passagem.
    private readonly HashSet<CityProblem> failedCityProblems =
        new HashSet<CityProblem>();

    // Manobras preparadas com F.
    private readonly Dictionary<TrickZone, int> preparedPoints =
        new Dictionary<TrickZone, int>();

    // Lista reutilizada para rearmar zonas apos o Player sair totalmente.
    private readonly List<TrickZone> zonesToRearm = new List<TrickZone>();

    private TrickZone activeZone;
    private Vector3 activeTravelDirection;
    private bool activeSuccessful;

    private float remainingSpeedDuration;
    private float extendedSpeedMultiplier = 1f;

    private Vector3 visualStartPosition;
    private Quaternion visualStartRotation;
    private Vector3 visualStartScale;

    private Coroutine fallCoroutine;
    private float feedbackTimer;

    public bool IsPlayingFallAnimation =>
        fallCoroutine != null;

    private void Awake()
    {
        if (character == null)
            character = transform;

        if (routeFollower == null)
            routeFollower = GetComponent<RouteFollower>();

        if (robotAnimation == null)
            robotAnimation = GetComponent<RobotAnimationController>();

        if (trickVFX == null)
            trickVFX = GetComponent<TrickVFXController>();

        if (spriteBillboard == null &&
            playerVisual != null)
        {
            spriteBillboard =
                playerVisual.GetComponentInChildren<SpriteBillboard>(
                    true);
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

        TrickZone traversalZone =
            FindTraversalZone(feet, direction);

        UpdateTraversal(traversalZone, direction);

        // Libera manobras atravessadas quando o jogador sai da area.
        RearmExitedZones(feet);

        // Nao oferece outra manobra enquanto
        // atravessa uma zona ou esta caindo.
        if (traversalZone != null ||
            fallCoroutine != null)
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

    private CityProblem GetCityProblem(TrickZone zone)
    {
        return zone != null
            ? zone.GetComponentInParent<CityProblem>()
            : null;
    }

    // Regiao unica que envolve a travessia e TODOS os Approaches.
    // Usar a uniao dos colliders impede rearmar no espaco entre
    // ApproachZone_1 e ApproachZone_2.
    private bool IsInsideTrickRegion(TrickZone zone, Vector3 feet)
    {
        if (zone == null || !zone.isActiveAndEnabled)
            return false;

        BoxCollider[] colliders =
            zone.GetComponentsInChildren<BoxCollider>(true);

        Bounds combined = new Bounds();
        bool found = false;

        foreach (BoxCollider box in colliders)
        {
            if (box == null || !box.enabled ||
                !box.gameObject.activeInHierarchy)
                continue;

            if (!found)
            {
                combined = box.bounds;
                found = true;
            }
            else
            {
                combined.Encapsulate(box.bounds);
            }
        }

        if (!found)
            return zone.IsInTraversal(feet) ||
                   zone.IsInApproach(feet);

        combined.Expand(rearmPadding * 2f);

        return combined.Contains(feet) ||
               combined.Contains(character.position);
    }

    private bool IsInsideProblemRegion(CityProblem problem, Vector3 feet)
    {
        if (problem == null || zones == null)
            return false;

        foreach (TrickZone zone in zones)
        {
            if (zone != null && GetCityProblem(zone) == problem &&
                IsInsideTrickRegion(zone, feet))
                return true;
        }

        return false;
    }

    // Repetir so depois de sair TOTALMENTE da regiao da manobra.
    // Inclui as duas aproximacoes, a travessia e o espaco entre elas.
    private void RearmExitedZones(Vector3 feet)
    {
        zonesToRearm.Clear();

        foreach (TrickZone zone in attemptedZones)
        {
            if (zone != activeZone &&
                !IsInsideTrickRegion(zone, feet))
                zonesToRearm.Add(zone);
        }

        foreach (TrickZone zone in traversedZones)
        {
            if (zone != activeZone &&
                !IsInsideTrickRegion(zone, feet) &&
                !zonesToRearm.Contains(zone))
                zonesToRearm.Add(zone);
        }

        foreach (TrickZone zone in zonesToRearm)
        {
            attemptedZones.Remove(zone);
            traversedZones.Remove(zone);
            preparedPoints.Remove(zone);

            CityProblem problem = GetCityProblem(zone);
            if (problem != null &&
                !IsInsideProblemRegion(problem, feet))
            {
                failedCityProblems.Remove(problem);
            }
        }

        zonesToRearm.Clear();
    }

    // ==================================================
    // APPROACH
    // ==================================================

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
                traversedZones.Contains(zone))
            {
                continue;
            }

            if (!zone.AllowsDirection(direction))
                continue;

            bool inside =
                zone.IsInApproach(feet) ||
                zone.IsInApproach(character.position);

            if (!inside)
                continue;

            if (zone.IsInTraversal(feet) ||
                zone.IsInTraversal(character.position))
            {
                continue;
            }

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

    private void PrepareTrick(
        TrickZone zone,
        Vector3 position)
    {
        if (zone == null)
            return;

        attemptedZones.Add(zone);

        bool success = zone.EvaluateApproachTiming(
            position,
            out int points,
            out string rating);

        Debug.Log(
            "[ROport Trick] " + zone.TrickName +
            " | Sucesso: " + success +
            " | Pontos: " + points);

        if (success)
        {
            preparedPoints[zone] = points;

            ShowFeedback(
                rating + "\nMANOBRA PREPARADA!");
        }
        else
        {
            preparedPoints.Remove(zone);

            // A penalidade acontece quando entrar
            // no problema, nao ao apertar F.
            ShowFeedback(
                "ERROU O TIMING!\nSEM BONUS");
        }

        SetHint("");
    }

    // ==================================================
    // TRAVERSAL
    // ==================================================

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

        if (zones == null)
            return null;

        foreach (TrickZone zone in zones)
        {
            if (zone == null ||
                !zone.isActiveAndEnabled ||
                traversedZones.Contains(zone))
            {
                continue;
            }

            bool inside =
                zone.IsInTraversal(feet) ||
                zone.IsInTraversal(character.position);

            if (!inside)
                continue;

            if (!zone.AllowsDirection(direction))
                continue;

            return zone;
        }

        return null;
    }

    private void UpdateTraversal(
        TrickZone zone,
        Vector3 direction)
    {
        if (zone != activeZone)
        {
            if (activeZone != null &&
                GetCityProblem(activeZone) == null &&
                activeZone.AffectsTravelSpeed)
            {
                // Mantem o boost/slow das rampas
                // por alguns segundos apos sair.
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
                remainingSpeedDuration = 0f;
                extendedSpeedMultiplier = 1f;

                BeginTraversal(zone, direction);
            }
        }

        float multiplier = 1f;

        // Rampas usam o multiplicador proprio.
        // Problemas usam ApplySlowdown.
        if (zone != null)
        {
            if (GetCityProblem(zone) == null &&
                zone.AffectsTravelSpeed)
            {
                multiplier = activeSuccessful
                    ? rampBoostMultiplier
                    : rampSlowMultiplier;
            }
        }
        else if (remainingSpeedDuration > 0f)
        {
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

        CityProblem problem = GetCityProblem(zone);

        activeSuccessful =
            preparedPoints.TryGetValue(zone, out int points);

        // A preparacao ja foi utilizada.
        preparedPoints.Remove(zone);

        Debug.Log(
            "[ROport Trick] Travessia: " +
            zone.TrickName +
            " | Preparado: " + activeSuccessful);

        if (activeSuccessful)
        {
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

        if (activeSuccessful)
        {
            if (robotAnimation != null)
                robotAnimation.BeginTrick(zone.Animation);

            if (trickVFX != null)
                trickVFX.PlaySuccess();

            ROportAudioManager.Instance?.PlayTrickSuccess();

            return;
        }

        // Falhou ou nao apertou F.
        if (problem != null)
        {
            ApplyCityProblemFailure(problem);
        }
        else if (zone.AffectsTravelSpeed)
        {
            ROportAudioManager.Instance?.PlayTrickFailure();
            // Mantem o comportamento das escadas.
            ShowFeedback(
                "ERROU A MANOBRA!\nVELOCIDADE REDUZIDA");

            StartFallAnimation();
        }
    }

    // ==================================================
    // PROBLEM FAILURE
    // ==================================================

    private void ApplyCityProblemFailure(CityProblem problem)
    {
        if (problem == null)
            return;

        // Evita aplicar a mesma falha duas vezes.
        if (!failedCityProblems.Add(problem))
            return;

        // AUDIO DE ERRO
        if (ROportAudioManager.Instance != null)
        {
            ROportAudioManager.Instance.PlayTrickFailure();
        }
        else
        {
            Debug.LogWarning(
                "[ROport Audio] AudioManager nao encontrado!");
        }

        // LENTIDAO
        if (routeFollower != null)
        {
            routeFollower.ApplySlowdown(
                cityProblemMissMultiplier,
                cityProblemMissDuration);
        }

        // FEEDBACK
        ShowFeedback(
            "TROPECOU!\nVELOCIDADE REDUZIDA");

        // ANIMACAO ROFALL + VFX
        StartFallAnimation();

        Debug.Log(
            "[ROport Trick] Falha em " +
            problem.DisplayName +
            " | Som de erro solicitado.");
    }

    // Evento recebido ao atingir um problema urbano.
    private void OnTrickObstacleHit(CityProblem problem)
    {
        if (problem == null ||
            runManager == null ||
            runManager.Phase != RunPhase.Executing)
        {
            return;
        }

        // Um salto preparado corretamente nao deve
        // ser cancelado pela colisao do problema.
        if (HasProtectedJump(problem))
            return;

        // Bloqueia todas as zonas do problema apenas nesta passagem.
        // Serao liberadas ao sair da area de travessia/aproximacao.
        if (zones != null)
        {
            foreach (TrickZone zone in zones)
            {
                if (zone == null ||
                    GetCityProblem(zone) != problem)
                    continue;

                attemptedZones.Add(zone);
                traversedZones.Add(zone);
                preparedPoints.Remove(zone);
            }
        }

        // Cancela uma manobra ativa que falhou.
        if (activeZone != null &&
            GetCityProblem(activeZone) == problem)
        {
            activeSuccessful = false;

            if (robotAnimation != null)
                robotAnimation.EndTrick();

            if (trickVFX != null)
                trickVFX.StopSuccess();

            RestoreVisual();
        }

        ApplyCityProblemFailure(problem);
        SetHint("");

    }

    private bool HasProtectedJump(CityProblem problem)
    {
        if (zones == null)
            return false;

        foreach (TrickZone zone in zones)
        {
            if (zone == null ||
                GetCityProblem(zone) != problem)
                continue;

            bool isJump =
                zone.Animation == TrickAnimation.Jump ||
                zone.Animation == TrickAnimation.JumpAndSpin;

            if (!isJump)
                continue;

            bool prepared =
                preparedPoints.ContainsKey(zone) ||
                (activeZone == zone && activeSuccessful);

            if (prepared)
                return true;
        }

        return false;
    }

    // ==================================================
    // TRICK VISUAL
    // ==================================================

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

        // Nao interrompe ROfall se ainda estiver
        // tocando a animacao de queda.
        if (robotAnimation != null &&
            fallCoroutine == null)
        {
            robotAnimation.EndTrick();
        }

        activeZone = null;
        activeSuccessful = false;
        activeTravelDirection = Vector3.zero;

        if (fallCoroutine == null)
            RestoreVisual();
    }

    // ==================================================
    // FALL ANIMATION
    // ==================================================

    private void StartFallAnimation()
    {
        // Nao reinicia a queda enquanto estiver tocando.
        if (fallCoroutine != null)
            return;

        if (trickVFX != null)
        {
            trickVFX.StopSuccess();
            trickVFX.PlayFailure();
        }

        if (robotAnimation != null)
        {
            robotAnimation.EndTrick();
            robotAnimation.PlayRampFall();
        }

        if (playerVisual != null)
        {
            RestoreVisual();

            fallCoroutine = StartCoroutine(AnimateFall());
        }
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

        if (robotAnimation != null)
            robotAnimation.EndTrick();
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

    // ==================================================
    // UI
    // ==================================================

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

        if (feedbackTimer <= 0f)
        {
            feedbackTimer = 0f;

            if (feedbackText != null)
                feedbackText.text = "";
        }
    }

    // ==================================================
    // PHASE AND RESET
    // ==================================================

    private void OnPhaseChanged(RunPhase phase)
    {
        if (phase == RunPhase.Planning)
        {
            remainingSpeedDuration = 0f;
            extendedSpeedMultiplier = 1f;

            // As zonas continuam bloqueadas enquanto o robo estiver
            // dentro da regiao dos dois Approaches e da travessia.
            // Nao limpar attemptedZones/traversedZones automaticamente
            // ao entrar no planejamento.
            preparedPoints.Clear();

            StopFallAnimation();
            EndTraversal();

            if (character != null)
                RearmExitedZones(GetFeetPosition());

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

    // Mantido para compatibilidade com outros scripts que ja possam chama-lo.
    // Agora todas as manobras sao repetiveis; limpa apenas o estado momentaneo.
    public void ResetCityTricksForNewGame()
    {
        attemptedZones.Clear();
        traversedZones.Clear();
        preparedPoints.Clear();
        failedCityProblems.Clear();
        zonesToRearm.Clear();
    }
}