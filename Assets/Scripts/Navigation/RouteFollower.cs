using System;
using System.Collections.Generic;
using UnityEngine;

public class RouteFollower : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform character;

    [Header("Movement")]
    [SerializeField, Min(0.1f)]
    private float movementSpeed = 4.5f;

    [SerializeField, Min(0f)]
    private float turnSpeed = 12f;

    [SerializeField, Min(0.01f)]
    private float waypointTolerance = 0.08f;

    [Header("Obstacle Detection")]
    [SerializeField] private LayerMask obstacleMask;

    [SerializeField, Min(0.1f)]
    private float collisionRadius = 0.35f;

    [SerializeField, Min(0.5f)]
    private float collisionHeight = 1.8f;

    [SerializeField, Min(0.01f)]
    private float collisionSkin = 0.05f;

    [Header("Jump Collision")]
    [SerializeField] private CapsuleCollider playerCollider;

    [Header("Blocked Route")]
    [SerializeField, Min(0.1f)]
    private float blockedWaitDuration = 3f;

    [Header("Trick Obstacle Penalty")]
    [SerializeField, Range(0.1f, 1f)]
    private float trickHitSpeedMultiplier = 0.4f;

    [SerializeField, Min(0.1f)]
    private float trickHitSlowdownDuration = 2f;

    private readonly List<Vector3> waypoints =
        new List<Vector3>();

    // Guarda os problemas de manobra ja atingidos
    // durante esta rota, evitando penalidades repetidas.
    private readonly HashSet<CityProblem> hitTrickProblems =
        new HashSet<CityProblem>();

    private readonly Collider[] overlapBuffer =
        new Collider[32];

    private readonly RaycastHit[] castBuffer =
        new RaycastHit[32];

    private Vector3 initialPosition;
    private Quaternion initialRotation;
    private Vector3 originalColliderCenter;

    private int targetIndex;
    private bool isFollowing;
    private bool isBlocked;
    private float blockedTimer;

    private float jumpOffset;

    // Velocidade das rampas e penalidades temporarias.
    private float rampSpeedMultiplier = 1f;
    private float speedMultiplier = 1f;

    private float recoveryDuration;
    private float recoveryElapsed;
    private float recoveryStartMultiplier = 1f;

    public bool IsFollowing => isFollowing;
    public bool IsBlocked => isBlocked;

    public float BlockTimeRemaining =>
        isBlocked ? Mathf.Max(0f, blockedTimer) : 0f;

    public float BlockWaitDuration => blockedWaitDuration;

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

    public Vector3 CurrentMoveDirection
    {
        get
        {
            if (!isFollowing ||
                isBlocked ||
                character == null ||
                targetIndex >= waypoints.Count)
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

    public event Action Completed;
    public event Action<Collider> Blocked;
    public event Action<CityProblem> Died;

    // Novo evento: o personagem bateu em
    // um obstaculo de manobra.
    public event Action<CityProblem> TrickObstacleHit;

    private void Awake()
    {
        if (character == null)
            character = transform;

        if (playerCollider == null)
            playerCollider = GetComponent<CapsuleCollider>();

        initialPosition = character.position;
        initialRotation = character.rotation;

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
            MovementSpeed * Time.deltaTime,
            distance);

        if (step <= 0f)
            return;

        Vector3 moveDirection = direction / distance;

        if (TryFindObstacle(
            moveDirection,
            step,
            out Collider obstacle,
            out float hitDistance,
            out bool isOverlapping))
        {
            HandleObstacleCollision(
                obstacle,
                moveDirection,
                step,
                hitDistance,
                isOverlapping);

            return;
        }

        character.position = Vector3.MoveTowards(
            character.position,
            target,
            step);

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

    // Retorna o primeiro obstaculo relevante.
    // Obstaculos Trick ja atingidos sao ignorados,
    // mas os outros continuam sendo detectados.
    private bool TryFindObstacle(
        Vector3 direction,
        float distance,
        out Collider obstacle,
        out float hitDistance,
        out bool isOverlapping)
    {
        obstacle = null;
        hitDistance = 0f;
        isOverlapping = false;

        GetCollisionCapsule(
            out Vector3 bottom,
            out Vector3 top);

        int overlapCount = Physics.OverlapCapsuleNonAlloc(
            bottom,
            top,
            collisionRadius,
            overlapBuffer,
            obstacleMask,
            QueryTriggerInteraction.Collide);

        for (int i = 0; i < overlapCount; i++)
        {
            Collider candidate = overlapBuffer[i];

            if (ShouldIgnoreObstacle(candidate))
                continue;

            obstacle = candidate;
            isOverlapping = true;
            return true;
        }

        int hitCount = Physics.CapsuleCastNonAlloc(
            bottom,
            top,
            collisionRadius,
            direction,
            castBuffer,
            distance + collisionSkin,
            obstacleMask,
            QueryTriggerInteraction.Collide);

        float nearestDistance = float.MaxValue;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = castBuffer[i];

            if (ShouldIgnoreObstacle(hit.collider))
                continue;

            if (hit.distance >= nearestDistance)
                continue;

            nearestDistance = hit.distance;
            obstacle = hit.collider;
        }

        if (obstacle == null)
            return false;

        hitDistance = nearestDistance;
        return true;
    }

    private void GetCollisionCapsule(
        out Vector3 bottom,
        out Vector3 top)
    {
        float halfSegment = Mathf.Max(
            0f,
            collisionHeight * 0.5f - collisionRadius);

        // O centro acompanha o salto.
        Vector3 center = playerCollider != null
            ? playerCollider.transform.TransformPoint(
                playerCollider.center)
            : character.position + Vector3.up * jumpOffset;

        bottom = center - Vector3.up * halfSegment;
        top = center + Vector3.up * halfSegment;
    }

    private bool ShouldIgnoreObstacle(Collider obstacle)
    {
        if (obstacle == null ||
            obstacle == playerCollider)
        {
            return true;
        }

        CityProblem problem =
            obstacle.GetComponentInParent<CityProblem>();

        if (problem == null)
            return false;

        if (problem.State == CityProblemState.Resolved)
            return true;

        return problem.ImpactType == CityProblemImpactType.Trick &&
               hitTrickProblems.Contains(problem);
    }

    private void HandleObstacleCollision(
        Collider obstacle,
        Vector3 direction,
        float step,
        float hitDistance,
        bool isOverlapping)
    {
        CityProblem problem = obstacle != null
            ? obstacle.GetComponentInParent<CityProblem>()
            : null;

        if (problem != null &&
            problem.ImpactType == CityProblemImpactType.Trick)
        {
            // Aplica a penalidade uma unica vez.
            HandleTrickObstacle(problem);

            // Continua movimentando o personagem
            // normalmente, sem bloquear a rota.
            character.position = Vector3.MoveTowards(
                character.position,
                waypoints[targetIndex],
                step);

            if (Vector3.Distance(
                character.position,
                waypoints[targetIndex]) <= waypointTolerance)
            {
                NextWaypoint();
            }

            return;
        }

        // Encosta antes do bloqueio, sem atravessa-lo.
        float safeDistance = isOverlapping
            ? 0f
            : Mathf.Clamp(
                hitDistance - collisionSkin,
                0f,
                step);

        character.position += direction * safeDistance;

        // Problema fatal ainda ativo: morte imediata.
        if (problem != null &&
            problem.State == CityProblemState.Active &&
            problem.ImpactType == CityProblemImpactType.Fatal)
        {
            Die(problem);
            return;
        }

        // Problema ja fotografado ou obstaculo comum:
        // mantem a espera de 3 segundos.
        EnterBlockedState(obstacle);
    }

    private void HandleTrickObstacle(CityProblem problem)
    {
        if (!hitTrickProblems.Add(problem))
            return;

        ApplySlowdown(
            trickHitSpeedMultiplier,
            trickHitSlowdownDuration);

        Debug.Log(
            "[ROport] Tropecou em: " +
            problem.DisplayName);

        // O TrickController recebe o evento
        // e executa a animacao de tropeço.
        TrickObstacleHit?.Invoke(problem);

        // Nao para a rota e nao provoca morte.
        // O problema sera ignorado nas proximas
        // verificacoes desta tentativa.
    }

    public bool WillTraverseTrickZone(
    TrickZone zone,
    float playerPivotHeight,
    float maxLookAhead = 35f)
    {
        if (zone == null ||
            character == null ||
            !isFollowing ||
            targetIndex >= waypoints.Count)
        {
            return false;
        }

        Vector3 from = character.position;
        float distanceChecked = 0f;

        bool wasInside = false;
        float entryHeight = 0f;

        const float sampleSpacing = 0.4f;
        const float minimumDescent = 0.35f;

        // Analisa os proximos pontos da rota desenhada.
        for (int i = targetIndex;
             i < waypoints.Count && distanceChecked < maxLookAhead;
             i++)
        {
            Vector3 to = waypoints[i];
            Vector3 difference = to - from;

            float segmentLength = difference.magnitude;

            if (segmentLength < 0.001f)
            {
                from = to;
                continue;
            }

            float remaining = Mathf.Min(
                segmentLength,
                maxLookAhead - distanceChecked);

            int samples = Mathf.Max(
                1,
                Mathf.CeilToInt(remaining / sampleSpacing));

            for (int j = 1; j <= samples; j++)
            {
                float distance =
                    remaining * (j / (float)samples);

                Vector3 samplePosition =
                    from + difference * (distance / segmentLength);

                Vector3 feet =
                    samplePosition - Vector3.up * playerPivotHeight;

                bool isInside = zone.IsInTraversal(feet);

                if (!isInside)
                {
                    wasInside = false;
                    continue;
                }

                // Manobras que permitem ambos os sentidos
                // precisam apenas atravessar a zona.
                if (!zone.OnlyDescending)
                    return true;

                // Para escadas, a rota precisa realmente
                // perder altura dentro da zona.
                if (!wasInside)
                {
                    wasInside = true;
                    entryHeight = feet.y;
                }
                else if (entryHeight - feet.y >= minimumDescent)
                {
                    return true;
                }
            }

            distanceChecked += remaining;
            from = to;
        }

        return false;
    }

    private void EnterBlockedState(Collider obstacle)
    {
        isBlocked = true;
        blockedTimer = blockedWaitDuration;

        Blocked?.Invoke(obstacle);
    }

    private void UpdateBlockedState()
    {
        blockedTimer -= Time.deltaTime;

        if (blockedTimer <= 0f)
            FinishRoute();
    }

    private void Die(CityProblem problem)
    {
        isFollowing = false;
        isBlocked = false;
        blockedTimer = 0f;

        Debug.Log(
            "[ROport] Player morreu: " +
            problem.DisplayName);

        Died?.Invoke(problem);
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

    // Mantem o Player no local em que parou.
    public void StopAtCurrentPosition()
    {
        ResetMovementState();

        waypoints.Clear();
        targetIndex = 0;
    }

    // Mantido para testes em que queremos
    // voltar ao spawn original.
    public void ResetFollower()
    {
        StopAtCurrentPosition();

        character.SetPositionAndRotation(
            initialPosition,
            initialRotation);
    }

    private void ResetMovementState()
    {
        isFollowing = false;
        isBlocked = false;
        blockedTimer = 0f;

        hitTrickProblems.Clear();

        rampSpeedMultiplier = 1f;
        speedMultiplier = 1f;

        recoveryDuration = 0f;
        recoveryElapsed = 0f;
        recoveryStartMultiplier = 1f;

        SetJumpOffset(0f);
    }

    public void SetRampSpeedMultiplier(float multiplier)
    {
        rampSpeedMultiplier =
            Mathf.Clamp(multiplier, 0.1f, 3f);
    }

    public void ApplySlowdown(float multiplier, float duration)
    {
        speedMultiplier =
            Mathf.Clamp(multiplier, 0.1f, 1f);

        recoveryStartMultiplier = speedMultiplier;
        recoveryDuration = Mathf.Max(0.1f, duration);
        recoveryElapsed = 0f;
    }

    private void UpdateSpeedRecovery()
    {
        if (speedMultiplier >= 1f ||
            recoveryDuration <= 0f)
        {
            return;
        }

        recoveryElapsed += Time.deltaTime;

        float progress = Mathf.Clamp01(
            recoveryElapsed / recoveryDuration);

        speedMultiplier = Mathf.Lerp(
            recoveryStartMultiplier,
            1f,
            progress);
    }

    // Move a capsula usada na colisao
    // junto com o salto visual.
    public void SetJumpOffset(float height)
    {
        jumpOffset = Mathf.Max(0f, height);

        if (playerCollider == null)
            return;

        playerCollider.center =
            originalColliderCenter +
            Vector3.up * jumpOffset;
    }

    // Usado pela barra de fotografia.
    // Calcula a distancia 3D restante dentro
    // da area de interacao do problema.
    public float GetRemainingDistanceInsideProblem(
        CityProblem problem)
    {
        if (problem == null ||
            character == null ||
            !isFollowing ||
            targetIndex >= waypoints.Count)
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

            float inside = 0f;
            float outside = 1f;

            for (int j = 0; j < 18; j++)
            {
                float middle = (inside + outside) * 0.5f;

                Vector3 testPosition =
                    Vector3.Lerp(start, end, middle);

                if (problem.IsPlayerNear(testPosition))
                    inside = middle;
                else
                    outside = middle;
            }

            return totalDistance + length * inside;
        }

        return totalDistance;
    }
}