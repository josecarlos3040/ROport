using System.Collections.Generic;
using UnityEngine;

public class RoadNetwork : MonoBehaviour
{
    private sealed class Node
    {
        public readonly Vector3 Position;
        public readonly List<int> Neighbors = new List<int>(4);

        public Node(Vector3 position)
        {
            Position = position;
        }
    }

    [Header("Grid")]
    [SerializeField, Min(0.25f)] private float nodeSpacing = 2f;
    [SerializeField, Min(0.01f)] private float mergeTolerance = 0.2f;
    [SerializeField] private RoadSegment[] roadSegments;
    [SerializeField] private bool showGizmos = true;

    private readonly List<Node> nodes = new List<Node>();

    // Permite pontos em diferentes alturas no mesmo X/Z.
    private readonly Dictionary<Vector2Int, List<int>> nodesByGrid =
        new Dictionary<Vector2Int, List<int>>();

    public int NodeCount => nodes.Count;
    public float NodeSpacing => nodeSpacing;

    private void Awake()
    {
        Rebuild();
    }

    [ContextMenu("Rebuild Road Network")]
    public void Rebuild()
    {
        nodes.Clear();
        nodesByGrid.Clear();

        RoadSegment[] segments =
            roadSegments != null && roadSegments.Length > 0
                ? roadSegments
                : GetComponentsInChildren<RoadSegment>(true);

        foreach (RoadSegment segment in segments)
        {
            if (segment == null || !segment.gameObject.activeInHierarchy)
                continue;

            if (segment.UseCustomEndpoints)
                AddElevatedSegment(segment);
            else
                AddFlatSegment(segment);
        }

        if (nodes.Count == 0)
        {
            Debug.LogError(
                "[ROport] RoadNetwork sem pontos navegaveis.", this);
            return;
        }

        Debug.Log($"[ROport] Grafo criado: {NodeCount} pontos.", this);
    }

    private void AddFlatSegment(RoadSegment segment)
    {
        Vector3 angles = segment.transform.eulerAngles;

        if (Mathf.Abs(Mathf.DeltaAngle(angles.x, 0f)) > 0.1f ||
            Mathf.Abs(Mathf.DeltaAngle(angles.y, 0f)) > 0.1f ||
            Mathf.Abs(Mathf.DeltaAngle(angles.z, 0f)) > 0.1f)
        {
            Debug.LogWarning(
                $"[ROport] {segment.name}: use Custom Endpoints " +
                "para ruas inclinadas.", segment);
            return;
        }

        Bounds bounds = segment.Collider.bounds;

        bool alongX =
            segment.Axis == RoadSegment.RoadAxis.HorizontalX;

        float fixedCoordinate =
            alongX ? bounds.center.z : bounds.center.x;

        int fixedGrid =
            Mathf.RoundToInt(fixedCoordinate / nodeSpacing);

        if (Mathf.Abs(fixedCoordinate - fixedGrid * nodeSpacing)
            > mergeTolerance)
        {
            Debug.LogWarning(
                $"[ROport] {segment.name}: centro fora do grid.",
                segment);
            return;
        }

        float min = alongX ? bounds.min.x : bounds.min.z;
        float max = alongX ? bounds.max.x : bounds.max.z;

        int first = Mathf.CeilToInt((min - 0.001f) / nodeSpacing);
        int last = Mathf.FloorToInt((max + 0.001f) / nodeSpacing);

        int previous = -1;

        for (int i = first; i <= last; i++)
        {
            Vector3 position = alongX
                ? new Vector3(
                    i * nodeSpacing,
                    bounds.max.y,
                    fixedGrid * nodeSpacing)
                : new Vector3(
                    fixedGrid * nodeSpacing,
                    bounds.max.y,
                    i * nodeSpacing);

            int current = GetOrCreateNode(position);

            if (previous >= 0)
                Connect(previous, current);

            previous = current;
        }
    }

