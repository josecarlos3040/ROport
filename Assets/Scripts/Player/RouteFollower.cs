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

    [Header("Blocked Route")]
    [SerializeField, Min(0.1f)]
    private float blockedWaitDuration = 3f;

    private readonly List<Vector3> waypoints =
        new List<Vector3>();

    private Vector3 initialPosition;
    private Quaternion initialRotation;

    private int targetIndex;
    private bool isFollowing;
    private bool isBlocked;
    private float blockedTimer;

    public float MovementSpeed => movementSpeed;
    public bool IsBlocked => isBlocked;

    public float BlockTimeRemaining =>
        isBlocked ? blockedTimer : 0f;

    public float BlockWaitDuration => blockedWaitDuration;

    public event Action Completed;
    public event Action<Collider> Blocked;

    private void Awake()
    {
        if (character == null)
            character = transform;

        initialPosition = character.position;
        initialRotation = character.rotation;
    }

    private void Update()
    {
        if (!isFollowing || targetIndex >= waypoints.Count)
            return;

        // Quando encontra um bloqueio, fica parado
        // por alguns segundos para permitir a fotografia.
        if (isBlocked)
        {
            blockedTimer -= Time.deltaTime;

            if (blockedTimer <= 0f)
                FinishRoute();

            return;
        }

        Vector3 target = waypoints[targetIndex];
        Vector3 toTarget = target - character.position;

        float distance = toTarget.magnitude;

        if (distance <= waypointTolerance)
        {
            NextWaypoint();
            return;
        }

        // Rotaciona apenas no plano horizontal.
        Vector3 horizontalDirection = toTarget;
        horizontalDirection.y = 0f;

        if (horizontalDirection.sqrMagnitude > 0.0001f)
        {
            Quaternion desiredRotation =
                Quaternion.LookRotation(
                    horizontalDirection.normalized);

            character.rotation = Quaternion.Slerp(
                character.rotation,
                desiredRotation,
                turnSpeed * Time.deltaTime);
        }

        float step = Mathf.Min(
            movementSpeed * Time.deltaTime,
            distance);

        Vector3 direction = toTarget / distance;

        // NOVO: verifica o caminho antes de se mover.
        if (DetectObstacle(direction, step, out RaycastHit hit))
        {
            // Aproxima-se do obstaculo sem atravessa-lo.
            float safeDistance = Mathf.Clamp(
                hit.distance - collisionSkin,
                0f,
                step);

            character.position += direction * safeDistance;

            isBlocked = true;
            blockedTimer = blockedWaitDuration;

            Debug.Log(
                "[ROport] Caminho bloqueado: " +
                (hit.collider != null
                    ? hit.collider.name
                    : "obstaculo sobreposto"));

            Blocked?.Invoke(hit.collider);
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

    private bool DetectObstacle(
        Vector3 direction,
        float distance,
        out RaycastHit hit)
    {
        hit = default;

        float radius = collisionRadius;

        // Pontas da capsula de colisao vertical.
        float halfSegment = Mathf.Max(
            0f,
            collisionHeight * 0.5f - radius);

        Vector3 bottom =
            character.position - Vector3.up * halfSegment;

        Vector3 top =
            character.position + Vector3.up * halfSegment;

        // Caso o personagem ja esteja sobreposto
        // a algum obstaculo.
        if (Physics.CheckCapsule(
            bottom,
            top,
            radius,
            obstacleMask.value,
            QueryTriggerInteraction.Ignore))
        {
            return true;
        }

        // Verifica se o proximo movimento encontrara
        // algum bloqueio.
        return Physics.CapsuleCast(
            bottom,
            top,
            radius,
            direction,
            out hit,
            distance + collisionSkin,
            obstacleMask.value,
            QueryTriggerInteraction.Ignore);
    }

    private void NextWaypoint()
    {
        targetIndex++;

        if (targetIndex >= waypoints.Count)
            FinishRoute();
    }

    private void FinishRoute()
    {
        isFollowing = false;
        isBlocked = false;
        blockedTimer = 0f;

        Completed?.Invoke();
    }

    public void Follow(IReadOnlyList<Vector3> route)
    {
        waypoints.Clear();

        isFollowing = false;
        isBlocked = false;
        blockedTimer = 0f;

        if (route == null || route.Count < 2)
            return;

        for (int i = 0; i < route.Count; i++)
            waypoints.Add(route[i]);

        targetIndex = 1;
        isFollowing = true;
    }

    public void ResetFollower()
    {
        isFollowing = false;
        isBlocked = false;
        blockedTimer = 0f;

        waypoints.Clear();
        targetIndex = 0;

        character.SetPositionAndRotation(
            initialPosition,
            initialRotation);
    }

    // Mantido para compatibilidade.
    public float GetRemainingDistanceInsideRadius(
        Vector3 center,
        float radius)
    {
        if (!isFollowing ||
            targetIndex >= waypoints.Count ||
            radius <= 0f)
            return 0f;

        Vector2 center2D = new Vector2(center.x, center.z);
        Vector2 start = new Vector2(
            character.position.x,
            character.position.z);

        if ((start - center2D).sqrMagnitude > radius * radius)
            return 0f;

        float total = 0f;

        for (int i = targetIndex; i < waypoints.Count; i++)
        {
            Vector2 end = new Vector2(
                waypoints[i].x,
                waypoints[i].z);

            Vector2 direction = end - start;
            float length = direction.magnitude;

            if (length < 0.0001f)
            {
                start = end;
                continue;
            }

            if ((end - center2D).sqrMagnitude >= radius * radius)
            {
                Vector2 offset = start - center2D;

                float a = Vector2.Dot(direction, direction);
                float b = 2f * Vector2.Dot(offset, direction);
                float c = Vector2.Dot(offset, offset)
                          - radius * radius;

                float discriminant = b * b - 4f * a * c;

                float exitT = (-b + Mathf.Sqrt(
                    Mathf.Max(0f, discriminant))) / (2f * a);

                return total + length * Mathf.Clamp01(exitT);
            }

            total += length;
            start = end;
        }

        return total;
    }

    // Mantido: calculo 3D do tempo de fotografia.
    public float GetRemainingDistanceInsideProblem(
        CityProblem problem)
    {
        if (problem == null ||
            character == null ||
            !isFollowing ||
            targetIndex >= waypoints.Count)
            return 0f;

        Vector3 start = character.position;

        if (!problem.IsPlayerNear(start))
            return 0f;

        float totalDistance = 0f;

        for (int i = targetIndex; i < waypoints.Count; i++)
        {
            Vector3 end = waypoints[i];
            float segmentLength = Vector3.Distance(start, end);

            if (segmentLength <= 0.0001f)
            {
                start = end;
                continue;
            }

            if (problem.IsPlayerNear(end))
            {
                totalDistance += segmentLength;
                start = end;
                continue;
            }

            float min = 0f;
            float max = 1f;

            for (int iteration = 0; iteration < 18; iteration++)
            {
                float middle = (min + max) * 0.5f;

                Vector3 position =
                    Vector3.Lerp(start, end, middle);

                if (problem.IsPlayerNear(position))
                    min = middle;
                else
                    max = middle;
            }

            return totalDistance + segmentLength * min;
        }

        return totalDistance;
    }
}