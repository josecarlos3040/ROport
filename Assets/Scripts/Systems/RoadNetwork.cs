using System.Collections.Generic;
using UnityEngine;

public class RoadNetwork : MonoBehaviour
{
    [Header("Grid")]
    [SerializeField, Min(0.25f)]
    private float nodeSpacing = 2f;

    [SerializeField, Min(0.01f)]
    private float mergeTolerance = 0.2f;

    [SerializeField]
    private RoadSegment[] roadSegments;

    [SerializeField]
    private bool showGizmos = true;

    private sealed class Node
    {
        public readonly Vector3 Position;

        public readonly List<int> Neighbors =
            new List<int>(4);

        public Node(Vector3 position)
        {
            Position = position;
        }
    }

    private readonly List<Node> nodes = new List<Node>();

    // Permite varios nos com o mesmo X/Z,
    // mas em alturas diferentes.
    private readonly Dictionary<Vector2Int, List<int>>
        nodesByGrid =
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
            if (segment == null ||
                !segment.gameObject.activeInHierarchy)
                continue;

            if (segment.UseCustomEndpoints)
                AddCustomSegment(segment);
            else
                AddFlatSegment(segment);
        }

        if (nodes.Count == 0)
        {
            Debug.LogError(
                "ROport: RoadNetwork vazio! Confira os RoadSegments.",
                this);
        }
        else
        {
            Debug.Log(
                $"ROport: grafo 3D criado com {nodes.Count} nos.",
                this);
        }
    }

    // Ruas planas continuam sendo criadas automaticamente.
    private void AddFlatSegment(RoadSegment segment)
    {
        Vector3 angles = segment.transform.eulerAngles;

        if (Mathf.Abs(Mathf.DeltaAngle(angles.x, 0f)) > 0.1f ||
            Mathf.Abs(Mathf.DeltaAngle(angles.y, 0f)) > 0.1f ||
            Mathf.Abs(Mathf.DeltaAngle(angles.z, 0f)) > 0.1f)
        {
            Debug.LogWarning(
                $"ROport: '{segment.name}' esta inclinado. " +
                "Marque Use Custom Endpoints e indique as pontas.",
                segment);
            return;
        }

        Bounds b = segment.Collider.bounds;

        bool horizontal =
            segment.Axis == RoadSegment.RoadAxis.HorizontalX;

        float fixedPosition =
            horizontal ? b.center.z : b.center.x;

        int fixedGrid =
            Mathf.RoundToInt(fixedPosition / nodeSpacing);

        if (Mathf.Abs(
                fixedPosition - fixedGrid * nodeSpacing)
            > mergeTolerance)
        {
            Debug.LogWarning(
                $"ROport: eixo de '{segment.name}' esta fora do grid.",
                segment);
            return;
        }

        float min = horizontal ? b.min.x : b.min.z;
        float max = horizontal ? b.max.x : b.max.z;

        int first = Mathf.CeilToInt(
            (min - 0.001f) / nodeSpacing);

        int last = Mathf.FloorToInt(
            (max + 0.001f) / nodeSpacing);

        int previousId = -1;

        for (int i = first; i <= last; i++)
        {
            Vector3 point = horizontal
                ? new Vector3(
                    i * nodeSpacing,
                    b.max.y,
                    fixedGrid * nodeSpacing)
                : new Vector3(
                    fixedGrid * nodeSpacing,
                    b.max.y,
                    i * nodeSpacing);

            int id = GetOrCreateNode(point);

            if (previousId >= 0)
                Connect(previousId, id);

            previousId = id;
        }
    }

    // Rampas e escadas usam pontos definidos no Inspector.
    private void AddCustomSegment(RoadSegment segment)
    {
        if (!segment.TryGetEndpoints(
                out Vector3 start,
                out Vector3 end))
        {
            Debug.LogWarning(
                $"ROport: faltam Start Point ou End Point em " +
                $"'{segment.name}'.",
                segment);
            return;
        }

        bool horizontal =
            segment.Axis == RoadSegment.RoadAxis.HorizontalX;

        float along = horizontal
            ? Mathf.Abs(end.x - start.x)
            : Mathf.Abs(end.z - start.z);

        float across = horizontal
            ? Mathf.Abs(end.z - start.z)
            : Mathf.Abs(end.x - start.x);

        int steps =
            Mathf.RoundToInt(along / nodeSpacing);

        // Sem diagonais no plano X/Z.
        // A variacao em Y e permitida.
        if (across > mergeTolerance ||
            steps < 1 ||
            Mathf.Abs(along - steps * nodeSpacing)
                > mergeTolerance ||
            !IsGridAligned(start) ||
            !IsGridAligned(end))
        {
            Debug.LogWarning(
                $"ROport: '{segment.name}' precisa ter pontas " +
                $"no grid de {nodeSpacing}, alinhadas ao eixo " +
                "escolhido e separadas por multiplos do espacamento.",
                segment);
            return;
        }

        int previousId = -1;

        // Distribui os pontos ao longo da inclinacao.
        for (int i = 0; i <= steps; i++)
        {
            Vector3 point = Vector3.Lerp(
                start,
                end,
                (float)i / steps);

            int id = GetOrCreateNode(point);

            if (previousId >= 0)
                Connect(previousId, id);

            previousId = id;
        }
    }

    private bool IsGridAligned(Vector3 p)
    {
        return
            Mathf.Abs(
                p.x -
                Mathf.Round(p.x / nodeSpacing) * nodeSpacing)
                <= mergeTolerance
            &&
            Mathf.Abs(
                p.z -
                Mathf.Round(p.z / nodeSpacing) * nodeSpacing)
                <= mergeTolerance;
    }

    // Dois pontos so sao unidos se coincidirem em X, Y e Z.
    private int GetOrCreateNode(Vector3 point)
    {
        Vector2Int key = new Vector2Int(
            Mathf.RoundToInt(point.x / nodeSpacing),
            Mathf.RoundToInt(point.z / nodeSpacing));

        if (!nodesByGrid.TryGetValue(
                key, out List<int> candidates))
        {
            candidates = new List<int>();
            nodesByGrid.Add(key, candidates);
        }

        foreach (int id in candidates)
        {
            Vector3 existing = nodes[id].Position;

            if (Mathf.Abs(existing.x - point.x)
                    <= mergeTolerance &&
                Mathf.Abs(existing.y - point.y)
                    <= mergeTolerance &&
                Mathf.Abs(existing.z - point.z)
                    <= mergeTolerance)
            {
                return id;
            }
        }

        int newId = nodes.Count;

        nodes.Add(new Node(point));
        candidates.Add(newId);

        return newId;
    }

    // Cria uma conexao bidirecional entre dois nos.
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

    // Mantem compatibilidade com chamadas antigas.
    public Vector3 GetWorldPosition(
        int nodeId, float ignoredHeight)
    {
        return GetWorldPosition(nodeId);
    }

    public bool TryFindNearestNode(
        Vector3 worldPosition,
        float maxDistance,
        out int nearestId)
    {
        nearestId = -1;
        float best = maxDistance * maxDistance;

        for (int i = 0; i < nodes.Count; i++)
        {
            float distance =
                (nodes[i].Position - worldPosition)
                .sqrMagnitude;

            if (distance <= best)
            {
                best = distance;
                nearestId = i;
            }
        }

        return nearestId >= 0;
    }

    private void OnDrawGizmosSelected()
    {
        if (!showGizmos || nodes.Count == 0)
            return;

        for (int i = 0; i < nodes.Count; i++)
        {
            Vector3 position =
                nodes[i].Position + Vector3.up * 0.15f;

            Gizmos.color = Color.cyan;
            Gizmos.DrawSphere(position, 0.14f);

            Gizmos.color = Color.yellow;

            foreach (int neighbor in nodes[i].Neighbors)
            {
                if (neighbor > i)
                {
                    Gizmos.DrawLine(
                        position,
                        nodes[neighbor].Position +
                            Vector3.up * 0.15f);
                }
            }
        }
    }
}