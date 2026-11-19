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
    [SerializeField]
    private string trickName = "Salto 360";

    [SerializeField]
    private TrickAnimation animation =
        TrickAnimation.JumpAndSpin;

    [Header("Zones")]
    [SerializeField]
    private BoxCollider approachCollider;

    [SerializeField]
    private BoxCollider approachCollider2;

    [SerializeField]
    private BoxCollider traversalCollider;

    [Header("Scoring")]
    [SerializeField, Min(1)]
    private int basePoints = 100;

    [SerializeField]
    private ZoneAxis scoringAxis = ZoneAxis.Z;

    [SerializeField, Range(0f, 1f)]
    private float perfectWindow = 0.25f;

    [SerializeField, Range(0f, 1f)]
    private float goodWindow = 0.65f;

    [Header("Movement Direction")]
    [SerializeField]
    private bool onlyDescending = false;

    [SerializeField]
    private Vector3 localDownhillDirection =
        Vector3.back;

    [SerializeField, Range(0f, 1f)]
    private float directionTolerance = 0.7f;

    [Header("Speed")]
    [SerializeField]
    private bool affectsTravelSpeed = true;

    public string TrickName => trickName;
    public TrickAnimation Animation => animation;
    public bool OnlyDescending => onlyDescending;
    public bool AffectsTravelSpeed => affectsTravelSpeed;

    private void Awake()
    {
        if (traversalCollider == null)
            traversalCollider = GetComponent<BoxCollider>();

        if (traversalCollider != null)
            traversalCollider.isTrigger = true;

        if (approachCollider != null)
            approachCollider.isTrigger = true;

        if (approachCollider2 != null)
            approachCollider2.isTrigger = true;
    }

    private void OnValidate()
    {
        if (traversalCollider == null)
            traversalCollider = GetComponent<BoxCollider>();

        goodWindow = Mathf.Max(
            goodWindow, perfectWindow);
    }

    // Detecta a posicao mesmo com colliders
    // rotacionados ou em objetos filhos.
    private bool IsInsideCollider(
        BoxCollider box,
        Vector3 worldPosition)
    {
        if (box == null ||
            !box.enabled ||
            !box.gameObject.activeInHierarchy)
            return false;

        Vector3 localPosition =
            box.transform.InverseTransformPoint(
                worldPosition);

        Vector3 difference =
            localPosition - box.center;

        Vector3 halfSize = box.size * 0.5f;

        return
            Mathf.Abs(difference.x) <= halfSize.x &&
            Mathf.Abs(difference.y) <= halfSize.y &&
            Mathf.Abs(difference.z) <= halfSize.z;
    }

    private BoxCollider GetCurrentApproach(
        Vector3 worldPosition)
    {
        if (IsInsideCollider(
            approachCollider, worldPosition))
        {
            return approachCollider;
        }

        if (IsInsideCollider(
            approachCollider2, worldPosition))
        {
            return approachCollider2;
        }

        return null;
    }

    // Agora reconhece dois Approaches.
    public bool IsInApproach(Vector3 worldPosition)
    {
        if (!isActiveAndEnabled)
            return false;

        return GetCurrentApproach(worldPosition) != null;
    }

    public bool IsInTraversal(Vector3 worldPosition)
    {
        if (!isActiveAndEnabled)
            return false;

        return IsInsideCollider(
            traversalCollider,
            worldPosition);
    }

    // Compatibilidade com scripts antigos.
    public bool ContainsPoint(Vector3 worldPosition)
    {
        return IsInTraversal(worldPosition);
    }

    public bool AllowsDirection(
        Vector3 movementDirection)
    {
        if (movementDirection.sqrMagnitude < 0.0001f)
            return false;

        if (!onlyDescending)
            return true;

        if (movementDirection.y >= -0.02f)
            return false;

        if (localDownhillDirection.sqrMagnitude <
            0.0001f)
            return false;

        Vector3 downhill =
            transform.TransformDirection(
                localDownhillDirection.normalized);

        float alignment = Vector3.Dot(
            movementDirection.normalized,
            downhill.normalized);

        return alignment >= directionTolerance;
    }

    // Calcula o timing no Approach correto.
    public bool EvaluateApproachTiming(
        Vector3 worldPosition,
        out int points,
        out string rating)
    {
        points = 0;
        rating = "ERROU!";

        BoxCollider usedCollider =
            GetCurrentApproach(worldPosition);

        if (usedCollider == null)
            return false;

        return EvaluateColliderTiming(
            usedCollider,
            worldPosition,
            out points,
            out rating);
    }

    private bool EvaluateColliderTiming(
        BoxCollider box,
        Vector3 worldPosition,
        out int points,
        out string rating)
    {
        points = 0;
        rating = "ERROU!";

        if (box == null)
            return false;

        Vector3 localPosition =
            box.transform.InverseTransformPoint(
                worldPosition);

        Vector3 difference =
            localPosition - box.center;

        Vector3 halfSize =
            box.size * 0.5f;

        float distanceFromCenter;
        float halfLength;

        if (scoringAxis == ZoneAxis.X)
        {
            distanceFromCenter =
                Mathf.Abs(difference.x);

            halfLength = halfSize.x;
        }
        else
        {
            distanceFromCenter =
                Mathf.Abs(difference.z);

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
            points = Mathf.RoundToInt(
                basePoints * 1.5f);

            rating = "BOA!";
            return true;
        }

        return false;
    }

    // Compatibilidade com a versao anterior.
    public bool EvaluateTiming(
        Vector3 worldPosition,
        out int points,
        out string rating)
    {
        points = 0;
        rating = "ERROU!";

        if (!IsInTraversal(worldPosition))
            return false;

        return EvaluateColliderTiming(
            traversalCollider,
            worldPosition,
            out points,
            out rating);
    }

    // Progresso da travessia: 0 a 1.
    // Usado para animar saltos e grinds.
    public float GetTraversalProgress(
        Vector3 worldPosition,
        Vector3 movementDirection)
    {
        if (traversalCollider == null)
            return 0f;

        Vector3 localPosition =
            traversalCollider.transform
                .InverseTransformPoint(worldPosition);

        Vector3 localDirection =
            traversalCollider.transform
                .InverseTransformDirection(
                    movementDirection);

        Vector3 difference =
            localPosition - traversalCollider.center;

        float coordinate;
        float halfLength;
        float directionSign;

        if (scoringAxis == ZoneAxis.X)
        {
            coordinate = difference.x;
            halfLength =
                traversalCollider.size.x * 0.5f;

            directionSign = localDirection.x;
        }
        else
        {
            coordinate = difference.z;
            halfLength =
                traversalCollider.size.z * 0.5f;

            directionSign = localDirection.z;
        }

        float progress =
            (coordinate /
            Mathf.Max(halfLength, 0.001f) + 1f)
            * 0.5f;

        // Inverte o progresso caso o personagem
        // atravesse a zona no sentido contrario.
        if (directionSign < 0f)
            progress = 1f - progress;

        return Mathf.Clamp01(progress);
    }

    private void DrawCollider(
        BoxCollider box,
        Color color)
    {
        if (box == null)
            return;

        Gizmos.matrix =
            box.transform.localToWorldMatrix;

        Gizmos.color = color;

        Gizmos.DrawWireCube(
            box.center,
            box.size);
    }

    private void OnDrawGizmosSelected()
    {
        Matrix4x4 originalMatrix = Gizmos.matrix;

        DrawCollider(
            traversalCollider != null
                ? traversalCollider
                : GetComponent<BoxCollider>(),
            Color.cyan);

        DrawCollider(
            approachCollider,
            Color.yellow);

        DrawCollider(
            approachCollider2,
            Color.green);

        Gizmos.matrix = originalMatrix;
    }
}