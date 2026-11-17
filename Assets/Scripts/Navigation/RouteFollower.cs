using System;
using System.Collections.Generic;
using UnityEngine;

public class RouteFollower : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform character;

    [Header("Movement")]
    [SerializeField, Min(0.1f)] private float movementSpeed = 4.5f;
    [SerializeField, Min(0f)] private float turnSpeed = 12f;
    [SerializeField, Min(0.01f)] private float waypointTolerance = 0.08f;

    [Header("Jump Collision")]
    [SerializeField] private CapsuleCollider playerCollider;
    private Vector3 originalColliderCenter;
    private float jumpOffset;

    [Header("Obstacle Detection")]
    [SerializeField] private LayerMask obstacleMask;
    [SerializeField, Min(0.1f)] private float collisionRadius = 0.35f;
    [SerializeField, Min(0.5f)] private float collisionHeight = 1.8f;
    [SerializeField, Min(0.01f)] private float collisionSkin = 0.05f;

    [Header("Blocked Route")]
    [SerializeField, Min(0.1f)] private float blockedWaitDuration = 3f;

    private readonly List<Vector3> waypoints = new List<Vector3>();

    private readonly Collider[] overlapBuffer = new Collider[8];

    private Vector3 initialPosition;
    private Quaternion initialRotation;

    private int targetIndex;
    private bool isFollowing;
    private bool isBlocked;
    private float blockedTimer;

    // Penalidade temporaria de velocidade.
    private float speedMultiplier = 1f;
    // Controla a velocidade dentro de rampas.
    private float rampSpeedMultiplier = 1f;
    private float recoveryStartMultiplier = 1f;
    private float recoveryDuration;
    private float recoveryElapsed;

    public float MovementSpeed
    {
        get
        {
            float multiplier = speedMultiplier < 1f
                ? Mathf.Min(speedMultiplier, rampSpeedMultiplier)
                : rampSpeedMultiplier;
            return movementSpeed * multiplier;
        }
    }


    public bool IsFollowing => isFollowing;
    public bool IsBlocked => isBlocked;

    public float BlockTimeRemaining =>
        isBlocked ? Mathf.Max(0f, blockedTimer) : 0f;

    public float BlockWaitDuration => blockedWaitDuration;

    public event Action Completed;
    public event Action<Collider> Blocked;

    // Notifica o RunManager quando o player morre.
    public event Action<CityProblem> Died;

    public Vector3 CurrentMoveDirection
    {
        get
        {
            if (!isFollowing || isBlocked ||
                character == null || targetIndex >= waypoints.Count)
            {
                return Vector3.zero;
            }

            Vector3 direction =
                waypoints[targetIndex] - character.position;

            return direction.sqrMagnitude > 0.0001f
                ? direction.normalized
                : Vector3.zero;
        }
    }
    public void SetRampSpeedMultiplier(float multiplier)
    {
        rampSpeedMultiplier = Mathf.Clamp(multiplier, 0.1f, 3f);
    }

    private void Awake()
    {
        if (character == null)
            character = transform;

        initialPosition = character.position;
        initialRotation = character.rotation;

        if (playerCollider == null)
            playerCollider = GetComponent<CapsuleCollider>();

        if (playerCollider != null)
            originalColliderCenter = playerCollider.center;
    }

    private void Update()
    {
        UpdateSpeedRecovery();

        if (!isFollowing)
            return;

        if (isBlocked)
        {
            UpdateBlockedState();
            return;
        }

        MoveAlongRoute();
    }

    private void MoveAlongRoute()
    {
        if (targetIndex >= waypoints.Count)
        {
            FinishRoute();
            return;
        }

        Vector3 target = waypoints[targetIndex];
        Vector3 direction = target - character.position;

        float distance = direction.magnitude;

        if (distance <= waypointTolerance)
        {
            NextWaypoint();
            return;
        }

        RotateCharacter(direction);

        float step = Mathf.Min(
            MovementSpeed * Time.deltaTime, distance);

        Vector3 normalizedDirection = direction / distance;


        if (DetectObstacle(
            normalizedDirection,
            step,
            out RaycastHit hit,
            out Collider overlappingCollider))
        {
            Collider obstacle = overlappingCollider != null
                ? overlappingCollider
                : hit.collider;

            float safeDistance = overlappingCollider != null
                ? 0f
                : Mathf.Clamp(hit.distance - collisionSkin, 0f, step);

            character.position += normalizedDirection * safeDistance;

            // Procura um CityProblem no objeto atingido
            // ou em seus objetos pais.
            CityProblem problem = obstacle != null
                ? obstacle.GetComponentInParent<CityProblem>()
                : null;

            // Problema ainda nao fotografado: morte.
            if (problem != null &&
                problem.State == CityProblemState.Active)
            {
                Die(problem);
                return;
            }

            // Problema fotografado ou obstaculo comum:
            // mantem o comportamento de bloqueio anterior.
            EnterBlockedState(obstacle);
            return;
        }

        character.position = Vector3.MoveTowards(
            character.position, target, step);

        if (Vector3.Distance(character.position, target)
            <= waypointTolerance)
        {
            NextWaypoint();
        }
    }

    private void RotateCharacter(Vector3 direction)
    {
        direction.y = 0f;

        if (direction.sqrMagnitude <= 0.0001f)
            return;

        Quaternion desiredRotation =
            Quaternion.LookRotation(direction.normalized);

        character.rotation = Quaternion.Slerp(
            character.rotation,
            desiredRotation,
            turnSpeed * Time.deltaTime);
    }

    private bool DetectObstacle(
        Vector3 direction,
        float distance,
        out RaycastHit hit,
        out Collider overlappingCollider)
    {
        hit = default;
        overlappingCollider = null;

        float halfSegment = Mathf.Max(
            0f, collisionHeight * 0.5f - collisionRadius);

        Vector3 collisionCenter = playerCollider != null
            ? playerCollider.transform.TransformPoint(playerCollider.center)
            : character.position + Vector3.up * jumpOffset;

        Vector3 bottom =
            collisionCenter - Vector3.up * halfSegment;

        Vector3 top =
            collisionCenter + Vector3.up * halfSegment;

        // Detecta obstaculos com os quais ja existe sobreposicao.
        int overlapCount = Physics.OverlapCapsuleNonAlloc(
            bottom,
            top,
            collisionRadius,
            overlapBuffer,
            obstacleMask,
            QueryTriggerInteraction.Ignore);

        if (overlapCount > 0)
        {
            overlappingCollider = overlapBuffer[0];
            return true;
        }

        // Detecta o bloqueio antes do proximo movimento.
        return Physics.CapsuleCast(
            bottom,
            top,
            collisionRadius,
            direction,
            out hit,
            distance + collisionSkin,
            obstacleMask,
            QueryTriggerInteraction.Ignore);
    }

    private void EnterBlockedState(Collider obstacle)
    {
        isBlocked = true;
        blockedTimer = blockedWaitDuration;

        Debug.Log(
            "[ROport] Rota bloqueada por: " +
            (obstacle != null ? obstacle.name : "obstaculo"));

        Blocked?.Invoke(obstacle);
    }

    private void UpdateBlockedState()
    {
        blockedTimer -= Time.deltaTime;

        if (blockedTimer <= 0f)
            FinishRoute();
    }

    private void NextWaypoint()
    {
        targetIndex++;

        if (targetIndex >= waypoints.Count)
            FinishRoute();
    }

    private void FinishRoute()
    {
        if (!isFollowing)
            return;

        isFollowing = false;
        isBlocked = false;
        blockedTimer = 0f;

        Completed?.Invoke();
    }

    public void Follow(IReadOnlyList<Vector3> route)
    {
        ResetMovementState();
        waypoints.Clear();

        if (route == null || route.Count < 2)
            return;

        for (int i = 0; i < route.Count; i++)
            waypoints.Add(route[i]);

        targetIndex = 1;
        isFollowing = true;
    }

    public void SetJumpOffset(float height)
    {
        jumpOffset = Mathf.Max(0f, height);

        if (playerCollider == null)
            return;

        // Levanta o Collider sem mover a rota do Player.
        playerCollider.center =
            originalColliderCenter +
            Vector3.up * jumpOffset;
    }

    public void ResetFollower()
    {
        ResetMovementState();
        waypoints.Clear();
        targetIndex = 0;

        character.SetPositionAndRotation(
            initialPosition, initialRotation);
    }

    private void ResetMovementState()
    {
        SetJumpOffset(0f);
        rampSpeedMultiplier = 1f;
        isFollowing = false;
        isBlocked = false;
        blockedTimer = 0f;

        speedMultiplier = 1f;
        recoveryStartMultiplier = 1f;
        recoveryElapsed = 0f;
        recoveryDuration = 0f;
    }

    public void ApplySlowdown(float multiplier, float duration)
    {
        speedMultiplier = Mathf.Clamp(multiplier, 0.1f, 1f);
        recoveryStartMultiplier = speedMultiplier;
        recoveryDuration = Mathf.Max(0.1f, duration);
        recoveryElapsed = 0f;
    }

    private void UpdateSpeedRecovery()
    {
        if (speedMultiplier >= 1f || recoveryDuration <= 0f)
            return;

        recoveryElapsed += Time.deltaTime;

        float progress =
            Mathf.Clamp01(recoveryElapsed / recoveryDuration);

        speedMultiplier = Mathf.Lerp(
            recoveryStartMultiplier, 1f, progress);
    }

    private void Die(CityProblem problem)
    {
        isFollowing = false;
        isBlocked = false;
        blockedTimer = 0f;

        Debug.Log(
            "[ROport] Player morreu ao colidir com: " +
            problem.DisplayName);

        Died?.Invoke(problem);
    }

    // Mede a distancia 3D restante ate sair da regiao
    // de um problema, considerando as curvas da rota.
    public float GetRemainingDistanceInsideProblem(CityProblem problem)
    {
        if (problem == null || character == null ||
            !isFollowing || targetIndex >= waypoints.Count)
        {
            return 0f;
        }

        Vector3 start = character.position;

        if (!problem.IsPlayerNear(start))
            return 0f;

        float totalDistance = 0f;

        for (int i = targetIndex; i < waypoints.Count; i++)
        {
            Vector3 end = waypoints[i];
            float length = Vector3.Distance(start, end);

            if (length <= 0.0001f)
            {
                start = end;
                continue;
            }

            if (problem.IsPlayerNear(end))
            {
                totalDistance += length;
                start = end;
                continue;
            }

            // Busca o primeiro ponto de saida da area.
            float inside = 0f;
            float outside = 1f;

            for (int j = 0; j < 18; j++)
            {
                float middle = (inside + outside) * 0.5f;

                if (problem.IsPlayerNear(
                    Vector3.Lerp(start, end, middle)))
                {
                    inside = middle;
                }
                else
                {
                    outside = middle;
                }
            }

            return totalDistance + length * inside;
        }

        return totalDistance;
    }
}