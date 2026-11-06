using System.Collections.Generic;
using UnityEngine;

// Constrói um grafo de ruas ortogonais a partir dos RoadSegments filhos.
// As conexões nascem somente no centro das vias e em cruzamentos reais.
public class RoadNetwork : MonoBehaviour
{
    [Header("Grid")]
    [SerializeField, Min(0.25f)] private float nodeSpacing = 2f;
    [SerializeField] private RoadSegment[] roadSegments;
    [SerializeField] private bool showGizmos = true;

    private sealed class Node
    {
        public readonly Vector2Int GridPosition;
        public readonly List<int> Neighbors = new List<int>(4);

        public Node(Vector2Int gridPosition) => GridPosition = gridPosition;
    }

    private readonly List<Node> nodes = new List<Node>();
    private readonly Dictionary<Vector2Int, int> nodeIds = new Dictionary<Vector2Int, int>();

    public int NodeCount => nodes.Count;

    private void Awake() => Rebuild();

    [ContextMenu("Rebuild Road Network")]
    public void Rebuild()
    {
        nodes.Clear();
        nodeIds.Clear();

        if (roadSegments == null || roadSegments.Length == 0)
            roadSegments = GetComponentsInChildren<RoadSegment>();

        foreach (RoadSegment segment in roadSegments)
        {
            if (segment == null || !segment.gameObject.activeInHierarchy)
                continue;

            if (Mathf.Abs(Mathf.DeltaAngle(segment.transform.eulerAngles.y, 0f)) > 0.1f)
            {
                Debug.LogWarning($"ROport: rua '{segment.name}' girada. Use ruas alinhadas aos eixos X/Z neste prototipo.", segment);
                continue;
            }

            Bounds bounds = segment.Collider.bounds;
            bool horizontal = segment.Axis == RoadSegment.RoadAxis.HorizontalX;
            float fixedWorld = horizontal ? bounds.center.z : bounds.center.x;
            int fixedGrid = Mathf.RoundToInt(fixedWorld / nodeSpacing);

            if (Mathf.Abs(fixedWorld - fixedGrid * nodeSpacing) > 0.1f)
            {
                Debug.LogWarning($"ROport: centro da rua '{segment.name}' fora do grid de {nodeSpacing}. Ajuste a posicao.", segment);
                continue;
            }

            float min = horizontal ? bounds.min.x : bounds.min.z;
            float max = horizontal ? bounds.max.x : bounds.max.z;
            int first = Mathf.CeilToInt((min - 0.001f) / nodeSpacing);
            int last = Mathf.FloorToInt((max + 0.001f) / nodeSpacing);
            int previousId = -1;

            for (int coordinate = first; coordinate <= last; coordinate++)
            {
                Vector2Int gridPoint = horizontal
                    ? new Vector2Int(coordinate, fixedGrid)
                    : new Vector2Int(fixedGrid, coordinate);

                int currentId = GetOrCreateNode(gridPoint);

                if (previousId >= 0)
                    Connect(previousId, currentId);

                previousId = currentId;
            }
        }

        if (nodes.Count == 0)
            Debug.LogError("ROport: RoadNetwork vazio. Adicione RoadSegment aos Cubes das ruas.", this);
        else
            Debug.Log($"ROport: grafo criado com {nodes.Count} pontos de rua.", this);
    }

    private int GetOrCreateNode(Vector2Int gridPoint)
    {
        if (nodeIds.TryGetValue(gridPoint, out int existingId))
            return existingId;

        int id = nodes.Count;
        nodes.Add(new Node(gridPoint));
        nodeIds.Add(gridPoint, id);
        return id;
    }

    private void Connect(int a, int b)
    {
        if (a == b) return;
        if (!nodes[a].Neighbors.Contains(b)) nodes[a].Neighbors.Add(b);
        if (!nodes[b].Neighbors.Contains(a)) nodes[b].Neighbors.Add(a);
    }

    public IReadOnlyList<int> GetNeighbors(int nodeId) => nodes[nodeId].Neighbors;

    public Vector3 GetWorldPosition(int nodeId, float height)
    {
        Vector2Int grid = nodes[nodeId].GridPosition;
        return new Vector3(grid.x * nodeSpacing, height, grid.y * nodeSpacing);
    }

    public bool TryFindNearestNode(Vector3 worldPosition, float maxDistance, out int nearestId)
    {
        nearestId = -1;
        float bestDistanceSquared = maxDistance * maxDistance;

        for (int i = 0; i < nodes.Count; i++)
        {
            Vector3 nodePosition = GetWorldPosition(i, worldPosition.y);
            float distanceSquared = (worldPosition - nodePosition).sqrMagnitude;

            if (distanceSquared <= bestDistanceSquared)
            {
                bestDistanceSquared = distanceSquared;
                nearestId = i;
            }
        }

        return nearestId >= 0;
    }

    private void OnDrawGizmosSelected()
    {
        if (!showGizmos || nodes.Count == 0) return;

        for (int i = 0; i < nodes.Count; i++)
        {
            Vector3 position = GetWorldPosition(i, 0.2f);
            Gizmos.color = Color.cyan;
            Gizmos.DrawSphere(position, 0.16f);

            Gizmos.color = Color.yellow;
            foreach (int neighbor in nodes[i].Neighbors)
            {
                if (neighbor > i)
                    Gizmos.DrawLine(position, GetWorldPosition(neighbor, 0.2f));
            }
        }
    }
}
