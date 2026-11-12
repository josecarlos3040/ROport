using UnityEngine;

// Representa um trecho navegavel da cidade.
[RequireComponent(typeof(BoxCollider))]
public class RoadSegment : MonoBehaviour
{
    public enum RoadAxis
    {
        HorizontalX,
        VerticalZ
    }

    [SerializeField]
    private RoadAxis axis = RoadAxis.HorizontalX;

    [Header("Trechos com elevacao")]
    [SerializeField]
    private bool useCustomEndpoints = false;

    [SerializeField]
    private Transform startPoint;

    [SerializeField]
    private Transform endPoint;

    public RoadAxis Axis => axis;

    public BoxCollider Collider =>
        GetComponent<BoxCollider>();

    public bool UseCustomEndpoints => useCustomEndpoints;

    public bool TryGetEndpoints(
        out Vector3 start,
        out Vector3 end)
    {
        start = end = Vector3.zero;

        if (!useCustomEndpoints ||
            startPoint == null ||
            endPoint == null)
        {
            return false;
        }

        start = startPoint.position;
        end = endPoint.position;

        return true;
    }
}