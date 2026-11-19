using UnityEngine;
using UnityEngine.InputSystem;

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
    [SerializeField] private ROportRunManager runManager;
    [SerializeField] private RouteFollower routeFollower;
    [SerializeField] private RobotAnimationController robotAnimation;

    [Header("Visibility")]
    [SerializeField] private LayerMask problemVisualLayers;
    [SerializeField] private GameObject playerSpriteVisual;
    [SerializeField] private GameObject mapMarker;

    [Header("Execution Camera - First Floor")]
    [SerializeField, Min(0.1f)]
    private float executionSize = 9f;

    [SerializeField, Range(-180f, 180f)]
    private float executionOrbitAngle = 80f;

    [SerializeField, Min(1f)]
    private float executionDistance = 12f;

    [SerializeField] private float executionHeight = 7f;
    [SerializeField] private float executionLookHeight = 1f;
    [SerializeField] private float lookAhead = 1.5f;

    [Header("Second Floor Camera")]
    [SerializeField] private bool enableSecondFloorView = true;
    [SerializeField] private float secondFloorEnterHeight = 8.5f;
    [SerializeField] private float secondFloorExitHeight = 7f;

    [SerializeField, Range(45f, 89f)]
    private float upperFloorPitch = 82f;

    [SerializeField, Min(1f)]
    private float upperFloorHeight = 14f;

    [SerializeField, Min(1f)]
    private float upperFloorSize = 10f;

    [SerializeField, Min(0f)]
    private float floorTransitionDuration = 1f;

    [Header("Player")]
    [SerializeField] private float playerPivotHeight = 1f;

    [Header("Phone Planning Camera")]

    [Tooltip("Rotacao manual X, Y e Z da camera de planejamento.")]
    [SerializeField]
    private Vector3 phonePlanningRotation =
        new Vector3(12f, 90f, 0f);

    [Tooltip("Distancia entre a camera e o personagem.")]
    [SerializeField, Min(1f)]
    private float phonePlanningDistance = 6f;

    [Tooltip("Altura adicional da camera.")]
    [SerializeField]
    private float phonePlanningHeight = 1f;

    [Tooltip("Deslocamento horizontal: positivo deixa o Player mais a esquerda.")]
    [SerializeField]
    private float phonePlanningLookSideBias = 3f;

    [Tooltip("Zoom ortografico durante o planejamento.")]
    [SerializeField, Min(0.1f)]
    private float phonePlanningSize = 4.2f;

    [Tooltip("Altura do ponto de referencia no Player.")]
    [SerializeField]
    private float phonePlanningLookHeight = 1f;

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

    private Vector3 lastValidForward = Vector3.forward;

    private bool isOnUpperFloor;
    private float upperFloorBlend;

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

        if (player == null)
            return;

        if (routeFollower == null)
            routeFollower = player.GetComponent<RouteFollower>();

        if (robotAnimation == null)
        {
            robotAnimation =
                player.GetComponent<RobotAnimationController>();
        }

        Vector3 initialForward = player.forward;
        initialForward.y = 0f;

        if (initialForward.sqrMagnitude > 0.0001f)
            lastValidForward = initialForward.normalized;
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
                "[ROport Camera] Referencias incompletas.",
                this);

            return;
        }

        OnPhaseChanged(runManager.Phase);
        UpdateCamera(true);
    }

    private void LateUpdate()
    {
        if (mainCamera == null || player == null)
            return;

        // Mantem o enquadramento enquanto desenha.
        bool isDrawing =
            currentView == CameraView.Planning &&
            Mouse.current != null &&
            Mouse.current.leftButton.isPressed;

        if (isDrawing)
            return;

        // Congela a camera durante queda ou impacto.
        if (robotAnimation != null &&
            robotAnimation.IsFallAnimationActive)
        {
            positionVelocity = Vector3.zero;
            zoomVelocity = 0f;
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

        if (view == CameraView.Planning)
        {
            isOnUpperFloor = false;
            upperFloorBlend = 0f;
        }

        UpdateVisibility();
    }

    private void UpdateVisibility()
    {
        bool planning =
            currentView == CameraView.Planning;

        if (mainCamera != null)
        {
            mainCamera.cullingMask = planning
                ? originalCullingMask &
                  ~problemVisualLayers.value
                : originalCullingMask;
        }

        // Robo visivel durante o planejamento.
        if (playerSpriteVisual != null)
            playerSpriteVisual.SetActive(true);

        // Marcador do minimapa somente no planejamento.
        if (mapMarker != null)
            mapMarker.SetActive(planning);
    }

    private Vector3 GetPlayerGroundPosition()
    {
        return player.position -
            Vector3.up * playerPivotHeight;
    }

    private Vector3 GetPlayerForward()
    {
        if (routeFollower != null &&
            routeFollower.IsFollowing &&
            !routeFollower.IsBlocked)
        {
            Vector3 direction =
                routeFollower.CurrentMoveDirection;

            direction.y = 0f;

            if (direction.sqrMagnitude > 0.0001f)
                lastValidForward = direction.normalized;
        }

        return lastValidForward;
    }

    // CAMERA DE PLANEJAMENTO
    // Rotacao totalmente controlada pelo Inspector.
    private void GetPhonePlanningPose(
        out Vector3 desiredPosition,
        out Quaternion desiredRotation)
    {
        desiredRotation =
            Quaternion.Euler(phonePlanningRotation);

        Vector3 pivot =
            GetPlayerGroundPosition() +
            Vector3.up * phonePlanningLookHeight;

        Vector3 cameraForward =
            desiredRotation * Vector3.forward;

        Vector3 cameraRight =
            desiredRotation * Vector3.right;

        Vector3 cameraUp =
            desiredRotation * Vector3.up;

        // Move a camera sem alterar sua rotacao.
        // Bias positivo desloca o Player para
        // o lado esquerdo da imagem.
        desiredPosition =
            pivot
            - cameraForward * phonePlanningDistance
            + cameraRight * phonePlanningLookSideBias
            + cameraUp * phonePlanningHeight;
    }

    // TRANSICAO ENTRE ANDARES
    private void UpdateFloorState(bool instant)
    {
        if (!enableSecondFloorView || player == null)
        {
            isOnUpperFloor = false;
            upperFloorBlend = 0f;
            return;
        }

        float groundHeight =
            GetPlayerGroundPosition().y;

        if (isOnUpperFloor)
        {
            if (groundHeight <= secondFloorExitHeight)
                isOnUpperFloor = false;
        }
        else
        {
            if (groundHeight >= secondFloorEnterHeight)
                isOnUpperFloor = true;
        }

        float targetBlend =
            isOnUpperFloor ? 1f : 0f;

        if (instant || floorTransitionDuration <= 0f)
        {
            upperFloorBlend = targetBlend;
            return;
        }

        upperFloorBlend = Mathf.MoveTowards(
            upperFloorBlend,
            targetBlend,
            Time.deltaTime / floorTransitionDuration);
    }

    // CAMERA DE EXECUCAO
    private void GetExecutionPose(
        bool instant,
        out Vector3 desiredPosition,
        out Quaternion desiredRotation,
        out float desiredSize)
    {
        UpdateFloorState(instant);

        Vector3 forward = GetPlayerForward();

        Vector3 right =
            Vector3.Cross(
                Vector3.up, forward).normalized;

        float angle =
            executionOrbitAngle * Mathf.Deg2Rad;

        Vector3 orbitDirection =
            forward * Mathf.Cos(angle) +
            right * Mathf.Sin(angle);

        Vector3 target =
            GetPlayerGroundPosition();

        if (currentView == CameraView.Following)
            target += forward * lookAhead;

        target +=
            Vector3.up * executionLookHeight;

        float cameraHeight = Mathf.Lerp(
            executionHeight,
            upperFloorHeight,
            upperFloorBlend);

        float upperPitchRadians =
            upperFloorPitch * Mathf.Deg2Rad;

        float upperHorizontalDistance =
            upperFloorHeight /
            Mathf.Tan(upperPitchRadians);

        float horizontalDistance = Mathf.Lerp(
            executionDistance,
            upperHorizontalDistance,
            upperFloorBlend);

        desiredPosition =
            target +
            orbitDirection * horizontalDistance +
            Vector3.up * cameraHeight;

        desiredRotation = Quaternion.LookRotation(
            target - desiredPosition,
            Vector3.up);

        desiredSize = Mathf.Lerp(
            executionSize,
            upperFloorSize,
            upperFloorBlend);
    }

    private void UpdateCamera(bool instant)
    {
        if (mainCamera == null || player == null)
            return;

        Vector3 desiredPosition;
        Quaternion desiredRotation;
        float desiredSize;

        if (currentView == CameraView.Planning)
        {
            GetPhonePlanningPose(
                out desiredPosition,
                out desiredRotation);

            desiredSize = phonePlanningSize;
        }
        else
        {
            GetExecutionPose(
                instant,
                out desiredPosition,
                out desiredRotation,
                out desiredSize);
        }

        Transform cameraTransform =
            mainCamera.transform;

        if (instant)
        {
            cameraTransform.SetPositionAndRotation(
                desiredPosition,
                desiredRotation);

            mainCamera.orthographicSize = desiredSize;
            return;
        }

        cameraTransform.position = Vector3.SmoothDamp(
            cameraTransform.position,
            desiredPosition,
            ref positionVelocity,
            positionSmoothTime);

        float rotationFactor =
            1f - Mathf.Exp(
                -rotationSpeed * Time.deltaTime);

        cameraTransform.rotation = Quaternion.Slerp(
            cameraTransform.rotation,
            desiredRotation,
            rotationFactor);

        mainCamera.orthographicSize =
            Mathf.SmoothDamp(
                mainCamera.orthographicSize,
                desiredSize,
                ref zoomVelocity,
                zoomSmoothTime);
    }

    private void OnDisable()
    {
        if (runManager != null)
            runManager.PhaseChanged -= OnPhaseChanged;

        if (mainCamera != null)
            mainCamera.cullingMask = originalCullingMask;

        if (playerSpriteVisual != null)
            playerSpriteVisual.SetActive(
                originalSpriteActive);

        if (mapMarker != null)
            mapMarker.SetActive(
                originalMarkerActive);
    }
}