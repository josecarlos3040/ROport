using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

public class RouteDrawer : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera topDownCamera;
    [SerializeField] private Transform player;
    [SerializeField] private LineRenderer routeLine;
    [SerializeField] private RoadNetwork roadNetwork;

    [Header("Layers")]
    [SerializeField] private LayerMask roadMask;
    [SerializeField] private LayerMask obstacleMask;

    [Header("Drawing")]
    [SerializeField, Min(0.1f)]
    private float startRadius = 1.75f;

    [SerializeField, Min(0.1f)]
    private float startingNodeRadius = 1.35f;

    [SerializeField, Min(0.1f)]
    private float snapRadius = 3.4f;

    //[SerializeField, Min(0.1f)]
    //private float obstacleClearance = 0.45f;

    [SerializeField, Min(2)]
    private int maxNodes = 300;

    [SerializeField]
    private float lineHeight = 0.2f;

    private readonly List<Vector3> points =
        new List<Vector3>();

    private readonly List<int> routeNodes =
        new List<int>();

    private readonly HashSet<int> visitedNodes =
        new HashSet<int>();

    private float characterHeightOffset;

    private bool isDrawing;
    private bool canEdit;
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

        if (!canEdit ||
            mouse == null ||
            player == null ||
            topDownCamera == null ||
            roadNetwork == null)
        {
            return;
        }

        if (mouse.rightButton.wasPressedThisFrame)
        {
            ClearRoute();
            return;
        }

        if (mouse.leftButton.wasPressedThisFrame)
        {
            if (EventSystem.current == null ||
                !EventSystem.current.IsPointerOverGameObject())
            {
                BeginDrawing();
            }
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

        points.Clear();
        routeNodes.Clear();
        visitedNodes.Clear();

        lastFeedback = null;

        if (player != null &&
            roadNetwork != null &&
            roadNetwork.TryFindNearestNode(
                player.position,
                startingNodeRadius,
                out int startId))
        {
            routeNodes.Add(startId);
            visitedNodes.Add(startId);

            // Distancia vertical entre a superficie
            // da rua e o pivot do personagem.
            characterHeightOffset =
                player.position.y -
                roadNetwork.GetWorldPosition(startId).y;

            points.Add(
                roadNetwork.GetWorldPosition(startId) +
                Vector3.up * characterHeightOffset);
        }
        else
        {
            Debug.LogError(
                "ROport: Player muito longe de um no do " +
                "RoadNetwork. Ajuste Starting Node Radius.",
                this);
        }

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

        Vector3 lastSurface =
            roadNetwork.GetWorldPosition(
                routeNodes[routeNodes.Count - 1]);

        if (Vector3.Distance(lastSurface, mousePoint)
            > startRadius)
        {
            ShowFeedback(
                "Comece pelo personagem ou pela " +
                "ultima ponta da rota.");
            return;
        }

        isDrawing = true;
    }

    private void ContinueDrawing()
    {
        if (!TryGetRoadPoint(out Vector3 mousePoint) ||
            routeNodes.Count == 0)
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

        Vector2 mousePosition =
            Mouse.current.position.ReadValue();

        Ray ray = topDownCamera.ScreenPointToRay(
            new Vector3(mousePosition.x, mousePosition.y, 0f));

        // Detecta somente as ruas.
        // Obstaculos nao impedem o desenho.
        if (!Physics.Raycast(
            ray,
            out RaycastHit hit,
            250f,
            roadMask.value,
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
        bool blockedByVisited = false;

        for (int step = 0; step < maxNodes; step++)
        {
            int lastIndex = routeNodes.Count - 1;
            int lastId = routeNodes[lastIndex];

            int previousId = lastIndex > 0
                ? routeNodes[lastIndex - 1]
                : -1;

            Vector3 from =
                roadNetwork.GetWorldPosition(lastId);

            float currentDistance =
                (target - from).sqrMagnitude;

            int bestNeighbor = -1;
            float bestDistance = currentDistance - 0.001f;

            foreach (int neighborId
                in roadNetwork.GetNeighbors(lastId))
            {
                Vector3 to =
                    roadNetwork.GetWorldPosition(neighborId);

                float distance =
                    (target - to).sqrMagnitude;

                if (distance >= bestDistance)
                    continue;

                // Voltar ao penultimo no apaga
                // o ultimo segmento da linha.
                if (neighborId == previousId)
                {
                    bestDistance = distance;
                    bestNeighbor = neighborId;
                    continue;
                }

                // Nao permite cruzar a propria rota.
                if (visitedNodes.Contains(neighborId))
                {
                    blockedByVisited = true;
                    continue;
                }

                if (routeNodes.Count >= maxNodes)
                    continue;

                // Verifica obstaculos na altura
                // percorrida pelo personagem.


                bestDistance = distance;
                bestNeighbor = neighborId;
            }

            if (bestNeighbor < 0)
                break;

            if (bestNeighbor == previousId)
            {
                // Apagamento progressivo.
                routeNodes.RemoveAt(lastIndex);
                points.RemoveAt(lastIndex);
                visitedNodes.Remove(lastId);
            }
            else
            {
                routeNodes.Add(bestNeighbor);
                visitedNodes.Add(bestNeighbor);

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
        else if (blockedByVisited)
        {
            ShowFeedback(
                "Essa rua ja foi usada. " +
                "Volte pela ponta para apagar.");
        }
    }

    private void ShowFeedback(string message)
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

        Vector3[] positions =
            new Vector3[routeNodes.Count];

        for (int i = 0; i < routeNodes.Count; i++)
        {
            positions[i] =
                roadNetwork.GetWorldPosition(routeNodes[i]) +
                Vector3.up * lineHeight;
        }

        routeLine.SetPositions(positions);
    }
}