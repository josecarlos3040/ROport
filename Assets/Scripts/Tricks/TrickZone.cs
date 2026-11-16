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

    [Header("Timing")]
    [SerializeField, Range(0f, 1f)]
    private float perfectWindow = 0.25f;

    [SerializeField, Range(0f, 1f)]
    private float goodWindow = 0.65f;

    [Header("Movement Direction")]
    [SerializeField]
    private bool onlyDescending = true;

    // Direcao da descida no espaco LOCAL da zona.
    // Na rampa que criamos, -Z representa a descida.
    [SerializeField]
    private Vector3 localDownhillDirection = Vector3.back;

    [SerializeField, Range(0f, 1f)]
    private float directionTolerance = 0.7f;

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

    // Verifica se o personagem esta realmente
    // descendo a escada ou rampa.
    public bool AllowsDirection(Vector3 movementDirection)
    {
        if (movementDirection.sqrMagnitude < 0.0001f)
            return false;

        if (!onlyDescending)
            return true;

        // Precisa existir movimento para baixo.
        if (movementDirection.y >= -0.02f)
            return false;

        Vector3 downhill =
            transform.TransformDirection(
                localDownhillDirection.normalized);

        float alignment = Vector3.Dot(
            movementDirection.normalized,
            downhill.normalized);

        return alignment >= directionTolerance;
    }

    // Retorna true se a manobra foi bem-sucedida.
    // Retorna false se o jogador errou o timing.
    public bool EvaluateTiming(
        Vector3 worldPosition,
        out int points,
        out string rating)
    {
        points = 0;
        rating = "ERROU!";

        if (!ContainsPoint(worldPosition))
            return false;

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
            distanceFromCenter /
            Mathf.Max(halfLength, 0.001f);

        if (normalizedDistance <= perfectWindow)
        {
            points = basePoints * 2;
            rating = "PERFEITA!";
            return true;
        }

        if (normalizedDistance <= goodWindow)
        {
            points = Mathf.RoundToInt(basePoints * 1.5f);
            rating = "BOA!";
            return true;
        }

        // Apertou nas bordas: caiu!
        return false;
    }

    private void OnDrawGizmosSelected()
    {
        BoxCollider box = GetComponent<BoxCollider>();

        if (box == null)
            return;

        Matrix4x4 originalMatrix = Gizmos.matrix;
        Gizmos.matrix = transform.localToWorldMatrix;

        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(box.center, box.size);

        Gizmos.matrix = originalMatrix;
    }
}