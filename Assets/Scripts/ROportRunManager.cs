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
    [SerializeField] private RouteDrawer routeDrawer;
    [SerializeField] private RouteFollower routeFollower;
    [SerializeField] private TMP_Text statusText;


    private readonly PlanningState planning = new PlanningState();
    private readonly ExecutingState executing = new ExecutingState();
    private readonly FinishedState finished = new FinishedState();
    private IRunState currentState;

    public RunPhase Phase => currentState == null ? RunPhase.Planning : currentState.Phase;
    public event Action<RunPhase> PhaseChanged;

    private void Awake()
    {
        if (routeFollower != null)
            routeFollower.Completed += OnRouteCompleted;

        if (routeDrawer != null)
            routeDrawer.RouteChanged += OnRouteChanged;
    }

    private void Start()
    {
        if (routeDrawer == null || routeFollower == null)
        {
            Debug.LogError("ROport: ligue RouteDrawer e RouteFollower no Inspector.");
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

        currentState?.Tick(this);
    }

    private void OnDestroy()
    {
        if (routeFollower != null)
            routeFollower.Completed -= OnRouteCompleted;

        if (routeDrawer != null)
            routeDrawer.RouteChanged -= OnRouteChanged;
    }

    public void ResetRun()
    {
        routeFollower.ResetFollower();
        routeDrawer.ClearRoute();
        ChangeState(planning);
    }

    private void ChangeState(IRunState next)
    {
        currentState?.Exit(this);
        currentState = next;
        currentState.Enter(this);
        PhaseChanged?.Invoke(currentState.Phase);
    }

    private void OnRouteCompleted()
    {
        if (Phase == RunPhase.Executing)
            ChangeState(finished);
    }

    private void OnRouteChanged(int totalPoints)
    {
        if (Phase != RunPhase.Planning || statusText == null)
            return;

        statusText.text = totalPoints >= 2
            ? "Rota pronta! ESPAÇO: executar | R: limpar"
            : "Clique perto do personagem e arraste para desenhar a rota";
    }

    private void ShowMessage(string message)
    {
        if (statusText != null)
            statusText.text = message;
        else
            Debug.Log("[ROport] " + message);
    }

    // STATE PATTERN: cada fase controla sua entrada, saída e atualização.
    private interface IRunState
    {
        RunPhase Phase { get; }
        void Enter(ROportRunManager context);
        void Tick(ROportRunManager context);
        void Exit(ROportRunManager context);
    }

    private sealed class PlanningState : IRunState
    {
        public RunPhase Phase => RunPhase.Planning;

        public void Enter(ROportRunManager context)
        {
            context.routeDrawer.SetEditable(true);
            context.ShowMessage("Clique perto do personagem e arraste. ESPAÇO: executar | R: limpar");
        }

        public void Tick(ROportRunManager context)
        {
            if (Keyboard.current.spaceKey.wasPressedThisFrame && context.routeDrawer.HasRoute)
                context.ChangeState(context.executing);
        }

        public void Exit(ROportRunManager context) { }
    }

    private sealed class ExecutingState : IRunState
    {
        public RunPhase Phase => RunPhase.Executing;

        public void Enter(ROportRunManager context)
        {
            context.routeDrawer.SetEditable(false);
            context.routeFollower.Follow(context.routeDrawer.Points);
            context.ShowMessage("Executando rota... R: reiniciar");
        }

        public void Tick(ROportRunManager context) { }
        public void Exit(ROportRunManager context) { }
    }

    private sealed class FinishedState : IRunState
    {
        public RunPhase Phase => RunPhase.Finished;

        public void Enter(ROportRunManager context)
        {
            context.ShowMessage("Percurso concluído! R: planejar nova rota");
        }

        public void Tick(ROportRunManager context) { }
        public void Exit(ROportRunManager context) { }
    }
}
