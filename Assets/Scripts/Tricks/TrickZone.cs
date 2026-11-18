using UnityEngine;

public enum TrickAnimation
{
    Jump,
    Spin,
    JumpAndSpin,
    Grind
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

    [Header("Zones")]
    [SerializeField] private BoxCollider approachCollider;
    [SerializeField] private BoxCollider traversalCollider;

    [Header("Scoring")]
    [SerializeField, Min(1)] private int basePoints = 100;
    [SerializeField] private ZoneAxis scoringAxis = ZoneAxis.Z;

    [SerializeField, Range(0f, 1f)]
    private float perfectWindow = 0.25f;

    [SerializeField, Range(0f, 1f)]
    private float goodWindow = 0.65f;

    [Header("Movement Direction")]
    public bool OnlyDescending => onlyDescending;
    [SerializeField] private bool onlyDescending;
    [SerializeField]
    private Vector3 localDownhillDirection =
        Vector3.back;

    [SerializeField, Range(0f, 1f)]
    private float directionTolerance = 0.7f;

    [Header("Speed")]
    [SerializeField] private bool affectsTravelSpeed = true;

    public string TrickName => trickName;
    public TrickAnimation Animation => animation;
    public bool AffectsTravelSpeed => affectsTravelSpeed;

    private void Awake()
    {
        if (traversalCollider == null)
            traversalCollider = GetComponent<BoxCollider>();

        if (approachCollider == null)
        {
            Debug.LogWarning(
                "[ROport] Approach Collider nao configurado: " +
                name, this);
        }

        if (approachCollider != null)
            approachCollider.isTrigger = true;

        if (traversalCollider != null)
            traversalCollider.isTrigger = true;
    }

    public bool IsInApproach(Vector3 position)
    {
        return IsInside(approachCollider, position);
    }

    public bool IsInTraversal(Vector3 position)
    {
        return IsInside(traversalCollider, position);
    }

    // Mantido para compatibilidade com
    // eventuais scripts antigos.
    public bool ContainsPoint(Vector3 position)
    {
        return IsInTraversal(position);
    }

    private bool IsInside(
        BoxCollider box,
        Vector3 worldPosition)
    {
        if (!isActiveAndEnabled ||
            box == null ||
            !box.enabled ||
            !box.gameObject.activeInHierarchy)
        {
            return false;
        }

        Vector3 local =
            box.transform.InverseTransformPoint(worldPosition);

        Vector3 difference = local - box.center;
        Vector3 half = box.size * 0.5f;

        const float tolerance = 0.01f;

        return
            Mathf.Abs(difference.x) <= half.x + tolerance &&
            Mathf.Abs(difference.y) <= half.y + tolerance &&
            Mathf.Abs(difference.z) <= half.z + tolerance;
    }

    // Verifica a direcao ANTES da rampa.
    // Nao exige movimento vertical, pois o Player
    // ainda pode estar numa rua plana.
    public bool AllowsDirection(Vector3 movementDirection)
    {
        Vector3 movement = movementDirection;
        movement.y = 0f;

        if (movement.sqrMagnitude < 0.0001f)
            return false;

        if (!onlyDescending)
            return true;

        Vector3 downhill = transform.TransformDirection(
            localDownhillDirection);

        downhill.y = 0f;

        if (downhill.sqrMagnitude < 0.0001f)
            return false;

        float alignment = Vector3.Dot(
            movement.normalized,
            downhill.normalized);

        return alignment >= directionTolerance;
    }

    // Avalia o momento em que F foi apertado
    // dentro da area de preparacao.
    public bool EvaluateApproachTiming(
        Vector3 position,
        out int points,
        out string rating)
    {
        points = 0;
        rating = "ERROU!";

        if (!IsInApproach(position))
            return false;

        float progress = GetProgress(
            approachCollider,
            position,
            Vector3.zero);

        float distanceFromCenter =
            Mathf.Abs(progress - 0.5f) * 2f;

        if (distanceFromCenter <= perfectWindow)
        {
            points = basePoints * 2;
            rating = "PERFEITA!";
            return true;
        }

        if (distanceFromCenter <=
            Mathf.Max(goodWindow, perfectWindow))
        {
            points = Mathf.RoundToInt(basePoints * 1.5f);
            rating = "BOA!";
            return true;
        }

        return false;
    }

    // Mantido para compatibilidade.
    public bool EvaluateTiming(
        Vector3 position,
        out int points,
        out string rating)
    {
        return EvaluateApproachTiming(
            position, out points, out rating);
    }

    // Retorna 0 na entrada e 1 na saida
    // da zona de execucao.
    public float GetTraversalProgress(
        Vector3 position,
        Vector3 movementDirection)
    {
        return GetProgress(
            traversalCollider,
            position,
            movementDirection);
    }

    private float GetProgress(
        BoxCollider box,
        Vector3 worldPosition,
        Vector3 movementDirection)
    {
        if (box == null)
            return 0f;

        Vector3 localPosition =
            box.transform.InverseTransformPoint(worldPosition)
            - box.center;

        Vector3 halfSize = box.size * 0.5f;

        float coordinate = scoringAxis == ZoneAxis.X
            ? localPosition.x
            : localPosition.z;

        float halfLength = scoringAxis == ZoneAxis.X
            ? halfSize.x
            : halfSize.z;

        float progress = Mathf.InverseLerp(
            -halfLength,
            halfLength,
            coordinate);

        if (movementDirection.sqrMagnitude > 0.0001f)
        {
            Vector3 localDirection =
                box.transform.InverseTransformDirection(
                    movementDirection);

            float axisDirection = scoringAxis == ZoneAxis.X
                ? localDirection.x
                : localDirection.z;

            if (axisDirection < 0f)
                progress = 1f - progress;
        }

        return Mathf.Clamp01(progress);
    }

    private void OnDrawGizmosSelected()
    {
        DrawZone(approachCollider, Color.yellow);

        BoxCollider execution = traversalCollider != null
            ? traversalCollider
            : GetComponent<BoxCollider>();

        DrawZone(execution, Color.cyan);
    }

    private void DrawZone(BoxCollider box, Color color)
    {
        if (box == null)
            return;

        Matrix4x4 originalMatrix = Gizmos.matrix;

        Gizmos.matrix = box.transform.localToWorldMatrix;
        Gizmos.color = color;

        Gizmos.DrawWireCube(box.center, box.size);

        Gizmos.matrix = originalMatrix;
    }
}