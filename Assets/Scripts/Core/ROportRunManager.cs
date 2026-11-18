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

    private readonly PlanningState planning = new PlanningState();
    private readonly ExecutingState executing = new ExecutingState();
    private readonly FinishedState finished = new FinishedState();
    private readonly DeadState dead = new DeadState();

    private IRunState currentState;

    public RunPhase Phase =>
        currentState != null
            ? currentState.Phase
            : RunPhase.Planning;

    public event Action<RunPhase> PhaseChanged;

    // Somente um checkpoint concluido aplica melhorias.
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

    // Agora reiniciar significa redesenhar do lugar atual.
    public void ResetRun()
    {
        if (Phase == RunPhase.Finished)
            return;

        // Abandonar uma tentativa em execucao
        // descarta as fotografias ainda nao confirmadas.
        if (Phase == RunPhase.Executing)
            StageFailed?.Invoke();

        BeginPlanningFromCurrentPosition();
    }

    private void BeginPlanningFromCurrentPosition()
    {
        // Diferente de ResetFollower(), nao volta ao spawn.
        routeFollower.StopAtCurrentPosition();

        routeDrawer.ClearRoute();

        ChangeState(planning);
    }

    public void TryStartRoute()
    {
        if (Phase != RunPhase.Planning)
            return;

        if (!routeDrawer.HasRoute)
        {
            ShowStatus("Desenhe uma rota primeiro.");
            return;
        }

        var points = routeDrawer.Points;

        Vector3 routeEnd = points[points.Count - 1];

        if (!checkpointManager.RouteEndsAtCheckpoint(routeEnd))
        {
            ShowStatus(
                "A rota precisa terminar no destino amarelo!");
            return;
        }

        ChangeState(executing);
    }

    private void OnRouteCompleted()
    {
        if (Phase != RunPhase.Executing)
            return;

        // Chegou fisicamente ao checkpoint.
        if (checkpointManager.IsAtCurrentCheckpoint(player.position))
        {
            CheckpointReached?.Invoke();

            checkpointManager.AdvanceCheckpoint();

            if (checkpointManager.IsComplete)
            {
                ChangeState(finished);
            }
            else
            {
                BeginPlanningFromCurrentPosition();
            }

            return;
        }

        // A rota foi interrompida antes do destino,
        // por exemplo apos fotografar um bloqueio.
        // Mantem o progresso fotografado pendente
        // e permite planejar outro caminho.
        BeginPlanningFromCurrentPosition();
    }

    private void OnPlayerDied(CityProblem problem)
    {
        if (Phase != RunPhase.Executing)
            return;

        StageFailed?.Invoke();

        ChangeState(dead);
    }

    private void OnRouteChanged(int pointCount)
    {
        if (Phase != RunPhase.Planning)
            return;

        if (pointCount >= 2)
        {
            ShowStatus(
                "Leve a rota ate o destino. ESPACO: executar");
        }
        else
        {
            ShowStatus(
                "Desenhe do personagem ate o destino.");
        }
    }

    private void OnRouteFeedback(string message)
    {
        if (Phase == RunPhase.Planning)
            ShowStatus(message);
    }

    private void ChangeState(IRunState nextState)
    {
        currentState = nextState;
        currentState.Enter(this);

        PhaseChanged?.Invoke(currentState.Phase);
    }

    private void ShowStatus(string message)
    {
        if (statusText != null)
            statusText.text = message;
        else
            Debug.Log("[ROport] " + message);
    }

    // State Pattern
    private interface IRunState
    {
        RunPhase Phase { get; }

        void Enter(ROportRunManager manager);

        void HandleInput(
            ROportRunManager manager,
            Keyboard keyboard);
    }

    private sealed class PlanningState : IRunState
    {
        public RunPhase Phase => RunPhase.Planning;

        public void Enter(ROportRunManager manager)
        {
            manager.routeDrawer.SetEditable(true);

            manager.ShowStatus(
                "Desenhe uma rota ate o destino atual.");
        }

        public void HandleInput(
            ROportRunManager manager,
            Keyboard keyboard)
        {
            if (keyboard.spaceKey.wasPressedThisFrame)
                manager.TryStartRoute();
        }
    }

    private sealed class ExecutingState : IRunState
    {
        public RunPhase Phase => RunPhase.Executing;

        public void Enter(ROportRunManager manager)
        {
            manager.routeDrawer.SetEditable(false);

            manager.routeFollower.Follow(
                manager.routeDrawer.Points);

            manager.ShowStatus("Executando rota...");
        }

        public void HandleInput(
            ROportRunManager manager,
            Keyboard keyboard)
        { }
    }

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
        { }
    }

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
        { }
    }
}