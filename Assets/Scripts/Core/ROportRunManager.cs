using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

public enum RunPhase
{
    Planning,
    Executing,
    Finished,
    Dead
}

public class ROportRunManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RouteDrawer routeDrawer;
    [SerializeField] private RouteFollower routeFollower;
    [SerializeField] private Transform player;
    [SerializeField] private CityCheckpointManager checkpointManager;
    [SerializeField] private TMP_Text statusText;

    [Header("Testing")]
    [SerializeField]
    private bool requireCheckpointToStart = true;

    private readonly PlanningState planning =
        new PlanningState();

    private readonly ExecutingState executing =
        new ExecutingState();

    private readonly FinishedState finished =
        new FinishedState();

    private readonly DeadState dead =
        new DeadState();

    private IRunState currentState;

    public RunPhase Phase =>
        currentState != null
            ? currentState.Phase
            : RunPhase.Planning;

    public event Action<RunPhase> PhaseChanged;

    // Entrega concluida com sucesso.
    public event Action CheckpointReached;

    // Morte ou abandono da tentativa atual.
    public event Action StageFailed;

    private void Awake()
    {
        if (routeFollower != null)
        {
            routeFollower.Completed += OnRouteCompleted;
            routeFollower.Died += OnPlayerDied;
        }

        if (routeDrawer != null)
        {
            routeDrawer.RouteChanged += OnRouteChanged;
            routeDrawer.Feedback += OnRouteFeedback;
        }
    }

    private void Start()
    {
        if (routeDrawer == null ||
            routeFollower == null ||
            player == null ||
            checkpointManager == null ||
            checkpointManager.CurrentCheckpoint == null)
        {
            Debug.LogError(
                "[ROport] Configure Drawer, Follower, " +
                "Player e Checkpoint Manager.", this);

            enabled = false;
            return;
        }

        BeginPlanningFromCurrentPosition();
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;

        if (keyboard == null)
            return;

        // R reinicia apenas a tentativa atual.
        if (Phase != RunPhase.Finished &&
            keyboard.rKey.wasPressedThisFrame)
        {
            ResetRun();
            return;
        }

        currentState?.HandleInput(this, keyboard);
    }

    private void OnDestroy()
    {
        if (routeFollower != null)
        {
            routeFollower.Completed -= OnRouteCompleted;
            routeFollower.Died -= OnPlayerDied;
        }

        if (routeDrawer != null)
        {
            routeDrawer.RouteChanged -= OnRouteChanged;
            routeDrawer.Feedback -= OnRouteFeedback;
        }
    }

    // ==========================================
    // RESET
    // ==========================================

    public void ResetRun()
    {
        if (Phase == RunPhase.Finished)
            return;

        // Descarta fotografias pendentes somente
        // ao abandonar uma tentativa em execucao.
        if (Phase == RunPhase.Executing)
            StageFailed?.Invoke();

        BeginPlanningFromCurrentPosition();
    }

    private void BeginPlanningFromCurrentPosition()
    {
        // Nao teleporta o Player ao spawn.
        routeFollower.StopAtCurrentPosition();

        routeDrawer.ClearRoute();

        ChangeState(planning);
    }

    // ==========================================
    // START ROUTE
    // ==========================================

    public void TryStartRoute()
    {
        if (Phase != RunPhase.Planning)
            return;

        if (!routeDrawer.HasRoute)
        {
            ShowStatus(
                "Desenhe uma rota primeiro.");
            return;
        }

        if (requireCheckpointToStart)
        {
            // Tenta completar automaticamente
            // a rota ate o checkpoint atual.
            bool connected =
                routeDrawer
                    .TryCompleteToCurrentCheckpoint();

            if (!connected)
            {
                // RouteDrawer informa o motivo
                // usando o evento Feedback.
                return;
            }

            // Verificacao final usando o
            // CityCheckpointManager existente.
            var points = routeDrawer.Points;

            Vector3 routeEnd =
                points[points.Count - 1];

            if (!checkpointManager
                .RouteEndsAtCheckpoint(routeEnd))
            {
                ShowStatus(
                    "O final da rota ainda esta " +
                    "fora da area de entrega.");

                return;
            }

            Debug.Log(
                "[ROport] Rota conectada ao " +
                "checkpoint. Iniciando execucao!");
        }

        ChangeState(executing);
    }

    // ==========================================
    // ROUTE EVENTS
    // ==========================================

    private void OnRouteCompleted()
    {
        if (Phase != RunPhase.Executing)
            return;

        // Confirma que o Player chegou
        // fisicamente ao checkpoint.
        if (checkpointManager
            .IsAtCurrentCheckpoint(player.position))
        {
            // Confirma fotografias, NPCs e VFX.
            CheckpointReached?.Invoke();

            // Passa para o proximo checkpoint.
            checkpointManager.AdvanceCheckpoint();

            if (checkpointManager.IsComplete)
            {
                // Todos os cinco concluidos.
                ChangeState(finished);
            }
            else
            {
                // Continua a partida do lugar atual.
                BeginPlanningFromCurrentPosition();
            }

            return;
        }

        // Terminou a rota sem chegar ao checkpoint.
        // Permite desenhar uma nova.
        BeginPlanningFromCurrentPosition();
    }

    private void OnPlayerDied(CityProblem problem)
    {
        if (Phase != RunPhase.Executing)
            return;

        StageFailed?.Invoke();

        ChangeState(dead);
    }

    // ==========================================
    // FEEDBACK
    // ==========================================

    private void OnRouteChanged(int pointCount)
    {
        if (Phase != RunPhase.Planning)
            return;

        if (pointCount >= 2)
        {
            ShowStatus(
                "Aproxime a rota do ponto de entrega.\n" +
                "Aperte ESPACO para começar o trajeto.");
        }
        else
        {
            ShowStatus(
                "Desenhe uma rota ate perto " +
                "do ponto de entrega.");
        }
    }

    private void OnRouteFeedback(string message)
    {
        if (Phase == RunPhase.Planning)
            ShowStatus(message);
    }

    private void ShowStatus(string message)
    {
        if (statusText != null)
            statusText.text = message;
        else
            Debug.Log("[ROport] " + message);
    }

    // ==========================================
    // STATE MANAGEMENT
    // ==========================================

    private void ChangeState(IRunState nextState)
    {
        currentState = nextState;
        currentState.Enter(this);

        PhaseChanged?.Invoke(currentState.Phase);
    }

    private interface IRunState
    {
        RunPhase Phase { get; }

        void Enter(ROportRunManager manager);

        void HandleInput(
            ROportRunManager manager,
            Keyboard keyboard);
    }

    // ==========================================
    // PLANNING
    // ==========================================

    private sealed class PlanningState : IRunState
    {
        public RunPhase Phase => RunPhase.Planning;

        public void Enter(ROportRunManager manager)
        {
            manager.routeDrawer.SetEditable(true);

            manager.ShowStatus(
                "Desenhe ate perto do ponto de entrega.\n" +
                "Aperte ESPACO para começar o trajeto.");
        }

        public void HandleInput(
            ROportRunManager manager,
            Keyboard keyboard)
        {
            if (keyboard.spaceKey.wasPressedThisFrame)
                manager.TryStartRoute();
        }
    }

    // ==========================================
    // EXECUTING
    // ==========================================

    private sealed class ExecutingState : IRunState
    {
        public RunPhase Phase => RunPhase.Executing;

        public void Enter(ROportRunManager manager)
        {
            manager.routeDrawer.SetEditable(false);

            manager.routeFollower.Follow(
                manager.routeDrawer.Points);

            manager.ShowStatus(
                "Executando rota...");
        }

        public void HandleInput(
            ROportRunManager manager,
            Keyboard keyboard)
        {
        }
    }

    // ==========================================
    // DEAD
    // ==========================================

    private sealed class DeadState : IRunState
    {
        public RunPhase Phase => RunPhase.Dead;

        public void Enter(ROportRunManager manager)
        {
            manager.routeDrawer.SetEditable(false);

            manager.ShowStatus(
                "VOCE MORREU!\n" +
                "R: desenhar novamente daqui.");
        }

        public void HandleInput(
            ROportRunManager manager,
            Keyboard keyboard)
        {
        }
    }

    // ==========================================
    // FINISHED
    // ==========================================

    private sealed class FinishedState : IRunState
    {
        public RunPhase Phase => RunPhase.Finished;

        public void Enter(ROportRunManager manager)
        {
            manager.routeDrawer.SetEditable(false);

            manager.ShowStatus(
                "PARABENS!\n" +
                "Voce concluiu todos os destinos!");
        }

        public void HandleInput(
            ROportRunManager manager,
            Keyboard keyboard)
        {
        }
    }
}