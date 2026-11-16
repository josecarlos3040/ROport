using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

public enum RunPhase
{
    Planning,
    Executing,
    Finished
}

public class ROportRunManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RouteDrawer routeDrawer;
    [SerializeField] private RouteFollower routeFollower;
    [SerializeField] private TMP_Text statusText;

    private readonly PlanningState planning = new PlanningState();
    private readonly ExecutingState executing = new ExecutingState();
    private readonly FinishedState finished = new FinishedState();

    private IRunState currentState;

    public RunPhase Phase =>
        currentState != null ? currentState.Phase : RunPhase.Planning;

    public event Action<RunPhase> PhaseChanged;

    private void Awake()
    {
        if (routeFollower != null)
            routeFollower.Completed += OnRouteCompleted;

        if (routeDrawer != null)
        {
            routeDrawer.RouteChanged += OnRouteChanged;
            routeDrawer.Feedback += OnRouteFeedback;
        }
    }

    private void Start()
    {
        if (routeDrawer == null || routeFollower == null)
        {
            Debug.LogError(
                "[ROport] Configure RouteDrawer e RouteFollower.",
                this);

            enabled = false;
            return;
        }

        ResetRun();
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;

        if (keyboard == null)
            return;

        if (keyboard.rKey.wasPressedThisFrame)
        {
            ResetRun();
            return;
        }

        currentState?.HandleInput(this, keyboard);
    }

    private void OnDestroy()
    {
        if (routeFollower != null)
            routeFollower.Completed -= OnRouteCompleted;

        if (routeDrawer != null)
        {
            routeDrawer.RouteChanged -= OnRouteChanged;
            routeDrawer.Feedback -= OnRouteFeedback;
        }
    }

    public void ResetRun()
    {
        if (routeFollower == null || routeDrawer == null)
            return;

        routeFollower.ResetFollower();
        routeDrawer.ClearRoute();

        ChangeState(planning);
    }

    private void ChangeState(IRunState nextState)
    {
        if (nextState == null)
            return;

        currentState = nextState;
        currentState.Enter(this);

        PhaseChanged?.Invoke(currentState.Phase);
    }

    private void OnRouteCompleted()
    {
        if (Phase == RunPhase.Executing)
            ChangeState(finished);
    }

    private void OnRouteChanged(int pointCount)
    {
        if (Phase != RunPhase.Planning)
            return;

        ShowStatus(pointCount >= 2
            ? "Rota pronta! ESPACO: executar | Botao direito: limpar"
            : "Arraste do personagem/ultima ponta. " +
              "ESPACO: executar | Botao direito: limpar");
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

    private interface IRunState
    {
        RunPhase Phase { get; }

        void Enter(ROportRunManager manager);
        void HandleInput(ROportRunManager manager, Keyboard keyboard);
    }

    private sealed class PlanningState : IRunState
    {
        public RunPhase Phase => RunPhase.Planning;

        public void Enter(ROportRunManager manager)
        {
            manager.routeDrawer.SetEditable(true);

            manager.ShowStatus(
                "Arraste para desenhar. " +
                "ESPACO: executar | Botao direito: limpar");
        }

        public void HandleInput(
            ROportRunManager manager, Keyboard keyboard)
        {
            if (keyboard.spaceKey.wasPressedThisFrame &&
                manager.routeDrawer.HasRoute)
            {
                manager.ChangeState(manager.executing);
            }
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

            manager.ShowStatus("Executando rota... R: reiniciar");
        }

        public void HandleInput(
            ROportRunManager manager, Keyboard keyboard)
        { }
    }

    private sealed class FinishedState : IRunState
    {
        public RunPhase Phase => RunPhase.Finished;

        public void Enter(ROportRunManager manager)
        {
            manager.ShowStatus(
                "Percurso concluido! R: planejar nova rota");
        }

        public void HandleInput(
            ROportRunManager manager, Keyboard keyboard)
        { }
    }
}