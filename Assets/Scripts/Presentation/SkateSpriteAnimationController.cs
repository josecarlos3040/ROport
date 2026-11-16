using UnityEngine;

public class SkateSpriteAnimationController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Animator animator;
    [SerializeField] private RouteFollower routeFollower;
    [SerializeField] private ROportRunManager runManager;

    [Header("Slope Detection")]
    [SerializeField, Range(0f, 45f)]
    private float minimumDescentAngle = 5f;

    [SerializeField, Min(0f)]
    private float descentGraceTime = 0.15f;

    private static readonly int IsMovingHash =
        Animator.StringToHash("IsMoving");

    private static readonly int IsDescendingHash =
        Animator.StringToHash("IsDescending");

    private float descentTimer;

    private void Awake()
    {
        if (animator == null)
            animator = GetComponent<Animator>();
    }

    private void Update()
    {
        if (animator == null ||
            routeFollower == null ||
            runManager == null)
        {
            return;
        }

        UpdateAnimationState();
    }

    private void UpdateAnimationState()
    {
        Vector3 direction = routeFollower.CurrentMoveDirection;

        bool isMoving =
            runManager.Phase == RunPhase.Executing &&
            routeFollower.IsFollowing &&
            !routeFollower.IsBlocked &&
            direction.sqrMagnitude > 0.0001f;

        // Detecta descida usando a inclinacao
        // real do movimento tridimensional.
        float descentThreshold = Mathf.Sin(
            minimumDescentAngle * Mathf.Deg2Rad);

        bool isDescendingNow =
            isMoving &&
            direction.y < -descentThreshold;

        if (isDescendingNow)
        {
            descentTimer = descentGraceTime;
        }
        else
        {
            descentTimer = Mathf.Max(
                0f,
                descentTimer - Time.deltaTime);
        }

        // Pequena tolerancia para evitar que
        // a animacao pisque entre degraus.
        bool isDescending =
            isMoving && descentTimer > 0f;

        animator.SetBool(IsMovingHash, isMoving);
        animator.SetBool(IsDescendingHash, isDescending);
    }

    private void OnDisable()
    {
        descentTimer = 0f;
    }
}