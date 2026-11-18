using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class RoadSegment : MonoBehaviour
{
    public enum RoadAxis
    {
        HorizontalX,
        VerticalZ
    }

    [SerializeField] private RoadAxis axis = RoadAxis.HorizontalX;

    [Header("Elevation")]
    [SerializeField] private bool useCustomEndpoints;
    [SerializeField] private Transform startPoint;
    [SerializeField] private Transform endPoint;

    [Header("Movement Restrictions")]
    [SerializeField]
    private bool downhillOnly = false;

    public bool DownhillOnly => downhillOnly;

    public RoadAxis Axis => axis;
    public bool UseCustomEndpoints => useCustomEndpoints;
    public BoxCollider Collider => GetComponent<BoxCollider>();

    public bool TryGetEndpoints(out Vector3 start, out Vector3 end)
    {
        start = Vector3.zero;
        end = Vector3.zero;

        if (!useCustomEndpoints || startPoint == null || endPoint == null)
            return false;

        start = startPoint.position;
        end = endPoint.position;
        return true;
    }
}