    private void AddElevatedSegment(RoadSegment segment)
    {
        if (!segment.TryGetEndpoints(
            out Vector3 start, out Vector3 end))
        {
            Debug.LogWarning(
                $"[ROport] {segment.name}: faltam os endpoints.",
                segment);
            return;
        }

        bool alongX =
            segment.Axis == RoadSegment.RoadAxis.HorizontalX;

        float length = alongX
            ? Mathf.Abs(end.x - start.x)
            : Mathf.Abs(end.z - start.z);

        float sideways = alongX
            ? Mathf.Abs(end.z - start.z)
            : Mathf.Abs(end.x - start.x);

        int steps = Mathf.RoundToInt(length / nodeSpacing);

        if (sideways > mergeTolerance ||
            steps < 1 ||
            Mathf.Abs(length - steps * nodeSpacing) > mergeTolerance ||
            !IsAligned(start) ||
            !IsAligned(end))
        {
            Debug.LogWarning(
                $"[ROport] {segment.name}: endpoints fora do grid " +
                "ou fora do eixo configurado.", segment);
            return;
        }

        int previous = -1;

        for (int i = 0; i <= steps; i++)
        {
            Vector3 position =
                Vector3.Lerp(start, end, (float)i / steps);

            int current = GetOrCreateNode(position);

            if (previous >= 0)
            {
                if (segment.DownhillOnly)
                {
                    float previousHeight = nodes[previous].Position.y;
                    float currentHeight = nodes[current].Position.y;

                    // Conecta apenas no sentido da descida.
                    if (previousHeight > currentHeight + 0.01f)
                    {
                        ConnectOneWay(previous, current);
                    }
                    else if (currentHeight > previousHeight + 0.01f)
                    {
                        ConnectOneWay(current, previous);
                    }
                    else
                    {
                        Debug.LogWarning(
                            "[ROport] Segmento sem inclinacao: " +
                            segment.name);
                    }
                }
                else
                {
                    Connect(previous, current);
                }
            }

            previous = current;
        }
    }

    private void ConnectOneWay(int from, int to)
    {
        if (from == to)
            return;

        if (!nodes[from].Neighbors.Contains(to))
            nodes[from].Neighbors.Add(to);
    }

    private bool IsAligned(Vector3 position)
    {
        float x = Mathf.Round(position.x / nodeSpacing) * nodeSpacing;
        float z = Mathf.Round(position.z / nodeSpacing) * nodeSpacing;

        return Mathf.Abs(position.x - x) <= mergeTolerance &&
               Mathf.Abs(position.z - z) <= mergeTolerance;
    }

    private int GetOrCreateNode(Vector3 position)
    {
        Vector2Int key = new Vector2Int(
            Mathf.RoundToInt(position.x / nodeSpacing),
            Mathf.RoundToInt(position.z / nodeSpacing));

        if (!nodesByGrid.TryGetValue(key, out List<int> candidates))
        {
            candidates = new List<int>();
            nodesByGrid.Add(key, candidates);
        }

        foreach (int id in candidates)
        {
            Vector3 existing = nodes[id].Position;

            if (Mathf.Abs(existing.x - position.x) <= mergeTolerance &&
                Mathf.Abs(existing.y - position.y) <= mergeTolerance &&
                Mathf.Abs(existing.z - position.z) <= mergeTolerance)
            {
                return id;
            }
        }

        int newId = nodes.Count;

        nodes.Add(new Node(position));
        candidates.Add(newId);

        return newId;
    }

    private void Connect(int a, int b)
    {
        if (a == b)
            return;

        if (!nodes[a].Neighbors.Contains(b))
            nodes[a].Neighbors.Add(b);

        if (!nodes[b].Neighbors.Contains(a))
            nodes[b].Neighbors.Add(a);
    }

    public IReadOnlyList<int> GetNeighbors(int nodeId)
    {
        return nodes[nodeId].Neighbors;
    }

    public Vector3 GetWorldPosition(int nodeId)
    {
        return nodes[nodeId].Position;
    }

    public bool TryFindNearestNode(
        Vector3 position,
        float maxDistance,
        out int nearestId)
    {
        nearestId = -1;
        float bestDistance = maxDistance * maxDistance;

        for (int i = 0; i < nodes.Count; i++)
        {
            float distance =
                (nodes[i].Position - position).sqrMagnitude;

            if (distance > bestDistance)
                continue;

            bestDistance = distance;
            nearestId = i;
        }

        return nearestId >= 0;
    }

    private void OnDrawGizmosSelected()
    {
        if (!showGizmos)
            return;

        for (int i = 0; i < nodes.Count; i++)
        {
            Vector3 position = nodes[i].Position + Vector3.up * 0.15f;

            Gizmos.color = Color.cyan;
            Gizmos.DrawSphere(position, 0.14f);

            Gizmos.color = Color.yellow;

            foreach (int neighbor in nodes[i].Neighbors)
            {
                if (neighbor > i)
                {
                    Gizmos.DrawLine(
                        position,
                        nodes[neighbor].Position + Vector3.up * 0.15f);
                }
            }
        }
    }
}