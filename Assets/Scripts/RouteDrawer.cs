using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class RouteDrawer : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera topDownCamera;
    [SerializeField] private Transform player;
    [SerializeField] private LineRenderer routeLine;

    [Header("Layers")]
    [SerializeField] private LayerMask roadMask;
    [SerializeField] private LayerMask obstacleMask;

    [Header("Route Settings")]
    [SerializeField, Min(0.05f)] private float minimumPointDistance = 0.3f;
    [SerializeField, Min(0.05f)] private float sampleStep = 0.35f;
    [SerializeField, Min(0.1f)] private float startRadius = 1.5f;
    [SerializeField, Min(0f)] private float obstacleClearance = 0.35f;
    [SerializeField] private float lineHeight = 0.15f;
    [SerializeField, Min(2)] private int maxPoints = 600;

    private readonly List<Vector3> points = new List<Vector3>();
    private bool isDrawing;
    private bool canEdit;

    public IReadOnlyList<Vector3> Points => points;
    public bool HasRoute => points.Count >= 2;
    public event Action<int> RouteChanged;

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
        if (!canEdit || mouse == null || player == null || topDownCamera == null)
            return;

        if (mouse.leftButton.wasPressedThisFrame)
            BeginDrawing();

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

        if (player != null)
            points.Add(player.position);

        RefreshLine();
        RouteChanged?.Invoke(points.Count);
    }

    private void BeginDrawing()
    {
        if (!TryGetRoadPoint(out Vector3 clickedPoint))
            return;

        Vector3 fromPlayer = clickedPoint - player.position;
        fromPlayer.y = 0f;
        if (fromPlayer.magnitude > startRadius)
            return;

        // Clicar perto do personagem substitui a rota antiga.
        ClearRoute();
        isDrawing = true;
        TryAppend(clickedPoint);
    }

    private void ContinueDrawing()
    {
        if (TryGetRoadPoint(out Vector3 mousePoint))
            TryAppend(mousePoint);
    }

    private bool TryGetRoadPoint(out Vector3 point)
    {
        point = Vector3.zero;

        Vector2 mousePosition = Mouse.current.position.ReadValue();
        Ray ray = topDownCamera.ScreenPointToRay(
            new Vector3(mousePosition.x, mousePosition.y, 0f));

        int mask = roadMask.value | obstacleMask.value;
        if (!Physics.Raycast(ray, out RaycastHit hit, 200f, mask,
                QueryTriggerInteraction.Ignore))
            return false;

        if (!IsInLayer(hit.collider.gameObject.layer, roadMask))
            return false;

        point = hit.point;
        // A raiz da cápsula fica em Y=1; a linha visual fica perto do chão.
        point.y = player.position.y;
        return true;
    }

    private void TryAppend(Vector3 destination)
    {
        if (points.Count == 0)
            return;

        Vector3 last = points[points.Count - 1];
        Vector3 delta = destination - last;
        delta.y = 0f;
        float distance = delta.magnitude;

        if (distance < minimumPointDistance)
            return;

        // Amostra o segmento inteiro para impedir atravessar ruas inexistentes.
        int steps = Mathf.Max(1, Mathf.CeilToInt(distance / sampleStep));
        if (points.Count + steps > maxPoints)
            return;

        for (int i = 1; i <= steps; i++)
        {
            Vector3 sample = Vector3.Lerp(last, destination, i / (float)steps);
            if (!IsRoadClear(sample))
                return;
        }

        for (int i = 1; i <= steps; i++)
            points.Add(Vector3.Lerp(last, destination, i / (float)steps));

        RefreshLine();
        RouteChanged?.Invoke(points.Count);
    }

    private bool IsRoadClear(Vector3 point)
    {
        Vector3 above = new Vector3(point.x, 20f, point.z);
        int mask = roadMask.value | obstacleMask.value;

        if (!Physics.Raycast(above, Vector3.down, out RaycastHit hit, 40f, mask,
                QueryTriggerInteraction.Ignore))
            return false;

        if (!IsInLayer(hit.collider.gameObject.layer, roadMask))
            return false;

        if (Physics.CheckSphere(point, obstacleClearance, obstacleMask.value,
                QueryTriggerInteraction.Ignore))
            return false;

        return true;
    }

    private static bool IsInLayer(int layer, LayerMask mask)
    {
        return (mask.value & (1 << layer)) != 0;
    }

    private void RefreshLine()
    {
        if (routeLine == null)
            return;

        routeLine.positionCount = points.Count;
        Vector3[] visiblePoints = new Vector3[points.Count];

        for (int i = 0; i < points.Count; i++)
            visiblePoints[i] = new Vector3(points[i].x, lineHeight, points[i].z);

        routeLine.SetPositions(visiblePoints);
    }
}
