using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

[RequireComponent(typeof(Camera))]
public class ROportCameraController : MonoBehaviour
{
    private enum CameraView
    {
        Planning,
        Following,
        Finished
    }

    [Header("References")]
    [SerializeField] private Camera mainCamera;
    [SerializeField] private Transform player;
    [SerializeField] private Transform cityCenter;
    [SerializeField] private ROportRunManager runManager;

    [Header("Visibility")]
    [SerializeField] private LayerMask problemVisualLayers;
    [SerializeField] private GameObject playerSpriteVisual;
    [SerializeField] private GameObject mapMarker;

    [Header("Planning Camera")]
    [SerializeField] private float overviewSize = 28f;
    [SerializeField] private float overviewDistance = 60f;

    [FormerlySerializedAs("drawingPitch")]
    [SerializeField, Range(0f, 90f)]
    private float planningPitch = 65f;

    [FormerlySerializedAs("drawingYaw")]
    [SerializeField, Range(-180f, 180f)]
    private float planningYaw = 30f;

    [FormerlySerializedAs("drawingRoll")]
    [SerializeField, Range(-30f, 30f)]
    private float planningRoll = 0f;

    [Header("Execution Camera")]
    [SerializeField] private float executionSize = 9f;

    [SerializeField, Range(-180f, 180f)]
    private float executionOrbitAngle = 80f;

    [SerializeField, Min(1f)]
    private float executionDistance = 12f;

    [SerializeField] private float executionHeight = 7f;
    [SerializeField] private float executionLookHeight = 1f;
    [SerializeField] private float lookAhead = 1.5f;

    [Header("Player")]
    [SerializeField] private float playerPivotHeight = 1f;

    [Header("Smoothing")]
    [SerializeField, Min(0.01f)]
    private float positionSmoothTime = 0.35f;

    [SerializeField, Min(0.01f)]
    private float zoomSmoothTime = 0.4f;

    [SerializeField, Min(0.1f)]
    private float rotationSpeed = 5f;

    private CameraView currentView = CameraView.Planning;

    private Vector3 positionVelocity;
    private float zoomVelocity;

    private int originalCullingMask;
    private bool originalSpriteActive;
    private bool originalMarkerActive;

    private void Awake()
    {
        if (mainCamera == null)
            mainCamera = GetComponent<Camera>();

        mainCamera.orthographic = true;
        originalCullingMask = mainCamera.cullingMask;

        if (playerSpriteVisual != null)
            originalSpriteActive = playerSpriteVisual.activeSelf;

        if (mapMarker != null)
            originalMarkerActive = mapMarker.activeSelf;
    }

    private void OnEnable()
    {
        if (runManager != null)
            runManager.PhaseChanged += OnPhaseChanged;
    }

    private void Start()
    {
        if (runManager == null || player == null)
        {
            Debug.LogWarning(
                "[ROport] Camera sem referencias obrigatorias.",
                this);
        }

        RunPhase phase = runManager != null
            ? runManager.Phase
            : RunPhase.Planning;

        OnPhaseChanged(phase);
        UpdateCamera(true);
    }

    private void LateUpdate()
    {
        // O enquadramento de planejamento nao muda
        // quando o jogador desenha ou solta o mouse.
        // Congela temporariamente transicoes antigas
        // durante o arrasto, evitando alterar o Raycast.
        if (currentView == CameraView.Planning &&
            Mouse.current != null &&
            Mouse.current.leftButton.isPressed)
        {
            return;
        }

        UpdateCamera(false);
    }

    private void OnPhaseChanged(RunPhase phase)
    {
        switch (phase)
        {
            case RunPhase.Planning:
                ChangeView(CameraView.Planning);
                break;

            case RunPhase.Executing:
                ChangeView(CameraView.Following);
                break;

            case RunPhase.Finished:
                ChangeView(CameraView.Finished);
                break;
        }
    }

    private void ChangeView(CameraView view)
    {
        currentView = view;

        positionVelocity = Vector3.zero;
        zoomVelocity = 0f;

        UpdateVisibility();
    }

    private void UpdateVisibility()
    {
        bool isPlanning = currentView == CameraView.Planning;

        mainCamera.cullingMask = isPlanning
            ? originalCullingMask & ~problemVisualLayers.value
            : originalCullingMask;

        if (playerSpriteVisual != null)
            playerSpriteVisual.SetActive(!isPlanning);

        if (mapMarker != null)
            mapMarker.SetActive(isPlanning);
    }

    private void UpdateCamera(bool instant)
    {
        if (mainCamera == null)
            return;

        Vector3 desiredPosition;
        Quaternion desiredRotation;
        float desiredSize;

        if (currentView == CameraView.Planning)
        {
            CalculatePlanningCamera(
                out desiredPosition,
                out desiredRotation);

            desiredSize = overviewSize;
        }
        else
        {
            CalculateExecutionCamera(
                out desiredPosition,
                out desiredRotation);

            desiredSize = executionSize;
        }

        if (instant)
        {
            transform.SetPositionAndRotation(
                desiredPosition, desiredRotation);

            mainCamera.orthographicSize = desiredSize;
            return;
        }

        transform.position = Vector3.SmoothDamp(
            transform.position,
            desiredPosition,
            ref positionVelocity,
            positionSmoothTime);

        float rotationFactor =
            1f - Mathf.Exp(-rotationSpeed * Time.deltaTime);

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            desiredRotation,
            rotationFactor);

        mainCamera.orthographicSize = Mathf.SmoothDamp(
            mainCamera.orthographicSize,
            desiredSize,
            ref zoomVelocity,
            zoomSmoothTime);
    }

    private void CalculatePlanningCamera(
        out Vector3 position,
        out Quaternion rotation)
    {
        Vector3 center = cityCenter != null
            ? cityCenter.position
            : Vector3.zero;

        rotation = Quaternion.Euler(
            planningPitch,
            planningYaw,
            planningRoll);

        position = center -
                   rotation * Vector3.forward * overviewDistance;
    }

    private void CalculateExecutionCamera(
        out Vector3 position,
        out Quaternion rotation)
    {
        Vector3 playerPosition = player != null
            ? player.position - Vector3.up * playerPivotHeight
            : Vector3.zero;

        Vector3 forward = player != null
            ? player.forward
            : Vector3.forward;

        forward.y = 0f;

        if (forward.sqrMagnitude < 0.0001f)
            forward = Vector3.forward;

        forward.Normalize();

        Vector3 right = Vector3.Cross(Vector3.up, forward);

        float angle = executionOrbitAngle * Mathf.Deg2Rad;

        Vector3 orbitDirection =
            forward * Mathf.Cos(angle) +
            right * Mathf.Sin(angle);

        Vector3 target = playerPosition;

        if (currentView == CameraView.Following)
            target += forward * lookAhead;

        target += Vector3.up * executionLookHeight;

        position = target +
                   orbitDirection * executionDistance +
                   Vector3.up * executionHeight;

        rotation = Quaternion.LookRotation(
            target - position, Vector3.up);
    }

    private void OnDisable()
    {
        if (runManager != null)
            runManager.PhaseChanged -= OnPhaseChanged;

        if (mainCamera != null)
            mainCamera.cullingMask = originalCullingMask;

        if (playerSpriteVisual != null)
            playerSpriteVisual.SetActive(originalSpriteActive);

        if (mapMarker != null)
            mapMarker.SetActive(originalMarkerActive);
    }
}