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
    [SerializeField, Min(0.1f)] private float startRadius = 1.75f;
    [SerializeField, Min(0.1f)] private float startingNodeRadius = 1.35f;
    [SerializeField, Min(0.1f)] private float snapRadius = 3.4f;
    [SerializeField, Min(2)] private int maxNodes = 300;
    [SerializeField] private float lineHeight = 0.2f;

    private readonly List<int> routeNodes = new List<int>();
    private readonly List<Vector3> points = new List<Vector3>();
    private readonly HashSet<int> visitedNodes = new HashSet<int>();

    private float characterHeightOffset;
    private bool canEdit;
    private bool isDrawing;
    private string lastFeedback;

    public IReadOnlyList<Vector3> Points => points;
    public bool HasRoute => points.Count >= 2;

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

        if (!canEdit || mouse == null ||
            player == null || topDownCamera == null ||
            roadNetwork == null)
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

        routeNodes.Clear();
        points.Clear();
        visitedNodes.Clear();

        if (player == null || roadNetwork == null ||
            !roadNetwork.TryFindNearestNode(
                player.position, startingNodeRadius, out int startId))
        {
            Debug.LogError(
                "[ROport] Player longe do grafo. " +
                "Verifique Starting Node Radius.", this);

            RefreshLine();
            RouteChanged?.Invoke(0);
            return;
        }

        AddNode(startId);

        Vector3 startPosition = roadNetwork.GetWorldPosition(startId);

        characterHeightOffset =
            player.position.y - startPosition.y;

        points.Add(
            startPosition + Vector3.up * characterHeightOffset);

        RefreshLine();
        RouteChanged?.Invoke(points.Count);
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
            SendFeedback("Comece pela ultima ponta da rota.");
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
            mousePoint, snapRadius, out _))
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

        Vector2 mousePosition = Mouse.current.position.ReadValue();

        Ray ray = topDownCamera.ScreenPointToRay(
            new Vector3(mousePosition.x, mousePosition.y, 0f));

        // Obstaculos nao participam do desenho.
        if (!Physics.Raycast(
            ray, out RaycastHit hit, 250f, roadMask,
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

        // O limite evita ciclos durante o processamento.
        for (int step = 0; step < maxNodes; step++)
        {
            int lastIndex = routeNodes.Count - 1;
            int lastId = routeNodes[lastIndex];

            int previousId = lastIndex > 0
                ? routeNodes[lastIndex - 1]
                : -1;

            Vector3 currentPosition =
                roadNetwork.GetWorldPosition(lastId);

            int bestNeighbor = -1;

            float bestDistance =
                (target - currentPosition).sqrMagnitude - 0.001f;

            foreach (int neighbor in roadNetwork.GetNeighbors(lastId))
            {
                Vector3 neighborPosition =
                    roadNetwork.GetWorldPosition(neighbor);

                float distance =
                    (target - neighborPosition).sqrMagnitude;

                if (distance >= bestDistance)
                    continue;

                // Voltar pelo ultimo segmento remove a ponta.
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
                points.RemoveAt(lastIndex);
                visitedNodes.Remove(removed);
            }
            else
            {
                AddNode(bestNeighbor);

                points.Add(
                    roadNetwork.GetWorldPosition(bestNeighbor) +
                    Vector3.up * characterHeightOffset);
            }

            changed = true;
        }

        if (changed)
        {
            lastFeedback = null;
            RefreshLine();
            RouteChanged?.Invoke(points.Count);
        }
        else if (encounteredVisitedNode)
        {
            SendFeedback(
                "Trecho ja utilizado. Volte pela ponta para apagar.");
        }
    }

    private void AddNode(int nodeId)
    {
        routeNodes.Add(nodeId);
        visitedNodes.Add(nodeId);
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

        routeLine.positionCount = routeNodes.Count;

        for (int i = 0; i < routeNodes.Count; i++)
        {
            Vector3 position =
                roadNetwork.GetWorldPosition(routeNodes[i]);

            routeLine.SetPosition(
                i, position + Vector3.up * lineHeight);
        }
    }
}