using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class RouteLineAppearance : MonoBehaviour
{
    public enum VisualStyle
    {
        SoftLine,
        Arrows
    }

    [Header("References")]
    [SerializeField] private RouteDrawer routeDrawer;
    [SerializeField] private LineRenderer routeLine;
    [SerializeField] private Material visualMaterial;

    [Header("Style")]
    [SerializeField]
    private VisualStyle style =
        VisualStyle.Arrows;

    [SerializeField]
    private Color routeColor =
        new Color(0.1f, 0.9f, 1f, 1f);

    [SerializeField, Range(0f, 1f)]
    private float opacity = 0.55f;

    [Header("Line")]
    [SerializeField, Min(0.01f)]
    private float lineWidth = 0.35f;

    [Header("Arrows")]
    [SerializeField, Min(0.1f)]
    private float arrowSpacing = 5f;

    [SerializeField, Min(0.1f)]
    private float arrowLength = 3f;

    [SerializeField, Min(0.1f)]
    private float arrowWidth = 1.4f;

    [SerializeField, Min(0f)]
    private float arrowHeightOffset = 0.08f;

    private GameObject arrowObject;
    private Mesh arrowMesh;
    private MeshRenderer arrowRenderer;

    private MaterialPropertyBlock colorBlock;

    private readonly List<Vector3> path =
        new List<Vector3>();

    private readonly List<Vector3> vertices =
        new List<Vector3>();

    private readonly List<int> triangles =
        new List<int>();

    private void Awake()
    {
        if (routeDrawer == null)
            routeDrawer = GetComponent<RouteDrawer>();

        if (routeLine == null)
            routeLine = GetComponentInChildren<LineRenderer>();

    }


    private bool initialized;

    private void OnEnable()
    {
        if (routeDrawer != null)
            routeDrawer.RouteChanged += OnRouteChanged;

        if (initialized)
            RefreshVisual();
    }

    private void Start()
    {
        initialized = true;
        RefreshVisual();
    }

    private void OnDisable()
    {
        if (routeDrawer != null)
            routeDrawer.RouteChanged -= OnRouteChanged;
    }

    private void OnRouteChanged(int pointCount)
    {
        RefreshVisual();
    }

    private void CreateArrowRenderer()
    {
        if (arrowObject != null)
            return;

        arrowObject = new GameObject("RouteArrowsVisual");
        arrowObject.transform.SetParent(transform, false);

        if (routeLine != null)
        {
            arrowObject.layer =
                routeLine.gameObject.layer;
        }

        MeshFilter filter =
            arrowObject.AddComponent<MeshFilter>();

        arrowRenderer =
            arrowObject.AddComponent<MeshRenderer>();

        arrowRenderer.shadowCastingMode =
            ShadowCastingMode.Off;

        arrowRenderer.receiveShadows = false;

        arrowMesh = new Mesh();
        arrowMesh.name = "ROport Route Arrows";
        arrowMesh.MarkDynamic();

        filter.sharedMesh = arrowMesh;
    }

    public void RefreshVisual()
    {
        if (routeLine == null)
            return;

        if (arrowObject == null)
            CreateArrowRenderer();

        Material material = visualMaterial != null
            ? visualMaterial
            : routeLine.sharedMaterial;

        if (material != null)
        {
            routeLine.sharedMaterial = material;
            arrowRenderer.sharedMaterial = material;
        }

        // Estilo da linha convencional.
        routeLine.widthMultiplier = 1f;
        routeLine.startWidth = lineWidth;
        routeLine.endWidth = lineWidth;

        routeLine.startColor = Color.white;
        routeLine.endColor = Color.white;

        // Cor e transparencia aplicadas
        // aos dois tipos de visual.
        if (colorBlock == null)
            colorBlock = new MaterialPropertyBlock();

        Color finalColor = routeColor;
        finalColor.a = opacity;

        colorBlock.Clear();
        colorBlock.SetColor("_BaseColor", finalColor);
        colorBlock.SetColor("_Color", finalColor);

        routeLine.SetPropertyBlock(colorBlock);
        arrowRenderer.SetPropertyBlock(colorBlock);

        bool useArrows = style == VisualStyle.Arrows;

        routeLine.enabled = !useArrows;
        arrowRenderer.enabled = useArrows;

        if (useArrows)
            RebuildArrows();
    }

    private void RebuildArrows()
    {
        if (routeLine == null || arrowMesh == null)
            return;

        path.Clear();
        vertices.Clear();
        triangles.Clear();

        int count = routeLine.positionCount;

        for (int i = 0; i < count; i++)
        {
            Vector3 position = routeLine.GetPosition(i);

            if (!routeLine.useWorldSpace)
            {
                position =
                    routeLine.transform.TransformPoint(position);
            }

            path.Add(position);
        }

        if (path.Count < 2)
        {
            arrowMesh.Clear();
            return;
        }

        float totalLength = 0f;

        for (int i = 1; i < path.Count; i++)
        {
            totalLength += Vector3.Distance(
                path[i - 1],
                path[i]);
        }

        float step = Mathf.Max(
            arrowSpacing,
            arrowLength + 0.1f);

        float halfLength = arrowLength * 0.5f;

        for (
            float distance = halfLength;
            distance + halfLength <= totalLength;
            distance += step)
        {
            if (!SamplePath(
                distance,
                out Vector3 center,
                out Vector3 direction))
            {
                continue;
            }

            AddArrow(center, direction);
        }

        arrowMesh.Clear();
        arrowMesh.SetVertices(vertices);
        arrowMesh.SetTriangles(triangles, 0);
        arrowMesh.RecalculateBounds();
        arrowMesh.RecalculateNormals();
    }

    private bool SamplePath(
        float distance,
        out Vector3 point,
        out Vector3 direction)
    {
        point = Vector3.zero;
        direction = Vector3.forward;

        float accumulated = 0f;

        for (int i = 1; i < path.Count; i++)
        {
            Vector3 start = path[i - 1];
            Vector3 end = path[i];

            float segmentLength =
                Vector3.Distance(start, end);

            if (segmentLength < 0.0001f)
                continue;

            if (accumulated + segmentLength >= distance)
            {
                float t =
                    (distance - accumulated) /
                    segmentLength;

                point = Vector3.Lerp(start, end, t);

                direction = end - start;
                direction.y = 0f;

                if (direction.sqrMagnitude < 0.0001f)
                    return false;

                direction.Normalize();
                return true;
            }

            accumulated += segmentLength;
        }

        return false;
    }

    private void AddArrow(
        Vector3 center,
        Vector3 forward)
    {
        center += Vector3.up * arrowHeightOffset;

        Vector3 right =
            new Vector3(forward.z, 0f, -forward.x);

        float halfLength = arrowLength * 0.5f;
        float headLength = arrowLength * 0.4f;

        float halfHeadWidth = arrowWidth * 0.5f;
        float halfShaftWidth = arrowWidth * 0.14f;

        Vector3 tail =
            center - forward * halfLength;

        Vector3 headBase =
            center + forward * (halfLength - headLength);

        Vector3 tip =
            center + forward * halfLength;

        int startIndex = vertices.Count;

        AddVertex(tail - right * halfShaftWidth);
        AddVertex(headBase - right * halfShaftWidth);
        AddVertex(headBase + right * halfShaftWidth);
        AddVertex(tail + right * halfShaftWidth);

        AddVertex(headBase - right * halfHeadWidth);
        AddVertex(tip);
        AddVertex(headBase + right * halfHeadWidth);

        // Corpo da seta.
        AddTriangle(startIndex, startIndex + 1,
            startIndex + 2);

        AddTriangle(startIndex, startIndex + 2,
            startIndex + 3);

        // Ponta da seta.
        AddTriangle(startIndex + 4,
            startIndex + 5,
            startIndex + 6);
    }

    private void AddVertex(Vector3 worldPosition)
    {
        vertices.Add(
            arrowObject.transform.InverseTransformPoint(
                worldPosition));
    }

    private void AddTriangle(int a, int b, int c)
    {
        triangles.Add(a);
        triangles.Add(b);
        triangles.Add(c);
    }

    private void OnDestroy()
    {
        if (arrowMesh != null)
            Destroy(arrowMesh);
    }
}