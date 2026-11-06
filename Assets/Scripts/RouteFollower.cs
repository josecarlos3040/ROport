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

    private readonly List<Vector3> waypoints = new List<Vector3>();
    private Vector3 initialPosition;
    private Quaternion initialRotation;
    private int targetIndex;
    private bool isFollowing;

    public event Action Completed;

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

        Vector3 target = waypoints[targetIndex];
        Vector3 toTarget = target - character.position;
        toTarget.y = 0f;

        if (toTarget.sqrMagnitude > 0.0001f)
        {
            Quaternion desiredRotation = Quaternion.LookRotation(toTarget.normalized);
            character.rotation = Quaternion.Slerp(character.rotation, desiredRotation,
                turnSpeed * Time.deltaTime);
        }

        character.position = Vector3.MoveTowards(
            character.position, target, movementSpeed * Time.deltaTime);

        if (Vector3.Distance(character.position, target) <= waypointTolerance)
        {
            targetIndex++;

            if (targetIndex >= waypoints.Count)
            {
                isFollowing = false;
                Completed?.Invoke();
            }
        }
    }

    public void Follow(IReadOnlyList<Vector3> route)
    {
        waypoints.Clear();
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
        waypoints.Clear();
        targetIndex = 0;
        character.SetPositionAndRotation(initialPosition, initialRotation);
    }
}
