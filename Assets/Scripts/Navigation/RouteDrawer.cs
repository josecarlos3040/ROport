using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class RouteDrawer : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera topDownCamera;
    [SerializeField] private Transform player;
    [SerializeField] private LineRenderer routeLine;
    [SerializeField] private RoadNetwork roadNetwork;

    [Header("Layers")]
    [SerializeField] private LayerMask roadMask;

    [Header("Drawing")]
    [SerializeField, Min(0.1f)]
    private float startRadius = 1.75f;

    [SerializeField, Min(0.1f)]
    private float startingNodeRadius = 1.5f;

    [SerializeField, Min(0.1f)]
    private float snapRadius = 3.4f;

    [SerializeField, Min(2)]
    private int maxNodes = 300;

    [SerializeField]
    private float lineHeight = 0.2f;

    [Header("Player")]
    [SerializeField, Min(0f)]
    private float playerPivotHeight = 1f;

    private readonly List<Vector3> points =
        new List<Vector3>();

    private readonly List<int> routeNodes =
        new List<int>();

    private readonly HashSet<int> visitedNodes =
        new HashSet<int>();

    private bool canEdit;
    private bool isDrawing;
    private string lastFeedback;

    public IReadOnlyList<Vector3> Points => points;

    // A ligacao inicial ao grafo nao conta
    // como uma rota completa.
    public bool HasRoute => routeNodes.Count >= 2;

    public event Action<int> RouteChanged;
    public event Action<string> Feedback;

    private void Awake()
    {
        if (topDownCamera == null)
            topDownCamera = Camera.main;

        if (routeLine != null)
        {
            routeLine.useWorldSpace = true;
            routeLine.positionCount = 0;
        }
    }

    private void Update()
    {
        Mouse mouse = Mouse.current;

        if (!canEdit ||
            mouse == null ||
            player == null ||
            roadNetwork == null ||
            topDownCamera == null)
        {
            return;
        }

        if (mouse.rightButton.wasPressedThisFrame)
        {
            ClearRoute();
            return;
        }

        if (mouse.leftButton.wasPressedThisFrame &&
            (EventSystem.current == null ||
             !EventSystem.current.IsPointerOverGameObject()))
        {
            BeginDrawing();
        }

        if (isDrawing && mouse.leftButton.isPressed)
            ContinueDrawing();

        if (mouse.leftButton.wasReleasedThisFrame)
            isDrawing = false;
    }

    public void SetEditable(bool editable)
    {
        canEdit = editable;

        if (!editable)
            isDrawing = false;
    }

    public void ClearRoute()
    {
        isDrawing = false;
        lastFeedback = null;

        points.Clear();
        routeNodes.Clear();
        visitedNodes.Clear();

        if (player == null || roadNetwork == null)
        {
            RefreshLine();
            RouteChanged?.Invoke(0);
            return;
        }

        Vector3 playerFeet =
            player.position - Vector3.up * playerPivotHeight;

        if (!roadNetwork.TryFindNearestNode(
            playerFeet,
            startingNodeRadius,
            out int startId))
        {
            Debug.LogError(
                "[ROport] Player longe do grafo. " +
                "Ajuste Starting Node Radius ou a posicao do Player.",
                this);

            RefreshLine();
            RouteChanged?.Invoke(0);
            return;
        }

        routeNodes.Add(startId);
        visitedNodes.Add(startId);

        // Preserva exatamente o lugar onde o Player parou.
        points.Add(player.position);

        Vector3 snappedPosition =
            roadNetwork.GetWorldPosition(startId) +
            Vector3.up * playerPivotHeight;

        // Cria a ligacao entre o Player e o grafo
        // apenas quando ela for necessaria.
        if (Vector3.Distance(
            player.position, snappedPosition) > 0.05f)
        {
            points.Add(snappedPosition);
        }

        RefreshLine();
        RouteChanged?.Invoke(routeNodes.Count);
    }

    private void BeginDrawing()
    {
        if (routeNodes.Count == 0 ||
            !TryGetRoadPoint(out Vector3 mousePoint))
        {
            return;
        }

        Vector3 tip = roadNetwork.GetWorldPosition(
            routeNodes[routeNodes.Count - 1]);

        if (Vector3.Distance(tip, mousePoint) > startRadius)
        {
            SendFeedback(
                "Comece perto da ultima ponta da rota.");
            return;
        }

        isDrawing = true;
    }

    private void ContinueDrawing()
    {
        if (routeNodes.Count == 0 ||
            !TryGetRoadPoint(out Vector3 mousePoint))
        {
            return;
        }

        if (!roadNetwork.TryFindNearestNode(
            mousePoint,
            snapRadius,
            out _))
        {
            return;
        }

        ExtendToward(mousePoint);
    }

    private bool TryGetRoadPoint(out Vector3 point)
    {
        point = Vector3.zero;

        if (Mouse.current == null || topDownCamera == null)
            return false;

        Vector2 mousePosition =
            Mouse.current.position.ReadValue();

        Ray ray = topDownCamera.ScreenPointToRay(
            new Vector3(mousePosition.x, mousePosition.y, 0f));

        // Os obstaculos nao impedem desenhar a rota.
        if (!Physics.Raycast(
            ray,
            out RaycastHit hit,
            250f,
            roadMask,
            QueryTriggerInteraction.Ignore))
        {
            return false;
        }

        point = hit.point;
        return true;
    }

    private void ExtendToward(Vector3 target)
    {
        bool changed = false;
        bool encounteredVisitedNode = false;

        for (int step = 0; step < maxNodes; step++)
        {
            int lastIndex = routeNodes.Count - 1;
            int lastId = routeNodes[lastIndex];

            int previousId = lastIndex > 0
                ? routeNodes[lastIndex - 1]
                : -1;

            Vector3 currentPosition =
                roadNetwork.GetWorldPosition(lastId);

            float bestDistance =
                (target - currentPosition).sqrMagnitude - 0.001f;

            int bestNeighbor = -1;

            if (previousId >= 0)
            {
                Vector3 previousPosition =
                    roadNetwork.GetWorldPosition(previousId);

                float previousDistance =
                    (target - previousPosition).sqrMagnitude;

                if (previousDistance < bestDistance)
                {
                    bestNeighbor = previousId;
                    bestDistance = previousDistance;
                }
            }

            foreach (int neighbor in roadNetwork.GetNeighbors(lastId))
            {
                Vector3 neighborPosition =
                    roadNetwork.GetWorldPosition(neighbor);

                float distance =
                    (target - neighborPosition).sqrMagnitude;

                if (distance >= bestDistance)
                    continue;

                if (neighbor == previousId)
                {
                    bestNeighbor = neighbor;
                    bestDistance = distance;
                    continue;
                }

                if (visitedNodes.Contains(neighbor))
                {
                    encounteredVisitedNode = true;
                    continue;
                }

                if (routeNodes.Count >= maxNodes)
                    continue;

                bestNeighbor = neighbor;
                bestDistance = distance;
            }

            if (bestNeighbor < 0)
                break;

            if (bestNeighbor == previousId)
            {
                int removed = routeNodes[lastIndex];

                routeNodes.RemoveAt(lastIndex);
                visitedNodes.Remove(removed);

                // Remove apenas a ponta.
                // Preserva a posicao original do Player.
                points.RemoveAt(points.Count - 1);
            }
            else
            {
                routeNodes.Add(bestNeighbor);
                visitedNodes.Add(bestNeighbor);

                points.Add(
                    roadNetwork.GetWorldPosition(bestNeighbor) +
                    Vector3.up * playerPivotHeight);
            }

            changed = true;
        }

        if (changed)
        {
            lastFeedback = null;

            RefreshLine();
            RouteChanged?.Invoke(routeNodes.Count);
        }
        else if (encounteredVisitedNode)
        {
            SendFeedback(
                "Trecho ja utilizado. Volte para apagar.");
        }
    }

    private void SendFeedback(string message)
    {
        if (lastFeedback == message)
            return;

        lastFeedback = message;
        Feedback?.Invoke(message);
    }

    private void RefreshLine()
    {
        if (routeLine == null)
            return;

        routeLine.positionCount = points.Count;

        for (int i = 0; i < points.Count; i++)
        {
            // O movimento usa a altura do Player,
            // mas a linha aparece proxima ao chao.
            Vector3 position =
                points[i] -
                Vector3.up * playerPivotHeight +
                Vector3.up * lineHeight;

            routeLine.SetPosition(i, position);
        }
    }
}