using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

// Desenha no grafo de ruas: sem diagonais, com retorno para apagar a ponta.
// Arraste a partir do jogador (ou da ponta da rota existente).
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
    [SerializeField, Min(0.1f)] private float startRadius = 1.75f;
    [SerializeField, Min(0.1f)] private float startingNodeRadius = 1f;
    [SerializeField, Min(0.1f)] private float snapRadius = 3.4f;
    [SerializeField, Min(0.1f)] private float obstacleClearance = 0.45f;
    [SerializeField, Min(2)] private int maxNodes = 300;
    [SerializeField] private float lineHeight = 0.2f;

    private readonly List<Vector3> points = new List<Vector3>();
    private readonly List<int> routeNodes = new List<int>();
    private readonly HashSet<int> visitedNodes = new HashSet<int>();

    private bool isDrawing;
    private bool canEdit;
    private string lastFeedback;

    public IReadOnlyList<Vector3> Points => points;
    public bool HasRoute => points.Count >= 2;
    public event Action<int> RouteChanged;
    public event Action<string> Feedback;

    private void Awake()
    {
        if (topDownCamera == null) topDownCamera = Camera.main;
        if (routeLine != null)
        {
            routeLine.useWorldSpace = true;
            routeLine.positionCount = 0;
        }
    }

    private void Update()
    {
        Mouse mouse = Mouse.current;
        if (!canEdit || mouse == null || player == null ||
            topDownCamera == null || roadNetwork == null)
            return;

        if (mouse.rightButton.wasPressedThisFrame)
        {
            ClearRoute();
            return;
        }

        if (mouse.leftButton.wasPressedThisFrame)
        {
            // Evita começar desenhando em cima de botões da interface.
            if (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject())
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
        if (!editable) isDrawing = false;
    }

    public void ClearRoute()
    {
        isDrawing = false;
        points.Clear();
        routeNodes.Clear();
        visitedNodes.Clear();
        lastFeedback = null;

        if (player != null && roadNetwork != null &&
            roadNetwork.TryFindNearestNode(player.position, startingNodeRadius, out int startNode))
        {
            routeNodes.Add(startNode);
            visitedNodes.Add(startNode);
            points.Add(roadNetwork.GetWorldPosition(startNode, player.position.y));
        }
        else
        {
            Debug.LogError("ROport: o Player deve comecar em cima do centro de uma rua do RoadNetwork.", this);
        }

        RefreshLine();
        RouteChanged?.Invoke(points.Count);
    }

    private void BeginDrawing()
    {
        if (routeNodes.Count == 0 || !TryGetRoadPoint(out Vector3 mousePoint))
            return;

        Vector3 last = points[points.Count - 1];
        if (FlatDistance(last, mousePoint) > startRadius)
        {
            ShowFeedback("Comece pelo personagem ou pela ultima ponta da rota.");
            return;
        }

        isDrawing = true;
    }

    private void ContinueDrawing()
    {
        if (!TryGetRoadPoint(out Vector3 mousePoint) || routeNodes.Count == 0)
            return;

        // Aproxima o ponto do mouse a um ponto real da malha de ruas.
        if (!roadNetwork.TryFindNearestNode(mousePoint, snapRadius, out _))
            return;

        ExtendToward(mousePoint);
    }

    private bool TryGetRoadPoint(out Vector3 point)
    {
        point = Vector3.zero;
        Vector2 position = Mouse.current.position.ReadValue();
        Ray ray = topDownCamera.ScreenPointToRay(new Vector3(position.x, position.y, 0f));

        int combinedMask = roadMask.value | obstacleMask.value;
        if (!Physics.Raycast(ray, out RaycastHit hit, 250f, combinedMask,
                QueryTriggerInteraction.Ignore))
            return false;

        if ((roadMask.value & (1 << hit.collider.gameObject.layer)) == 0)
            return false;

        point = hit.point;
        point.y = player.position.y; // mesma altura usada pelo RouteFollower
        return true;
    }

    private void ExtendToward(Vector3 target)
    {
        bool changed = false;
        bool blockedByVisited = false;

        // Cada iteracao avanca OU apaga um no, para acompanhar o mouse
        // mesmo quando ele se move rapidamente entre dois frames.
        for (int step = 0; step < maxNodes; step++)
        {
            int lastIndex = routeNodes.Count - 1;
            int lastId = routeNodes[lastIndex];
            int previousId = lastIndex > 0 ? routeNodes[lastIndex - 1] : -1;

            Vector3 from = roadNetwork.GetWorldPosition(lastId, player.position.y);
            float currentDistance = (target - from).sqrMagnitude;

            int bestNeighbor = -1;
            float bestDistance = currentDistance - 0.001f;

            foreach (int neighborId in roadNetwork.GetNeighbors(lastId))
            {
                Vector3 to = roadNetwork.GetWorldPosition(neighborId, player.position.y);
                float distance = (target - to).sqrMagnitude;
                if (distance >= bestDistance) continue;

                // O unico no visitado permitido e o penultimo ponto:
                // voltar por ele apaga o ultimo trecho da rota.
                if (neighborId == previousId)
                {
                    bestDistance = distance;
                    bestNeighbor = neighborId;
                    continue;
                }

                // Proibe cruzar a propria rota ou revisitar outro no.
                if (visitedNodes.Contains(neighborId))
                {
                    blockedByVisited = true;
                    continue;
                }

                // Mesmo com rota cheia, apagar pelo mouse continua permitido.
                if (routeNodes.Count >= maxNodes) continue;

                if (Physics.CheckCapsule(from, to, obstacleClearance,
                        obstacleMask.value, QueryTriggerInteraction.Ignore))
                    continue;

                bestDistance = distance;
                bestNeighbor = neighborId;
            }

            if (bestNeighbor < 0) break;

            if (bestNeighbor == previousId)
            {
                // Remove somente a ponta: o restante do caminho permanece.
                routeNodes.RemoveAt(lastIndex);
                points.RemoveAt(lastIndex);
                visitedNodes.Remove(lastId);
            }
            else
            {
                routeNodes.Add(bestNeighbor);
                visitedNodes.Add(bestNeighbor);
                points.Add(roadNetwork.GetWorldPosition(bestNeighbor, player.position.y));
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
            ShowFeedback("Essa rua ja foi usada. Volte pela ponta da linha para apagar.");
        }
    }

    private void ShowFeedback(string message)
    {
        if (lastFeedback == message) return;
        lastFeedback = message;
        Feedback?.Invoke(message);
    }

    private static float FlatDistance(Vector3 a, Vector3 b)
    {
        a.y = b.y = 0f;
        return Vector3.Distance(a, b);
    }

    private void RefreshLine()
    {
        if (routeLine == null) return;

        routeLine.positionCount = points.Count;
        Vector3[] linePositions = new Vector3[points.Count];
        for (int i = 0; i < points.Count; i++)
            linePositions[i] = new Vector3(points[i].x, lineHeight, points[i].z);
        routeLine.SetPositions(linePositions);
    }
}
 