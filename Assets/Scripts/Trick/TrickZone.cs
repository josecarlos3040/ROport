using UnityEngine;

public enum TrickAnimation
{
    Jump,
    Spin,
    JumpAndSpin
}

[RequireComponent(typeof(BoxCollider))]
public class TrickZone : MonoBehaviour
{
    public enum ZoneAxis
    {
        X,
        Z
    }

    [Header("Trick")]
    [SerializeField] private string trickName = "Salto 360";
    [SerializeField]
    private TrickAnimation animation =
        TrickAnimation.JumpAndSpin;

    [Header("Scoring")]
    [SerializeField, Min(1)]
    private int basePoints = 100;

    [SerializeField]
    private ZoneAxis scoringAxis = ZoneAxis.Z;

    private BoxCollider zoneCollider;

    public string TrickName => trickName;
    public TrickAnimation Animation => animation;

    private void Awake()
    {
        zoneCollider = GetComponent<BoxCollider>();
        zoneCollider.isTrigger = true;
    }

    public bool ContainsPoint(Vector3 worldPosition)
    {
        if (!isActiveAndEnabled)
            return false;

        if (zoneCollider == null)
            zoneCollider = GetComponent<BoxCollider>();

        // Converte a posicao para o espaco da area.
        Vector3 localPoint =
            transform.InverseTransformPoint(worldPosition);

        Vector3 difference =
            localPoint - zoneCollider.center;

        Vector3 halfSize = zoneCollider.size * 0.5f;

        return
            Mathf.Abs(difference.x) <= halfSize.x &&
            Mathf.Abs(difference.y) <= halfSize.y &&
            Mathf.Abs(difference.z) <= halfSize.z;
    }

    public int CalculateScore(
        Vector3 worldPosition,
        out string rating)
    {
        Vector3 localPoint =
            transform.InverseTransformPoint(worldPosition);

        Vector3 difference =
            localPoint - zoneCollider.center;

        Vector3 halfSize = zoneCollider.size * 0.5f;

        float distanceFromCenter;
        float halfLength;

        if (scoringAxis == ZoneAxis.X)
        {
            distanceFromCenter = Mathf.Abs(difference.x);
            halfLength = halfSize.x;
        }
        else
        {
            distanceFromCenter = Mathf.Abs(difference.z);
            halfLength = halfSize.z;
        }

        float normalizedDistance =
            distanceFromCenter / Mathf.Max(halfLength, 0.001f);

        if (normalizedDistance <= 0.25f)
        {
            rating = "PERFEITA!";
            return basePoints * 2;
        }

        if (normalizedDistance <= 0.65f)
        {
            rating = "BOA!";
            return Mathf.RoundToInt(basePoints * 1.5f);
        }

        rating = "NORMAL";
        return basePoints;
    }

    private void OnDrawGizmosSelected()
    {
        BoxCollider box = GetComponent<BoxCollider>();

        if (box == null)
            return;

        Matrix4x4 oldMatrix = Gizmos.matrix;
        Gizmos.matrix = transform.localToWorldMatrix;

        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(box.center, box.size);

        Gizmos.matrix = oldMatrix;
    }
}